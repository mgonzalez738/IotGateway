using System;
using System.Collections.Generic;
using System.IO;
using GatewayGeneral;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace TowerInclinometer
{
    public class TowerInclinometerDataPollConfiguration
    {
        private bool enabled;
        private int period;
        private ScheduleUnit unit;

        public TowerInclinometerDataPollConfiguration()
        {
            enabled = true;
            period = 15;
            unit = ScheduleUnit.minute;
        }

        public static TowerInclinometerDataPollConfiguration FromJsonString(string st)
        {
            try
            {
                return JsonConvert.DeserializeObject<TowerInclinometerDataPollConfiguration>(st, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
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

    public class TowerInclinometerDetachedTelemetryConfiguration
    {
        private bool enabled;
        private int period;
        private ScheduleUnit unit;

        public TowerInclinometerDetachedTelemetryConfiguration()
        {
            enabled = false;
            period = 15;
            unit = ScheduleUnit.minute;
        }

        public static TowerInclinometerDetachedTelemetryConfiguration FromJsonString(string st)
        {
            try
            {
                return JsonConvert.DeserializeObject<TowerInclinometerDetachedTelemetryConfiguration>(st, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
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

    public class TowerInclinometerAnemometerConfiguration
    {
        private int address;
        private float heigh;

        [JsonConstructor]
        public TowerInclinometerAnemometerConfiguration(int add, float ht)
        {
            this.address = add;
            this.heigh = ht;
        }

        public static TowerInclinometerNodeConfiguration FromJsonString(string st)
        {
            try
            {
                return JsonConvert.DeserializeObject<TowerInclinometerNodeConfiguration>(st, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
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
        public int Address
        {
            get { return this.address; }
            set { this.address = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public float Heigh
        {
            get { return this.heigh; }
            set { this.heigh = value; }
        }
    }

    public class TowerInclinometerNodeConfiguration
    {
        private int address;
        private float heigh;
        private float length;
        private bool delete;

        public TowerInclinometerNodeConfiguration(int add, bool del)
        {
            this.address = add;
            this.heigh = Single.NaN;
            this.length = Single.NaN;
            this.delete = del;
        }

        [JsonConstructor]
        public TowerInclinometerNodeConfiguration(int add, float ht, float lt)
        {
            this.address = add;
            this.heigh = ht;
            this.length = lt;
            this.delete = false;
        }

        public static TowerInclinometerNodeConfiguration FromJsonString(string st)
        {
            try
            {
                return JsonConvert.DeserializeObject<TowerInclinometerNodeConfiguration>(st, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
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
        public int Address
        {
            get { return this.address; }
            set { this.address = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public float Heigh
        {
            get { return this.heigh; }
            set { this.heigh = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public float Length
        {
            get { return this.length; }
            set { this.length = value; }
        }

        [JsonIgnore]
        public bool Delete
        {
            get { return this.delete; }
            set { this.delete = value; }
        }
    }

    public class TowerInclinometerProperties
    {
        private TowerInclinometerDataPollConfiguration pollData;
        private TowerInclinometerDetachedTelemetryConfiguration detachedTelemetry;
        private TowerInclinometerAnemometerConfiguration anemometer;
        private List<TowerInclinometerNodeConfiguration> nodes;

        public event EventHandler PollDataChanged;
        public event EventHandler DetachedTelemetryChanged;
        public event EventHandler AnemometerChanged;
        public event EventHandler NodesChanged;

        public TowerInclinometerProperties()
        {
            this.pollData = new TowerInclinometerDataPollConfiguration();
            this.detachedTelemetry = new TowerInclinometerDetachedTelemetryConfiguration();
            this.anemometer = new TowerInclinometerAnemometerConfiguration(30, 10.0F);
            this.nodes = new List<TowerInclinometerNodeConfiguration>();
        }

        public void DeleteMarkedNodes()
        {
            int i;
            for (i = 0; i < this.nodes.Count; i++)
                if (this.nodes[i].Delete)
                    this.nodes.RemoveAt(i);
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

        public static TowerInclinometerProperties FromJsonFile(string filePath)
        {
            try
            {
                string st = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<TowerInclinometerProperties>(st);
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

        protected virtual void OnAnemometerChanged(EventArgs e)
        {
            AnemometerChanged?.Invoke(this, e);
        }

        protected virtual void OnNodesChanged(EventArgs e)
        {
            NodesChanged?.Invoke(this, e);
        }

        public TowerInclinometerDataPollConfiguration PollData
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

        public TowerInclinometerDetachedTelemetryConfiguration DetachedTelemetry
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

        public TowerInclinometerAnemometerConfiguration Anemometer
        {
            get { return anemometer; }
            set
            {
                bool changed = false;

                if (anemometer.Address != value.Address)
                    changed = true;
                if (anemometer.Heigh != value.Heigh)
                    changed = true;
               
                anemometer = value;

                if (changed)
                    OnAnemometerChanged(new EventArgs());
            }
        }

        [JsonConverter(typeof(TowerInclinometerNodeConfigurationListJsonConverter))]
        public List<TowerInclinometerNodeConfiguration> Nodes
        {
            get { return this.nodes; }
            set
            {
                bool changed = false;
                int i, j;

                for (i = 0; i < value.Count; i++)
                {
                    bool matched = false;
                    for (j = 0; j < this.nodes.Count; j++)
                    {
                        // Modifica el elemento si existe (incluye marcarlo para eliminar)
                        if (value[i].Address == this.nodes[j].Address)
                        {
                            matched = true;
                            if (value[i].Heigh != this.nodes[j].Heigh)
                                changed = true;
                            if (value[i].Length != this.nodes[j].Length)
                                changed = true;
                            if (value[i].Delete != this.nodes[j].Delete)
                                changed = true;
                            this.nodes[j] = value[i];
                            break;
                        }
                    }
                    // Lo agrega si no lo encontro
                    if (!matched)
                    {
                        this.nodes.Add(value[i]);
                        changed = true;
                    }
                }

                if (changed)
                    OnNodesChanged(new EventArgs());
            }
        }
    }

    public class TowerInclinometerNodeConfigurationListJsonConverter : JsonConverter
    {
        // Convierte la lista en un Json sin formato de array [] (No soportado por Twin)
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.FloatFormatHandling = FloatFormatHandling.String;
            JObject lista = new JObject();
            foreach (var node in (List<TowerInclinometerNodeConfiguration>)value)
            {
                if (node.Delete)
                {
                    lista.Add(node.Address.ToString(), null);
                }
                else
                {
                    JObject jObj = (JObject)JToken.FromObject(node);
                    jObj.Remove("Address");
                    lista.Add(node.Address.ToString(), jObj);
                }
            }
            lista.WriteTo(writer);
        }

        // Convierte el Json en lista de C#
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var response = new List<TowerInclinometerNodeConfiguration>();
            // Loading the JSON object
            JObject nodes = JObject.Load(reader);
            // Looping through all the properties. C# treats it as key value pair
            foreach (var node in nodes)
            {
                if (node.Value.Type != JTokenType.Null)
                {
                    // Finally I'm deserializing the value into an actual Player object
                    TowerInclinometerNodeConfiguration item = JsonConvert.DeserializeObject<TowerInclinometerNodeConfiguration>(node.Value.ToString());
                    // Also using the key as the player DeviceId
                    item.Address = int.Parse(node.Key);
                    response.Add(item);
                }
                else
                {
                    TowerInclinometerNodeConfiguration item = new TowerInclinometerNodeConfiguration(int.Parse(node.Key), true);
                    response.Add(item);
                }
            }

            return response;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(List<TowerInclinometerNodeConfiguration>);
    }
}
