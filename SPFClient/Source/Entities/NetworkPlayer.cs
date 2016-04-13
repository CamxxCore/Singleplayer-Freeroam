using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFClient.Types;
using SPFLib;
using System;

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
            get { return lastState; }
        }

        public NetworkVehicle ActiveVehicle
        {
            get; set;
        }

        private int snapshotCount;

        private bool frozen = false;

        private DateTime lastUpdateTime;

        private AnimationManager animationManager;
        private static BicyleController bicyleController;

        private PlayerSnapshot[] moveBuffer = new PlayerSnapshot[20];

        private ClientState lastState;

        /// <summary>
        /// Setup entity for the network client.
        /// </summary>
        public NetworkPlayer(ClientState state) : base(SetupPed(state))
        {
            ID = state.ClientID;
            Name = state.Name;
            lastState = state;
            lastUpdateTime = NetworkTime.Now;
           // ExitWater += OnExitWater;
            animationManager = new AnimationManager(new Ped(Handle));
        }

        private void OnExitWater(object sender, EntityChangedEventArgs e)
        {
          //  Function.Call(Hash.CLEAR_PED_TASKS_IMMEDIATELY, Handle);
        }

        static Ped SetupPed(ClientState state)
        {
            var pedModel = new Model((int) state.PedHash);

            if (!pedModel.IsLoaded)
                pedModel.Request(1000);

            var position = state.Position.Deserialize();

            var rotation = state.Rotation.Deserialize();

            var ped = new Ped(Function.Call<int>(Hash.CREATE_PED, 26, pedModel.Hash, position.X, position.Y, position.Z, 0f, false, true));

            ped.PositionNoOffset = position;

            pedModel.MarkAsNoLongerNeeded();

            ped.BlockPermanentEvents = true;

            Function.Call(Hash.SET_PED_CAN_RAGDOLL_FROM_PLAYER_IMPACT, ped.Handle, false);

            Function.Call(Hash.CLEAR_ALL_PED_PROPS, ped.Handle);

            Function.Call((Hash)0xE861D0B05C7662B8, ped.Handle, 0, 0);

            Function.Call((Hash)0x4668D80430D6C299, ped.Handle);

            Function.Call((Hash)0x687C0B594907D2E8, ped.Handle);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 189, 1);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 407, 1);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 410, 1);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 411, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 342, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 122, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 134, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 115, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, 185, 1);

            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 1);
            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 16);

            Function.Call(Hash.SET_PED_MOVE_ANIMS_BLEND_OUT, ped.Handle);

            Function.Call(Hash.SET_PED_SUFFERS_CRITICAL_HITS, ped.Handle, false);

            ped.Quaternion = rotation;

            ped.CanRagdoll = false;

            Function.Call(Hash.SET_PED_CAN_EVASIVE_DIVE, ped.Handle, false);

            var curWeaponHash = Helpers.WeaponIDToHash(state.WeaponID);      

            ped.Weapons.Give(curWeaponHash, -1, true, true);

            Function.Call(Hash.SET_PED_INFINITE_AMMO_CLIP, ped.Handle, true);

            Function.Call(Hash.SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE, ped.Handle, 3);

            Function.Call(Hash.SET_ENTITY_CAN_BE_DAMAGED, ped.Handle, false);
        
            bicyleController = new BicyleController(ped);

            var blip = ped.AddBlip();
            blip.Color = BlipColor.White;

            blip.SetName(state.Name);

            return ped;
        }

        public void HandleUpdate(ClientState state, DateTime time)
        {
            if (!state.InVehicle)
            {
                if (lastState.InVehicle && !state.InVehicle)
                {
                    Function.Call(Hash.TASK_LEAVE_ANY_VEHICLE, Handle, 0, 0);

                    var dt = DateTime.Now + TimeSpan.FromMilliseconds(1000);

                    while (DateTime.Now < dt) Script.Yield();
                }

                var weaponHash = (int) Helpers.WeaponIDToHash(state.WeaponID);

                if (Function.Call<int>(Hash.GET_SELECTED_PED_WEAPON, Handle) != weaponHash)
                {
                    Function.Call(Hash.GIVE_WEAPON_TO_PED, Handle, weaponHash, -1, true, true);
                }

                if (!string.IsNullOrWhiteSpace(state.Name) && Name != state.Name)
                    Name = state.Name;

                for (int i = moveBuffer.Length - 1; i > 0; i--)
                    moveBuffer[i] = moveBuffer[i - 1];

                moveBuffer[0] = new PlayerSnapshot(state.Position.Deserialize(), state.Velocity.Deserialize(), 
                    state.Rotation.Deserialize(), state.AimCoords.Deserialize(), state.ActiveTask, 
                    state.MovementFlags, time);

                snapshotCount = Math.Min(snapshotCount + 1, moveBuffer.Length);
            }

            lastState = state;
            lastUpdateTime = NetworkTime.Now;
        }

        public override void Update()
        {
            if (lastState == null) return;

            if (NetworkTime.Now - lastUpdateTime > TimeSpan.FromMilliseconds(1000) ||
                lastState.PedHash != 0 && Model.Hash != (int)lastState.PedHash ||
                lastState.Health > 0 && Health <= 0)
            {
                if (Exists())
                    Dispose();
                return;
            }

            if (lastState.Health < 0)
            {
                Function.Call(Hash.SET_ENTITY_CAN_BE_DAMAGED, Handle, true);
                Health = -1;
            }

            if (lastState.Health != Health)
            {
                Health = lastState.Health;
            }

            if (lastState.Health < 0 || IsDead)
            { 
                if (CurrentBlip?.Sprite != BlipSprite.Dead)
                {
                    CurrentBlip.Sprite = BlipSprite.Dead;
                }

                return;
            }

            else
            {       
                if (CurrentBlip?.Sprite != BlipSprite.Standard)
                {
                    CurrentBlip.Sprite = BlipSprite.Standard;
                }
            }

            if (lastState.InVehicle)
            {
                bicyleController?.Update();
            }

            else
            {
                if (snapshotCount > EntityExtrapolator.SnapshotMin)
                {
                    var entityPosition = EntityExtrapolator.GetExtrapolatedPosition(Position, Quaternion,
                        moveBuffer, snapshotCount, 0.6f);

                    if (frozen)
                    {
                        frozen = FreezePosition = false;
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

                        animationManager.SetFlags(entityPosition.MovementFlags, entityPosition.ActiveTask);

                        animationManager.UpdateLocalAngles(entityPosition.Position, entityPosition.AimCoords);
                    }
                }

                else
                {
                    if (!frozen)
                    {
                        frozen = FreezePosition = true;              
                    }
                }

                animationManager?.Update();
            }

            base.Update();
        }

        /// <summary>
        /// Set the state of the players bicycle.
        /// </summary>
        /// <param name="state"></param>
        public void SetBicycleState(BicycleTask state)
        {
            bicyleController.SetTask(state);
        }
    }
}
