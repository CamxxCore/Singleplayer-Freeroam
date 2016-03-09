using SPFLib.Types;
using GTA;
using GTA.Native;

namespace SPFClient.Entities
{
    public sealed class NetHeli : NetworkVehicle
    {
        public NetHeli(VehicleState state) : base(state)
        {
            OnUpdateRecieved += UpdateReceived;
        }

        private void UpdateReceived(NetworkVehicle sender, VehicleState e)
        {
        }

        public override void Update()
        {
            Function.Call(Hash.SET_HELI_BLADES_FULL_SPEED, Handle);

            base.Update();
        }
    }
}
