using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFClient.Types;
using System;
using Vector3 = GTA.Math.Vector3;
using UIResText = NativeUI.UIResText;

namespace SPFClient.Entities
{
    public class NetworkPlayer : GameEntity
    {
        public readonly int ID;

        public string Name
        {
            get; private set;
        }

        public ClientState LastState
        {
            get { return lastReceivedState; }
        }

        public NetworkVehicle ActiveVehicle
        {
            get; set;
        }

        private int currentPedHash;
        private static int currentPedID;

        private int lastWeaponID;
        private int snapshotCount;
        private bool frozen = false;

        private static UIResText playerName;

        private static MovementController animationManager;
        private static BicyleAnimController bicyleController;

        private ExtrapolationManager extrapolator = new ExtrapolationManager();
        private EntitySnapshot[] moveBuffer = new EntitySnapshot[20];
        private ClientState lastReceivedState;

        /// <summary>
        /// Setup entity for the network client.
        /// </summary>
        public NetworkPlayer(ClientState state) : base(SetupPed(state))
        {
            ID = state.ID;
            Name = state.Name;
            lastReceivedState = state;
        }

        static Ped SetupPed(ClientState state)
        {
            var pedModel = new Model(Helpers.PedIDToHash(state.PedID));

            if (!pedModel.IsLoaded)
                pedModel.Request(1000);

            var position = state.InVehicle ? state.VehicleState.Position.Deserialize() : state.Position.Deserialize();
            var rotation = state.InVehicle ? state.VehicleState.Rotation.Deserialize() : state.Rotation.Deserialize();

            // handle the ped

            var spawnPos = position + new Vector3(0, 0, -1f);

            var ped = new Ped(Function.Call<int>(Hash.CREATE_PED, 26, pedModel.Hash, spawnPos.X, spawnPos.Y, spawnPos.Z, 0f, false, true));

            Function.Call(Hash.CLEAR_ALL_PED_PROPS, ped.Handle);

            Function.Call((Hash)0xE861D0B05C7662B8, ped.Handle, 0, 0);

            Function.Call((Hash)0x4668D80430D6C299, ped.Handle);

            Function.Call((Hash)0x687C0B594907D2E8, ped.Handle);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 189, 1);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 407, 1);

            Function.Call(Hash.SET_PED_CAN_RAGDOLL_FROM_PLAYER_IMPACT, ped.Handle, false);

            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 1);
            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 16);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 410, 1);
//            Function.Call(Hash.SET_ENTITY_ALPHA, ped.Handle, 255, 0);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 411, 1);

            Function.Call(Hash.SET_PED_MOVE_ANIMS_BLEND_OUT, ped.Handle);

         //   Function.Call((Hash)0xFF300C7649724A0B, Game.Player.Handle, 0);

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

            bicyleController = new BicyleAnimController(ped);

            var blip = ped.AddBlip();
            blip.Color = BlipColor.White;

            blip.SetName(state.Name);

            playerName = new UIResText(state.Name, 
                new System.Drawing.Point(), 
                0.6f, 
                System.Drawing.Color.White, 
                Font.ChaletLondon, 
                UIResText.Alignment.Centered);
           
            currentPedID = state.PedID;

            return ped;
        }

        public void HandleUpdatePacket(ClientState state, DateTime svTime)
        {

            // packet is out of range if this fails
            if (state.PktID > 0 && state.PktID == lastReceivedState.PktID + 1)
            {
                if (!state.InVehicle)
                {
                    // update client information from the queue.
                    if (lastReceivedState.InVehicle && !state.InVehicle)
                    {
                        Function.Call(Hash.TASK_LEAVE_ANY_VEHICLE, Handle, 0, 0);

                        var dt = DateTime.Now + TimeSpan.FromMilliseconds(1000);

                        while (DateTime.Now < dt)
                            Script.Yield();
                    }

                    var position = state.Position.Deserialize();
                    var vel = state.Velocity.Deserialize();
                    var rotation = state.Rotation.Deserialize();

                    var angles = state.Angles.Deserialize();

                    if (lastWeaponID != state.WeaponID)
                    {
                        new Ped(Handle).Weapons.Give(Helpers.WeaponIDToHash(state.WeaponID), 100, true, true);
                        lastWeaponID = state.WeaponID;
                    }

                    for (int i = moveBuffer.Length - 1; i > 0; i--)
                        moveBuffer[i] = moveBuffer[i - 1];

                    moveBuffer[0] = new EntitySnapshot(position, vel, rotation, angles, svTime, state.PktID);
                    snapshotCount = Math.Min(snapshotCount + 1, moveBuffer.Length);

                    if (state.Health <= 0) Health = -1;
                    else
                        Health = state.Health;

                    animationManager.UpdateAnimationFlags(state.MovementFlags, state.ActiveTask);

                }
                lastReceivedState = state;
            }

        /*    if (state.PktID - lastReceivedState.PktID > 10)
            {
                lastReceivedState = state;
            }*/
        }

        public override void Update()
        {
            if (Health <= 0)
            {
                if (!IsDead) Health = -1;
                return;
            }

            if (lastReceivedState.InVehicle)
            {
                bicyleController?.Update();
            }

            else
            {
                var entityPosition = extrapolator.GetExtrapolatedPosition(Position, Quaternion, moveBuffer, snapshotCount, 0.4f);

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
                        animationManager.UpdateLocalAngles(entityPosition.Position, entityPosition.Angles);
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
            }

            if (Position.DistanceTo(Game.Player.Character.Position) < 20f)
            {
                RenderPlayerName();
            }

            base.Update();
        }

        /// <summary>
        /// Set the state of the players bicycle.
        /// </summary>
        /// <param name="state"></param>
        public void SetBicycleState(BicycleState state)
        {
            bicyleController.SetBicycleState(state);
        }

        /// <summary>
        /// Set the state of the players active bicycle.
        /// </summary>
        /// <param name="state"></param>
        public void SetBicycleState(short state)
        {
            bicyleController.SetBicycleState((BicycleState)state);
        }

        /// <summary>
        /// Set the username for this NetworkPlayer.
        /// </summary>
        /// <param name="name"></param>
        public void SetName(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Render the in- game player name.
        /// </summary>
        private void RenderPlayerName()
        {
            playerName.Caption = Name == null ? "" : Name;

            var coords = Function.Call<Vector3>(Hash._GET_ENTITY_BONE_COORDS, Handle, 31086);
            var pos = coords + Position + new Vector3(0, 0, 1.50f);

            Function.Call(Hash.SET_DRAW_ORIGIN, pos.X, pos.Y, pos.Z, 0);

            playerName.Draw();

            Function.Call(Hash.CLEAR_DRAW_ORIGIN);
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
