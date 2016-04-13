using SPFLib.Types;
using SPFLib.Enums;
using SPFClient.Types;
using GTA.Native;
using GTA;
using System;

namespace SPFClient.Entities
{
    public sealed class NetworkCar : NetworkVehicle
    {
        AutomobileState updatedState;

        private VehicleSnapshot[] stateBuffer = new VehicleSnapshot[20];

        private int snapshotCount = 0;

        public NetworkCar(VehicleState state) : base(state)
        {
            OnUpdateReceived += UpdateReceived;
        }

        public NetworkCar(Vehicle vehicle, int id) : base(vehicle, id)
        {
            OnUpdateReceived += UpdateReceived;
        }

        public void SetCurrentRPM(float value)
        {
            if (Address <= 0) return;
            MemoryAccess.WriteSingle(Address + Offsets.CVehicle.RPM, value);
        }

        private void UpdateReceived(DateTime timeSent, VehicleState e)
        {
            updatedState = e as AutomobileState;

            var position = updatedState.Position.Deserialize();
            var vel = updatedState.Velocity.Deserialize();
            var rotation = updatedState.Rotation.Deserialize();

            for (int i = stateBuffer.Length - 1; i > 0; i--)
                stateBuffer[i] = stateBuffer[i - 1];

            stateBuffer[0] = new VehicleSnapshot(position, vel, rotation, 
                updatedState.WheelRotation, updatedState.Steering, updatedState.CurrentRPM, timeSent);

            snapshotCount = Math.Min(snapshotCount + 1, stateBuffer.Length);

            var flags = (DamageFlags)e.ExtraFlags;
            var vehicle = new Vehicle(Handle);

            if (flags.HasFlag(DamageFlags.LWindow) && 
                Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontLeftWindow))
                vehicle.SmashWindow(VehicleWindow.FrontLeftWindow);

            if (flags.HasFlag(DamageFlags.RWindow) && 
                Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.FrontRightWindow))
                vehicle.SmashWindow(VehicleWindow.FrontRightWindow);

            if (flags.HasFlag(DamageFlags.BLWindow) && 
                Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackLeftWindow))
                vehicle.SmashWindow(VehicleWindow.BackLeftWindow);

            if (flags.HasFlag(DamageFlags.BRWindow) && 
                Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle.Handle, (int)VehicleWindow.BackRightWindow))
                vehicle.SmashWindow(VehicleWindow.BackRightWindow);

            if (flags.HasFlag(DamageFlags.LDoor) && 
                !vehicle.IsDoorBroken(VehicleDoor.FrontLeftDoor))
                vehicle.BreakDoor(VehicleDoor.FrontLeftDoor);

            if (flags.HasFlag(DamageFlags.RDoor) &&
                !vehicle.IsDoorBroken(VehicleDoor.FrontRightDoor))
                vehicle.BreakDoor(VehicleDoor.FrontRightDoor);

            if (flags.HasFlag(DamageFlags.BLDoor) &&
                !vehicle.IsDoorBroken(VehicleDoor.BackLeftDoor))
                vehicle.BreakDoor(VehicleDoor.BackLeftDoor);

            if (flags.HasFlag(DamageFlags.BRDoor) &&
                !vehicle.IsDoorBroken(VehicleDoor.BackRightDoor))
                vehicle.BreakDoor(VehicleDoor.BackRightDoor);

            if (flags.HasFlag(DamageFlags.Hood) &&
                !vehicle.IsDoorBroken(VehicleDoor.Hood))
                vehicle.BreakDoor(VehicleDoor.Hood);

            if (flags.HasFlag(DamageFlags.LHeadlight) && !vehicle.LeftHeadLightBroken)
                vehicle.LeftHeadLightBroken = true;

            if (flags.HasFlag(DamageFlags.RHeadlight) && !vehicle.RightHeadLightBroken)
                vehicle.RightHeadLightBroken = true;
        }

        public AutomobileState GetAutomobileState()
        {
            var v = new Vehicle(Handle);

            var state = new AutomobileState(ID,
                Position.Serialize(),
                Velocity.Serialize(),
                Quaternion.Serialize(),
                v.CurrentRPM,
                GetWheelRotation(),
                GetSteering(),
                0, Convert.ToInt16(Health),
                (byte)v.PrimaryColor, (byte)v.SecondaryColor,
                GetRadioStation(),
                GetVehicleID());

            if (Function.Call<bool>(Hash.IS_HORN_ACTIVE, Handle))
            {
                state.Flags |= VehicleFlags.HornPressed;
            }

      //      if (IsDead)
         //       state.Flags |= VehicleFlags.Exploded;

            if (Function.Call<bool>((Hash)0x5EF77C9ADD3B11A3, Handle)) //Left Headlight?
                state.ExtraFlags |= (ushort)DamageFlags.LHeadlight;

            if (Function.Call<bool>((Hash)0xA7ECB73355EB2F20, Handle)) //Right Headlight?
                state.ExtraFlags |= (ushort)DamageFlags.RHeadlight;

            if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int)VehicleDoor.FrontLeftDoor))
                state.ExtraFlags |= (ushort)DamageFlags.LDoor;

            if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int)VehicleDoor.FrontRightDoor))
                state.ExtraFlags |= (ushort)DamageFlags.RDoor;

            if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int)VehicleDoor.BackLeftDoor))
                state.ExtraFlags |= (ushort)DamageFlags.BLDoor;

            if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int)VehicleDoor.BackRightDoor))
                state.ExtraFlags |= (ushort)DamageFlags.BRDoor;

            if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int)VehicleDoor.Hood))
                state.ExtraFlags |= (ushort)DamageFlags.Hood;

            return state;
        }

        public override void Update()
        {
            if (snapshotCount > EntityExtrapolator.SnapshotMin)
            {
                var snapshot = EntityExtrapolator.GetExtrapolatedPosition(Position,
                    Quaternion, stateBuffer, snapshotCount, 0.946f);

                PositionNoOffset = snapshot.Position;
                Quaternion = snapshot.Rotation;
                Velocity = snapshot.Velocity;
                SetWheelRotation(snapshot.WheelRotation);
                SetSteering(snapshot.Steering);
                SetCurrentRPM(snapshot.RPM);
            }

            if (updatedState != null && updatedState.Flags.HasFlag(VehicleFlags.HornPressed))
            {
                Function.Call(Hash.START_VEHICLE_HORN, Handle, 0, 0x4F485502, 0);
            }

            base.Update();
        }
    }
}
