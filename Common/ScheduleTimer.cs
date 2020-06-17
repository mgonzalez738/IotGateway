using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;

namespace Common
{
    
    public class ScheduleTimer
    {
        public enum ScheduleUnit { second, minute, hour, day };

        public event EventHandler Elapsed;

        private int periodMs;
        private ScheduleUnit periodUnit;

        private Timer timer;

        public ScheduleTimer()
        {
            timer = new Timer();
            timer.AutoReset = true;
            timer.Elapsed += Timer_Elapsed;
        }

        public async Task Start(int period, ScheduleUnit unit)
        {
            periodUnit = unit;

            // Calcula el periodo en base a la unidad

            switch (periodUnit)
            {
                case ScheduleUnit.second:
                    periodMs = period * 1000;
                    break;
                case ScheduleUnit.minute:
                    periodMs = period * 60 * 1000;
                    break;
                case ScheduleUnit.hour:
                    periodMs = period * 60 * 60 * 1000;
                    break;
                case ScheduleUnit.day:
                    // TODO: Analizar como implementar, desborda el máximo
                    throw new NotImplementedException();
                    //break;
            }

            timer.Interval = periodMs;

            // Calcula la demora hasta la primer ejecución

            DateTime timeNow = DateTime.Now;
            TimeSpan spanPeriod = TimeSpan.FromMilliseconds(periodMs);

            long divFirstExec = (long)Math.Floor((decimal)(timeNow.Ticks / spanPeriod.Ticks)) + 1;

            DateTime firstExec = new DateTime(divFirstExec * spanPeriod.Ticks);

            Console.WriteLine($"{DateTime.Now}> Primera ejecucion telemetria datos: {firstExec} / Periodo: {periodMs} ms");

            await Task.Delay(firstExec.Subtract(timeNow));   
            
            // Arranca el timer y ejecuta la primera vez

            timer.Start();
            Timer_Elapsed(this, null);
        }

        public void Stop()
        {
            timer.Stop();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            OnElapsed(e);
        }

        protected virtual void OnElapsed(ElapsedEventArgs e)
        {
            Elapsed?.Invoke(this, e);
        }
    }
}
