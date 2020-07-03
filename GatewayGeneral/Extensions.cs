using System;
using System.Collections.Generic;
using System.Text;

namespace GatewayGeneral
{
    public static class RoundDateTime
    {
        public static DateTime RoundToSeconds(DateTime dateTime)
        {
            DateTime dt = DateTime.MinValue.AddSeconds(Math.Round((dateTime - DateTime.MinValue).TotalSeconds)); // Redondea
            return new DateTime(dt.Ticks, dateTime.Kind);

            //return dateTime.AddTicks(-(dateTime.Ticks % (TimeSpan.FromSeconds(1)).Ticks)); // Trunca
        }
    }
}
