namespace GatewayCoreModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using System.Net;
    using System.Net.Sockets;

    using Hardware;
    using TplSockets;
    using TplResult;
    using GatewayGeneral;
    using System.Runtime.Intrinsics.X86;
    using Newtonsoft.Json.Linq;
    using System.Linq;
    using System.Globalization;
    using System.IO.Ports;

    using TowerInclinometer;
    using System.Collections.Generic;
    using Iot.Device.BrickPi3.Models;
    using System.Runtime.CompilerServices;

    class Program
    {
        private static IGatewayHardware gwHardware;
        private static ModuleClient gatewayModuleClient;
        private static ScheduleTimer timerPollData;
        private static ScheduleTimer timerSendData;
        private static string storageFolder;
        private static string configFolder;
        private static string deviceId;

        private static GatewayProperties gwProperties;
        private static GatewayData gwData;

        static int statusLedPeriodMs = 1000;
        static int statusLedOnMs = 200;

        private static List<IDevicesRs485> DevicesRs485;

        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

        static void Main()
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Inicializa el modulo Gateway
        /// </summary>
        static async Task Init()
        {
            WriteColor.Set(WriteColor.AnsiColors.Yellow);
            Art.Write();
            WriteColor.Set(WriteColor.AnsiColors.Reset);

            // Establece el protocolo de comunicacion

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            //AmqpTransportSettings ampqSetting = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
            //ITransportSettings[] settings = { ampqSetting };

            // Crea conexion al hardware

            gwHardware = new GatewayRPI3Plus();
            //gwHardware = new GatewayRPI4();
            gwHardware.UserButtonPushed += GwHardware_UserButtonPushed;
            
            // Crea una conexion al runtime Edge

            gatewayModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            gatewayModuleClient.SetRetryPolicy(new NoRetry());

            await gatewayModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, gatewayModuleClient);
            await gatewayModuleClient.SetMethodDefaultHandlerAsync(OnNotImplementedMethod, gatewayModuleClient);
            await gatewayModuleClient.SetMethodHandlerAsync("UpdateRtcFromNtp", OnUpdateRtcFromNtp, gatewayModuleClient);
            //await gatewayModuleClient.SetMethodHandlerAsync("SetUserLedState", OnSetUserLedState, gatewayModuleClient);
            await gatewayModuleClient.SetMethodHandlerAsync("ToggleUserLedState", OnToggleUserLedState, gatewayModuleClient);
            await gatewayModuleClient.SetMethodHandlerAsync("PollData", OnPollData, gatewayModuleClient);
            await gatewayModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, gatewayModuleClient);

            // Obtiene variables de entorno

            storageFolder = Environment.GetEnvironmentVariable("storageFolder");
            configFolder = Environment.GetEnvironmentVariable("configFolder");
            deviceId = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");

            // Inicia el modulo

            Console.WriteLine("");
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Inicializando dispositivo " + deviceId + ".");
            await gatewayModuleClient.OpenAsync();

            // Carga la configuracion desde host
            // En caso de error, crea configuracion default y la guarda en Host

            try
            {
                gwProperties = GatewayProperties.FromJsonFile(configFolder + "config.json");
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Configuracion cargada desde host.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error cargando configuracion desde host -> {ex.Message}");
                gwProperties = new GatewayProperties();

                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Configuracion por defecto creada.");
                try
                {
                    gwProperties.ToJsonFile(configFolder + "config.json");
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Configuracion por defecto guardada en host.");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error guardando configuración en Host -> {ex2.Message}");
                }         
            }

            // Crea los datos del gateway y carga la configuracion de variables

            gwData = new GatewayData();
            gwData.PowerVoltage.Config = gwProperties.Variable.PowerVoltage;
            gwData.SensedVoltage.Config = gwProperties.Variable.SensedVoltage;
            gwData.BatteryVoltage.Config = gwProperties.Variable.BatteryVoltage;
            gwData.Temperature.Config = gwProperties.Variable.Temperature;

            gwData.StateChanged += gwData_VarableStateChanged;

            // Actualiza fecha y hora del RTC por NTP

            try
            {
                var res = await GetNetworkTime();
                if (res.Success)
                {
                    gwHardware.SetRtcDateTime(res.Value);
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Fecha y Hora del RTC actualizada por NTP [{res.Value}].");
                }
                else
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error actualizando RTC por NTP[{ res.Error}].");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error obteniendo hora por NTP -> {ex.Message}.");
            }

            // Envia el evento de reinicio al Hub

            GatewayEvent gevt = new GatewayEvent();
            gevt.UtcTime = RoundDateTime.RoundToSeconds(DateTime.Now);
            gevt.MessageType = EventType.Info;
            gevt.Message = "Gateway Reiniciado";
            await SendEventMessage(gevt);

            // Obtiene el gemelo y sincroniza propiedades con deseadas del Hub

            try
            {
                var twin = await gatewayModuleClient.GetTwinAsync();
                WriteColor.Set(WriteColor.AnsiColors.Cyan);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Descarga propiedades deseadas (V:" + twin.Properties.Desired.Version.ToString() + ").");
                WriteColor.Set(WriteColor.AnsiColors.Reset);
                await UpdateGatewayProperties(twin.Properties.Desired);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error descargando propiedades deseadas del Hub -> {ex.Message}.");
            }

            // Actualiza propiedades reportadas

            await SendReportedProperties(gwProperties);
            WriteColor.Set(WriteColor.AnsiColors.Cyan);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Envio propiedades reportadas.");
            WriteColor.Set(WriteColor.AnsiColors.Reset);

            // Inicia la tarea del led de status

            var threadStatusLed = new Thread(() => ThreadBodyStatusLed(gatewayModuleClient));
            threadStatusLed.Start();

            // Crea los timer de encuesta y telemetria de datos del gateway

            timerPollData = new ScheduleTimer();
            timerPollData.Elapsed += TimerPollData_Elapsed;

            timerSendData = new ScheduleTimer();
            timerSendData.Elapsed += TimerSendData_Elapsed;

            if (gwProperties.PollData.Enabled)
            {
                // Inicia el timer de encuesta de datos
                timerPollData.Start(gwProperties.PollData.Period, gwProperties.PollData.Unit);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Primera ejecucion encuesta datos: {timerPollData.FirstExcecution} / Periodo: {timerPollData.PeriodMs} ms");             
                if(gwProperties.DetachedTelemetry.Enabled)
                {
                    // Inicia el timer de telemetria de datos si esta desdoblado
                    timerSendData.Start(gwProperties.DetachedTelemetry.Period, gwProperties.DetachedTelemetry.Unit);
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Primera ejecucion telemetria datos: {timerSendData.FirstExcecution} / Periodo: {timerSendData.PeriodMs} ms");
                }
                else
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Telemetria de datos simultanea con encuesta.");
                }
            }
            else
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Encuesta datos y telemetria deshabilitadas.");
            }

            // Registra eventos de cambio de propiedades

            gwProperties.PollDataChanged += GwProperties_PollDataChanged;
            gwProperties.DetachedTelemetryChanged += GwProperties_DetachedTelemetryChanged;
            gwProperties.DevicesRs485Changed += GwProperties_DevicesRs485Changed;

            // CREA DISPOSITIVOS

            // Rs485

            DevicesRs485 = new List<IDevicesRs485>();
            foreach(DeviceRs485Configuration devConf in gwProperties.DevicesRs485)
            {
                if (devConf.DeviceType == DeviceTypes.TowerInclinometer)
                    DevicesRs485.Add(new DeviceTowerInclinometer(devConf.DeviceId, DeviceInterfaces.Rs485, devConf.ConnectionString));
            }

            foreach (IDevicesRs485 dev in DevicesRs485)
                await dev.Init();
        }

       
        // ENCUESTA Y TELEMETRIA DE DATOS

        private static void TimerPollData_Elapsed(object sender, EventArgs e)
        {
            // Obtiene los datos del gateway
            GetGatewayData();

            // Envia la telemetria si no esta separada
            if(!gwProperties.DetachedTelemetry.Enabled)
                _ = SendDataMessage();
        }

        private static void TimerSendData_Elapsed(object sender, EventArgs e)
        {
            // Envia la telemetria
            _ = SendDataMessage();
        }

        // TAREA STATUS

        private static async void ThreadBodyStatusLed(object userContext)
        {
            while (true)
            {
                // turn on the LED
                gwHardware.SetStatusLed(LedState.On);
                await Task.Delay(statusLedOnMs);

                // turn off the LED
                gwHardware.SetStatusLed(LedState.Off);
                await Task.Delay(statusLedPeriodMs);
            }
        }

        // EVENTOS DE HARDWARE

        private static void GwHardware_UserButtonPushed(object sender, EventArgs e)
        {
            // Loggea en consola el boton pulsado
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] User Button presionado.");

            // Cambia el estado del led de usuario
            gwHardware.ToggleUserLed();

            // Crea el evento
            GatewayEvent gevt = new GatewayEvent();
            gevt.UtcTime = RoundDateTime.RoundToSeconds(DateTime.Now);
            gevt.MessageType = EventType.Info;
            gevt.Message = "User Button presionado";
                        
            // Envia el evento
            _ = SendEventMessage(gevt);

            gevt.ToJsonFile(configFolder + "boton.json");
        }

        // EVENTOS DE CAMBIO DE PROPIEDADES

        private static void GwProperties_PollDataChanged(object sender, EventArgs e)
        {
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Propiedad PollData modificada.");

            // Detiene la encuesta

            timerPollData.Stop();

            if (gwProperties.PollData.Enabled)
            {
                timerPollData.Start(gwProperties.PollData.Period, gwProperties.PollData.Unit);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Proxima ejecucion encuesta datos: {timerPollData.FirstExcecution} / Periodo: {timerPollData.PeriodMs} ms");
            }
            else
            {
                timerSendData.Stop();
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Encuesta datos y telemetria datos deshabilitadas.");
            }
        }

        private static void GwProperties_DetachedTelemetryChanged(object sender, EventArgs e)
        {
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Propiedad DetachedTelemetry modificada.");

            // Detiene la telemetria

            timerSendData.Stop();

            if (gwProperties.DetachedTelemetry.Enabled)
            {
                timerSendData.Start(gwProperties.DetachedTelemetry.Period, gwProperties.DetachedTelemetry.Unit);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Proxima ejecucion telemetria datos: {timerSendData.FirstExcecution} / Periodo: {timerSendData.PeriodMs} ms");
            }
            else
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Telemetria de datos simultanea con encuesta.");
        }

        private static async void GwProperties_DevicesRs485Changed(object sender, EventArgs e)
        {
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Propiedad DevicesRs485 modificada.");

            // Desconecta los dispositivos actuales y borra la lista
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Reinicia dispositivos RS485.");
            foreach (IDevicesRs485 dev in DevicesRs485)
            {
                await dev.Stop();
            }

            // Crea los nuevos dispositivos y los inicializa
            if (gwProperties.DevicesRs485.Count > 0)
            {
                await Task.Delay(10);
                //Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Creando nuevos dispositivos RS485.");
                DevicesRs485 = new List<IDevicesRs485>();
                foreach (DeviceRs485Configuration devConf in gwProperties.DevicesRs485)
                {
                    if ((devConf.DeviceType == DeviceTypes.TowerInclinometer) && !devConf.Delete)
                        DevicesRs485.Add(new DeviceTowerInclinometer(devConf.DeviceId, DeviceInterfaces.Rs485, devConf.ConnectionString));
                }

                foreach (IDevicesRs485 dev in DevicesRs485)
                    await dev.Init();
            }
        }

        // EVENTOS DEL MODULO

        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            WriteColor.Set(WriteColor.AnsiColors.Cyan);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Recepcion propiedades deseadas (V:" + desiredProperties.Version.ToString() + ").");
            WriteColor.Set(WriteColor.AnsiColors.Reset);

            await UpdateGatewayProperties(desiredProperties);

            // Envia las propiedades reportadas

            await SendReportedProperties(gwProperties);

            // Elimina propiedades marcadas
            gwProperties.DeleteMarkedRS485Devices();

            WriteColor.Set(WriteColor.AnsiColors.Cyan);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Envio propiedades reportadas.");
            WriteColor.Set(WriteColor.AnsiColors.Reset);
        }

        /// <summary>
        /// Metodo que procesa los mensajes recibidos por el modulo
        /// </summary>
        static Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Mensaje recibido.");

            var moduleClient = (ModuleClient)userContext;
            if (moduleClient == null)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error: No indica Modulo en el contexto.");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);

            return Task.FromResult(MessageResponse.Completed);
        }

        public static async Task<Result<DateTime>> GetNetworkTime()
        {
            // Servidor NTP
            const string ntpServer = "time.windows.com";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = await Dns.GetHostEntryAsync(ntpServer);

            //NTP uses UDP

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {

                var connectResult = await socket.ConnectWithTimeoutAsync(addresses.AddressList[0].ToString(), 123, 5000);

                if (connectResult.Failure)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error conctando al servidor NTP {ntpServer}");
                    Console.WriteLine($"{connectResult.Error}");
                    return Result.Fail<DateTime>(connectResult.Error);
                }

                var sendResult = await socket.SendWithTimeoutAsync(ntpData, 0, ntpData.Length, 0, 5000);

                if (sendResult.Failure)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error enviando datos al servidor NTP {ntpServer}");
                    Console.WriteLine($"{sendResult.Error}");
                    return Result.Fail<DateTime>(connectResult.Error);
                }

                var receiveResult = await socket.ReceiveWithTimeoutAsync(ntpData, 0, ntpData.Length, 0, 5000);

                if (receiveResult.Failure)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error recibiendo datos del servidor NTP {ntpServer}");
                    Console.WriteLine($"{receiveResult.Error}");
                    return Result.Fail<DateTime>(connectResult.Error);
                }

                socket.Close();
            }

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return Result.Ok<DateTime>(networkDateTime);
        }

        // stackoverflow.com/a/3294698/162671
        static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }

        static Task UpdateGatewayProperties(TwinCollection desiredProp)
        {
            // Poll Data
            try
            {
                if (desiredProp["PollData"] == null)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No se puede eliminar la propiedad PollData.");
                }
                else
                {
                    GatewayDataPollConfiguration gpdc = GatewayDataPollConfiguration.FromJsonString(desiredProp["PollData"].ToString());
                    gwProperties.PollData = gpdc;
                }
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No hay configuracion para PollData.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error en configuracion de PollData (Ingnorando cambios) -> {ex.Message}");              
            }

            // Detached Telemetry
            try
            {
                if (desiredProp["DetachedTelemetry"] == null)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No se puede eliminar la propiedad DetachedTelemetry.");
                }
                else
                {
                    GatewayDetachedTelemetryConfiguration gdtc = GatewayDetachedTelemetryConfiguration.FromJsonString(desiredProp["DetachedTelemetry"].ToString());
                    gwProperties.DetachedTelemetry = gdtc;
                }
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No hay configuracion para DetachedTelemetry.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error en configuracion de DetachedTelemetry (Ingnorando cambios) -> {ex.Message}");               
            }

            // Variable
            try
            {
                if (desiredProp["Variable"] == null)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No se puede eliminar la propiedad Variable.");
                }
                else
                {
                    GatewayVariableConfiguration gvc = GatewayVariableConfiguration.FromJsonString(desiredProp["Variable"].ToString());
                    gwProperties.Variable = gvc;
                    gwData.PowerVoltage.Config = gwProperties.Variable.PowerVoltage;
                    gwData.SensedVoltage.Config = gwProperties.Variable.SensedVoltage;
                    gwData.BatteryVoltage.Config = gwProperties.Variable.BatteryVoltage;
                    gwData.Temperature.Config = gwProperties.Variable.Temperature;
                }
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No hay configuracion para Variable.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error en configuracion de Variable (Ingnorando cambios) -> {ex.Message}");                   
            }

            // DevicesRs485
            try
            {
                if (desiredProp["DevicesRs485"] == null)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No se puede eliminar la propiedad DevicesRs485.");
                }
                else
                {
                    List<DeviceRs485Configuration> ldev = JsonConvert.DeserializeObject<List<DeviceRs485Configuration>>(desiredProp["DevicesRs485"].ToString(), new DeviceRs485ConfigurationListJsonConverter());
                    if (ldev == null)
                        Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No hay configuracion de dispositivos para DevicesRs485.");
                    else if (ldev.Count == 0)
                        Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No hay configuracion de dispositivos para DevicesRs485.");
                    else
                    {
                        // Guarda la nueva configuracion de dispositivos
                        gwProperties.DevicesRs485 = ldev;
                    }
                }          
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] No hay configuracion para DevicesRs485.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Error en configuracion de DevicesRs485 (Ingnorando cambios) -> {ex.Message}");
            }

            // Guarda las propiedades en archivo de configuraion
            gwProperties.ToJsonFile(configFolder + "config.json");

            return Task.FromResult("Ok"); 
        }

        static void GetGatewayData()
        {
            // Adquiere datos del gateway
            DateTime dt = DateTime.UtcNow;
            float power = gwHardware.GetPowerVoltage();
            float sensed = gwHardware.GetSensedVoltage();
            float battery = gwHardware.GetBatteryVoltage();
            float temperature = gwHardware.GetRtcTemperature();

            // Loggea la adquisicion
            Console.Write($"{dt}> [Gateway] Encuesta datos: ");
            Console.Write($"PowerVoltage = {power:0.00} {gwData.PowerVoltage.Config.Unit} | ");
            Console.Write($"SensedVoltage = {sensed:0.00} {gwData.SensedVoltage.Config.Unit} | ");
            Console.Write($"BatteryVoltage = {battery:0.00} {gwData.BatteryVoltage.Config.Unit} | ");
            Console.WriteLine($"Temperature = {temperature:0.00} {gwData.Temperature.Config.Unit}.");

            // Guarda los datos
            gwData.UtcTime = DateTime.UtcNow;
            gwData.PowerVoltage.Value = power;
            gwData.SensedVoltage.Value = sensed;
            gwData.BatteryVoltage.Value = battery;
            gwData.Temperature.Value = temperature;
            gwData.Sent = false;        
        }

        private static void gwData_VarableStateChanged(object sender, StateChangeEventArgs e)
        {
            AnalogValue val = (AnalogValue)sender;
            GatewayEvent gevt = new GatewayEvent();
            gevt.UtcTime = RoundDateTime.RoundToSeconds(DateTime.Now);
            gevt.MessageType = EventType.Warning;
            gevt.Message = $"Cambio de estado. {e.PropertyName} = {val.Value:0.00} {val.Config.Unit} ({e.PreviousState} -> {e.ActualState}).";
            Task.Delay(10); // Evita que se envien dos mensajes de datos
            _ = SendDataMessage();
            _ = SendEventMessage(gevt);
        }

        private static async Task SendDataMessage()
        {
            // Verifica que los datos no hayan sido enviados aun
            if (!gwData.Sent)
            {
                // Crea el mensaje a partir de los datos del gateway
                Message msg = new Message(Encoding.UTF8.GetBytes(gwData.ToJsonString()));
                msg.ContentEncoding = "utf-8";
                msg.ContentType = "application/json";

                // Agrega propiedad identificando al mensaje como de datos
                msg.Properties.Add("MessageType", MessageType.Data.ToString());
                msg.Properties.Add("DeviceType", DeviceTypes.Gateway.ToString());

                // Marca los datos como enviados y los envia
                gwData.Sent = true;
                await gatewayModuleClient.SendEventAsync("output1", msg);

                // Loggea el envio en consola
                WriteColor.Set(WriteColor.AnsiColors.Green);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Envio Datos: {gwData.ToJsonString()}");
                WriteColor.Set(WriteColor.AnsiColors.Reset);
            }
        }

        private static async Task SendEventMessage(GatewayEvent gevt)
        {
            // Crea el mensaje a partir del evento del gateway
            Message msg = new Message(Encoding.UTF8.GetBytes(gevt.ToJsonString()));
            msg.ContentEncoding = "utf-8";
            msg.ContentType = "application/json";

            // Agrega propiedad identificando al mensaje como Evento
            msg.Properties.Add("MessageType", MessageType.Event.ToString());
            msg.Properties.Add("DeviceType", DeviceTypes.Gateway.ToString());
            await gatewayModuleClient.SendEventAsync("output1", msg);

            // Loggea el envio en consola
            WriteColor.Set(WriteColor.AnsiColors.Yellow);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Envio Evento: {gevt.ToJsonString()}");
            WriteColor.Set(WriteColor.AnsiColors.Reset);
        }

        private static async Task SendReportedProperties(GatewayProperties gp)
        {
            TwinCollection reportedProperties = new TwinCollection(gp.ToJsonString());
            //Console.WriteLine(reportedProperties);

            await gatewayModuleClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        // METODOS DIRECTOS DEL MODULO       

        /// <summary>
        /// Maneja los metodos recibidos no implementados
        /// </summary>
        private static Task<MethodResponse> OnNotImplementedMethod(MethodRequest methodRequest, object userContext)
        {
            string message = $"Metodo {methodRequest.Name} no implementado";
            
            WriteColor.Set(WriteColor.AnsiColors.Blue);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] {message}.");
            WriteColor.Set(WriteColor.AnsiColors.Reset);

            var result = JsonConvert.SerializeObject(message, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 404));
        }

        /// <summary>
        /// Actualiza el RTC por NTP
        /// </summary>
        private static async Task<MethodResponse> OnUpdateRtcFromNtp(MethodRequest methodRequest, object userContext)
        {
            WriteColor.Set(WriteColor.AnsiColors.Blue);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Metodo {methodRequest.Name} recibido.");
            WriteColor.Set(WriteColor.AnsiColors.Reset);

            var res = await GetNetworkTime();
            
            if (res.Success)
            {
                gwHardware.SetRtcDateTime(res.Value);
                string message = $"Fecha y Hora del RTC actualizada por NTP [{res.Value}].";
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] {message}.");
                var result = JsonConvert.SerializeObject(message, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
            }
            else
            {
                string message = $"Error actualizando RTC por NTP [{res.Error}].";
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] {message}.");
                var result = JsonConvert.SerializeObject(message, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                return new MethodResponse(Encoding.UTF8.GetBytes(result), 404);
            }
        }

        /// <summary>
        /// Alterna el estado del led de usuario
        /// </summary>
        private static Task<MethodResponse> OnToggleUserLedState(MethodRequest methodRequest, object userContext)
        {
            WriteColor.Set(WriteColor.AnsiColors.Blue);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Metodo {methodRequest.Name} recibido.");
            WriteColor.Set(WriteColor.AnsiColors.Reset);

            gwHardware.ToggleUserLed();
            LedState st = gwHardware.GetUserLed();

            object payload = new { UserLedState = st.ToString() };
            string result = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] {result}");

            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        /// <summary>
        /// Fuerza la adquisicion y envio de telemetria
        /// </summary>
        private static async Task<MethodResponse> OnPollData(MethodRequest methodRequest, object userContext)
        {
            WriteColor.Set(WriteColor.AnsiColors.Blue);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Metodo {methodRequest.Name} recibido.");
            WriteColor.Set(WriteColor.AnsiColors.Reset);

            // Obtiene los datos del gateway
            GetGatewayData();

            // Envia la telemetria
            await SendDataMessage();

            // Envia los datos como respuesta del metodo
            return new MethodResponse(Encoding.UTF8.GetBytes(gwData.ToJsonString()), 200);
        }

    }
}
