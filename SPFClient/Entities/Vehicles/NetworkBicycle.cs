using SPFLib.Types;
using GTA;
using System;
using GTA.Native;
using SPFClient.Types;

namespace SPFClient.Entities
{
    public sealed class NetworkBicycle : NetworkVehicle
    {
        BicycleState updatedState;

        private BicycleSnapshot[] stateBuffer = new BicycleSnapshot[20];

        private int snapshotCount = 0;

        public NetworkBicycle(VehicleState state) : base(state)
        {
            OnUpdateReceived += UpdateReceived; 
        }

        public NetworkBicycle(Vehicle vehicle, int id) : base(vehicle, id)
        { }

        private void UpdateReceived(DateTime timeSent, VehicleState e)
        {
            updatedState = e as BicycleState;

            var position = updatedState.Position.Deserialize();
            var vel = updatedState.Velocity.Deserialize();
            var rotation = updatedState.Rotation.Deserialize();

            for (int i = stateBuffer.Length - 1; i > 0; i--)
                stateBuffer[i] = stateBuffer[i - 1];

            stateBuffer[0] = new BicycleSnapshot(position, vel, rotation, updatedState.WheelRotation, 
                updatedState.Steering, timeSent);

            snapshotCount = Math.Min(snapshotCount + 1, stateBuffer.Length);
        }

        public override VehicleState GetState()
        {
            var state = base.GetState();

            if (Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 2, (int)Control.VehiclePushbikePedal))
            {
                if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 2, (int)Control.ReplayPreviewAudio))
                    state.ExtraFlags = (ushort)BicycleTask.TuckPedaling;
                else state.ExtraFlags = (ushort)BicycleTask.Pedaling;
            }

            else
            {
                if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 2, (int)Control.ReplayPreviewAudio))
                    state.ExtraFlags = (ushort)BicycleTask.TuckCruising;
                else state.ExtraFlags = (ushort)BicycleTask.Cruising;
            }

            return state;
        }

        public override void Update()
        {
            if (snapshotCount > EntityExtrapolator.SnapshotMin)
            {
                var snapshot = EntityExtrapolator.GetExtrapolatedPosition(Position,
                Quaternion, stateBuffer, snapshotCount, 0.89f);
                PositionNoOffset = snapshot.Position;
                Quaternion = snapshot.Rotation;
                Velocity = snapshot.Velocity;
                SetWheelRotation(snapshot.WheelRotation);
                SetSteering(snapshot.Steering);
            }

            base.Update();
        }
    }
}
