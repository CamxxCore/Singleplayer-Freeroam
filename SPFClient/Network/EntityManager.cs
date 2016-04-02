using System;
using System.Collections.Generic;
using System.Linq;
using SPFClient.Entities;
using SPFLib.Types;
using SPFLib.Enums;
using GTA;
using GTA.Native;

namespace SPFClient.Network
{
    public class EntityManager : Script
    {
        private static LocalPlayer localPlayer;
        public static LocalPlayer LocalPlayer { get { return localPlayer; } }

        public static List<NetworkPlayer> ActivePlayers { get { return activePlayers; } }

        public static List<NetworkAI> ActiveAI { get { return activeAI; } }

        public static bool HostingAI { get; set; }

        private static Queue<KeyValuePair<ClientState, DateTime>> updateQueue =
            new Queue<KeyValuePair<ClientState, DateTime>>();

        private static Queue<KeyValuePair<AIState, DateTime>> aiUpdateQueue =
         new Queue<KeyValuePair<AIState, DateTime>>();

        private static Queue<ClientState> localClientQueue =
            new Queue<ClientState>();

        private static List<NetworkPlayer> activePlayers =
            new List<NetworkPlayer>();

        private static List<NetworkVehicle> activeVehicles =
            new List<NetworkVehicle>();

        private static List<NetworkAI> activeAI =
            new List<NetworkAI>();

        private static List<Ped> localAI =
            new List<Ped>();

        private static Queue<KeyValuePair<NetworkPlayer, bool>> clientDeletionQueue =
            new Queue<KeyValuePair<NetworkPlayer, bool>>();

        private static Queue<KeyValuePair<NetworkVehicle, bool>> vehicleDeletionQueue =
            new Queue<KeyValuePair<NetworkVehicle, bool>>();

        private static Queue<KeyValuePair<NetworkAI, bool>> aiDeletionQueue =
         new Queue<KeyValuePair<NetworkAI, bool>>();

        private bool firstSync = true;

        public static event EventHandler WorldSynced;

        public static bool Enabled { get; set; }

        public EntityManager()
        {
            localPlayer = new LocalPlayer();
            Tick += OnTick;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!Enabled) return;

            try
            {
                HandleEntityQueues();

                if (localPlayer != null)
                {
                    localPlayer.Update();
                }

                foreach (NetworkPlayer client in activePlayers)
                {
                    client.Update();
                }

                foreach (NetworkAI ai in activeAI)
                {
                    ai.Update();
                }

                foreach (NetworkVehicle vehicle in activeVehicles)
                {
                    if (!vehicle.Exists() || !vehicle.IsAlive)
                        DeleteVehicle(vehicle, false);
                    vehicle.Update();
                }

                if (firstSync)
                {
                    WorldSynced?.Invoke(this, new EventArgs());
                    firstSync = false;
                }
            }

            catch (Exception ex)
            {
                GTA.UI.ShowSubtitle("Error: " + ex.ToString());
            }
        }

        private static void HandleEntityQueues()
        {
            while (updateQueue.Count > 0)
            {
                // dequeue the client that needs an update.
                var remoteClient = updateQueue.Dequeue();

                var clientState = remoteClient.Key;

                // make sure the client position is valid
                if (clientState == null ||
                    (!clientState.InVehicle &&
                    clientState.Position != null &&
                    clientState.Position.X == 0 &&
                    clientState.Position.Y == 0 &&
                    clientState.Position.Z == 0))
                {
                    continue;
                }

                NetworkPlayer client = PlayerFromID(clientState.ClientID);

                // the client doesn't exist
                if (client == null)
                {
                    client = new NetworkPlayer(clientState);
                    AddClient(client);
                }

                else
                {
                    if (!client.Exists() || clientState.Health > 0 && client.Health <= 0)
                    {
                        DeleteClient(client);
                        continue;
                    }

                    // vehicle exists. queue for update.
                    if (clientState.InVehicle && clientState.VehicleState != null)
                    {
                        var vehicleState = clientState.VehicleState;

                        NetworkVehicle vehicle;

                        // network player is in our vehicle, so use the reference in LocalPlayer instead
                        if (vehicleState.ID == localPlayer.Vehicle?.ID)
                        {
                            vehicle = localPlayer.Vehicle;
                        }

                        else
                        {
                            vehicle = VehicleFromID(vehicleState.ID);

                            if (vehicle == null)
                            {
                                vehicle = CreateAndAddVehicle(vehicleState);
                                client.ActiveVehicle = vehicle;
                            }
                        }

                        if (client.IsAlive && !Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, client.Handle, vehicle.Handle, true))
                        {
                            GTA.UI.ShowSubtitle("ENTER");

                            Function.Call(Hash.TASK_ENTER_VEHICLE, client.Handle, vehicle.Handle, -1, (int)clientState.VehicleSeat, 0.0f, client.LastState.InVehicle ? 16 : 3, 0);

                            var dt = DateTime.Now + TimeSpan.FromMilliseconds(1800);

                            while (DateTime.Now < dt)
                                Yield();
                        }

                        if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, vehicle.Model.Hash))
                        {
                            client.SetBicycleState((Types.BicycleState)vehicleState.ExtraFlags);
                        }

                        // if it isnt the driver, we don't need to handle the update.
                        if (clientState.VehicleSeat == SPFLib.Enums.VehicleSeat.Driver)
                            vehicle.HandleStateUpdate(vehicleState, remoteClient.Value);
                    }

