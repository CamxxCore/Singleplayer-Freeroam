﻿using GTA;
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

        public NetworkVehicle ActiveVehicle
        {
            get; set;
        }

        private int currentPedHash;
        private static PedType pedType;
        private bool updatingPosition;
        private DateTime lastUpdateTime;
        private int snapshotCount;

        private static Vector3 lastPosition;
        private static GTA.Math.Quaternion lastRotation;

        private static UIResText playerName;

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

            var ped = new Ped(Function.Call<int>(Hash.CREATE_PED, 26, pedModel.Hash, spawnPos.X, spawnPos.Y, spawnPos.Z, 0f, false, true));

            pedModel.MarkAsNoLongerNeeded();

            ped.BlockPermanentEvents = true;

            Function.Call(Hash.SET_PED_CAN_RAGDOLL_FROM_PLAYER_IMPACT, ped.Handle, false);

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
                state.Rotation.Deserialize(),
                svTime);

            snapshotCount = Math.Min(snapshotCount + 1, moveBuffer.Length);

            UI.UIManager.UISubtitleProxy(state.Health.ToString());

            if (state.Health <= 0) Health = -1;

            else if (state.Health < Health)
            {
                Function.Call(Hash.APPLY_DAMAGE_TO_PED, Handle, Health - state.Health, true);
            }

            else if (state.Health > Health)
            {
                Health = state.Health;
            }


            lastReceivedState = state;
            lastUpdateTime = NetworkTime.Now;
        }

        public override void Update()
        {
            if (Health <= 0)
            {
                if (!IsDead) Health = -1;

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
                }
            }

            if (updatingPosition)
            {
                var entityPosition = EntityExtrapolator.GetExtrapolatedPosition(Position, Quaternion, moveBuffer, snapshotCount, 0.6f);

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
