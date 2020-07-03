using System;
using System.IO;
using GatewayGeneral;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GatewayCoreModule
{
    public class GatewayVariableConfiguration
    {
        private AnalogValueConfiguration powerVoltage;
        private AnalogValueConfiguration sensedVoltage;
        private AnalogValueConfiguration batteryVoltage;
        private AnalogValueConfiguration temperature;

        public GatewayVariableConfiguration()
        {
            powerVoltage = new AnalogValueConfiguration(UnitsVoltage.Volts);
            sensedVoltage = new AnalogValueConfiguration(UnitsVoltage.Volts);
            batteryVoltage = new AnalogValueConfiguration(UnitsVoltage.Volts);
            temperature = new AnalogValueConfiguration(UnitsTemperature.Celsius);
        }

        public static GatewayVariableConfiguration FromJsonString(string st)
        {
            try
            {
                return JsonConvert.DeserializeObject<GatewayVariableConfiguration>(st, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
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
        public AnalogValueConfiguration PowerVoltage
        {
            get { return powerVoltage; }
            set { powerVoltage = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public AnalogValueConfiguration SensedVoltage
        {
            get { return sensedVoltage; }
            set { sensedVoltage = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public AnalogValueConfiguration BatteryVoltage
        {
            get { return batteryVoltage; }
            set { batteryVoltage = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public AnalogValueConfiguration Temperature
        {
            get { return temperature; }
            set { temperature = value; }
        }
    }

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
            get { return enabled; }
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

    public class GatewayDetachedTelemetryConfiguration
    {
        private bool enabled;
        private int period;
        private ScheduleUnit unit;

        public GatewayDetachedTelemetryConfiguration()
        {
            enabled = false;
            period = 15;
            unit = ScheduleUnit.minute;
        }

        public static GatewayDetachedTelemetryConfiguration FromJsonString(string st)
        {
            try
            {
                return JsonConvert.DeserializeObject<GatewayDetachedTelemetryConfiguration>(st, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
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
            get { return enabled; }
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
        private GatewayDetachedTelemetryConfiguration detachedTelemetry;
        private GatewayVariableConfiguration variable;
        private string deviceTag;

        public event EventHandler PollDataChanged;
        public event EventHandler DetachedTelemetryChanged;


        public GatewayProperties()
        {
            pollData = new GatewayDataPollConfiguration();
            detachedTelemetry = new GatewayDetachedTelemetryConfiguration();
            variable = new GatewayVariableConfiguration();
            deviceTag = "";
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

        protected virtual void OnDetachedTelemetryChanged(EventArgs e)
        {
            DetachedTelemetryChanged?.Invoke(this, e);
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

        public GatewayDetachedTelemetryConfiguration DetachedTelemetry
        {
            get { return detachedTelemetry; }
            set
            {
                bool changed = false;

                if (detachedTelemetry.Enabled != value.Enabled)
                    changed = true;
                if (detachedTelemetry.Period != value.Period)
                    changed = true;
                if (detachedTelemetry.Unit != value.Unit)
                    changed = true;

                detachedTelemetry = value;

                if (changed)
                    OnDetachedTelemetryChanged(new EventArgs());
            }
        }

        public GatewayVariableConfiguration Variable
        {
            get { return variable; }
            set { variable = value; }
        }

        public string DeviceTag
        {
            get { return deviceTag; }
            set { deviceTag = value; }
        }

    }
}
