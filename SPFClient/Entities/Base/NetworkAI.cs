using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFClient.Types;
using System;
using Vector3 = GTA.Math.Vector3;

namespace SPFClient.Entities
{
    public class NetworkAI : ManagedEntity
    {
        public readonly int ID;

        public readonly bool IsLocal;

        public AIState LastState
        {
            get { return lastReceivedState; }
        }

        private int currentPedHash;
        private static int currentPedID;

        private int lastPacketID;

        private int lastWeaponID;
        private int snapshotCount;
        private bool frozen = false;

        private static MovementController animationManager;

        private Extrapolator extrapolator = new Extrapolator();
        private EntitySnapshot[] moveBuffer = new EntitySnapshot[20];
        private AIState lastReceivedState;

        /// <summary>
        /// Setup entity for the network client.
        /// </summary>
        public NetworkAI(AIState state, bool isLocal) : base(SetupPed(state))
        {
            ID = state.ID;
            IsLocal = isLocal;
            lastReceivedState = state;
        }

        /// <summary>
        /// Setup entity for the network client.
        /// </summary>
        public NetworkAI(AIState state) : this(state, false)
        { }

        static Ped SetupPed(AIState state)
        {
            var pedModel = new Model(Helpers.PedIDToHash(state.PedID));

            if (!pedModel.IsLoaded)
                pedModel.Request(1000);

            var position = state.Position.Deserialize();

            var rotation = state.Rotation.Deserialize();

            // handle the ped

            var spawnPos = position + new Vector3(0, 0, -1f);

            var ped = new Ped(Function.Call<int>(Hash.CREATE_PED, 26, pedModel.Hash, spawnPos.X, spawnPos.Y, spawnPos.Z, 0f, false, true));

            Function.Call((Hash)0xE861D0B05C7662B8, ped.Handle, 0, 0);

            Function.Call((Hash)0x4668D80430D6C299, ped.Handle);

            Function.Call((Hash)0x687C0B594907D2E8, ped.Handle);
            Function.Call(Hash.CLEAR_ALL_PED_PROPS, ped.Handle);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 189, 1);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 407, 1);

            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 1);
            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 16);

            Function.Call(Hash.SET_PED_CAN_RAGDOLL_FROM_PLAYER_IMPACT, ped.Handle, false);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 410, 1);
            //            Function.Call(Hash.SET_ENTITY_ALPHA, ped.Handle, 255, 0);
            Function.Call(Hash.SET_PLAYER_VEHICLE_DEFENSE_MODIFIER, Game.Player.Handle, 0.5);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 411, 1);

            Function.Call((Hash)0xFF300C7649724A0B, Game.Player.Handle, 0);

            var dt = DateTime.Now + TimeSpan.FromMilliseconds(250);

            while (DateTime.Now < dt)
                Script.Yield();

            ped.Quaternion = rotation;

            ped.BlockPermanentEvents = true;

            ped.CanRagdoll = false;

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 342, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 122, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 134, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 115, 1);

            Function.Call(Hash.SET_PED_CAN_BE_DRAGGED_OUT, ped.Handle, true);

            Function.Call((Hash)0x687C0B594907D2E8, ped.Handle);

            Function.Call(Hash.SET_PLAYER_VEHICLE_DEFENSE_MODIFIER, Game.Player.Handle, 0.5);

            Function.Call(Hash.SET_PED_CAN_EVASIVE_DIVE, ped.Handle, false);

            Function.Call(Hash.SET_PED_SUFFERS_CRITICAL_HITS, ped.Handle, false);

            Function.Call(Hash.SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE, ped.Handle, 1);

            ped.Weapons.Give(Helpers.WeaponIDToHash(state.WeaponID), -1, true, true);

            animationManager = new MovementController(ped);

            var blip = ped.AddBlip();
            blip.Color = BlipColor.Green;


            currentPedID = state.PedID;

            return ped;
        }

        public void HandleUpdatePacket(AIState state, int packetID, DateTime svTime)
        {

            // packet is out of range if this fails
            if (packetID > 0 && packetID > lastPacketID)
            {

                var position = state.Position.Deserialize();
                var vel = state.Velocity.Deserialize();
                var rotation = state.Rotation.Deserialize();

                if (lastWeaponID != state.WeaponID)
                {
                    new Ped(Handle).Weapons.Give(Helpers.WeaponIDToHash(state.WeaponID), 100, true, true);
                    lastWeaponID = state.WeaponID;
                }

                for (int i = moveBuffer.Length - 1; i > 0; i--)
                    moveBuffer[i] = moveBuffer[i - 1];

                moveBuffer[0] = new EntitySnapshot(position, vel, rotation, svTime, packetID);
                snapshotCount = Math.Min(snapshotCount + 1, moveBuffer.Length);

                if (state.Health <= 0) Health = -1;
                else
                    Health = state.Health;

                animationManager.UpdateAnimationFlags(state.MovementFlags, 0);

                lastReceivedState = state;
                lastPacketID = packetID;
            }

            else
            {
                extrapolator.QueueUnorderedPacket(state, svTime, packetID);
            }


            if (packetID - lastPacketID > 5)
            {
                lastReceivedState = state;
                lastPacketID = packetID;
            }
        }

        public override void Update()
        {
            if (Health <= 0)
            {
                if (!IsDead) Health = -1;
                return;
            }

            var entityPosition = extrapolator.GetExtrapolatedPosition(Position, Quaternion, moveBuffer, snapshotCount, 1f);

            if (entityPosition != null)
            {
                if (frozen)
                {
                    FreezePosition = false;
                    frozen = false;
                }

                if (Position.DistanceTo(entityPosition.Position) > 10f)
                {
                    PositionNoOffset = moveBuffer[0].Position;
                    Quaternion = moveBuffer[0].Rotation;
                }

                else
                {
                    PositionNoOffset = entityPosition.Position;
                    Quaternion = entityPosition.Rotation;
                    Velocity = entityPosition.Velocity;
                }
            }

            else
            {
                if (!frozen)
                {
                    FreezePosition = true;
                    frozen = true;
                }
            }

            animationManager.Update();


            base.Update();
        }

        /// <summary>
        /// Get the ped ID of this NetworkPlayer.
        /// </summary>
        /// <returns></returns>
        public int GetPedID()
        {
            if (Model.Hash != currentPedHash)
            {
                currentPedHash = Model.Hash;
                currentPedID = Helpers.PedHashtoID((PedHash)currentPedHash);
            }

            return currentPedID;
        }

        /// <summary>
        /// Get an entity snapshot from the buffer.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public EntitySnapshot GetEntitySnapshot(int index)
        {
            if (index > moveBuffer.Length - 1) throw new ArgumentOutOfRangeException("index: out of range.");
            return moveBuffer[index];
        }

        /// <summary>
        /// Unload player related things.
        /// </summary>
        public void Unload()
        {
            IsInvincible = false;
        }

        /// <summary>
        /// Remove the ped from the game world.
        /// </summary>
        public void Remove()
        {
            CurrentBlip?.Remove();
            Delete();
        }
    }
}
