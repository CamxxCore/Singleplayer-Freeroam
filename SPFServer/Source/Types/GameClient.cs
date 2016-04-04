using System;
using System.Collections.Generic;
using SPFServer.Natives;
using SPFLib.Types;
using Lidgren.Network;

namespace SPFServer.Types
{
    public class GameClient : IGameEntity
    {
        public TimeSpan Ping { get; internal set; }
        public ClientInfo Info { get; internal set; }
        public NetConnection Connection { get; }
        internal List<NativeCall> PendingNatives;
        internal List<TimeSpan> AvgPing;
        internal TimeSpan TimeDiff;
        internal DateTime LastUpd;
        internal DateTime LastSync;
        internal bool WaitForRespawn;
        internal uint LastSequence;   
        public short Health { get; set; }

        public Vector3 Position
        {
            get { return State.Position; }
            set { NativeFunctions.SetPosition(this, value); }
        }

        public Quaternion Rotation
        {
            get { return State.Rotation; }
            set { NativeFunctions.SetRotation(this, value); }
        }

        internal ClientState State { get; private set; }

        public Vector3 Angles { get { return State.Angles; } }

        public GameClient(NetConnection connection, ClientInfo info)
        {
            Connection = connection;
            Info = info;
            State = new ClientState(info.UID, info.Name);
            AvgPing = new List<TimeSpan>();
            PendingNatives = new List<NativeCall>();
            Health = 100;
        }

        internal void UpdateState(ClientState state, DateTime currentTime)
        {
            State = state;
            State.Name = Info.Name;
            State.ClientID = Info.UID;
            State.Health = Health;
            LastUpd = currentTime;
        }
    }
}
