namespace GatewayModule
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
    using Common;

    class Program
    {
        private static IGatewayHardware gwHardware;

        private static ModuleClient gatewayModuleClient;

        private static ScheduleTimer timerPollData;

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
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Crea conexion al hardware, datos

            gwHardware = new GatewayRPI3Plus();

            // Registra funciones para manejar eventos del hardware

            gwHardware.UserButtonPushed += Gateway_UserButtonPushed;

            // Crea una conexion al runtime Edge

            gatewayModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

            // Registra la funcion para manejar cambios de propiedades deseadas

            await gatewayModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, gatewayModuleClient);

            // Registra las funciones para manejar metodos directos 

            await gatewayModuleClient.SetMethodDefaultHandlerAsync(OnNotImplementedMethod, gatewayModuleClient);
            await gatewayModuleClient.SetMethodHandlerAsync("UpdateRtcFromNtp", OnUpdateRtcFromNtp, gatewayModuleClient);
            //await gatewayModuleClient.SetMethodHandlerAsync("SetUserLedState", OnSetUserLedState, gatewayModuleClient);
            await gatewayModuleClient.SetMethodHandlerAsync("ToggleUserLedState", OnToggleUserLedState, gatewayModuleClient);
            await gatewayModuleClient.SetMethodHandlerAsync("PollData", OnPollData, gatewayModuleClient);

            // Registra las funciones para manejar los mensajes recibidos

            await gatewayModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, gatewayModuleClient);

            // Inicia el modulo

            await gatewayModuleClient.OpenAsync();

            Console.WriteLine($"{DateTime.Now}> Modulo Gateway inicializado.");

            // Actualiza fecha y hora del RTC por NTP

            var res = await GetNetworkTime();

            if (res.Success)
            {
                gwHardware.SetRtcDateTime(res.Value);
                string message = $"Fecha y Hora del RTC actualizada por NTP [{res.Value}].";
                Console.WriteLine($"{DateTime.Now}> {message}.");
            }
            else
            {
                string message = $"Error actualizando RTC por NTP [{res.Error}].";
                Console.WriteLine($"{DateTime.Now}> {message}.");
            }

            // Obtiene el gemelo y propiedades deseadas

            Console.WriteLine($"{DateTime.Now}> Obtiene propiedades deseadas."); 
            var twin = await gatewayModuleClient.GetTwinAsync();
            Console.WriteLine(JsonConvert.SerializeObject(twin.Properties));

            // Actualiza propiedades reportadas

            Console.WriteLine($"{DateTime.Now}> Envia DateTimeLastAppLaunch como propiedad reportada.");
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastAppLaunch"] = DateTime.Now;
            await gatewayModuleClient.UpdateReportedPropertiesAsync(reportedProperties);
            Console.WriteLine(JsonConvert.SerializeObject(reportedProperties));        

            // Inicia la tarea del led de status

            var threadStatusLed = new Thread(() => ThreadBodyStatusLed(gatewayModuleClient));
            threadStatusLed.Start();

            // Inicia el timer de encuesta de datos del gateway

            timerPollData = new ScheduleTimer();
            timerPollData.Elapsed += TimerPollData_Elapsed;
            _ = timerPollData.Start(15, ScheduleTimer.ScheduleUnit.minute);
        }

        private static async void Gateway_UserButtonPushed(object sender, EventArgs e)
        {
            Console.WriteLine($"{DateTime.Now}> User Button presionado.");

            gwHardware.ToggleUserLed();
            LedState st = gwHardware.GetUserLed();

            object payload = new { UserLedState = st.ToString() };
            string result = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            Console.WriteLine($"{DateTime.Now}> {result}");

            await gatewayModuleClient.SendEventAsync("output1", new Message(Encoding.UTF8.GetBytes(result)));
        }
        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"{DateTime.Now}> Desired property change.");
            Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));
            Console.WriteLine($"{DateTime.Now}> Sending datetime as reported property.");
            TwinCollection reportedProperties = new TwinCollection
            {
                ["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now
            };

            await ((ModuleClient)userContext).UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
        }

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

        static GatewayData GetGatewayData()
        {
            // Adquiere datos del gateway

            GatewayData gd = new GatewayData
            {               
                SampleUtcTime = DateTime.UtcNow,
                PowerVoltage = gwHardware.GetPowerVoltage(),
                SensedVoltage = gwHardware.GetSensedVoltage(),
                BatteryVoltage = gwHardware.GetBatteryVoltage(),
                Temperature = gwHardware.GetRtcTemperature()
            };

            return gd;
        }
        private static async Task SendTelemetry(GatewayData gd)
        {
            // Serializa los datos a JSON

            string result = JsonConvert.SerializeObject(gd, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            // Envia los datos

            Message msg = new Message(Encoding.UTF8.GetBytes(result));
            msg.Properties.Add("Type", "Telemetry");
            await gatewayModuleClient.SendEventAsync("output1", msg);

            // Loggea el envio

            Console.WriteLine($"{DateTime.Now}> Telemetria Datos: {result}");
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

            GatewayData gd = GetGatewayData();

            // Envia la telemetria

            _ = SendTelemetry(gd);

            // Envia los datos como respuesta del metodo

            string result = JsonConvert.SerializeObject(gd, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        // ENCUESTA Y TELEMETRIA DE DATOS

        private static async void TimerPollData_Elapsed(object sender, EventArgs e)
        {
            // Obtiene los datos del gateway

            GatewayData gd = GetGatewayData();

            // Envia la telemetria

            await SendTelemetry(gd);            
        }
    }
}
