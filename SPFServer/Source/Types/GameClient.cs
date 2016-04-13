using System;
using System.Collections.Generic;
using SPFServer.Main;
using SPFLib.Types;
using Lidgren.Network;

namespace SPFServer.Types
{
    public class GameClient : IGameEntity
    {

        public TimeSpan Ping { get; internal set; }
        public ClientInfo Info { get; internal set; }
        public NetConnection Connection { get; }
        internal ClientState State { get; private set; }
        internal List<NativeCall> PendingNatives;
        internal List<TimeSpan> AvgPing;
        internal TimeSpan TimeDiff;
        internal DateTime LastUpd;
        internal DateTime LastSync;
        internal DateTime IdleTimer;
        internal bool Respawning;
        internal bool WaitForKick;
        internal uint LastSequence;
        internal int ValidStates;
        internal bool DoNameSync;
        public short Health { get; set; }
        internal GameClient LastKiller { get; set; }

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

        public GameClient(NetConnection connection, ClientInfo info)
        {
            Connection = connection;
            Info = info;
            ValidStates = 0;
            AvgPing = new List<TimeSpan>();
            PendingNatives = new List<NativeCall>();
            State = new ClientState();
            Health = 100;
        }

        internal void UpdateState(uint sequence, ClientState state, DateTime currentTime)
        {
            if ((sequence > LastSequence &&
             sequence - LastSequence <= uint.MaxValue / 2 ||
             LastSequence > sequence &&
             LastSequence - sequence > uint.MaxValue / 2) ||
             LastSequence == 0)
            {
                State = state;
                State.Health = Health;
                LastSequence = sequence;
            }

            LastUpd = currentTime;

        }
    }
}
