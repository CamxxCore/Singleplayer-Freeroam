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
    internal class ServerManager
    {
        public bool VehicleSpawning = true;
    
        SessionState[] historyBuffer = new SessionState[20];

        public List<GameClient> ActiveClients { get { return activeClients; } }

        public List<GameVehicle> ActiveVehicles { get { return activeVehicles; } }

        private List<GameClient> activeClients = new List<GameClient>();

        private List<GameVehicle> activeVehicles = new List<GameVehicle>();

        int snapshotCount = 0;

        public object SyncObj = new object();

        /// <summary>
        /// Adds a client to the session with the given ID and username.
        /// </summary>
        /// <param name="connection">Client connection.</param>
        /// <param name="uid">Client User ID.</param>
        /// <param name="name">Client Username.</param>
        /// <returns></returns>
        internal GameClient AddClient(NetConnection connection, int uid, string name)
        {
            var client = new GameClient(connection, new ClientInfo(uid, name));
            client.LastUpd = NetworkTime.Now;
            client.LastSync = NetworkTime.Now;
            activeClients.Add(client);
            return client;
        }

        /// <summary>
        /// Adds a vehicle to the session with the specified parameters.
        /// </summary>
        /// <param name="id">Unique ID.</param>
        /// <param name="hash">Vehicle Model Hash.</param>
        /// <param name="position">Spawn Position.</param>
        /// <param name="rotation">Spawn Rotation.</param>
        /// <param name="primaryColor">Initial Primary Color.</param>
        /// <param name="secondaryColor">Initial Secondary Color.</param>
        /// <returns></returns>
        internal GameVehicle AddVehicle(int id, VehicleHash hash, Vector3 position, Quaternion rotation, byte primaryColor = 0, byte secondaryColor = 0)
        {
            var vehicle = new GameVehicle(SPFLib.Helpers.VehicleStateFromArgs(id, hash, 
                position, rotation, primaryColor, secondaryColor));

            Console.WriteLine(vehicle.State.GetType().ToString());
            activeVehicles.Add(vehicle);
            return vehicle;
        }

        /// <summary>
        /// Handle a state update sent by a client.
        /// </summary>
        /// <param name="sender">The message sender.</param>
        /// <param name="sequence">The packet sequence number.</param>
        /// <param name="state">The ClientState object that was sent.</param>
        /// <param name="vehicle">The VehicleState object that was sent (if applicable)</param>
        internal void HandleClientUpdate(NetConnection sender, uint sequence, ClientState state, VehicleState vehicle)
        {
            try
            {
                GameClient client;

                if (ClientFromID(state.ClientID, out client))
                {
                    client.UpdateState(sequence, state, NetworkTime.Now);

                    if (vehicle != null)
                        HandleVehicleUpdate(vehicle);
                }
            }

            catch (Exception e)
            {
                Server.WriteToConsole(string.Format("Update state failed.\n\nException:\n{0}", e.ToString()));
            }
        }

        /// <summary>
        /// Handle a VehicleState update sent by a client.
        /// </summary>
        /// <param name="state"></param>
        internal void HandleVehicleUpdate(VehicleState state)
        {
            GameVehicle vehicle;

            // verify the vehicle actually exists.
            if ((vehicle = activeVehicles.Find(x => x.ID == state.ID)) != null)
            {
                vehicle.UpdateState(state, NetworkTime.Now);
            }

            else
            {
                // allow the client to spawn a vehicle if allowed.
                if (VehicleSpawning && state.Health > 0)
                {
                    AddVehicle(state.ID,
                        SPFLib.Helpers.VehicleIDToHash(state.ModelID),
                        state.Position,
                        state.Rotation,
                        state.PrimaryColor,
                        state.SecondaryColor);
                }         
            }
        }

        /// <summary>
        /// Save a SessionState object in the game state history buffer.
        /// </summary>
        /// <param name="state">State object.</param>
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
        /// Verify a bullet impact event sent by the client, outputing the client that was killed.
        /// </summary>
        /// <param name="killer">The client that triggered the event.</param>
        /// <param name="impactCoords">Impact coords, according to the client.</param>
        /// <param name="timestamp">Client timestamp from when the event was fired.</param>
        /// <param name="damage">Damage of the clients weapon.</param>
        /// <param name="targetID">User ID of the target, as reported by the client.</param>
        /// <param name="victim">Out: The victim of this bullet impact event.</param>
        /// <param name="wasKilled">Out: If the victim was killed (Health = -1)</param>
        /// <returns></returns>
        internal bool VerifyClientImpact(GameClient killer, Vector3 impactCoords, DateTime timestamp, 
            float damage, int targetID, out GameClient victim, out bool wasKilled)
        {
            var playbackTime = timestamp.Subtract(killer.TimeDiff);

            // get the game state from when this bullet was fired.
            var gameState = FindBufferedState(playbackTime);

            GameClient target;
            wasKilled = false;

            Console.WriteLine(gameState.Clients.Count());
            for (int i = 0; i < gameState.Clients.Length; i++)
            {
                var client = gameState.Clients[i];

                // make sure the target exists
                if (client.Position.DistanceTo(impactCoords) < 0.87f &&
                    ClientFromID(client.ClientID, out target) && target.State.Health > -1)
                {
                    // modify health
                    target.Health = (short)Helpers.Clamp((target.Health - damage), -1, 100);

                    victim = target;

                    if (target.Health < 0)
                    {
                        target.LastKiller = killer;
                        wasKilled = true;
                    }

                    return true;
                }
            }

            victim = null;
            return false;
        }

        /// <summary>
        /// Return an array of active client states.
        /// </summary>
        /// <returns></returns>
        internal ClientState[] GetClientStates(bool onlyRecent = false)
        {
            if (onlyRecent)
                return activeClients
                    .Where(y => NetworkTime.Now - y.LastUpd < TimeSpan.FromMilliseconds(1000))
                    .Select(x => x.State).ToArray();
            else return activeClients.Select(x => x.State).ToArray();
        }

        /// <summary>
        /// Return an array of active vehicle states.
        /// </summary>
        /// <returns></returns>
        internal VehicleState[] GetVehicleStates(bool onlyRecent = false)
        {
            if (onlyRecent)
                return activeVehicles
                    .Where(y => NetworkTime.Now - y.LastUpd < TimeSpan.FromMilliseconds(1000))
                    .Select(x => x.State).ToArray();
            else return activeVehicles.Select(x => x.State).ToArray();
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
        /// Get a vehicle by its ID.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool VehicleFromID(int id, out GameVehicle gameVehicle)
        {
            GameVehicle vehicle;

            if ((vehicle = activeVehicles.Find(x => x.ID == id)) != null)
            {
                gameVehicle = vehicle;
                return true;
            }

            else
            {
                gameVehicle = null;
                return false;
            }
        }

        /// <summary>
        /// Get a client by its ID.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool ClientFromID(int id, out GameClient gameClient)
        {
            GameClient client;

            if ((client = activeClients.Find(x => x.Info.UID == id)) != null)
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
        /// Remove a client.
        /// </summary>
        /// <param name="endpoint"></param>
        internal void RemoveClient(GameClient client)
        {
            lock (SyncObj)
            activeClients.Remove(client);
        }

        /// <summary>
        /// Remove a vehicle.
        /// </summary>
        /// <param name="endpoint"></param>
        internal void RemoveVehicle(GameVehicle vehicle)
        {
            lock (SyncObj)
            activeVehicles.Remove(vehicle);
        }


        /// <summary>
        /// Remove a client directly by client ID.
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
