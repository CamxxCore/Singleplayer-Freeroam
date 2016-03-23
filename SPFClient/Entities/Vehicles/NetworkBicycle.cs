using SPFLib.Types;
using GTA;
namespace SPFClient.Entities
{
    public sealed class NetworkBicycle : NetworkVehicle
    {
        public NetworkBicycle(VehicleState state) : base(state)
        {
        }
        public NetworkBicycle(Vehicle vehicle, int id) : base(vehicle, id)
        { }
    }
}
