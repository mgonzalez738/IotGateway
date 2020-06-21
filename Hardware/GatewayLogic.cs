using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static Common.ScheduleTimer;

namespace Hardware
{
    public enum GatewayMessageType { Telemetry, Event }

    public enum GatewayEventType { Info, Error, Debug }

    // DATOS
    
    public class GatewayData
    {
        private DateTime utcTime;
        private Single powerVoltage;
        private Single sensedVoltage;
        private Single batteryVoltage;
        private Single temperature;

        public GatewayData()
        {
            utcTime = new DateTime();
            powerVoltage = Single.NaN;
            sensedVoltage = Single.NaN;
            batteryVoltage = Single.NaN;
            temperature = Single.NaN;
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

        public Single PowerVoltage
        {
            get { return powerVoltage; }
            set { powerVoltage = value; }
        }

        public Single SensedVoltage
        {
            get { return sensedVoltage; }
            set { sensedVoltage = value; }
        }

        public Single BatteryVoltage
        {
            get { return batteryVoltage; }
            set { batteryVoltage = value; }
        }

        public Single Temperature
        {
            get { return temperature; }
            set { temperature = value; }
        }
    }

    // EVENTOS

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

    // CONFIGURACION

    public class GatewayDataPollConfiguration
    {
        private bool enabled;
        private int period;
        private ScheduleUnit unit;

        public GatewayDataPollConfiguration()
        {
            enabled = true;
            period = 15;
            unit = ScheduleUnit.minute;
        }

        public static GatewayDataPollConfiguration FromJsonString(string st)
        {
            try
            {
                return JsonConvert.DeserializeObject<GatewayDataPollConfiguration>(st, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String });
        }

        [JsonProperty(Required = Required.Always)]
        public bool Enabled
        {
            get { return enabled;  }
            set { enabled = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public int Period
        {
            get { return period; }
            set { period = value; }
        }

        [JsonProperty(Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScheduleUnit Unit
        {
            get { return unit; }
            set { unit = value; }
        }
    }

    public class GatewayProperties
    {
        private GatewayDataPollConfiguration pollData;

        public event EventHandler PollDataChanged;

        public GatewayProperties()
        {
            pollData = new GatewayDataPollConfiguration();
        }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String });
        }

        public void ToJsonFile(string filePath)
        {
            string st = JsonConvert.SerializeObject(this, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, FloatFormatHandling = FloatFormatHandling.String, Formatting = Formatting.Indented });
            File.WriteAllText(filePath, st);
        }

        public static GatewayProperties FromJsonFile(string filePath)
        {
            try
            {
                string st = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<GatewayProperties>(st);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected virtual void OnPollDataChanged(EventArgs e)
        {
            PollDataChanged?.Invoke(this, e);
        }

        public GatewayDataPollConfiguration PollData
        {
            get { return pollData; }
            set 
            {
                bool changed = false;

                if (pollData.Enabled != value.Enabled)
                    changed = true;
                if (pollData.Period != value.Period)
                    changed = true;
                if (pollData.Unit != value.Unit)
                    changed = true;

                pollData = value;

                if (changed)
                    OnPollDataChanged(new EventArgs());
            }
        }

    }


}



