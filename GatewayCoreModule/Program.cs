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

    class Program
    {
        private static IGatewayHardware gwHardware;
        private static ModuleClient gatewayModuleClient;
        private static ScheduleTimer timerPollData;
        private static String storageFolder;
        private static String configFolder;

        private static GatewayProperties gwProperties;
        private static GatewayData gwData;

        static int statusLedPeriodMs = 1000;
        static int statusLedOnMs = 200;

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

            // Inicia el modulo

            await gatewayModuleClient.OpenAsync();
            Console.WriteLine($"{DateTime.Now}> Modulo Gateway inicializado.");

            // Envia el evento de reinicio al Hub

            GatewayEvent gevt = new GatewayEvent();
            gevt.UtcTime = DateTime.Now;
            gevt.MessageType = GatewayEventType.Info;
            gevt.Message = "Gateway Reiniciado";
            _ = SendEventMessage(gevt);

            // Obtiene el path a las carpetas en el host

            storageFolder = Environment.GetEnvironmentVariable("storageFolder");
            configFolder = Environment.GetEnvironmentVariable("configFolder");
            Console.WriteLine($"{DateTime.Now}> Path a carpeta de almacenamiento cargado: {storageFolder}.");
            Console.WriteLine($"{DateTime.Now}> Path a carpeta de configuracion cargado: {configFolder}.");

            // Carga la configuracion desde host
            // En caso de error, crea configuracion default y la guarda en Host

            try
            {
                gwProperties = GatewayProperties.FromJsonFile(configFolder + "config.json");
                Console.WriteLine($"{DateTime.Now}> Configuracion cargada desde host.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}> Error cargando configuracion desde host -> {ex.Message}");
                gwProperties = new GatewayProperties();
                Console.WriteLine($"{DateTime.Now}> Configuracion por defecto creada.");
                try
                {
                    gwProperties.ToJsonFile(configFolder + "config.json");
                    Console.WriteLine($"{DateTime.Now}> Configuracion por defecto guardada en host.");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"{DateTime.Now}> Error guardando configuración en Host -> {ex2.Message}");
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
                    Console.WriteLine($"{DateTime.Now}> Fecha y Hora del RTC actualizada por NTP [{res.Value}].");
                }
                else
                    Console.WriteLine($"{DateTime.Now}> Error actualizando RTC por NTP[{ res.Error}].");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}> Error obteniendo hora por NTP -> {ex.Message}.");
            }

            // Obtiene el gemelo y sincroniza propiedades con deseadas del Hub

            try
            {
                var twin = await gatewayModuleClient.GetTwinAsync();
                Console.WriteLine($"{DateTime.Now}> Propiedades deseadas descargadas desde el Hub.");
                UpdateGatewayProperties(twin.Properties.Desired);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}> Error descargando propiedades deseadas del Hub -> {ex.Message}.");
            }

            // Actualiza propiedades reportadas

            await SendReportedProperties(gwProperties);
            Console.WriteLine($"{DateTime.Now}> Actualizacion de propiedades reportadas enviada.");

            // Inicia la tarea del led de status

            var threadStatusLed = new Thread(() => ThreadBodyStatusLed(gatewayModuleClient));
            threadStatusLed.Start();

            // Crea el timer de telemetria de datos del gateway y lo inicia si esta habilitado

            timerPollData = new ScheduleTimer();
            timerPollData.Elapsed += TimerPollData_Elapsed;
            if (gwProperties.PollData.Enabled)
            {
                timerPollData.Start(gwProperties.PollData.Period, gwProperties.PollData.Unit);
                Console.WriteLine($"{DateTime.Now}> Primera ejecucion telemetria datos: {timerPollData.FirstExcecution} / Periodo: {timerPollData.PeriodMs} ms");
            }
            else
                Console.WriteLine($"{DateTime.Now}> Telemetria datos deshabilitada.");

            // Registra eventos de cambio de propiedades

            gwProperties.PollDataChanged += GwProperties_PollDataChanged;
        }

        // ENCUESTA Y TELEMETRIA DE DATOS

        private static void TimerPollData_Elapsed(object sender, EventArgs e)
        {
            // Obtiene los datos del gateway
            GetGatewayData();

            // Envia la telemetria
            _ = SendTelemetryMessage();
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
            Console.WriteLine($"{DateTime.Now}> User Button presionado.");

            // Cambia el estado del led de usuario
            gwHardware.ToggleUserLed();

            // Crea el evento
            GatewayEvent gevt = new GatewayEvent();
            gevt.UtcTime = DateTime.Now;
            gevt.MessageType = GatewayEventType.Info;
            gevt.Message = "User Button presionado";
                        
            // Envia el evento
            _ = SendEventMessage(gevt);
        }

        // EVENTOS DE CAMBIO DE PROPIEDADES

        private static void GwProperties_PollDataChanged(object sender, EventArgs e)
        {
            Console.WriteLine($"{DateTime.Now}> Propiedad PollData modificada.");

            // Detiene la encuesta

            timerPollData.Stop();

            if (gwProperties.PollData.Enabled)
            {
                timerPollData.Start(gwProperties.PollData.Period, gwProperties.PollData.Unit);
                Console.WriteLine($"{DateTime.Now}> Nueva ejecucion telemetria datos: {timerPollData.FirstExcecution} / Periodo: {timerPollData.PeriodMs} ms");
            }
            else
                Console.WriteLine($"{DateTime.Now}> Telemetria datos deshabilitada.");
        }

        // EVENTOS DEL MODULO

        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"{DateTime.Now}> Actualizacion de propiedades deseadas recibida.");

            UpdateGatewayProperties(desiredProperties);

            // Envia las propiedades reportadas

            await SendReportedProperties(gwProperties);

            Console.WriteLine($"{DateTime.Now}> Actualizacion de propiedades reportadas enviada.");
        }

        /// <summary>
        /// Metodo que procesa los mensajes recibidos por el modulo
        /// </summary>
        static Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            Console.WriteLine($"{DateTime.Now}: Mensaje recibido.");

            var moduleClient = (ModuleClient)userContext;
            if (moduleClient == null)
            {
                Console.WriteLine($"{DateTime.Now}: Error: No indica Modulo en el contexto.");
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
                    Console.WriteLine($"{DateTime.Now}: Error conctando al servidor NTP {ntpServer}");
                    Console.WriteLine($"{connectResult.Error}");
                    return Result.Fail<DateTime>(connectResult.Error);
                }

                var sendResult = await socket.SendWithTimeoutAsync(ntpData, 0, ntpData.Length, 0, 5000);

                if (sendResult.Failure)
                {
                    Console.WriteLine($"{DateTime.Now}: Error enviando datos al servidor NTP {ntpServer}");
                    Console.WriteLine($"{sendResult.Error}");
                    return Result.Fail<DateTime>(connectResult.Error);
                }

                var receiveResult = await socket.ReceiveWithTimeoutAsync(ntpData, 0, ntpData.Length, 0, 5000);

                if (receiveResult.Failure)
                {
                    Console.WriteLine($"{DateTime.Now}: Error recibiendo datos del servidor NTP {ntpServer}");
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

        static void UpdateGatewayProperties(TwinCollection desiredProp)
        {
            // Poll Data
            try
            {
                GatewayDataPollConfiguration gpdc = GatewayDataPollConfiguration.FromJsonString(desiredProp["PollData"].ToString());
                gwProperties.PollData = gpdc;     
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"{DateTime.Now}> No hay configuracion para Poll Data -> {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}> Error en configuracion de Poll Data (Ingnorando cambios) -> {ex.Message}");
            }

            // Guarda las propiedades en archivo de configuraion
            gwProperties.ToJsonFile(configFolder + "config.json");
        }

        static void GetGatewayData()
        {
            // Adquiere datos del gateway
            
            gwData.UtcTime = DateTime.UtcNow;
            gwData.PowerVoltage.Value = gwHardware.GetPowerVoltage();
            gwData.SensedVoltage.Value = gwHardware.GetSensedVoltage();
            gwData.BatteryVoltage.Value = gwHardware.GetBatteryVoltage();
            gwData.Temperature.Value = gwHardware.GetRtcTemperature();           
        }

        private static void gwData_VarableStateChanged(object sender, StateChangeEventArgs e)
        {
            AnalogValue val = (AnalogValue)sender;
            GatewayEvent gevt = new GatewayEvent();
            gevt.UtcTime = DateTime.Now;
            gevt.MessageType = GatewayEventType.Warning;
            gevt.Message = $"Cambio de estado. {e.PropertyName} = {val.Value:0.000000} {val.Config.Unit} ({e.PreviousState} -> {e.ActualState}).";
            _ = SendEventMessage(gevt);
        }

        private static async Task SendTelemetryMessage()
        {
            // Crea el mensaje a partir de los datos del gateway
            Message msg = new Message(Encoding.UTF8.GetBytes(gwData.ToJsonString()));

            // Agrega propiedad identificando al mensaje como de Telemetria
            msg.Properties.Add("Type", GatewayMessageType.Telemetry.ToString());
            await gatewayModuleClient.SendEventAsync("output1", msg);

            // Loggea el envio en consola
            Console.WriteLine($"{DateTime.Now}> Envio Telemetria: {gwData.ToJsonString()}");
        }

        private static async Task SendEventMessage(GatewayEvent gevt)
        {
            // Crea el mensaje a partir del evento del gateway
            Message msg = new Message(Encoding.UTF8.GetBytes(gevt.ToJsonString()));

            // Agrega propiedad identificando al mensaje como Evento
            msg.Properties.Add("Type", GatewayMessageType.Event.ToString());
            await gatewayModuleClient.SendEventAsync("output1", msg);

            // Loggea el envio en consola
            Console.WriteLine($"{DateTime.Now}> Envio Evento: {gevt.ToJsonString()}");
        }

        private static async Task SendReportedProperties(GatewayProperties gp)
        {
            TwinCollection reportedProperties = new TwinCollection(gp.ToJsonString());
            await gatewayModuleClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        // METODOS DIRECTOS DEL MODULO       

        /// <summary>
        /// Maneja los metodos recibidos no implementados
        /// </summary>
        private static Task<MethodResponse> OnNotImplementedMethod(MethodRequest methodRequest, object userContext)
        {
            string message = $"Metodo {methodRequest.Name} no implementado";

            Console.WriteLine($"{DateTime.Now}> {message}.");
           
            var result = JsonConvert.SerializeObject(message, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 404));
        }

        /// <summary>
        /// Actualiza el RTC por NTP
        /// </summary>
        private static async Task<MethodResponse> OnUpdateRtcFromNtp(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"{DateTime.Now}> Metodo {methodRequest.Name} recibido.");
            var res = await GetNetworkTime();
            
            if (res.Success)
            {
                gwHardware.SetRtcDateTime(res.Value);
                string message = $"Fecha y Hora del RTC actualizada por NTP [{res.Value}].";
                Console.WriteLine($"{DateTime.Now}> {message}.");
                var result = JsonConvert.SerializeObject(message, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
            }
            else
            {
                string message = $"Error actualizando RTC por NTP [{res.Error}].";
                Console.WriteLine($"{DateTime.Now}> {message}.");
                var result = JsonConvert.SerializeObject(message, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                return new MethodResponse(Encoding.UTF8.GetBytes(result), 404);
            }
        }

        /// <summary>
        /// Alterna el estado del led de usuario
        /// </summary>
        private static Task<MethodResponse> OnToggleUserLedState(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"{DateTime.Now}> Metodo {methodRequest.Name} recibido.");

            gwHardware.ToggleUserLed();
            LedState st = gwHardware.GetUserLed();

            object payload = new { UserLedState = st.ToString() };
            string result = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            Console.WriteLine($"{DateTime.Now}> {result}");

            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        /// <summary>
        /// Fuerza la adquisicion y envio de telemetria
        /// </summary>
        private static Task<MethodResponse> OnPollData(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"{DateTime.Now}> Metodo {methodRequest.Name} recibido.");

            // Obtiene los datos del gateway

            GetGatewayData();

            // Envia la telemetria

            _ = SendTelemetryMessage();

            // Envia los datos como respuesta del metodo

            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(gwData.ToJsonString()), 200));
        }

        
    }
}
