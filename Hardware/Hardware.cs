using System;
using System.Device.Gpio;
using System.Device.I2c;
using Iot.Device.Rtc;

namespace Hardware
{
    public enum LedState { On, Off }
    public interface IGateway
    {
        // Status and User Leds

        void SetStatusLed(LedState state);
        LedState GetStatusLed();
        void ToggleStatusLed();

        void SetUserLed(LedState state);
        LedState GetUserLed();
        void ToggleUserLed();

        // User Button

        event EventHandler UserButtonPushed;

        // Rtc

        DateTime GetRtcDateTime();
        void SetRtcDateTime(DateTime dt);
        double GetRtcTemperature();
    }

    public class GatewayRPI3Plus : IGateway
    {
        // Hardware
        private static readonly int statusLedPin = 17;
        private static readonly int userLedPin = 27;
        private static readonly int userButtonPin = 22;

        private static GpioController gpio;

        private static LedState statusLedState;
        private static LedState userLedState;

        public event EventHandler UserButtonPushed;

        private I2cDevice i2cDeviceRtc;
        private Ds1307 rtcDs3231;                    // Utiliza Ds1307 para evitar conflictos con el century bit 

        public GatewayRPI3Plus()
        {
            // Inicializa el controlador de GPIO
            gpio = new GpioController();

            // Establece la direccion de los pines de leds y boton

            gpio.OpenPin(statusLedPin, PinMode.Output);
            gpio.OpenPin(userLedPin, PinMode.Output);
            gpio.OpenPin(userButtonPin, PinMode.Input);

            // Establece el estado inicial de los leds
            statusLedState = LedState.Off;
            gpio.Write(statusLedPin, PinValue.Low);
            userLedState = LedState.Off; 
            gpio.Write(userLedPin, PinValue.Low);

            // Registra el pulsado del boton de ususario y lanza el evento

            gpio.RegisterCallbackForPinValueChangedEvent(userButtonPin, PinEventTypes.Rising, (o, e) =>
            {
                OnUserButtonPushed(new EventArgs());
            });

            // Configura el RTC DS3231 como dispositivo del bus I2C 1 

            I2cConnectionSettings settings = new I2cConnectionSettings(1, Ds1307.DefaultI2cAddress);
            i2cDeviceRtc = I2cDevice.Create(settings);
            rtcDs3231 = new Ds1307(i2cDeviceRtc);
        }

        /// <summary>
        /// Devuelve el estado del led de status.
        /// </summary>
        public LedState GetStatusLed()
        {
            return statusLedState;
        }

        /// <summary>
        /// Establece el estado del led de status.
        /// </summary>
        public void SetStatusLed(LedState state)
        {
            statusLedState = state;
            if(statusLedState == LedState.On)
                gpio.Write(statusLedPin, PinValue.High);
            if (statusLedState == LedState.Off)
                gpio.Write(statusLedPin, PinValue.Low);
        }

        /// <summary>
        /// Alterna el estado del led de status.
        /// </summary>
        public void ToggleStatusLed()
        {
            if (statusLedState == LedState.On)
            {
                statusLedState = LedState.Off;
                gpio.Write(statusLedPin, PinValue.Low);
                return;
            }
            if (statusLedState == LedState.Off)
            {
                statusLedState = LedState.On;
                gpio.Write(statusLedPin, PinValue.High);
                return;
            }
        }

        /// <summary>
        /// Devuelve el estado del led de usuario.
        /// </summary>
        public LedState GetUserLed()
        {
            return userLedState;
        }

        /// <summary>
        /// Establece el estado del led de usuario.
        /// </summary>
        public void SetUserLed(LedState state)
        {
            userLedState = state;
            if (userLedState == LedState.On)
                gpio.Write(userLedPin, PinValue.High);
            if (userLedState == LedState.Off)
                gpio.Write(userLedPin, PinValue.Low);
        }

        /// <summary>
        /// Alterna el estado del led de usuario.
        /// </summary>
        public void ToggleUserLed()
        {
            if (userLedState == LedState.On)
            {
                userLedState = LedState.Off;
                gpio.Write(userLedPin, PinValue.Low);
                return;
            }
            if (userLedState == LedState.Off)
            {
                userLedState = LedState.On;
                gpio.Write(userLedPin, PinValue.High);
                return;
            }
        }

        /// <summary>
        /// Invoca el evento UserButtonPushed
        /// </summary>
        protected virtual void OnUserButtonPushed(EventArgs e)
        {
            UserButtonPushed?.Invoke(this, e);
        }

        /// <summary>
        /// Permite obtener la hora desde el RTC de la placa
        /// </summary>
        public DateTime GetRtcDateTime()
        {
            return rtcDs3231.DateTime;
        }

        /// <summary>
        /// Permite establecer la hora al RTC de la placa
        /// </summary>
        public void SetRtcDateTime(DateTime dt)
        {
            rtcDs3231.DateTime = dt;
        }

        /// <summary>
        /// Permite obtener la temperatura desde el RTC de la placa
        /// </summary>
        public double GetRtcTemperature()
        {
            byte RTC_TEMP_MSB_REG_ADDR = 0x11;

            Span<byte> data = stackalloc byte[2];

            i2cDeviceRtc.WriteByte(RTC_TEMP_MSB_REG_ADDR);
            i2cDeviceRtc.Read(data);

            // datasheet Temperature part
            return data[0] + (data[1] >> 6) * 0.25;
        }
    }

    public class GatewayProperties
    {

    }
}
