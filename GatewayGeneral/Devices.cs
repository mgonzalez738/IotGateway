using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GatewayGeneral
{
    public enum DeviceTypes { Gateway, TowerInclinometer }
    public enum DeviceInterfaces { Rs485 }

    public interface IDevicesRs485
    {
        //Metodos
        public Task Init();
        public Task Stop();

        // Properties
        string DeviceId { get; set; }
        DeviceTypes DeviceType { get; }
        string ConnectionString { get; set; }
        DeviceInterfaces DeviceInterface { get; set; }
    }
}
