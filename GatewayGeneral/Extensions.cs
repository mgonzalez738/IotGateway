using System;
using System.Collections.Generic;
using System.Text;

namespace GatewayGeneral
{
    public enum MessageType { Data, Event }

    public enum EventType { Info, Warning, Error, Debug }

    public static class RoundDateTime
    {
        public static DateTime RoundToSeconds(DateTime dateTime)
        {
            DateTime dt = DateTime.MinValue.AddSeconds(Math.Round((dateTime - DateTime.MinValue).TotalSeconds)); // Redondea
            return new DateTime(dt.Ticks, dateTime.Kind);

            //return dateTime.AddTicks(-(dateTime.Ticks % (TimeSpan.FromSeconds(1)).Ticks)); // Trunca
        }
    }

    public static class WriteColor
    {
        public enum AnsiColors { Red, Green, Yellow, Blue, Magenta, Cyan, Reset }
        public static void Set(AnsiColors col)
        {
            switch (col)
            {
                case AnsiColors.Red:
                    Console.Write("\u001b[31m");
                    break;
                case AnsiColors.Green:
                    Console.Write("\u001b[32m");
                    break;
                case AnsiColors.Yellow:
                    Console.Write("\u001b[33m");
                    break;
                case AnsiColors.Blue:
                    Console.Write("\u001b[34m");
                    break;
                case AnsiColors.Magenta:
                    Console.Write("\u001b[35m");
                    break;
                case AnsiColors.Cyan:
                    Console.Write("\u001b[36m");
                    break;
                case AnsiColors.Reset:
                    Console.Write("\u001b[0m");
                    break;
            }


        }
    }

    public static class Art
    {
        public static void Write()
        {
            Console.WriteLine(@"                                                                             ");
            Console.WriteLine(@"    ________    _____ ______________________ __      __   _____  _____.___.  ");
            Console.WriteLine(@"   /  _____/   /  _  \\__    ___/\_   _____//  \    /  \ /  _  \ \__  |   |  ");
            Console.WriteLine(@"  /   \  ___  /  /_\  \ |    |    |    __)_ \   \/\/   //  /_\  \ /   |   |  ");
            Console.WriteLine(@"  \    \_\  \/    |    \|    |    |        \ \        //    |    \\____   |  ");
            Console.WriteLine(@"   \______  /\____|__  /|____|   /_______  /  \__/\  / \____|__  // ______|  ");
            Console.WriteLine(@"          \/         \/                  \/        \/          \/ \/         ");
            Console.WriteLine(@"                                                                             ");
        }
    }

}
