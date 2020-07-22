using System;
using System.Collections.Generic;
using System.IO;
using GatewayGeneral;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace GatewayCoreModule
{
    public class DeviceRs485Configuration
    {
        private string deviceId;
        private DeviceTypes deviceType;
        private string connectionString;
        private bool delete;

        public DeviceRs485Configuration(string dId, bool del)
        {
            this.deviceId = dId;
            this.deviceType = DeviceTypes.Undefined;
            this.connectionString = "";
            this.delete = del;
        }

        [JsonConstructor]
        public DeviceRs485Configuration(string dId, DeviceTypes dt, string conn)
        {
            this.deviceId = dId;
            this.deviceType = dt;
            this.connectionString = conn;
            this.delete = false;
        }

        public static DeviceRs485Configuration FromJsonString(string st)
        {
            try
            {
                return JsonConvert.DeserializeObject<DeviceRs485Configuration>(st, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
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

        [JsonProperty(Required = Required.Default)]
        public string DeviceId
        {
            get { return this.deviceId; }
            set { this.deviceId = value; }
        }

        [JsonProperty(Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public DeviceTypes DeviceType
        {
            get { return this.deviceType; }
            set { this.deviceType = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public string ConnectionString
        {
            get { return this.connectionString; }
            set { this.connectionString = value; }
        }

        [JsonIgnore]
        public bool Delete
        {
            get { return this.delete; }
            set { this.delete = value; }
        }
    }

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
        private List<DeviceRs485Configuration> devicesRs485;

        public event EventHandler PollDataChanged;
        public event EventHandler DetachedTelemetryChanged;
        public event EventHandler DevicesRs485Changed;

        public GatewayProperties()
        {
            this.pollData = new GatewayDataPollConfiguration();
            this.detachedTelemetry = new GatewayDetachedTelemetryConfiguration();
            this.variable = new GatewayVariableConfiguration();
            this.devicesRs485 = new List<DeviceRs485Configuration>();
        }

        public void DeleteMarkedRS485Devices()
        {
            int i;
            for (i = 0; i < this.devicesRs485.Count; i++)
                if (this.devicesRs485[i].Delete)
                    this.devicesRs485.RemoveAt(i);
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

        protected virtual void OnDevicesRs485Changed(EventArgs e)
        {
            DevicesRs485Changed?.Invoke(this, e);
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

        [JsonConverter(typeof(DeviceRs485ConfigurationListJsonConverter))]
        public List<DeviceRs485Configuration> DevicesRs485
        {
            get { return this.devicesRs485; }
            set 
            {
                bool changed = false;
                int i, j;

                for (i = 0; i < value.Count; i++)
                {
                    bool matched = false;
                    for (j = 0; j < this.devicesRs485.Count; j++)
                    {             
                        // Modifica el elemento si existe (incluye marcarlo para eliminar)
                        if (value[i].DeviceId == this.devicesRs485[j].DeviceId)
                        {
                            matched = true;
                            if (value[i].DeviceType != this.devicesRs485[j].DeviceType)
                                changed = true;
                            if (value[i].ConnectionString != this.devicesRs485[j].ConnectionString)
                                changed = true;
                            if (value[i].Delete != this.devicesRs485[j].Delete)
                                changed = true;
                            this.devicesRs485[j] = value[i];
                            break;
                        }
                    }
                    // Lo agrega si no lo encontro
                    if (!matched)
                    {
                        this.devicesRs485.Add(value[i]);
                        changed = true;
                    }
                }

                if (changed)
                    OnDevicesRs485Changed(new EventArgs());
            }
        }
    }

    public class DeviceRs485ConfigurationListJsonConverter : JsonConverter
    {
        // Convierte la lista en un Json sin formato de array [] (No soportado por Twin)
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.FloatFormatHandling = FloatFormatHandling.String;
            JObject lista = new JObject();
            foreach (var device in (List<DeviceRs485Configuration>)value)
            {
                if (device.Delete)
                {
                    lista.Add(device.DeviceId, null);
                }
                else
                {
                    JObject jObj = (JObject)JToken.FromObject(device);
                    jObj.Remove("DeviceId");
                    lista.Add(device.DeviceId, jObj);
                }
            }
            lista.WriteTo(writer);
        }

        // Convierte el Json en lista de C#
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var response = new List<DeviceRs485Configuration>();
            // Loading the JSON object
            JObject devices = JObject.Load(reader);
            // Looping through all the properties. C# treats it as key value pair
            foreach (var device in devices)
            {
                if (device.Value.Type != JTokenType.Null)
                {
                    // Finally I'm deserializing the value into an actual Player object
                    DeviceRs485Configuration item = JsonConvert.DeserializeObject<DeviceRs485Configuration>(device.Value.ToString());
                    // Also using the key as the player DeviceId
                    item.DeviceId = device.Key;
                    response.Add(item);
                }
                else
                {
                    DeviceRs485Configuration item = new DeviceRs485Configuration(device.Key, true);
                    response.Add(item);
                }
            }

            return response;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(List<DeviceRs485Configuration>);
    }

}