                    client.HandleStateUpdate(clientState, remoteClient.Value);
                }
            }

            while (aiUpdateQueue.Count > 0)
            {
                var remoteAI = aiUpdateQueue.Dequeue();

                var aiState = remoteAI.Key;

                // make sure the client position is valid
                if (aiState == null ||
                    (aiState.Position != null &&
                    aiState.Position.X == 0 &&
                    aiState.Position.Y == 0 &&
                    aiState.Position.Z == 0))
                {
                    continue;
                }

                NetworkAI client = AIFromID(aiState.ClientID);

                // the client doesn't exist
                if (client == null)
                {
                    client = CreateAndAddAI(aiState);
                }

                else
                {
                    if (aiState.PedType != PedType.None && client.GetPedType() != aiState.PedType || !client.Exists())
                    {
                        DeleteAI(client);
                        continue;
                    }

                    client.HandleStateUpdate(aiState, remoteAI.Value, HostingAI);
                }
            }

            while (localClientQueue.Count > 0)
            {
                var clientUpdate = localClientQueue.Dequeue();

                if (clientUpdate.Health <= 0 && Game.Player.Character.Health > -1)
                    Game.Player.Character.Health = -1;

                else if (clientUpdate.Health != Game.Player.Character.Health)
                {
                    Game.Player.Character.Health = clientUpdate.Health;
                }
            }

            while (clientDeletionQueue.Count > 0)
            {
                var client = clientDeletionQueue.Dequeue();
                if (client.Value)
                {
                    client.Key.MarkAsNoLongerNeeded();
                    client.Key.Dispose();
                }

                activePlayers.Remove(client.Key);
            }

            while (vehicleDeletionQueue.Count > 0)
            {
                var vehicle = vehicleDeletionQueue.Dequeue();
                if (vehicle.Value)
                {
                    vehicle.Key.MarkAsNoLongerNeeded();
                    vehicle.Key.Dispose();
                }

                activeVehicles.Remove(vehicle.Key);
            }

            while (aiDeletionQueue.Count > 0)
            {
                var ai = aiDeletionQueue.Dequeue();
                if (ai.Value)
                {
                    ai.Key.MarkAsNoLongerNeeded();
                    ai.Key.Dispose();
                }

                activeAI.Remove(ai.Key);
            }
        }

        /// <summary>
        /// Add an already created NetworkPlayer object to the active list of entities.
        /// </summary>
        /// <param name="client"></param>
        internal static void AddClient(NetworkPlayer client)
        {
            if (activePlayers.Contains(client)) activePlayers.Remove(client);
            activePlayers.Add(client);
        }

        /// <summary>
        /// Queue a client update from a client state object sent over the network.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="serverTime"></param>
        /// <param name="isLocal"></param>
        internal static void QueueClientUpdate(ClientState state, DateTime serverTime, bool isLocal)
        {
            if (isLocal)
            {
                localClientQueue.Enqueue(state);
            }
            else
            {
                updateQueue.Enqueue(new KeyValuePair<ClientState, DateTime>(state, serverTime));
            }
        }

        /// <summary>
        /// Queue an AI update from an AI state object sent over the network.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="serverTime"></param>
        /// <param name="isLocal"></param>
        internal static void QueueAIUpdate(AIState state, DateTime serverTime)
        {
            aiUpdateQueue.Enqueue(new KeyValuePair<AIState, DateTime>(state, serverTime));
        }

        internal static IEnumerable<AIState> GetAIForUpdate()
        {
            foreach (var ai in activeAI)
            {
                if (ai.Position != ai.LastState.Position.Deserialize())
                {
                    yield return new AIState(ai.ID, ai.Name, (short) ai.Health, ai.GetPedType(), ai.Position.Serialize(), ai.Quaternion.Serialize());
                }
            }
        }

        /// <summary>
        /// Get a NetworkPlayer from its respective ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static NetworkPlayer PlayerFromID(int id)
        {
            return activePlayers.Find(x => x.ID == id);
        }

        /// <summary>
        /// Get a NetworkPlayer from its handle in the local game world.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        internal static NetworkPlayer PlayerFromLocalHandle(int handle)
        {
            return activePlayers.Find(x => x.Handle == handle);
        }

        /// <summary>
        /// Get a NetworkVehicle from its respective ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static NetworkVehicle VehicleFromID(int id)
        {
            return activeVehicles.FirstOrDefault(x => x.ID == id);
        }

        /// <summary>
        /// Get a NetworkVehicle from its handle in the local game world.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        internal static NetworkVehicle VehicleFromLocalHandle(int handle)
        {
            return activeVehicles.FirstOrDefault(x => x.Handle == handle);
        }

        /// <summary>
        /// Get a NetworkVehicle from its respective ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static NetworkAI AIFromID(int id)
        {
            return activeAI.FirstOrDefault(x => x.ID == id);
        }

        /// <summary>
        /// Get a NetworkVehicle from its handle in the local game world.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        internal static NetworkAI AIFromLocalHandle(int handle)
        {
            return activeAI.FirstOrDefault(x => x.Handle == handle);
        }

        /// <summary>
        /// Takes a Vehiclestate object and create its representation in the local world.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        internal static NetworkVehicle CreateAndAddVehicle(VehicleState state)
        {
            NetworkVehicle vehicle;

            var hash = (int)Helpers.VehicleIDToHash(state.VehicleID);

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, hash))
                vehicle = new NetworkCar(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_HELI, hash))
                vehicle = new NetworkHeli(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, hash))
                vehicle = new NetworkPlane(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, hash))
                vehicle = new NetworkBicycle(state);

            else vehicle = new NetworkVehicle(state);

            activeVehicles.Add(vehicle);

            return vehicle;
        }

        /// <summary>
        /// Takes an AIstate object and create its representation in the local world.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        internal static NetworkAI CreateAndAddAI(AIState state)
        {
            NetworkAI ai = new NetworkAI(state);

            activeAI.Add(ai);

            return ai;
        }

        /// <summary>
        /// Queue a client for deletion next game loop.
        /// </summary>
        /// <param name="client"></param>
        internal static void DeleteClient(NetworkPlayer client, bool removeFromWorld = true)
        {
            clientDeletionQueue.Enqueue(
            new KeyValuePair<NetworkPlayer, bool>(client, removeFromWorld));
        }

        /// <summary>
        /// Queue a vehicle for deletion next game loop.
        /// </summary>
        /// <param name="vehicle"></param>
        internal static void DeleteVehicle(NetworkVehicle vehicle, bool removeFromWorld = true)
        {
            vehicleDeletionQueue.Enqueue(
                new KeyValuePair<NetworkVehicle, bool>(vehicle, removeFromWorld));
        }

        /// <summary>
        /// Queue AI for deletion next game loop.
        /// </summary>
        /// <param name="vehicle"></param>
        internal static void DeleteAI(NetworkAI ai, bool removeFromWorld = true)
        {
            aiDeletionQueue.Enqueue(
                new KeyValuePair<NetworkAI, bool>(ai, removeFromWorld));
        }

        /// <summary>
        /// Delete all spawned remote entities from the world.
        /// </summary>
        /// <param name="removeFromWorld"></param>
        internal static void DeleteAllEntities(bool removeFromWorld = true)
        {
            activePlayers.ForEach(x => DeleteClient(x, removeFromWorld));
            activeVehicles.ForEach(x => DeleteVehicle(x, removeFromWorld));
            activeAI.ForEach(x => DeleteAI(x, removeFromWorld));
            activePlayers.Clear();
            activeVehicles.Clear();
            activeAI.Clear();
        }
    }
}
