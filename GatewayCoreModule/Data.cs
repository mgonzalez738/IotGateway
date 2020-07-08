using GatewayGeneral;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;

namespace GatewayCoreModule
{

    public class InclinometerNodeData
    {
        private AnalogValue heigh;
        private AnalogValue tiltX;    
        private AnalogValue tiltY;
        private AnalogValue rotation;

        public InclinometerNodeData()
        {
            heigh = new AnalogValue(UnitsDistance.Meters);
            tiltX = new AnalogValue(UnitsTilt.Degrees);
            tiltY = new AnalogValue(UnitsTilt.Degrees);
            rotation = new AnalogValue(UnitsAngle.Degrees);
        }

        public string ToJsonString()
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String });
            return st;
        }

        public AnalogValue Heigh
        {
            get { return heigh; }
            set { heigh = value; }
        }

        public AnalogValue TiltX
        {
            get { return tiltX; }
            set { tiltX = value; }
        }

        public AnalogValue TiltY
        {
            get { return tiltY; }
            set { tiltY = value; }
        }

        public AnalogValue Rotation
        {
            get { return rotation; }
            set { rotation = value; }
        }
    }

    public class WindNodeData
    {
        private AnalogValue speed;
        private AnalogValue direction;

        public WindNodeData()
        {
            speed = new AnalogValue(UnitsSpeed.KilometerPerHour);
            direction = new AnalogValue(UnitsAngle.Degrees);
        }

        public string ToJsonString()
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String });
            return st;
        }

        public AnalogValue Speed
        {
            get { return speed; }
            set { speed = value; }
        }

        public AnalogValue Direction
        {
            get { return direction; }
            set { direction = value; }
        }
    }

    public class InclinometerChainData
    {
        private int nodesQuantity;
        private DateTime utcTime;
        private List<InclinometerNodeData> nodes;
        private WindNodeData wind;

        public InclinometerChainData(int nQty)
        {
            int i;
            nodesQuantity = nQty;

            utcTime = new DateTime();
            nodes = new List<InclinometerNodeData>();
            for(i=0; i<nodesQuantity; i++)
                nodes.Add(new InclinometerNodeData());
            wind = new WindNodeData();
        }

        public string ToJsonString()
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String });
            return st;
        }

        public void ToJsonFile(string filePath)
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String, Formatting = Formatting.Indented });
            File.WriteAllText(filePath, st);
        }

        public DateTime UtcTime
        {
            get { return utcTime; }
            set { utcTime = RoundDateTime.RoundToSeconds(((DateTime)value).ToUniversalTime()); }
        }

        public List<InclinometerNodeData> Nodes
        {
            get { return nodes; }
            set { nodes = value; }
        }

        public WindNodeData Wind
        {
            get { return wind; }
            set { wind = value; }
        }
    }

    public class GatewayData
    {
        private DateTime utcTime;
        private AnalogValue powerVoltage;
        private AnalogValue sensedVoltage;
        private AnalogValue batteryVoltage;
        private AnalogValue temperature;
        private bool sent;

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
            sent = false;
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

        public void ToJsonFile(string filePath)
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String, Formatting = Formatting.Indented });
            File.WriteAllText(filePath, st);
        }

        public DateTime UtcTime
        {
            get { return utcTime; }
            set { utcTime = RoundDateTime.RoundToSeconds(((DateTime)value).ToUniversalTime()); }
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

        [JsonIgnore]
        public bool Sent
        {
            get { return sent; }
            set { sent = value; }
        }
    }

    public class GatewayEvent
    {
        private DateTime utcTime;
        private EventType messageType;
        private String message;

        public GatewayEvent()
        {
            utcTime = new DateTime();
            messageType = EventType.Info;
            message = "";
        }

        public string ToJsonString()
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String });
            return st;
        }

        public void ToJsonFile(string filePath)
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String, Formatting = Formatting.Indented });
            File.WriteAllText(filePath, st);
        }

        public DateTime UtcTime
        {
            get { return utcTime; }
            set { utcTime = RoundDateTime.RoundToSeconds(((DateTime)value).ToUniversalTime()); }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public EventType MessageType
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
