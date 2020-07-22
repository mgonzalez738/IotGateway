using GatewayGeneral;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TowerInclinometer
{

    public class TowerInclinometerNodeData
    {
        private AnalogValue heigh;
        private AnalogValue deviationX;
        private AnalogValue deviationY;
        private AnalogValue rotation;

        public TowerInclinometerNodeData()
        {
            heigh = new AnalogValue(UnitsDistance.Meters);
            deviationX = new AnalogValue(UnitsDistance.Millimeters);
            deviationY = new AnalogValue(UnitsDistance.Millimeters);
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

        public AnalogValue DeviationX
        {
            get { return deviationX; }
            set { deviationX = value; }
        }

        public AnalogValue DeviationY
        {
            get { return deviationY; }
            set { deviationY = value; }
        }

        public AnalogValue Rotation
        {
            get { return rotation; }
            set { rotation = value; }
        }
    }

    public class TowerInclinometerAnemometerData
    {
        private AnalogValue speed;
        private AnalogValue direction;

        public TowerInclinometerAnemometerData()
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

    public class TowerInclinometerData
    {
        private DateTime utcTime;
        private List<TowerInclinometerNodeData> nodes;
        private TowerInclinometerAnemometerData anemometer;
        private bool sent;

        public TowerInclinometerData(int nQty)
        {
            int i;

            utcTime = new DateTime();
            nodes = new List<TowerInclinometerNodeData>();
            for (i = 0; i < nQty; i++)
                nodes.Add(new TowerInclinometerNodeData());
            anemometer = new TowerInclinometerAnemometerData();
            sent = false;
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

        public List<TowerInclinometerNodeData> Nodes
        {
            get { return nodes; }
            set { nodes = value; }
        }

        public TowerInclinometerAnemometerData Anemometer
        {
            get { return anemometer; }
            set { anemometer = value; }
        }

        [JsonIgnore]
        public bool Sent
        {
            get { return sent; }
            set { sent = value; }
        }
    }

    public class TowerInclinometerEvent
    {
        private DateTime utcTime;
        private EventType messageType;
        private String message;

        public TowerInclinometerEvent()
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
