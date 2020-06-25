using GatewayGeneral;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace GatewayCoreModule
{
    public enum GatewayMessageType { Telemetry, Event }

    public enum GatewayEventType { Info, Warning, Error, Debug }

    public class GatewayData
    {
        private DateTime utcTime;
        private AnalogValue powerVoltage;
        private AnalogValue sensedVoltage;
        private AnalogValue batteryVoltage;
        private AnalogValue temperature;

        public event EventHandler<StateChangeEventArgs> StateChanged;

        public GatewayData()
        {
            utcTime = new DateTime();

            powerVoltage = new AnalogValue(UnitsVoltage.Volts);
            powerVoltage.StateChanged += PowerVoltage_StateChanged;
            sensedVoltage = new AnalogValue(UnitsVoltage.Volts);
            sensedVoltage.StateChanged += SensedVoltage_StateChanged;
            batteryVoltage = new AnalogValue(UnitsVoltage.Volts);
            batteryVoltage.StateChanged += BatteryVoltage_StateChanged;
            temperature = new AnalogValue(UnitsTemperature.Celsius);
            temperature.StateChanged += Temperature_StateChanged;
        }

        private void PowerVoltage_StateChanged(object sender, StateChangeEventArgs e)
        {
            e.PropertyName = "PowerVoltage";
            OnStateChanged(this.PowerVoltage, e);
        }

        private void SensedVoltage_StateChanged(object sender, StateChangeEventArgs e)
        {
            e.PropertyName = "SensedVoltage";
            OnStateChanged(this.SensedVoltage, e);
        }

        private void BatteryVoltage_StateChanged(object sender, StateChangeEventArgs e)
        {
            e.PropertyName = "BatteryVoltage";
            OnStateChanged(this.BatteryVoltage, e);
        }

        private void Temperature_StateChanged(object sender, StateChangeEventArgs e)
        {
            e.PropertyName = "Temperature";
            OnStateChanged(this.Temperature, e);
        }

        protected virtual void OnStateChanged(object sender, StateChangeEventArgs e)
        {
            StateChanged?.Invoke(sender, e);
        }

        public string ToJsonString()
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String });
            return st;
        }

        public DateTime UtcTime
        {
            get { return utcTime; }
            set { utcTime = value; }
        }

        public AnalogValue PowerVoltage
        {
            get { return powerVoltage; }
            set { powerVoltage = value; }
        }

        public AnalogValue SensedVoltage
        {
            get { return sensedVoltage; }
            set { sensedVoltage = value; }
        }

        public AnalogValue BatteryVoltage
        {
            get { return batteryVoltage; }
            set { batteryVoltage = value; }
        }

        public AnalogValue Temperature
        {
            get { return temperature; }
            set { temperature = value; }
        }
    }

    public class GatewayEvent
    {
        private DateTime utcTime;
        private GatewayEventType messageType;
        private String message;

        public GatewayEvent()
        {
            utcTime = new DateTime();
            messageType = GatewayEventType.Info;
            message = "";
        }

        public string ToJsonString()
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String });
            return st;
        }

        public DateTime UtcTime
        {
            get { return utcTime; }
            set { utcTime = value; }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public GatewayEventType MessageType
        {
            get { return messageType; }
            set { messageType = value; }
        }

        public String Message
        {
            get { return message; }
            set { message = value; }
        }
    }
}
