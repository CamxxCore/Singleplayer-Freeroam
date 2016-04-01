using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFLib.Enums;
using SPFLib;
using System;
using Vector3 = GTA.Math.Vector3;
using UIResText = NativeUI.UIResText;

namespace SPFClient.Entities
{
    public class NetworkAI : GameEntity
    {
        public readonly int ID;

        public string Name
        {
            get; private set;
        }

        public AIState LastState
        {
            get { return lastReceivedState; }
        }

        public NetworkVehicle ActiveVehicle
        {
            get; set;
        }

        private int currentPedHash;
        private static PedType pedType;

        private DateTime lastUpdateTime;

        private static UIResText playerName;

        private AIState lastReceivedState;

        /// <summary>
        /// Setup entity for the network client.
        /// </summary>
        public NetworkAI(AIState state) : base(SetupPed(state))
        {
            ID = state.ClientID;
            Name = state.Name;
            lastReceivedState = state;
            lastUpdateTime = NetworkTime.Now;
        }

        static Ped SetupPed(AIState state)
        {
            PedHash result;

            if (!Enum.TryParse(state.PedType.ToString(), out result))
                result = PedHash.Michael;

            var pedModel = new Model(result);

            if (!pedModel.IsLoaded)
                pedModel.Request(1000);

            var position = state.Position.Deserialize();

            var rotation = state.Rotation.Deserialize();

            var spawnPos = position + new Vector3(0, 0, -1f);

            var ped = new Ped(Function.Call<int>(Hash.CREATE_PED, 26, pedModel.Hash, spawnPos.X, spawnPos.Y, spawnPos.Z, 0f, false, true));

       /*     Function.Call(Hash.CLEAR_ALL_PED_PROPS, ped.Handle);

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

            Function.Call(Hash.SET_PED_CAN_RAGDOLL_FROM_PLAYER_IMPACT, ped.Handle, false);

            //      Function.Call(Hash.SET_ENTITY_CAN_BE_DAMAGED, ped.Handle, false);

            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 1);
            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 16);

            Function.Call(Hash.SET_PED_MOVE_ANIMS_BLEND_OUT, ped.Handle);

            var dt = DateTime.Now + TimeSpan.FromMilliseconds(250);

            while (DateTime.Now < dt)
                Script.Yield();

            ped.Quaternion = rotation;

            //  ped.BlockPermanentEvents = true;

            ped.CanRagdoll = false;

            Function.Call(Hash.SET_ENTITY_COLLISION, ped.Handle, true, false);

            Function.Call(Hash.SET_PED_CAN_EVASIVE_DIVE, ped.Handle, false);

            Function.Call(Hash.SET_PED_CAN_BE_DRAGGED_OUT, ped.Handle, true);

            Function.Call((Hash)0x687C0B594907D2E8, ped.Handle);

            Function.Call(Hash.SET_PLAYER_VEHICLE_DEFENSE_MODIFIER, Game.Player.Handle, 0.5);

            Function.Call((Hash)0x26695EC767728D84, ped.Handle, 8208);

            Function.Call(Hash.SET_PED_SUFFERS_CRITICAL_HITS, ped.Handle, false);*/

            var blip = ped.AddBlip();
            blip.Color = BlipColor.White;

            blip.SetName(state.Name);

            playerName = new UIResText(state.Name,
                new System.Drawing.Point(),
                0.6f,
                System.Drawing.Color.White,
                Font.ChaletLondon,
                UIResText.Alignment.Centered);

            pedType = state.PedType;

            return ped;
        }

        public void HandleUpdatePacket(AIState state, DateTime svTime)
        {
            PositionNoOffset = state.Position.Deserialize();
            Quaternion = state.Rotation.Deserialize();        
            lastReceivedState = state;
            lastUpdateTime = NetworkTime.Now;
        }

        public override void Update()
        {
          /*  if (NetworkTime.Now - lastUpdateTime > TimeSpan.FromMilliseconds(1000))
            {
                if (Exists())
                    Dispose();
                return;
            }*/

            if (Position.DistanceTo(Game.Player.Character.Position) < 20f)
            {
                RenderPlayerName();
            }

            base.Update();
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
        /// Avoid iterating inside xxHashtoID while running the game loop.
        /// </summary>
        /// <returns></returns>
        public PedType GetPedType()
        {
            if (Model.Hash != currentPedHash)
            {
                currentPedHash = Model.Hash;
                Enum.TryParse(((PedHash)currentPedHash).ToString(), out pedType);
            }

            return pedType;
        }
    }
}
