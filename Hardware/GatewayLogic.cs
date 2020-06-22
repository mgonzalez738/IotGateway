using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using static Common.ScheduleTimer;

namespace Hardware
{
    public enum GatewayMessageType { Telemetry, Event }

    public enum GatewayEventType { Info, Error, Debug }

    public enum AnalogValueState { Normal, HighHigh, High, Low, LowLow }

    // VARIABLES

    public class AnalogValueConfiguration
    {
        private double limitHighHigh;
        private double limitHigh;
        private double limitLow;
        private double limitLowLow;
        private double limitHysteresis;
        private bool enableHighHigh;
        private bool enableHigh;
        private bool enableLow;
        private bool enableLowLow;

        public AnalogValueConfiguration()
        {
            limitHighHigh = 0.0;
            limitHigh = 0.0;
            limitLow = 0.0;
            limitLowLow = 0.0;
            limitHysteresis = 0.0;
            enableHighHigh = false;
            enableHigh = false;
            enableLow = false;
            enableLowLow = false;
        }

        public static AnalogValueConfiguration FromJsonString(string st)
        {
            try
            {
                return JsonConvert.DeserializeObject<AnalogValueConfiguration>(st, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
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
        public Double LimitHighHigh
        {
            get { return limitHighHigh; }
            set { limitHighHigh = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public Double LimitHigh
        {
            get { return limitHigh; }
            set { limitHigh = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public Double LimitLow
        {
            get { return limitLow; }
            set { limitLow = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public Double LimitLowLow
        {
            get { return limitLowLow; }
            set { limitLowLow = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public Double LimitHysteresis
        {
            get { return limitHysteresis; }
            set { limitHysteresis = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public Boolean EnableHighHigh
        {
            get { return enableHighHigh; }
            set { enableHighHigh = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public Boolean EnableHigh
        {
            get { return enableHigh; }
            set { enableHigh = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public Boolean EnableLow
        {
            get { return enableLow; }
            set { enableLow = value; }
        }

        [JsonProperty(Required = Required.Always)]
        public Boolean EnableLowLow
        {
            get { return enableLowLow; }
            set { enableLowLow = value; }
        }
    }

    public class StateChangeEventArgs : EventArgs
    {
        private AnalogValueState previousState;
        private AnalogValueState actualState;

        public StateChangeEventArgs(AnalogValueState previous, AnalogValueState actual)
        {
            previousState = previous;
            actualState = actual;
        }

        public AnalogValueState PreviousState
        {
            get { return previousState; }
        }

        public AnalogValueState ActualState
        {
            get { return actualState; }
        }
    }

    [JsonConverter(typeof(AnalogValueJsonConverter))]
    public class AnalogValue
    {
        private double value;
        private AnalogValueConfiguration config;
        private AnalogValueState state;

        public event EventHandler StateChanged;

        public AnalogValue()
        {
            value = Double.NaN;
            config = new AnalogValueConfiguration();
            state = AnalogValueState.Normal;
        }

        private void checkState()
        {
            AnalogValueState next = state;

            switch (state)
            {
                case AnalogValueState.Normal:
                    next = checkInNormalState();
                    break;

                case AnalogValueState.HighHigh:
                    next = checkInHighHighState();
                    break;

                case AnalogValueState.High:
                    next = checkInHighState();
                    break;

                case AnalogValueState.Low:
                    next = checkInLowState();
                    break;

                case AnalogValueState.LowLow:
                    next = checkInLowLowState();
                    break;
            }

            if (next != state)
            {
                OnStateChanged(new StateChangeEventArgs(state, next));
                state = next;
            }
        }

        private AnalogValueState checkInNormalState()
        {
            AnalogValueState next = AnalogValueState.Normal;

            if(config.EnableLowLow && (value < config.LimitLowLow))
                next = AnalogValueState.LowLow;
            else if (config.EnableLow && (value < config.LimitLow))
                next = AnalogValueState.Low;
            else if (config.EnableHighHigh && (value > config.LimitHighHigh))
                next = AnalogValueState.HighHigh;
            else if (config.EnableHigh && (value > config.LimitHigh))
                next = AnalogValueState.High;           

            return next;                
        }

        private AnalogValueState checkInHighState()
        {
            AnalogValueState next = AnalogValueState.High;

            if (config.EnableLowLow && (value < config.LimitLowLow))
                next = AnalogValueState.LowLow;
            else if (config.EnableLow && (value < config.LimitLow))
                next = AnalogValueState.Low;
            else if (value < (config.LimitHigh - config.LimitHysteresis))
                next = AnalogValueState.Normal;
            else if (config.EnableHighHigh && (value > config.LimitHighHigh))
                next = AnalogValueState.HighHigh;

            return next;
        }

        private AnalogValueState checkInLowState()
        {
            AnalogValueState next = AnalogValueState.Low;

            if (config.EnableLowLow && (value < config.LimitLowLow))
                next = AnalogValueState.LowLow;
            else if (config.EnableHighHigh && (value > config.LimitHighHigh))
                next = AnalogValueState.HighHigh;
            else if (config.EnableHigh && (value > config.LimitHigh))
                next = AnalogValueState.High;
            else if (value > (config.LimitLow + config.LimitHysteresis))
                next = AnalogValueState.Normal;

            return next;
        }

        private AnalogValueState checkInHighHighState()
        {
            AnalogValueState next = AnalogValueState.HighHigh;

            if (config.EnableLowLow && (value < config.LimitLowLow))
                next = AnalogValueState.LowLow;
            else if (config.EnableLow && (value < config.LimitLow))
                next = AnalogValueState.Low;
            else if (value < config.LimitHigh)
                next = AnalogValueState.Normal;
            else if (config.EnableHigh && (value < (config.LimitHighHigh - config.LimitHysteresis)))
                next = AnalogValueState.High;

            return next;
        }

        private AnalogValueState checkInLowLowState()
        {
            AnalogValueState next = AnalogValueState.LowLow;

            if (config.EnableHighHigh && (value > config.LimitHighHigh))
                next = AnalogValueState.HighHigh;
            else if (config.EnableHigh && (value > config.LimitHigh))
                next = AnalogValueState.High;
            else if (value > config.LimitLow)
                next = AnalogValueState.Normal;
            else if (config.EnableLow && (value > (config.LimitLowLow + config.LimitHysteresis)))
                next = AnalogValueState.Low;

            return next;
        }

        protected virtual void OnStateChanged(StateChangeEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        public Double Value
        {
            get { return this.value; }
            set 
            { 
                this.value = value;
                checkState();
            }
        }

        public AnalogValueConfiguration Config
        {
            get { return this.config; }
        }

        public AnalogValueState State
        {
            get { return this.state; }
        }
    }


    // DATOS GATEWAY
    
    public class GatewayData
    {
        private DateTime utcTime;
        private AnalogValue powerVoltage;
        private AnalogValue sensedVoltage;
        private AnalogValue batteryVoltage;
        private AnalogValue temperature;

        public GatewayData()
        {
            utcTime = new DateTime();
            powerVoltage = new AnalogValue();
            sensedVoltage = new AnalogValue();
            batteryVoltage = new AnalogValue();
            temperature = new AnalogValue();
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

    // SERIALIZADORES JSON

    // Serializa a Json solo el miembro Value del objeto AnalogValue
    // No deserializa
    public class AnalogValueJsonConverter : JsonConverter<AnalogValue>
    {
        public override AnalogValue ReadJson(JsonReader reader, Type objectType, [AllowNull] AnalogValue existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, AnalogValue value, Newtonsoft.Json.JsonSerializer serializer)
        {
            writer.WriteValue(value.Value.ToString("0.000000"));
        }
    }
}



