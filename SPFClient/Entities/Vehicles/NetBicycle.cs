using SPFLib.Types;
using GTA;
namespace SPFClient.Entities
{
    public sealed class NetBicycle : NetworkVehicle
    {
        public NetBicycle(VehicleState state) : base(state)
        {
        }
        public NetBicycle(Vehicle vehicle, int id) : base(vehicle, id)
        { }
    }
}
