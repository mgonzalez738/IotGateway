using System;
using System.Collections.Generic;
using System.Text;

namespace Hardware
{
    public class GatewayData
    {
        private DateTime sampleUtcTime;
        private Single powerVoltage;
        private Single sensedVoltage;
        private Single batteryVoltage;
        private Single temperature;

        public GatewayData()
        {
            sampleUtcTime = new DateTime();
            powerVoltage = Single.NaN;
            sensedVoltage = Single.NaN;
            batteryVoltage = Single.NaN;
            temperature = Single.NaN;
        }

        public DateTime SampleUtcTime
        {
            get { return sampleUtcTime; }
            set { sampleUtcTime = value; }
        }

        public Single PowerVoltage
        {
            get { return powerVoltage; }
            set { powerVoltage = value; }
        }

        public Single SensedVoltage
        {
            get { return sensedVoltage; }
            set { sensedVoltage = value; }
        }

        public Single BatteryVoltage
        {
            get { return batteryVoltage; }
            set { batteryVoltage = value; }
        }

        public Single Temperature
        {
            get { return temperature; }
            set { temperature = value; }
        }
    }
}
