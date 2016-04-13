using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using SPFClient.Entities;
using SPFLib.Types;

namespace SPFClient.Network
{
    public class VehicleManager : Script
    {
        private static List<NetworkVehicle> activeVehicles;
        private static Queue<KeyValuePair<VehicleState, DateTime>> updateQueue;
        private static Queue<Tuple<NetworkVehicle, bool>> deleteQueue;

        public static List<NetworkVehicle> ActiveVehicles {  get { return activeVehicles; } }

        public static bool Enabled { get; set; } = false;

        public VehicleManager()
        {
            Tick += OnTick;
            activeVehicles = new List<NetworkVehicle>();
            updateQueue = new Queue<KeyValuePair<VehicleState, DateTime>>();
            deleteQueue = new Queue<Tuple<NetworkVehicle, bool>>();
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                while (deleteQueue.Count > 0)
                {
                    var vehicle = deleteQueue.Dequeue();
                    if (vehicle.Item2)
                    {
                        vehicle.Item1.MarkAsNoLongerNeeded();
                        vehicle.Item1.Dispose();
                    }

                    activeVehicles.Remove(vehicle.Item1);
                }

                if (!Enabled) return;

                while (updateQueue.Count > 0)
                {
                    var vehicleUpdate = updateQueue.Dequeue();

                    NetworkVehicle vehicle;

                    // make sure the client position is valid
                    if (vehicleUpdate.Key == null ||
                        (vehicleUpdate.Key.Position != null &&
                        vehicleUpdate.Key.Position.X == 0 &&
                        vehicleUpdate.Key.Position.Y == 0 &&
                        vehicleUpdate.Key.Position.Z == 0))
                    {
                        continue;
                    }

                    // network player is in our vehicle, so use the reference in LocalPlayer instead
                    if (vehicleUpdate.Key.ID == ClientManager.LocalPlayer.Vehicle?.ID)
                    {
                        vehicle = ClientManager.LocalPlayer.Vehicle;
                    }

                    else
                    {
                        vehicle = VehicleFromID(vehicleUpdate.Key.ID);

                        if (vehicle == null)
                        {
                            vehicle = CreateNewVehicle(vehicleUpdate.Key);
                        }
                    }

                    /*if (vehicle is NetworkBicycle)
                    {
                        player.SetBicycleState((Types.BicycleTask)vehicle.LastState.ExtraFlags);
                    }*/

                    vehicle.HandleUpdate(vehicleUpdate.Key, vehicleUpdate.Value);
                }

                foreach (NetworkVehicle vehicle in activeVehicles)
                {
                    if (!vehicle.Exists())
                        Delete(vehicle, false);
                    if (ClientManager.LocalPlayer?.Vehicle?.ID == vehicle.ID)
                        continue;
                    vehicle.Update();
                }
            }

            catch (Exception ex)
            {
                var st = new System.Diagnostics.StackTrace(ex, true);
                var line = st.GetFrame(0).GetFileLineNumber();
                Logger.Log(ex.StackTrace.ToString() + " line: " + line);
                throw;
            }
        }

        /// <summary>
        /// Takes a Vehiclestate object and create its representation in the local world.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        internal static NetworkVehicle CreateNewVehicle(VehicleState state)
        {
            NetworkVehicle vehicle;

            var hash = (int)SPFLib.Helpers.VehicleIDToHash(state.ModelID);

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, hash))
                vehicle = new NetworkCar(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_HELI, hash))
                vehicle = new NetworkHeli(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, hash))
                vehicle = new NetworkPlane(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BOAT, hash))
                vehicle = new NetworkBoat(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, hash))
                vehicle = new NetworkBicycle(state);

            else vehicle = new NetworkVehicle(state);

            activeVehicles.Add(vehicle);

            return vehicle;
        }

        /// <summary>
        /// Get a NetworkVehicle from its respective ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static NetworkVehicle VehicleFromID(int id)
        {
            return activeVehicles.FirstOrDefault(x => x.ID == id);
        }

        /// <summary>
        /// Get a NetworkVehicle from its handle in the local game world.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static NetworkVehicle VehicleFromLocalHandle(int handle)
        {
            return activeVehicles.Find(x => x.Handle == handle);
        }

        /// <summary>
        /// Queue a client update from a client state object sent over the network.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="serverTime"></param>
        /// <param name="isLocal"></param>
        internal static void QueueUpdate(VehicleState state, DateTime time)
        {
            updateQueue.Enqueue(
                new KeyValuePair<VehicleState, DateTime>(state, time));
        }

        /// <summary>
        /// Queue a vehicle for deletion next game loop.
        /// </summary>
        /// <param name="vehicle"></param>
        internal static void Delete(NetworkVehicle vehicle, bool removeFromWorld = true)
        {
            deleteQueue.Enqueue(
                new Tuple<NetworkVehicle, bool>(vehicle, removeFromWorld));
        }

        /// <summary>
        /// Delete all vehicles.
        /// </summary>
        internal static void DeleteAll()
        {
            activeVehicles.ForEach(x => Delete(x));
        }
    }
}
