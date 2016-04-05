using System;
using System.Collections.Generic;
using System.Linq;
using SPFServer.Types;
using SPFLib.Types;
using SPFLib;
using Lidgren.Network;
using System.Net;
using SPFLib.Enums;

namespace SPFServer.Main
{
    internal class GameManager
    {
        private List<AIClient> activeAI;
        private List<GameClient> activeClients;

        public List<GameClient> ActiveClients { get { return activeClients; } }
        public List<AIClient> ActiveAI { get { return activeAI; } }

        public object SyncObj = new object();

        private GameClient activeAIHost;

        int snapshotCount = 0;
        SessionState[] historyBuffer = new SessionState[20];

        internal GameManager()
        {
            activeAI = new List<AIClient>();
            activeClients = new List<GameClient>();
        }

        internal void AddClient(NetConnection connection, int uid, string name)
        {
            var client = new GameClient(connection, new ClientInfo(uid, name));
            client.LastUpd = NetworkTime.Now;
            client.LastSync = NetworkTime.Now;
            activeClients.Add(client);
        }

        internal AIClient AddAI(string name, PedType type, Vector3 position, Quaternion rotation)
        {
            var ai = new AIClient(name, new AIState(SPFLib.Helpers.GenerateUniqueID(), name, 100, type, position, rotation));
            activeAI.Add(ai);
            return ai;
        }

        /// <summary>
        /// Return the best host based on latency.
        /// </summary>
        /// <returns></returns>
        internal GameClient GetActiveAIHost()
        {
            if (activeAIHost == null || !activeClients.Contains(activeAIHost))
                activeAIHost = activeClients.OrderBy(x => x.Ping).FirstOrDefault();
            return activeAIHost;
        }


        internal void HandleClientUpdate(NetConnection sender, uint sequence, ClientState state)
        {
            try
            {
                GameClient client;

                if (ClientFromEndpoint(sender.RemoteEndPoint, out client) &&
                    SPFLib.Helpers.ValidateSequence(sequence, client.LastSequence, uint.MaxValue))
                {
                    client.LastSequence = sequence;
                    client.UpdateState(state, NetworkTime.Now);
                }
            }

            catch (Exception e)
            {
                Server.WriteToConsole(string.Format("Update state failed.\n\nException:\n{0}", e.ToString()));
            }
        }

        internal void HandleClientUpdate(NetConnection sender, uint sequence, ClientState state, AIState[] ai)
        {
            try
            {
                GameClient client;

                if (ClientFromEndpoint(sender.RemoteEndPoint, out client) &&
                    SPFLib.Helpers.ValidateSequence(sequence, client.LastSequence, uint.MaxValue))
                {
                    client.LastSequence = sequence;
                    client.UpdateState(state, NetworkTime.Now);

                    foreach (var aiState in ai)
                    {
                        HandleAIUpdate(aiState);
                    }
                }
            }

            catch (Exception e)
            {
                Server.WriteToConsole(string.Format("Update state failed.\n\nException:\n{0}", e.ToString()));
            }
        }

        internal void HandleAIUpdate(AIState state)
        {
            AIClient aiClient;

            if ((aiClient = activeAI.Find(x => x.ID == state.ClientID)) != null)
            {
                aiClient.UpdateState(state, NetworkTime.Now);
            }
        }

        internal void SaveGameState(SessionState state)
        {
            for (int i = historyBuffer.Length - 1; i > 0; i--)
                historyBuffer[i] = historyBuffer[i - 1];

            historyBuffer[0] = state;

            snapshotCount = Math.Min(snapshotCount + 1, historyBuffer.Length);
        }

        /// <summary>
        /// Return an array of active client states based on the given timestamp.
        /// </summary>
        /// <returns></returns>
        internal SessionState FindBufferedState(DateTime timestamp)
        {
            for (int i = 0; i < snapshotCount; i++)
            {
                if (historyBuffer[i].Timestamp <= timestamp)
                    return historyBuffer[i];
            }
            return historyBuffer[0];
        }

        /// <summary>
        /// Return an array of active client states.
        /// </summary>
        /// <returns></returns>
        internal ClientState[] GetClientStates()
        {
            return activeClients.Select(x => x.State).ToArray();
        }

        /// <summary>
        /// Return an array of active vehicle states.
        /// </summary>
        /// <returns></returns>
        internal VehicleState[] GetVehicleStates()
        {
            return activeClients.Select(x => x.State.VehicleState).
                Where(y => y != null).ToArray();
        }

        /// <summary>
        /// Return an array of active client states.
        /// </summary>
        /// <returns></returns>
        internal AIState[] GetAIStates()
        {
            return activeAI.Select(x => x.State).ToArray();
        }

        /// <summary>
        /// Get a client by endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool ClientFromEndpoint(IPEndPoint endpoint, out GameClient client)
        {
            client = activeClients.Find(x => x.Connection.RemoteEndPoint.
            Equals(endpoint));
            return client != null;
        }

        /// <summary>
        /// Get a client by its user ID.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool ClientFromID(int clientID, out GameClient gameClient)
        {
            GameClient client;

            if ((client = activeClients.Find(x => x.Info.UID == clientID)) != null)
            {
                gameClient = client;
                return true;
            }

            else
            {
                gameClient = null;
                return false;
            }
        }

        /// <summary>
        /// Get a client by its user ID.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool AIFromID(int aiID, out AIClient aiClient)
        {
            AIClient ai;

            if ((ai = activeAI.Find(x => x.ID == aiID)) != null)
            {
                aiClient = ai;
                return true;
            }

            else
            {
                aiClient = null;
                return false;
            }
        }

        /// <summary>
        /// Remove a client by endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        internal void RemoveAI(AIClient client)
        {
            lock (SyncObj)
           activeAI.Remove(client);
        }

        /// <summary>
        /// Remove a client by endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        internal void RemoveClient(GameClient client)
        {
            lock (SyncObj)
            activeClients.Remove(client);
        }

        /// <summary>
        /// Remove a client by client ID.
        /// </summary>
        /// <param name="endpoint"></param>
        internal void RemoveClient(int clientID)
        {
            var client = activeClients.Find(x =>
             x.Info.UID == clientID);

            if (client?.Connection != null)
                lock (SyncObj)
                RemoveClient(client);
        }
    }
}
