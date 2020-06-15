using System;
using System.Device.Gpio;

namespace Hardware
{
    public enum LedState { On, Off }
    public interface IGateway
    {
        void SetStatusLed(LedState state);
        LedState GetStatusLed();
        void ToggleStatusLed();

        void SetUserLed(LedState state);
        LedState GetUserLed();
        void ToggleUserLed();

        event EventHandler UserButtonPushed;
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

        protected virtual void OnUserButtonPushed(EventArgs e)
        {
            UserButtonPushed?.Invoke(this, e);
        }
    }
}
