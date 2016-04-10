using System;
using System.Linq;
using GTA;
using SPFClient.Network;
using SPFLib.Enums;
using GTA.Native;
using SPFClient.Types;
using System.Collections.Generic;
using SPFLib.Types;

namespace SPFClient.Entities
{
    public sealed class LocalPlayer
    {
        public NetworkVehicle Vehicle { get; private set; }

        public readonly int ID = Helpers.GetUserID();

        public ClientFlags ClientFlags { get { return clientFlags; } }

        private ClientFlags clientFlags;

        private int localPedHash, localWeaponHash, localVehicleHash;
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

            for (int i = 0; i <= 5; i++)
            {
                Function.Call(Hash.DISABLE_HOSPITAL_RESTART, i, false);
            }

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
                SPFLib.NetworkTime.Now.Subtract(lastWeaponDamage) <
                TimeSpan.FromMilliseconds(100)) return;

            // get range of the current weapon
            var weaponRange = Function.Call<float>((Hash)0x814C9D19DFD69679, Ped.Handle);

            var destCoord = GameplayCamera.Position + Helpers.RotationToDirection(GameplayCamera.Rotation) * weaponRange;

            var result = World.Raycast(GameplayCamera.Position, destCoord, IntersectOptions.Everything);

            if (result.DitHitEntity)
            {
                var player = PlayerManager.ActivePlayers.Find(x => x.Handle == result.HitEntity.Handle);

                if (player != null)
                {
                    var hitCoords = result.HitEntity.Position.Serialize();
                    var dmg = GetCurrentWeaponDamage();

                    NetworkManager.Current.SendImpactData(
                        new ImpactData(hitCoords, player.ID, dmg));

                    lastWeaponDamage = SPFLib.NetworkTime.Now;

                    return;
                }

                /* var ai = EntityManager.ActiveAI.Find(x => x.Handle == result.HitEntity.Handle);

                 if (ai != null)
                 {
                     var hitCoords = result.HitEntity.Position.Serialize();
                     var dmg = GetCurrentWeaponDamage();

                     NetworkSession.Current.SendWeaponData(
                      new WeaponData(hitCoords, ai.ID, dmg));

                     lastWeaponDamage = SPFLib.NetworkTime.Now;        

                     return;
                 }*/
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
            clientState.PedHash = (SPFLib.Enums.PedHash)Ped.Model.Hash;

            if (!Ped.IsInVehicle() && !Ped.IsGettingIntoAVehicle)
            {
                clientState.Position = Ped.Position.Serialize();
                clientState.Velocity = Ped.Velocity.Serialize();
                clientState.Angles = GameplayCamera.Rotation.Serialize();
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

            ResetClientFlags();

            return clientState;
        }

        public VehicleState GetVehicleState()
        {
            if (Vehicle == null ||
                !Ped.IsInVehicle() ||
                Ped.CurrentVehicleSeat() != GTA.VehicleSeat.Driver &&
                !Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, Vehicle.Handle, -1))
                return null;

            if (Vehicle is NetworkCar)
                return (Vehicle as NetworkCar).GetAutomobileState();

            else if (Vehicle is NetworkPlane)
                return (Vehicle as NetworkPlane).GetPlaneState();

            else if (Vehicle is NetworkHeli)
                return (Vehicle as NetworkHeli).GetHeliState();

            else if (Vehicle is NetworkBicycle)
                return (Vehicle as NetworkBicycle).GetState();

            else return Vehicle.GetState();
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

        /// <summary>
        /// Check the status of an input command for the local client
        /// </summary>
        /// <param name="command"></param>
        internal bool IsClientFlagSet(ClientFlags command)
        {
            return !((clientFlags & command) == 0);
        }

        /// <summary>
        /// Reset all local user command flags
        /// </summary>
        internal void ResetClientFlags()
        {
            clientFlags = 0;
        }

        private int GetClosestVehicleDoorIndex(NetworkVehicle vehicle)
        {
            if (vehicle is NetworkCar)
            {
                var bones = new int[]
                {
                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , vehicle.Handle, "handle_dside_f"), //-1 front left
                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , vehicle.Handle, "handle_pside_f"), //0 front right
                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , vehicle.Handle, "handle_dside_r"), //1 back left                     
                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , vehicle.Handle, "handle_pside_r") //2 back right                     
                };

                var closestBone = bones.OrderBy(x =>
                Function.Call<GTA.Math.Vector3>((Hash)0x44A8FCB8ED227738, vehicle.Handle, x)
                .DistanceTo(Ped.Position)).First();

                return (Array.IndexOf(bones, closestBone) - 1);
            }

            else
            {
                var bones = new int[]
                    {
                        Function.Call<int>((Hash)0xFB71170B7E76ACBA, vehicle.Handle, "door_dside_f"), //-1 front left\
                        Function.Call<int>((Hash)0xFB71170B7E76ACBA, vehicle.Handle, "door_pside_f"), //0 front right
                        Function.Call<int>((Hash)0xFB71170B7E76ACBA, vehicle.Handle, "door_dside_r"), //1 back left                     
                        Function.Call<int>((Hash)0xFB71170B7E76ACBA, vehicle.Handle, "door_pside_r") //2 back right                     
                    };

                var closestBone = bones.OrderBy(x =>
                Function.Call<GTA.Math.Vector3>((Hash)0x44A8FCB8ED227738, vehicle.Handle, x)
                .DistanceTo(Ped.Position)).First();

                return (Array.IndexOf(bones, closestBone) - 1);
            }
        }

        /// <summary>
        /// Checks local client actions and sets the corrosponding flags to be sent to the server
        /// </summary>
        internal void UpdateUserCommands()
        {
            if (Ped.IsDead)
            {
                SetClientFlag(ClientFlags.Dead);
            }

            if (Ped.IsInMeleeCombat)
            {
                UpdateWeaponDamage();
                SetClientFlag(ClientFlags.Punch);
            }

            if (Ped.IsRagdoll && !Ped.IsDead)
            {
                SetClientFlag(ClientFlags.Ragdoll);
            }

            if (Function.Call<bool>(Hash.IS_PED_CLIMBING, Ped.Handle))
            {
                SetClientFlag(ClientFlags.Climbing);
            }

            if (Function.Call<bool>((Hash)0xF731332072F5156C, Ped.Handle, 0xFBAB5776))
            {
                SetClientFlag(ClientFlags.HasParachute);
            }

            if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Ped.Handle) == 2)
            {
                SetClientFlag(ClientFlags.ParachuteOpen);
            }

            if (Game.Player.IsAiming)
            {
                SetClientFlag(ClientFlags.Aiming);
            }

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

            if (Ped.IsInMeleeCombat)
                SetClientFlag(ClientFlags.Punch);
        }

        bool isDead;

        internal bool CheckVehicleDamage(out NetworkVehicle vehicle)
        {
            foreach (var v in VehicleManager.ActiveVehicles)
            {
                if (Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, Ped.Handle, v.Handle))
                {
                    vehicle = v;
                    return true;
                }
            }

            vehicle = null;
            return false;
        }

        internal void Update()
        {

            if (Ped.IsShooting && Ped.Weapons.Current.AmmoInClip > 0)
                UpdateWeaponDamage();

            if (Ped.IsDead)
            {
                //    if (!isDead)
                //  {
                Game.TimeScale = 1f;


                Function.Call(Hash.SET_NO_LOADING_SCREEN);
                Function.Call(Hash.CLEAR_TIMECYCLE_MODIFIER);
                Function.Call(Hash.IGNORE_NEXT_RESTART, 1);
                Function.Call(Hash._DISABLE_AUTOMATIC_RESPAWN, 0);
                if (!isDead)
                {
                    NetworkVehicle vehicle;


                    if (CheckVehicleDamage(out vehicle))
                    {


                        GTA.UI.ShowSubtitle(vehicle.Handle.ToString(), 20000);
                    }

                    Function.Call(Hash.SET_NO_LOADING_SCREEN);
                    Function.Call(Hash.CLEAR_TIMECYCLE_MODIFIER);
                    Function.Call(Hash.IGNORE_NEXT_RESTART, 1);
                    Function.Call(Hash._DISABLE_AUTOMATIC_RESPAWN, 0);
                    isDead = true;
                }

                //   Function.Call(Hash.CLEAR_TIMECYCLE_MODIFIER);
                //  Function.Call(Hash.IGNORE_NEXT_RESTART, 1);

                //   }
            }

            else
            {
                if (isDead)
                {
                    if (Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE))
                    {
                        int i = 0;

                        while (i == 1)
                        {
                            var state = Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE);
                            if (Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING))
                            {
                                Function.Call(Hash.STOP_CUTSCENE, 0);
                            }
                            if (Function.Call<bool>(Hash.HAS_CUTSCENE_LOADED))
                            {
                                Function.Call(Hash.REMOVE_CUTSCENE);
                            }
                            if (Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) &&
                                !Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING))
                            {
                                i = 0;
                            }
                            Script.Wait(0);
                        }
                    }

                    isDead = false;
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

            //  else Vehicle = null;

            // Another player is driving our vehicle, so we update it based on vehiclestate updates sent by the player.
            if (Vehicle != null && Vehicle.Exists())
            {
                var driver = Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, Vehicle.Handle, -1);
                if (driver != 0 && driver != Ped.Handle)
                    Vehicle.Update();
            }

            if (Game.IsControlJustPressed(0, Control.VehicleExit))
            {
                var closestVehicle = World.GetClosestVehicle(Ped.Position, 6.223f); //Ped.GetClosestVehicle(2.223f);

                if (closestVehicle != null)
                {
                    NetworkVehicle veh = closestVehicle.Handle == Vehicle?.Handle ?
                        Vehicle : VehicleManager.VehicleFromLocalHandle(closestVehicle.Handle);

                    if (veh != null && !Ped.IsInVehicle(closestVehicle) && veh.Handle != Ped.CurrentVehicle?.Handle)
                    {
                        var doorNum = GetClosestVehicleDoorIndex(veh);

                        if (doorNum == -1 && !closestVehicle.IsSeatFree((GTA.VehicleSeat)doorNum))
                        {
                            while (!closestVehicle.IsSeatFree((GTA.VehicleSeat)doorNum) && doorNum < 3)
                            {
                                doorNum++;
                            }

                            if (doorNum >= 3) return;
                        }

                        Ped.Task.ClearAll();

                        Function.Call(Hash.TASK_ENTER_VEHICLE, Ped.Handle, veh.Handle, -1, doorNum, 0.0f, 3, 0);
                    }
                }
            }

            UpdateUserCommands();
        }
    }
}
