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
    using System.Device.Gpio;

    class Program
    {
        static int counter;

        //static GpioController controller = null;

        private static IGateway gateway;
                
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
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime

            ModuleClient gatewayModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            gatewayModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, gatewayModuleClient).Wait();
            await gatewayModuleClient.OpenAsync();

            Console.WriteLine($"{DateTime.Now}: Gateway module initialized.");

            // Obtiene el gemelo y propiedades deseadas

            Console.WriteLine($"{DateTime.Now}: Retrieving twin..."); 
            var twinTask = gatewayModuleClient.GetTwinAsync();
            twinTask.Wait();
            var twin = twinTask.Result;
            Console.WriteLine(JsonConvert.SerializeObject(twin.Properties));

            // Actualiza propiedades reportadas

            Console.WriteLine($"{DateTime.Now}: Sending datetime app launch as reported property.");
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastAppLaunch"] = DateTime.Now;
            await gatewayModuleClient.UpdateReportedPropertiesAsync(reportedProperties);

            // Register callback to be called when a message is received by the module
            await gatewayModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, gatewayModuleClient);

            gateway = new GatewayRPI3Plus();

            gateway.UserButtonPushed += Gateway_UserButtonPushed;          

            // Inicia la tarea del led de status

            var threadStatusLed = new Thread(() => ThreadBodyStatusLed(gatewayModuleClient));
            threadStatusLed.Start();
        }

        private static void Gateway_UserButtonPushed(object sender, EventArgs e)
        {
            Console.WriteLine($"{DateTime.Now}: User Button presionado.");
            gateway.ToggleUserLed();
        }
        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"{DateTime.Now}: Desired property change.");
            Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));
            Console.WriteLine($"{DateTime.Now}: Sending datetime as reported property.");
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
                gateway.SetStatusLed(LedState.On);
                await Task.Delay(statusLedOnMs);

                // turn off the LED
                gateway.SetStatusLed(LedState.Off);
                await Task.Delay(10000);//statusLedPeriodMs);

                Console.WriteLine($"UTC Time received: {GetNetworkTime().Result.Value}."); 
            }
            
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            //Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                using (var pipeMessage = new Message(messageBytes))
                {
                    foreach (var prop in message.Properties)
                    {
                        pipeMessage.Properties.Add(prop.Key, prop.Value);
                    }
                    await moduleClient.SendEventAsync("output1", pipeMessage);

                    //Console.WriteLine("Received message sent");
                }
            }
            return MessageResponse.Completed;
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
    }
}
