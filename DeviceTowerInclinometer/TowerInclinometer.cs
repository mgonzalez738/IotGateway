using GatewayGeneral;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TowerInclinometer
{
    public class DeviceTowerInclinometer : IDevicesRs485
    {
        private string deviceId;
        private DeviceTypes deviceType;
        private string connectionString;
        private DeviceInterfaces deviceInterface;
        private DeviceClient device;
        private TowerInclinometerProperties deviceProperties;
        private TowerInclinometerData deviceData;
        private string storageFolder;
        private string configFolder;

        private ScheduleTimer timerPollData;
        private ScheduleTimer timerSendData;

        // CONSTRUCTOR

        public DeviceTowerInclinometer(string id, DeviceInterfaces iface, string connection)
        {
            deviceId = id;
            deviceInterface = iface;
            deviceType = DeviceTypes.TowerInclinometer;
            connectionString = connection;
        }

        // METODOS

        public async Task Init()
        {
            // Obtiene variables de entorno

            storageFolder = Environment.GetEnvironmentVariable("storageFolder");
            configFolder = Environment.GetEnvironmentVariable("configFolder");

            // Inicializa
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Inicializando.");

            try
            {
                // Registra el dispositivo
                this.device = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt_Tcp_Only);
                await this.device.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, this.device);
                //await this.device.SetMethodDefaultHandlerAsync(OnNotImplementedMethod, this.device);
                //await this.device.SetMethodHandlerAsync("PollData", OnPollData, this.device);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Error -> {ex.Message}");
                return;
            }

            // Carga la configuracion desde host
            // En caso de error, crea configuracion default y la guarda en Host

            try
            {
                deviceProperties = TowerInclinometerProperties.FromJsonFile(configFolder + "config" + this.deviceId + ".json");
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Configuracion cargada desde host.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Error cargando configuracion desde host -> {ex.Message}");
                deviceProperties = new TowerInclinometerProperties();

                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Configuracion por defecto creada.");
                try
                {
                    deviceProperties.ToJsonFile(configFolder + "config" + this.deviceId + ".json");
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Configuracion por defecto guardada en host.");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Error guardando configuración en Host -> {ex2.Message}");
                }
            }

            // Envia el evento de reinicio al Hub

            TowerInclinometerEvent evt = new TowerInclinometerEvent();
            evt.UtcTime = RoundDateTime.RoundToSeconds(DateTime.Now);
            evt.MessageType = EventType.Info;
            evt.Message = "Inclinometro Reiniciado";
            await SendEventMessage(evt);

            // Obtiene el gemelo y sincroniza propiedades con deseadas del Hub

            try
            {
                var twin = await this.device.GetTwinAsync();
                WriteColor.Set(WriteColor.AnsiColors.Cyan);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Descarga propiedades deseadas (V:" + twin.Properties.Desired.Version.ToString() + ").");
                WriteColor.Set(WriteColor.AnsiColors.Reset);
                await UpdateDeviceProperties(twin.Properties.Desired);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Error descargando propiedades deseadas del Hub -> {ex.Message}.");
            }

            // Actualiza propiedades reportadas

            await SendReportedProperties(deviceProperties);
            WriteColor.Set(WriteColor.AnsiColors.Cyan);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Envio propiedades reportadas.");
            WriteColor.Set(WriteColor.AnsiColors.Reset);

            // Crea los datos del dispositivo

            this.deviceData = new TowerInclinometerData(this.deviceProperties.Nodes.Count);

            // Crea los timer de encuesta y telemetria de datos del dispositivo

            this.timerPollData = new ScheduleTimer();
            this.timerPollData.Elapsed += TimerPollData_Elapsed;

            this.timerSendData = new ScheduleTimer();
            this.timerSendData.Elapsed += TimerSendData_Elapsed;

            if (this.deviceProperties.PollData.Enabled)
            {
                // Inicia el timer de encuesta de datos
                this.timerPollData.Start(this.deviceProperties.PollData.Period, this.deviceProperties.PollData.Unit);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Primera ejecucion encuesta datos: {timerPollData.FirstExcecution} / Periodo: {timerPollData.PeriodMs} ms");
                if (this.deviceProperties.DetachedTelemetry.Enabled)
                {
                    // Inicia el timer de telemetria de datos si esta desdoblado
                    this.timerSendData.Start(this.deviceProperties.DetachedTelemetry.Period, this.deviceProperties.DetachedTelemetry.Unit);
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Primera ejecucion telemetria datos: {timerSendData.FirstExcecution} / Periodo: {timerSendData.PeriodMs} ms");
                }
                else
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Telemetria de datos simultanea con encuesta.");
                }
            }
            else
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Encuesta datos y telemetria deshabilitadas.");
            }

            // Registra eventos de cambio de propiedades

            this.deviceProperties.PollDataChanged += DeviceProperties_PollDataChanged;
            this.deviceProperties.DetachedTelemetryChanged += DeviceProperties_DetachedTelemetryChanged;
            this.deviceProperties.NodesChanged += DeviceProperties_NodesChanged;
            this.deviceProperties.AnemometerChanged += DeviceProperties_AnemometerChanged;
        }

        // EVENTOS DEL DISPOSITIVO

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            WriteColor.Set(WriteColor.AnsiColors.Cyan);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Recepcion propiedades deseadas (V:" + desiredProperties.Version.ToString() + ").");
            WriteColor.Set(WriteColor.AnsiColors.Reset);

            await UpdateDeviceProperties(desiredProperties);

            // Envia las propiedades reportadas

            await SendReportedProperties(this.deviceProperties);

            // Elimina propiedades marcadas
            this.deviceProperties.DeleteMarkedNodes();
            // Guarda las propiedades en archivo de configuraion
            this.deviceProperties.ToJsonFile(configFolder + "config" + this.deviceId + ".json");

            WriteColor.Set(WriteColor.AnsiColors.Cyan);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Envio propiedades reportadas.");
            WriteColor.Set(WriteColor.AnsiColors.Reset);
        }

        private Task UpdateDeviceProperties(TwinCollection desiredProp)
        {
            // Poll Data
            try
            {
                if (desiredProp["PollData"] == null)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] No se puede eliminar la propiedad PollData.");
                }
                else
                {
                    TowerInclinometerDataPollConfiguration ipdc = TowerInclinometerDataPollConfiguration.FromJsonString(desiredProp["PollData"].ToString());
                    this.deviceProperties.PollData = ipdc;
                }
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] No hay configuracion para PollData.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Error en configuracion de PollData (Ingnorando cambios) -> {ex.Message}");
            }

            // Detached Telemetry
            try
            {
                if (desiredProp["DetachedTelemetry"] == null)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] No se puede eliminar la propiedad DetachedTelemetry.");
                }
                else
                {
                    TowerInclinometerDetachedTelemetryConfiguration idtc = TowerInclinometerDetachedTelemetryConfiguration.FromJsonString(desiredProp["DetachedTelemetry"].ToString());
                    this.deviceProperties.DetachedTelemetry = idtc;
                }
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] No hay configuracion para DetachedTelemetry.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Error en configuracion de DetachedTelemetry (Ingnorando cambios) -> {ex.Message}");
            }

            // Nodes
            try
            {
                if (desiredProp["Nodes"] == null)
                {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] No se puede eliminar la propiedad Nodes.");
                }
                else
                {
                    List<TowerInclinometerNodeConfiguration> lnod = JsonConvert.DeserializeObject<List<TowerInclinometerNodeConfiguration>>(desiredProp["Nodes"].ToString(), new TowerInclinometerNodeConfigurationListJsonConverter());
                    if (lnod == null)
                        Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] No hay configuracion de nodos para Nodes.");
                    else if (lnod.Count == 0)
                        Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] No hay configuracion de nodos para Nodes.");
                    else
                    {
                        // Guarda la nueva configuracion de dispositivos
                        this.deviceProperties.Nodes = lnod;
                    }
                }
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] No hay configuracion para DevicesRs485.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Error en configuracion de DevicesRs485 (Ingnorando cambios) -> {ex.Message}");
            }

            // Guarda las propiedades en archivo de configuraion
            this.deviceProperties.ToJsonFile(configFolder + "config" + this.deviceId + ".json");

            return Task.FromResult("Ok");
        }

        // EVENTOS DE CAMBIO DE PROPIEDADES

        private void DeviceProperties_PollDataChanged(object sender, EventArgs e)
        {
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Propiedad PollData modificada.");

            // Detiene la encuesta

            this.timerPollData.Stop();

            if (this.deviceProperties.PollData.Enabled)
            {
                timerPollData.Start(this.deviceProperties.PollData.Period, this.deviceProperties.PollData.Unit);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Proxima ejecucion encuesta datos: {timerPollData.FirstExcecution} / Periodo: {timerPollData.PeriodMs} ms");
            }
            else
            {
                timerSendData.Stop();
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Encuesta datos y telemetria datos deshabilitadas.");
            }
        }

        private void DeviceProperties_DetachedTelemetryChanged(object sender, EventArgs e)
        {
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Propiedad DetachedTelemetry modificada.");

            // Detiene la telemetria

            timerSendData.Stop();

            if (this.deviceProperties.DetachedTelemetry.Enabled)
            {
                timerSendData.Start(this.deviceProperties.DetachedTelemetry.Period, this.deviceProperties.DetachedTelemetry.Unit);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Proxima ejecucion telemetria datos: {timerSendData.FirstExcecution} / Periodo: {timerSendData.PeriodMs} ms");
            }
            else
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Telemetria de datos simultanea con encuesta.");
        }

        private void DeviceProperties_NodesChanged(object sender, EventArgs e)
        {
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Propiedad Nodes modificada.");

            this.deviceData.Nodes.Clear();
            foreach(var nodeConf in this.deviceProperties.Nodes)
            {
                if (!nodeConf.Delete)
                    this.deviceData.Nodes.Add(new TowerInclinometerNodeData());
            }
        }

        private void DeviceProperties_AnemometerChanged(object sender, EventArgs e)
        {
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Propiedad Anemometer modificada.");

            // Hacer algo ¿?
        }

        private async Task SendReportedProperties(TowerInclinometerProperties dp)
        {
            TwinCollection reportedProperties = new TwinCollection(dp.ToJsonString());
            //Console.WriteLine(reportedProperties);

            await this.device.UpdateReportedPropertiesAsync(reportedProperties);

            // Guarda las propiedades en archivo de configuraion
            this.deviceProperties.ToJsonFile(configFolder + "config" + this.deviceId + ".json");
        }

        private void TimerSendData_Elapsed(object sender, EventArgs e)
        {
            
        }

        private void TimerPollData_Elapsed(object sender, EventArgs e)
        {
            // Obtiene los datos del gateway
            GetDeviceData();

            // Envia la telemetria si no esta separada
            if (!this.deviceProperties.DetachedTelemetry.Enabled)
                _ = SendDataMessage();
        }

        private void GetDeviceData()
        {
            // Prueba
            int i;
            Random rnd = new Random();
            this.deviceData.UtcTime = RoundDateTime.RoundToSeconds(DateTime.Now);
            this.deviceData.Nodes[0].Heigh.Value = 6;
            this.deviceData.Nodes[0].DeviationX.Value = 20 * (rnd.NextDouble() - 0.5);
            this.deviceData.Nodes[0].DeviationY.Value = 20 * (rnd.NextDouble() - 0.5);
            this.deviceData.Nodes[0].Rotation.Value = 2 * (rnd.NextDouble() - 0.5);
            for (i = 1; i < this.deviceData.Nodes.Count; i++)
            {
                this.deviceData.Nodes[i].Heigh.Value = 6 + i * 6;
                this.deviceData.Nodes[i].DeviationX.Value = 20 * (rnd.NextDouble() - 0.5) + this.deviceData.Nodes[i - 1].DeviationX.Value;
                this.deviceData.Nodes[i].DeviationY.Value = 20 * (rnd.NextDouble() - 0.5) + this.deviceData.Nodes[i - 1].DeviationY.Value;
                this.deviceData.Nodes[i].Rotation.Value = 2 * (rnd.NextDouble() - 0.5) + this.deviceData.Nodes[i - 1].Rotation.Value;
            }
            this.deviceData.Anemometer.Speed.Value = 20 * rnd.NextDouble();
            this.deviceData.Anemometer.Direction.Value = 360 * rnd.NextDouble();

            this.deviceData.Sent = false;

            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Encuesta Datos.");
        }

        private async Task SendDataMessage()
        {
            // Verifica que los datos no hayan sido enviados aun
            if (!this.deviceData.Sent)
            {
                // Crea el mensaje a partir de los datos del gateway
                Message msg = new Message(Encoding.UTF8.GetBytes(this.deviceData.ToJsonString()));
                msg.ContentEncoding = "utf-8";
                msg.ContentType = "application/json";

                // Agrega propiedad identificando al mensaje como de datos
                msg.Properties.Add("MessageType", MessageType.Data.ToString());
                msg.Properties.Add("DeviceType", DeviceTypes.TowerInclinometer.ToString());

                // Marca los datos como enviados y los envia
                this.deviceData.Sent = true;
                await this.device.SendEventAsync(msg);

                // Loggea el envio en consola
                WriteColor.Set(WriteColor.AnsiColors.Green);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Envio Datos.");
                WriteColor.Set(WriteColor.AnsiColors.Reset);
            }
        }

        public async Task Stop()
        {
            try 
            {
                await this.device.CloseAsync();
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Desconectado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Error -> {ex.Message}");
            }
            
        }

        private async Task SendEventMessage(TowerInclinometerEvent gevt)
        {
            // Crea el mensaje a partir del evento del dispositivoy
            Message msg = new Message(Encoding.UTF8.GetBytes(gevt.ToJsonString()));
            msg.ContentEncoding = "utf-8";
            msg.ContentType = "application/json";

            // Agrega propiedad identificando al mensaje como Evento
            msg.Properties.Add("MessageType", MessageType.Event.ToString());
            msg.Properties.Add("DeviceType", DeviceTypes.TowerInclinometer.ToString());
            await device.SendEventAsync(msg);

            // Loggea el envio en consola
            WriteColor.Set(WriteColor.AnsiColors.Yellow);
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Envio Evento: {gevt.ToJsonString()}");
            WriteColor.Set(WriteColor.AnsiColors.Reset);
        }

        // PROPIEDADES

        public string DeviceId
        {
            get { return this.deviceId; }
            set { this.deviceId = value; }
        }
        public DeviceTypes DeviceType
        {
            get { return this.deviceType; }
        }
        public string ConnectionString
        {
            get { return this.connectionString; }
            set { this.connectionString = value; }
        }
        public DeviceInterfaces DeviceInterface
        {
            get { return this.deviceInterface; }
            set { this.deviceInterface = value; }
        }
    }
}
