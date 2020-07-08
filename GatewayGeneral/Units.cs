using System;
using System.Collections.Generic;
using System.Text;

namespace GatewayGeneral
{
    public static class UnitsVoltage
    {
        public static String Volts { get { return "V"; } }
        public static String MiliVolts { get { return "mV"; } }
    }

    public static class UnitsTemperature
    {
        public static String Celsius { get { return "grC"; } }
    }

    public static class UnitsDistance
    {
        public static String Meters { get { return "m"; } }
        public static String Millimeters { get { return "mm"; } }
    }

    public static class UnitsTilt
    {
        public static String MillimetersPerMeter { get { return "mm/m"; } }
        public static String Degrees { get { return "°"; } }
    }

    public static class UnitsSpeed
    {
        public static String KilometerPerHour { get { return "Km/h"; } }
    }

    public static class UnitsAngle
    {
        public static String Degrees { get { return "°"; } }
    }
}
