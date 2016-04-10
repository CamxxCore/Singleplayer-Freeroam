#define debug
using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using SPFClient.Entities;
using SPFLib.Types;

namespace SPFClient.Network
{
    public class PlayerManager : Script
    {
        public static LocalPlayer LocalPlayer
        {
            get { return localPlayer; }
        }

        public static List<NetworkPlayer> ActivePlayers
        {
            get { return activePlayers; }
        }

        private static LocalPlayer localPlayer;
        private static List<NetworkPlayer> activePlayers;
        private static Queue<Tuple<ClientState, DateTime>> updateQueue;
        private static Queue<Tuple<NetworkPlayer, bool>> deleteQueue;
        private static Queue<ClientState> localUpdateQueue;

        public static bool Enabled { get; set; } = false;

        public PlayerManager()
        {
            Tick += OnTick;
            localPlayer = new LocalPlayer();
            activePlayers = new List<NetworkPlayer>();
            updateQueue = new Queue<Tuple<ClientState, DateTime>>();
            deleteQueue = new Queue<Tuple<NetworkPlayer, bool>>();
            localUpdateQueue = new Queue<ClientState>();
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                while (deleteQueue.Count > 0)
                {
                    var client = deleteQueue.Dequeue();
                    if (client.Item2)
                    {
                        client.Item1.MarkAsNoLongerNeeded();
                        client.Item1.Dispose();
                    }

                    activePlayers.Remove(client.Item1);
                }

                if (!Enabled) return;

                while (updateQueue.Count > 0)
                {
                    // dequeue the client that needs an update.
                    var clientUpdate = updateQueue.Dequeue();

                    var clientState = clientUpdate.Item1;

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

                    NetworkPlayer player = PlayerFromID(clientState.ClientID);

                    // the client doesn't exist
                    if (player == null)
                    {
                        player = new NetworkPlayer(clientState);
                        AddPlayer(player);
                    }

                    else
                    {
                        if (!player.Exists() || 
                            clientState.Health > 0 && 
                            player.Health <= 0)
                        {
                            Delete(player);
                            continue;
                        }

                        // vehicle exists. queue for update.
                        if (clientState.InVehicle)
                        {
                            NetworkVehicle vehicle = (clientState.VehicleID == localPlayer.Vehicle?.ID) ?
                                localPlayer.Vehicle : VehicleManager.VehicleFromID(clientState.VehicleID);

                            if (vehicle != null && !Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, player.Handle, vehicle.Handle, true))
                            {
                                if (Game.Player.Character.Position.DistanceTo(vehicle.Position) < 1000f)
                                {
                                    Function.Call(Hash.TASK_ENTER_VEHICLE, player.Handle, vehicle.Handle, -1,
                                        (int)clientState.VehicleSeat, 0.0f, player.LastState.InVehicle ? 16 : 3, 0);

                                    var dt = DateTime.Now + TimeSpan.FromMilliseconds(1900);

                                    while (DateTime.Now < dt)
                                        Yield();
                                }
                                else
                                {
                                    Function.Call(Hash.SET_PED_INTO_VEHICLE, player.Handle, vehicle.Handle,
                                        (int)clientState.VehicleSeat);
                                }
                            }
                        }

                        player.HandleStateUpdate(clientState, clientUpdate.Item2);
                    }
                }

                while (localUpdateQueue.Count > 0)
                {
                    var clientUpdate = localUpdateQueue.Dequeue();

                    if (clientUpdate.Health <= 0 && Game.Player.Character.Health > -1)
                        Game.Player.Character.Health = -1;

                    else if (clientUpdate.Health != Game.Player.Character.Health)
                    {
                        Game.Player.Character.Health = clientUpdate.Health;
                    }
                }

                localPlayer.Update();

#if debug
                var pWeapon = Function.Call<Entity>((Hash)0x3B390A939AF0B5FC, Game.Player.Character.Handle);

                if (pWeapon != null)
                {
                    var bone = Function.Call<int>(Hash._GET_ENTITY_BONE_INDEX, pWeapon, "Gun_Muzzle");

                    var bonePos = Function.Call<GTA.Math.Vector3>(Hash._GET_ENTITY_BONE_COORDS, pWeapon.Handle, bone, 0.0, 0.0, 0.0);

                    if (pWeapon != null && Game.Player.IsAiming)
                    {
                        var aimCoords = GameplayCamera.Position + Helpers.RotationToDirection(GameplayCamera.Rotation) * 1000f;

                        var cast = World.Raycast(pWeapon.Position, Helpers.RotationToDirection(pWeapon.Rotation) * 100, IntersectOptions.Everything);

                        Function.Call(Hash.DRAW_LINE, bonePos.X, bonePos.Y, bonePos.Z,
                             aimCoords.X, aimCoords.Y, aimCoords.Z, 255, 255, 0, 255);
                    }

                    foreach (NetworkPlayer client in activePlayers)
                    {
                        if (client.IsAlive && client.Position.DistanceTo(GameplayCamera.Position) < 100f)
                            Function.Call(Hash.DRAW_LINE, bonePos.X, bonePos.Y, bonePos.Z,
                               client.Position.X, client.Position.Y, client.Position.Z, 255, 0, 0, 255);
                    }
                }
#endif

                foreach (NetworkPlayer client in activePlayers)
                {
                    client.Update();
                }
            }

            catch (Exception ex)

            {// Get stack trace for the exception with source file information
                var st = new System.Diagnostics.StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                Logger.Log(ex.StackTrace.ToString() + " line: " + line);
                GTA.UI.Notify(ex.ToString() + " line: " + line);
            }
        }

        /// <summary>
        /// Add a NetworkPlayer object to the active list of entities.
        /// </summary>
        /// <param name="client"></param>
        internal static void AddPlayer(NetworkPlayer client)
        {
            if (activePlayers.Contains(client)) activePlayers.Remove(client);
            activePlayers.Add(client);
        }

        /// <summary>
        /// Get a NetworkPlayer from its ID.
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
        /// Queue a client for deletion next game loop.
        /// </summary>
        /// <param name="client"></param>
        internal static void Delete(NetworkPlayer client, bool removeFromWorld = true)
        {
            deleteQueue.Enqueue(
            new Tuple<NetworkPlayer, bool>(client, removeFromWorld));
        }

        /// <summary>
        /// Queue a client update from a client state object.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="serverTime"></param>
        /// <param name="isLocal"></param>
        internal static void QueueUpdate(ClientState state, DateTime serverTime, bool isLocal)
        {
            if (isLocal)
            {
                localUpdateQueue.Enqueue(state);
            }
            else
            {
                updateQueue.Enqueue(
                    new Tuple<ClientState, DateTime>(state, serverTime));
            }
        }

        /// <summary>
        /// Delete all active vehicles.
        /// </summary>
        internal static void DeleteAll()
        {
            activePlayers.ForEach(x => Delete(x));
        }
    }
}
