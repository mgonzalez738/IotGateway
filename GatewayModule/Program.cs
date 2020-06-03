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

    using System.Device.Gpio;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    class Program
    {
        static int counter;

        static GpioController controller = null;
                
        static int statusLedPin = 17;      // Status Led Pin 
        static int userLedPin = 27;        // User Led Pin
        static int userButtonPin = 22;     // User Button 

        static bool userLedOn = false;
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

            // Construct GPIO controller
            controller = new GpioController();

            // Establece la direccion de los pines de leds y boton

            controller.OpenPin(statusLedPin, PinMode.Output);
            controller.OpenPin(userLedPin, PinMode.Output);
            controller.OpenPin(userButtonPin, PinMode.Input);

            // Registra la funcion de llamada cuando se presiona el boton de usuario

            controller.RegisterCallbackForPinValueChangedEvent(userButtonPin, PinEventTypes.Rising, (o, e) =>
            {
                Console.Write($"{DateTime.Now}: User Button presionado.");
                if (userLedOn)
                {
                    controller.Write(userLedPin, PinValue.Low);
                    userLedOn = false;
                    Console.WriteLine($" User Led apagado.");
                }
                else
                {
                    controller.Write(userLedPin, PinValue.High);
                    userLedOn = true;
                    Console.WriteLine($" User Led encendido.");
                }
            });

            // Inicia la tarea del led de status

            var threadStatusLed = new Thread(() => ThreadBodyStatusLed(gatewayModuleClient));
            threadStatusLed.Start();
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
                controller.Write(statusLedPin, PinValue.Low);
                await Task.Delay(statusLedOnMs);

                // turn off the LED
                controller.Write(statusLedPin, PinValue.High);
                await Task.Delay(statusLedPeriodMs);
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
    }
}
