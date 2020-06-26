using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace GatewayGeneral
{
    public enum ScheduleUnit { second, minute, hour, day };
    public enum ScheduleState { stopped, waiting, running };

    public class ScheduleTimer
    {
        public event EventHandler Elapsed;

        private int periodMs;
        private DateTime firstExecution;
        private ScheduleState state;
        private System.Timers.Timer timer;
        private CancellationTokenSource tokenSource; 

        public ScheduleTimer()
        {
            timer = new Timer();
            timer.AutoReset = true;
            timer.Elapsed += Timer_Elapsed;
            state = ScheduleState.stopped;
        }

        private async Task start(int period, ScheduleUnit unit, CancellationToken token)
        {
            state = ScheduleState.waiting;

            // Calcula el periodo en base a la unidad

            switch (unit)
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

            firstExecution = new DateTime(divFirstExec * spanPeriod.Ticks);

            // Espera se cumpla el periodo o se cancele el timer

            try
            {
                await Task.Delay(firstExecution.Subtract(timeNow), token);
            }
            catch
            {
                return;
            }

            // Arranca el timer si no se cancelo y ejecuta la primera vez el evento

             state = ScheduleState.running;
            timer.Start();
            Timer_Elapsed(this, null);        
        }

        public void Start(int period, ScheduleUnit unit)
        {
            tokenSource = new CancellationTokenSource();
            _ = start(period, unit, tokenSource.Token);
        }

        public void Stop()
        {
            if (state == ScheduleState.waiting)
                tokenSource.Cancel();
            if( state == ScheduleState.running)
                timer.Stop();
            state = ScheduleState.stopped;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            OnElapsed(e);
        }

        protected virtual void OnElapsed(ElapsedEventArgs e)
        {
            Elapsed?.Invoke(this, e);
        }

        public ScheduleState State
        {
            get { return state; }
        }

        public int PeriodMs
        {
            get { return periodMs; }
        }

        public DateTime FirstExcecution
        {
            get { return firstExecution; }
        }
    }
}
