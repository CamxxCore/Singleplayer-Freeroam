using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFLib.Enums;
using SPFLib;
using System;
using SPFClient.Types;
using Vector3 = GTA.Math.Vector3;
using UIResText = NativeUI.UIResText;

namespace SPFClient.Entities
{
    public class NetworkAI : GameEntity
    {
        public readonly int ID;

        public string Name
        {
            get { return playerName.Caption; }
        }

        public AIState LastState
        {
            get { return lastReceivedState; }
        }

        private int currentPedHash;
        private static PedType pedType;
        private bool updatingPosition;
        private DateTime lastUpdateTime;
        private int snapshotCount;

        private int deathCounter;

        private static Vector3 lastPosition;
        private static GTA.Math.Quaternion lastRotation;

        private static UIResText playerName;

        private static UIRectangle playerHealth;

        private AISnapshot[] moveBuffer = new AISnapshot[20];

        private AIState lastReceivedState;

        /// <summary>
        /// Setup entity for the network client.
        /// </summary>
        public NetworkAI(AIState state) : base(SetupPed(state.Name, 
            state.Position.Deserialize(), 
            state.Rotation.Deserialize(), 
            state.PedType))
        {
            ID = state.ClientID;
            lastReceivedState = state;
            lastUpdateTime = NetworkTime.Now;
        }

        static Ped SetupPed(string name, Vector3 position, GTA.Math.Quaternion rotation, PedType type)
        {
            PedHash result;

            if (!Enum.TryParse(type.ToString(), true, out result))
                result = PedHash.Michael;

            var pedModel = new Model(result);

            if (!pedModel.IsLoaded)
                pedModel.Request(1000);

            while (!pedModel.IsLoaded)
            { }

            var spawnPos = position + new Vector3(0, 0, -1f);

            var ped = new Ped(Function.Call<int>(Hash.CREATE_PED, 26, pedModel.Hash, 
                spawnPos.X, spawnPos.Y, spawnPos.Z, 0f, false, true));

            pedModel.MarkAsNoLongerNeeded();

            ped.BlockPermanentEvents = true;

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

            Function.Call(Hash.SET_PED_CAN_RAGDOLL_FROM_PLAYER_IMPACT, ped.Handle, false);

            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 1);
            Function.Call(Hash._0x26695EC767728D84, ped.Handle, 16);

            Function.Call(Hash.SET_PED_MOVE_ANIMS_BLEND_OUT, ped.Handle);

            Function.Call(Hash.SET_PED_SUFFERS_CRITICAL_HITS, ped.Handle, false);

            var blip = ped.AddBlip();
            blip.Color = BlipColor.Yellow;

            name = name ?? "";

            blip.SetName(name);

            playerName = new UIResText(name,
                new System.Drawing.Point(),
                0.6f,
                System.Drawing.Color.White,
                Font.ChaletLondon,
                UIResText.Alignment.Centered);   
            
            var playerHealth = new UIRectangle(new System.Drawing.Point(),
                new System.Drawing.Size(100, 40),
                System.Drawing.Color.White);

            pedType = type;

            lastPosition = position;
            lastRotation = rotation;

            return ped;
        }

        public void HandleStateUpdate(AIState state, DateTime svTime, bool iAmHost)
        {
            updatingPosition = !iAmHost;

            for (int i = moveBuffer.Length - 1; i > 0; i--)
                moveBuffer[i] = moveBuffer[i - 1];

            moveBuffer[0] = new AISnapshot(state.Position.Deserialize(),
                state.Rotation.Deserialize(), svTime);

            snapshotCount = Math.Min(snapshotCount + 1, moveBuffer.Length);

            lastReceivedState = state;
            lastUpdateTime = NetworkTime.Now;
        }

        public override void Update()
        {
            if (LastState.Health <= 0) Health = -1;

            else if (LastState.Health < Health)
            {
                Function.Call(Hash.APPLY_DAMAGE_TO_PED,
                    Handle, Health - LastState.Health, true);
            }

            else if (LastState.Health > Health)
            {
                Health = LastState.Health;
            }

            if (LastState.Health <= 0)
            {
                if (deathCounter > 0 && Game.GameTime > deathCounter)
                {
                    CurrentBlip.Remove();
                }

                if (deathCounter == 0)
                {
                    deathCounter = Game.GameTime + 10000;
                }
              //  if (!IsDead) Health = -1;

                if (CurrentBlip.Sprite != BlipSprite.Dead)
                {
                    CurrentBlip.Sprite = BlipSprite.Dead;
                }

                return;
            }

            else
            {
                if (CurrentBlip.Sprite != BlipSprite.Standard)
                {
                    CurrentBlip.Sprite = BlipSprite.Standard;
                    CurrentBlip.Color = BlipColor.Yellow;
                }
            }

            if (updatingPosition)
            {
                var entityPosition = EntityExtrapolator.GetExtrapolatedPosition(
                    Position, Quaternion, moveBuffer, snapshotCount, 0.6f);

                if (entityPosition != null)
                {

                    if (Position.DistanceTo(entityPosition.Position) > 10f)
                    {
                        PositionNoOffset = moveBuffer[0].Position;
                        Quaternion = moveBuffer[0].Rotation;
                    }

                    else
                    {
                        PositionNoOffset = entityPosition.Position;
                        Quaternion = entityPosition.Rotation;
                    }
                }
            }

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
            playerName.Caption = name == null ? "" : name;
        }

        /// <summary>
        /// 
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

        /// <summary>
        /// Render the in- game player name.
        /// </summary>
        private void RenderPlayerName()
        {     
            var coords = Function.Call<Vector3>(Hash._GET_ENTITY_BONE_COORDS, Handle, 31086);
            var pos = coords + Position + new Vector3(0, 0, 1.50f);

            Function.Call(Hash.SET_DRAW_ORIGIN, pos.X, pos.Y, pos.Z, 0);

            playerName.Draw();

            Function.Call(Hash.CLEAR_DRAW_ORIGIN);
        }
    }
}
