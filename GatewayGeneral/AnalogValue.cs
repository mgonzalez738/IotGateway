using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;


namespace GatewayGeneral
{
    // Serializa a Json solo el miembro Value del objeto AnalogValue
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

    // Estados de Analog Value
    public enum AnalogValueState { Normal, HighHigh, High, Low, LowLow }

    // Argumentos del evento StateChange
    public class StateChangeEventArgs : EventArgs
    {
        private string propertyName;
        private AnalogValueState previousState;
        private AnalogValueState actualState;

        public StateChangeEventArgs(AnalogValueState previous, AnalogValueState actual)
        {
            propertyName = "";
            previousState = previous;
            actualState = actual;
        }

        public StateChangeEventArgs(AnalogValueState previous, AnalogValueState actual, string propName)
        {
            propertyName = propName;
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

        public String PropertyName
        {
            get { return propertyName; }
            set { propertyName = value; }
        }
    }

    // Configuracion de Analog Value
    public class AnalogValueConfiguration
    {
        private string unit;
        private double limitHighHigh;
        private double limitHigh;
        private double limitLow;
        private double limitLowLow;
        private double limitHysteresis;
        private bool enableHighHigh;
        private bool enableHigh;
        private bool enableLow;
        private bool enableLowLow;

        public AnalogValueConfiguration(string valueUnit)
        {
            unit = valueUnit;
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
        [JsonProperty(Required = Required.Always)]
        public String Unit
        {
            get { return unit; }
        }
    }

    // Analog Value
    [JsonConverter(typeof(AnalogValueJsonConverter))]
    public class AnalogValue
    {
        private double value;
        private AnalogValueConfiguration config;
        private AnalogValueState state;

        public event EventHandler<StateChangeEventArgs> StateChanged;

        public AnalogValue(string valueUnit)
        {
            value = Double.NaN;
            config = new AnalogValueConfiguration(valueUnit);
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

            if (config.EnableLowLow && (value < config.LimitLowLow))
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
            set { this.config = value; }
        }

        public AnalogValueState State
        {
            get { return this.state; }
        }
    }
}
