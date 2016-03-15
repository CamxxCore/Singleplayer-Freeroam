using SPFLib.Types;
using SPFLib.Enums;
using GTA.Native;
using GTA;

namespace SPFClient.Entities
{
    public sealed class NetCar : NetworkVehicle
    {
        public NetCar(VehicleState state) : base(state)
        {
            OnUpdateRecieved += UpdateReceived;
        }

        public NetCar(Vehicle vehicle, int id) : base(vehicle, id)
        {
            OnUpdateRecieved += UpdateReceived;
        }

        private void UpdateReceived(NetworkVehicle sender, VehicleState e)
        {
            var flags = (VDamageFlags)e.ExtraFlags;
            var vehicle = new Vehicle(Handle);

            if (flags.HasFlag(VDamageFlags.LDoorBroken) && !vehicle.IsDoorBroken(VehicleDoor.FrontLeftDoor))
                vehicle.BreakDoor(VehicleDoor.FrontLeftDoor);

            if (flags.HasFlag(VDamageFlags.RDoorBroken) && !vehicle.IsDoorBroken(VehicleDoor.FrontRightDoor))
                vehicle.BreakDoor(VehicleDoor.FrontRightDoor);

            if (flags.HasFlag(VDamageFlags.BLDoorBroken) && !vehicle.IsDoorBroken(VehicleDoor.BackLeftDoor))
                vehicle.BreakDoor(VehicleDoor.BackLeftDoor);

            if (flags.HasFlag(VDamageFlags.BRDoorBroken) && !vehicle.IsDoorBroken(VehicleDoor.BackRightDoor))
                vehicle.BreakDoor(VehicleDoor.BackRightDoor);

            if (flags.HasFlag(VDamageFlags.HoodBroken) && !vehicle.IsDoorBroken(VehicleDoor.Hood))
                vehicle.BreakDoor(VehicleDoor.Hood);

            if (flags.HasFlag(VDamageFlags.LWindowBroken) && Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontLeftWindow))
                vehicle.SmashWindow(VehicleWindow.FrontLeftWindow);

            if (flags.HasFlag(VDamageFlags.RWindowBroken) && Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontRightWindow))
                vehicle.SmashWindow(VehicleWindow.FrontRightWindow);

            if (flags.HasFlag(VDamageFlags.BLWindowBroken) && Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackLeftWindow))
                vehicle.SmashWindow(VehicleWindow.BackLeftWindow);

            if (flags.HasFlag(VDamageFlags.BRWindowBroken) && Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackRightWindow))
                vehicle.SmashWindow(VehicleWindow.BackRightWindow);

            if (flags.HasFlag(VDamageFlags.LHeadlightBroken) && !vehicle.LeftHeadLightBroken)
                vehicle.LeftHeadLightBroken = true;

            if (flags.HasFlag(VDamageFlags.RHeadlightBroken) && !vehicle.RightHeadLightBroken)
                vehicle.RightHeadLightBroken = true;

            /*   if (Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontLeftWindow))
                   state.VehicleState.Flags |= VehicleDMGFlags.LWindowBroken;

               if (Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontRightWindow))
                   state.VehicleState.Flags |= VehicleDMGFlags.RWindowBroken;

               if (Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackLeftWindow))
                   state.VehicleState.Flags |= VehicleDMGFlags.BLWindowBroken;

               if (Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackRightWindow))
                   state.VehicleState.Flags |= VehicleDMGFlags.BRWindowBroken;*/
        }

        public override void Update()
        {
            base.Update();
        }
    }
}
