using System;
using GTA;
using SPFClient.Network;
using SPFLib.Enums;
using GTA.Native;
using SPFLib.Types;

namespace SPFClient.Entities
{
    public sealed class LocalPlayer
    {
        public NetworkVehicle Vehicle { get; private set; }

        public readonly int ID = Helpers.GetUserID();

        public ClientFlags ClientFlags { get { return clientFlags; } }
        private ClientFlags clientFlags;

        bool isRespawning;

        private int localWeaponHash, localVehicleHash;
        private short localWeaponID, localVehicleID;
        private float localWeaponDamage = 0.0f;

        public Ped Ped
        {
            get { return Game.Player.Character; }
        }

        internal ulong EntityAddress
        {
            get
            {
                return MemoryAccess.GetEntityAddress(Ped.Handle);
            }
        }

        internal void Setup()
        {
            localWeaponDamage = GetCurrentWeaponDamage();

            Function.Call((Hash)0x2C2B3493FBF51C71, true);

            Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, Ped.Handle, true, true);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 342, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 122, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 134, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 115, 1);
        }

        DateTime lastWeaponDamage;

        private void UpdateWeaponDamage()
        {
            if (lastWeaponDamage != null &&
                DateTime.Now.Subtract(lastWeaponDamage) <
                TimeSpan.FromMilliseconds(10)) return;

            // get range of the current weapon
            var weaponRange = Function.Call<float>((Hash)0x814C9D19DFD69679, Ped.Handle);

            var result = World.GetCrosshairCoordinates();

            if (result.DitHitEntity)
            {
                var player = ClientManager.ActivePlayers.Find(x => x.Handle == result.HitEntity.Handle);

                if (player != null)
                {
                    var hitCoords = result.HitCoords.Serialize();
                    var dmg = GetCurrentWeaponDamage();

                    ClientSession.Current.SendImpactData(
                        new ImpactData(hitCoords, player.ID, dmg));

                    lastWeaponDamage = DateTime.Now;

                    return;
                }
            }
        }

        private ActiveTask GetActiveTask()
        {
            return (ActiveTask)MemoryAccess.ReadInt16(EntityAddress + Offsets.CLocalPlayer.Stance);
        }

        private float GetCurrentWeaponDamage()
        {
            if (Ped == null || Ped.Weapons.Current == null)
                return 0.0f;
            try
            {
                return MemoryAccess.GetPedWeaponDamage(Ped.Handle);
            }

            catch
            {
                return 0.0f;
            }
        }

        /// <summary>
        /// Avoid iterating inside xxHashtoID while running the game loop.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        private short GetWeaponID()
        {
            if (Ped != null && Ped.IsInVehicle()) return 0;

            if ((int)Ped.Weapons.Current.Hash != localWeaponHash)
            {
                localWeaponHash = (int)Ped.Weapons.Current.Hash;
                localWeaponID = Helpers.WeaponHashtoID((WeaponHash)localWeaponHash);
                localWeaponDamage = GetCurrentWeaponDamage();
            }
            return localWeaponID;
        }

        /// <summary>
        /// Avoid iterating inside xxHashtoID while running the game loop.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        private short GetVehicleID()
        {
            if (Ped.CurrentVehicle.Model.Hash != localVehicleHash)
            {
                localVehicleHash = Vehicle.Model.Hash;
                localVehicleID = SPFLib.Helpers.VehicleHashtoID(
                    (SPFLib.Enums.VehicleHash)localVehicleHash);
            }
            return localVehicleID;
        }

        public ClientState GetClientState()
        {
            var clientState = new ClientState();

            clientState.Health = Convert.ToInt16(Ped.Health);
            clientState.WeaponID = GetWeaponID();


            if (!Ped.IsInVehicle() && !Ped.IsGettingIntoAVehicle)
            {
                clientState.PedHash = (SPFLib.Enums.PedHash)Ped.Model.Hash;
                clientState.AimCoords = World.GetCrosshairCoordinates().HitCoords.Serialize() 
                    ?? new Vector3();
                clientState.Position = Ped.Position.Serialize();
                clientState.Velocity = Ped.Velocity.Serialize();
                clientState.Rotation = Ped.Quaternion.Serialize();
                clientState.MovementFlags = clientFlags;
                clientState.ActiveTask = GetActiveTask();
            }

            else if (Vehicle != null && Vehicle.Health > 0)
            {
                clientState.InVehicle = true;
                clientState.VehicleID = Vehicle.ID;
                clientState.VehicleSeat = (SPFLib.Enums.VehicleSeat)Ped.CurrentVehicleSeat();
            }

            clientFlags = 0;

            return clientState;
        }

        public VehicleState GetVehicleState()
        {
            if (Vehicle == null || Ped == null || !Ped.IsInVehicle() ||
                Ped.CurrentVehicleSeat() != GTA.VehicleSeat.Driver &&
                !Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, Vehicle.Handle, -1))
                return null;

            if (Vehicle is NetworkCar)
                return (Vehicle as NetworkCar).GetAutomobileState();

            else if (Vehicle is NetworkPlane)
                return (Vehicle as NetworkPlane).GetPlaneState();

            else if (Vehicle is NetworkHeli)
                return (Vehicle as NetworkHeli).GetHeliState();

            else if (Vehicle is NetworkBoat)
                return (Vehicle as NetworkBoat).GetBoatState();

            else if (Vehicle is NetworkBicycle)
                return (Vehicle as NetworkBicycle).GetState();

            else return Vehicle.GetState();
        }

        internal void SetCurrentVehicle(Vehicle vehicle)
        {
            if (Vehicle == null || vehicle.Handle != Vehicle.Handle)
            {
                //  check if the vehicle already exists
                Vehicle = VehicleManager.VehicleFromLocalHandle(vehicle.Handle);

                //   if not, create a NetworkVehicle instance from the players vehicle.
                //   this wont be visible unless the server has client vehicle spawning enabled.
                if (Vehicle == null)
                {
                    Vehicle = Helpers.GameVehicleToNetworkVehicle(vehicle);
                }
            }
        }

        internal void Update()
        {
            UpdateUserCommands();

            if (Function.Call<bool>((Hash)0x3317DEDB88C95038, Ped.Handle, 1))
            {
                Game.TimeScale = 1f;

                if (!isRespawning)
                {
                    if (Ped.IsInVehicle()) Function.Call(Hash.TASK_LEAVE_ANY_VEHICLE, Ped.Handle);
                    Function.Call(Hash._DISABLE_AUTOMATIC_RESPAWN, true);
                    Function.Call(Hash.IGNORE_NEXT_RESTART, true);
                    isRespawning = true;
                }
            }

            else
            {
                if (isRespawning)
                {
                    if (Function.Call<bool>(Hash.IS_SCREEN_FADING_IN))
                    {
                        Function.Call(Hash.DO_SCREEN_FADE_OUT, 1);

                        while (!Game.Player.CanControlCharacter)
                            Script.Wait(0);

                        World.DestroyAllCameras();

                        World.RenderingCamera = null;

                        var position = Helpers.GetRandomPositionNearEntity(Ped, Vehicle != null ? 0.625f : 1.8f); //Helpers.GetVehicleNodeForRespawn(Ped.Position);

                        var node = Helpers.GetVehicleNodeForRespawn(position);

                        Function.Call(Hash.NETWORK_RESURRECT_LOCAL_PLAYER, node.Item1.X, node.Item1.Y, node.Item1.Z, node.Item2, 0, 1, 1);

                        Function.Call((Hash)0x388A47C51ABDAC8E, Game.Player.Handle);

                        Function.Call(Hash._RESET_LOCALPLAYER_STATE);

                        Function.Call(Hash.DO_SCREEN_FADE_IN, 1000);

                        isRespawning = false;
                    }
                }
            }

            if (Ped.IsInVehicle())
            {
                SetCurrentVehicle(Ped.CurrentVehicle);
            }

            else if (Ped.IsGettingIntoAVehicle)
            {
                SetCurrentVehicle(new Vehicle(Function.Call<int>((Hash)0x814FA8BE5449445D, Ped.Handle)));
            }

            // Another player is driving our vehicle, so we update it based on vehiclestate updates sent by the player.
            if (Vehicle != null && Vehicle.Exists())
            {
                var driver = Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, Vehicle.Handle, -1);
                if (driver != 0 && driver != Ped.Handle)
                    Vehicle.Update();
            }

            if (Ped.IsShooting && Ped.Weapons.Current?.AmmoInClip > 0)
                UpdateWeaponDamage();

            if (Game.IsControlJustPressed(0, Control.VehicleExit))
            {
                var closestVehicle = World.GetClosestVehicle(Ped.Position, 10.0f); //Ped.GetClosestVehicle(2.223f);

                if (closestVehicle != null)
                {
                    NetworkVehicle veh = closestVehicle.Handle == Vehicle?.Handle ?
                        Vehicle : VehicleManager.VehicleFromLocalHandle(closestVehicle.Handle);

                    if (veh != null && !Ped.IsInVehicle(closestVehicle) && veh.Handle != Ped.CurrentVehicle?.Handle)
                    {
                        var doorNum = Ped.GetClosestVehicleDoorIndex(veh);

                        var numPassengers = Function.Call<int>(Hash.GET_VEHICLE_NUMBER_OF_PASSENGERS, veh.Handle);

                        if (doorNum < 0 && !closestVehicle.IsSeatFree((GTA.VehicleSeat)doorNum))
                        {
                            while (!closestVehicle.IsSeatFree((GTA.VehicleSeat)doorNum) && doorNum < numPassengers)
                            {
                                doorNum++;
                            }
                        }

                        Function.Call(Hash.CLEAR_PED_TASKS, Ped.Handle);

                        Function.Call(Hash.TASK_ENTER_VEHICLE, Ped.Handle, veh.Handle, -1, doorNum, 0.0f, 3, 0);
                    }
                }
            }
        }

        /// <summary>
        /// Set an input command for the local client
        /// </summary>
        /// <param name="command"></param>
        internal void SetClientFlag(ClientFlags command)
        {
            if ((clientFlags & command) == 0)
            {
                clientFlags |= command;
            }
        }

        /// <summary>
        /// Checks local client actions and sets the corrosponding flags to be sent to the server
        /// </summary>
        internal void UpdateUserCommands()
        {
            if (Ped.IsDead)
                SetClientFlag(ClientFlags.Dead);

            if (Ped.IsRagdoll && !Ped.IsDead)
                SetClientFlag(ClientFlags.Ragdoll);

            if (Function.Call<bool>(Hash.IS_PED_CLIMBING, Ped.Handle))
                SetClientFlag(ClientFlags.Climbing);

            if (Function.Call<bool>((Hash)0xF731332072F5156C, Ped.Handle, 0xFBAB5776))
                SetClientFlag(ClientFlags.HasParachute);

            if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Ped.Handle) == 2)
                SetClientFlag(ClientFlags.ParachuteOpen);

            if (Game.Player.IsAiming)
                SetClientFlag(ClientFlags.Aiming);

            if (Game.IsControlPressed(2, Control.Attack) && Ped.Weapons.Current.AmmoInClip > 0)
                SetClientFlag(ClientFlags.Shooting);

            if (Ped.IsReloading)
                SetClientFlag(ClientFlags.Reloading);

            if (Function.Call<bool>(Hash.IS_PED_JUMPING, Ped.Handle))
                SetClientFlag(ClientFlags.Jumping);

            else if (Function.Call<bool>(Hash.IS_PED_DIVING, Ped.Handle))
                SetClientFlag(ClientFlags.Diving);

            if (Ped.IsRunning)
                SetClientFlag(ClientFlags.Running);

            else if (Ped.IsSprinting)
                SetClientFlag(ClientFlags.Sprinting);

            else if (Ped.IsWalking)
                SetClientFlag(ClientFlags.Walking);

            else if (Ped.IsStopped)
                SetClientFlag(ClientFlags.Stopped);

            if (Ped.IsSwimming)
                SetClientFlag(ClientFlags.Swimming);

            if (Ped.IsInMeleeCombat)
            {
                UpdateWeaponDamage();
                SetClientFlag(ClientFlags.Punch);
            }
        }

    }
}
