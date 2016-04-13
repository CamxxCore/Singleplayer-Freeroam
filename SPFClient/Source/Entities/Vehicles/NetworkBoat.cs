using SPFLib.Types;
using SPFClient.Types;
using GTA;
using System;
using GTA.Native;

namespace SPFClient.Entities
{
    public sealed class NetworkBoat : NetworkVehicle
    {
        BoatState updatedState;

        private BoatSnapshot[] stateBuffer = new BoatSnapshot[20];

        private int snapshotCount = 0;

        public NetworkBoat(VehicleState state) : base(state)
        {
            OnUpdateReceived += UpdateReceived;
        }

        public NetworkBoat(Vehicle vehicle, int id) : base(vehicle, id)
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
            updatedState = e as BoatState;

            var position = updatedState.Position.Deserialize();
            var vel = updatedState.Velocity.Deserialize();
            var rotation = updatedState.Rotation.Deserialize();

            for (int i = stateBuffer.Length - 1; i > 0; i--)
                stateBuffer[i] = stateBuffer[i - 1];

            stateBuffer[0] = new BoatSnapshot(position, vel, rotation, updatedState.Steering, 
                updatedState.CurrentRPM, timeSent);

            snapshotCount = Math.Min(snapshotCount + 1, stateBuffer.Length);

            if (updatedState.WaveHeight != Function.Call<float>((Hash)0x2B2A2CC86778B619))
                Function.Call((Hash)0xB96B00E976BE977F, updatedState.WaveHeight);
        }

        public BoatState GetBoatState()
        {
            var v = new Vehicle(Handle);

            var state = new BoatState(ID,
                Position.Serialize(),
                Velocity.Serialize(),
                Quaternion.Serialize(),
                v.CurrentRPM,
                GetSteering(),
                0, Convert.ToInt16(Health),
                (byte)v.PrimaryColor, (byte)v.SecondaryColor,
                GetRadioStation(),
                GetVehicleID());

            state.WaveHeight = Function.Call<float>((Hash)0x2B2A2CC86778B619);

            return state;
        }

        public override void Update()
        {
            if (snapshotCount > EntityExtrapolator.SnapshotMin)
            {
                var snapshot = EntityExtrapolator.GetExtrapolatedPosition(Position,
                    Quaternion, stateBuffer, snapshotCount, 0.62f);
                PositionNoOffset = snapshot.Position;
                Quaternion = snapshot.Rotation;
                Velocity = snapshot.Velocity;
                SetSteering(snapshot.Steering);
                SetCurrentRPM(snapshot.RPM);
            }

            base.Update();
        }
    }
}
