using GatewayGeneral;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TowerInclinometer
{
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
