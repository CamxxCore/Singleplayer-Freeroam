using SPFLib.Types;
using GTA;
using GTA.Native;

namespace SPFClient.Entities
{
    public sealed class NetworkHeli : NetworkVehicle
    {
        public NetworkHeli(VehicleState state) : base(state)
        {
        }

        public NetworkHeli(Vehicle vehicle, int id) : base(vehicle, id)
        {
        }

        public override void Update()
        {
            Function.Call(Hash.SET_HELI_BLADES_FULL_SPEED, Handle);
            base.Update();
        }
    }
}
