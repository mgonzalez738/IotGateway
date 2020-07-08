using GatewayGeneral;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using System;
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
            // Inicializa
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Inicializando.");

            // Registra el dispositivo
            this.device = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt_Tcp_Only);

            // Envia el evento de reinicio al Hub

            TowerInclinometerEvent evt = new TowerInclinometerEvent();
            evt.UtcTime = RoundDateTime.RoundToSeconds(DateTime.Now);
            evt.MessageType = EventType.Info;
            evt.Message = "Inclinometro Reiniciado";
            await SendEventMessage(evt);

            // Crea los timer de encuesta y telemetria de datos del dispositivo

            timerPollData = new ScheduleTimer();
            timerPollData.Elapsed += TimerPollData_Elapsed;

//            timerSendData = new ScheduleTimer();
//            timerSendData.Elapsed += TimerSendData_Elapsed;

//            if (gwProperties.PollData.Enabled)
//            {
                // Inicia el timer de encuesta de datos
                timerPollData.Start(1, ScheduleUnit.minute);
                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Primera ejecucion encuesta datos: {timerPollData.FirstExcecution} / Periodo: {timerPollData.PeriodMs} ms");
 //               if (gwProperties.DetachedTelemetry.Enabled)
 //               {
 //                   // Inicia el timer de telemetria de datos si esta desdoblado
//                   timerSendData.Start(gwProperties.DetachedTelemetry.Period, gwProperties.DetachedTelemetry.Unit);
 //                   Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Primera ejecucion telemetria datos: {timerSendData.FirstExcecution} / Periodo: {timerSendData.PeriodMs} ms");
 //               }
 //               else
  //              {
                    Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Telemetria de datos simultanea con encuesta.");
 //               }
//            }
//            else
//            {
//                Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [Gateway] Encuesta datos y telemetria deshabilitadas.");
//            }
        }

        private void TimerSendData_Elapsed(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void TimerPollData_Elapsed(object sender, EventArgs e)
        {
            // Obtiene los datos del gateway
            //GetGatewayData();

            // Envia la telemetria si no esta separada
            //if (!gwProperties.DetachedTelemetry.Enabled)
            //    _ = SendDataMessage();

            // Envia el evento de prueba, borrar

            TowerInclinometerEvent evt = new TowerInclinometerEvent();
            evt.UtcTime = RoundDateTime.RoundToSeconds(DateTime.Now);
            evt.MessageType = EventType.Info;
            evt.Message = "Prueba";
            _ = SendEventMessage(evt);
        }

        public async Task Stop()
        {
            await this.device.CloseAsync();
            Console.WriteLine($"{RoundDateTime.RoundToSeconds(DateTime.Now)}> [{this.deviceId}] Desconectado.");
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
