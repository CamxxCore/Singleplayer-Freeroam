using SPFLib.Types;
using SPFLib.Enums;
using GTA.Native;
using GTA;

namespace SPFClient.Entities
{
    public sealed class NetworkCar : NetworkVehicle
    {
        public NetworkCar(VehicleState state) : base(state)
        {
            OnUpdateRecieved += UpdateReceived;
        }

        public NetworkCar(Vehicle vehicle, int id) : base(vehicle, id)
        {
            OnUpdateRecieved += UpdateReceived;
        }

        private void UpdateReceived(NetworkVehicle sender, VehicleState e)
        {
            var flags = (DamageFlags)e.ExtraFlags;
            var vehicle = new Vehicle(Handle);

            if (flags.HasFlag(DamageFlags.LDoor) && !vehicle.IsDoorBroken(VehicleDoor.FrontLeftDoor))
                vehicle.BreakDoor(VehicleDoor.FrontLeftDoor);

            if (flags.HasFlag(DamageFlags.RDoor) && !vehicle.IsDoorBroken(VehicleDoor.FrontRightDoor))
                vehicle.BreakDoor(VehicleDoor.FrontRightDoor);

            if (flags.HasFlag(DamageFlags.BLDoor) && !vehicle.IsDoorBroken(VehicleDoor.BackLeftDoor))
                vehicle.BreakDoor(VehicleDoor.BackLeftDoor);

            if (flags.HasFlag(DamageFlags.BRDoor) && !vehicle.IsDoorBroken(VehicleDoor.BackRightDoor))
                vehicle.BreakDoor(VehicleDoor.BackRightDoor);

            if (flags.HasFlag(DamageFlags.Hood) && !vehicle.IsDoorBroken(VehicleDoor.Hood))
                vehicle.BreakDoor(VehicleDoor.Hood);

            if (flags.HasFlag(DamageFlags.LWindow) && Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontLeftWindow))
                vehicle.SmashWindow(VehicleWindow.FrontLeftWindow);

            if (flags.HasFlag(DamageFlags.RWindow) && Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontRightWindow))
                vehicle.SmashWindow(VehicleWindow.FrontRightWindow);

            if (flags.HasFlag(DamageFlags.BLWindow) && Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackLeftWindow))
                vehicle.SmashWindow(VehicleWindow.BackLeftWindow);

            if (flags.HasFlag(DamageFlags.BRWindow) && Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackRightWindow))
                vehicle.SmashWindow(VehicleWindow.BackRightWindow);

            if (flags.HasFlag(DamageFlags.LHeadlight) && !vehicle.LeftHeadLightBroken)
                vehicle.LeftHeadLightBroken = true;

            if (flags.HasFlag(DamageFlags.RHeadlight) && !vehicle.RightHeadLightBroken)
                vehicle.RightHeadLightBroken = true;

            if (flags.HasFlag(DamageFlags.RRearHeadlight) && 
                (MemoryAccess.ReadInt32(MemoryAccess.GetEntityAddress(vehicle.Handle) + 
                Offsets.CVehicle.LightDamage) & 8) != 0)
            {

            }

            if (flags.HasFlag(DamageFlags.RRearHeadlight) &&
                (MemoryAccess.ReadInt32(MemoryAccess.GetEntityAddress(vehicle.Handle) + 
                Offsets.CVehicle.LightDamage) & 8) != 0)
            {

            }


            /*   if (Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontLeftWindow))
                   state.VehicleState.Flags |= VehicleDMGFlags.LWindow;

               if (Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontRightWindow))
                   state.VehicleState.Flags |= VehicleDMGFlags.RWindow;

               if (Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackLeftWindow))
                   state.VehicleState.Flags |= VehicleDMGFlags.BLWindow;

               if (Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackRightWindow))
                   state.VehicleState.Flags |= VehicleDMGFlags.BRWindow;*/
        }

        public override void Update()
        {
            base.Update();
        }
    }
}
