using System;
using System.Linq;
using GTA;
using SPFClient.Network;
using SPFLib.Enums;
using GTA.Native;
using SPFClient.Types;
using SPFLib.Types;

namespace SPFClient.Entities
{
    public sealed class LocalPlayer
    {
        public NetworkVehicle Vehicle { get; private set; }

        public readonly int ID = Helpers.GetUserID();

        public ClientFlags ClientFlags { get { return clientFlags; } }

        private ClientFlags clientFlags;

        private PedType pedType;

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

            Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, false);

            Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, Ped.Handle, true, true);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 342, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 122, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 134, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 115, 1);
        }

        private void UpdateWeaponDamage()
        {
            // get range of the current weapon
            var weaponRange = Function.Call<float>((Hash)0x814C9D19DFD69679, Ped.Handle);

            var result = World.Raycast(GameplayCamera.Position,
                GameplayCamera.Position + Helpers.RotationToDirection(GameplayCamera.Rotation) * weaponRange,
                IntersectOptions.Everything);

            if (result.DitHitEntity/* && NetworkManager.ActivePlayers.Any(x => x.Handle == result.HitEntity.Handle)*/)
            {
                var hitCoords = result.HitEntity.Position.Serialize();
                var dmg = (short)GetCurrentWeaponDamage();
                NetworkSession.Current.SendWeaponData(dmg, hitCoords);
            }
        }

        private ActiveTask GetActiveTask()
        {
            return (ActiveTask)MemoryAccess.ReadInt16(EntityAddress + Offsets.CLocalPlayer.Stance);
        }

        private float GetCurrentWeaponDamage()
        {
            if (Ped == null) return 0.0f;
            return MemoryAccess.GetPedWeaponDamage(Ped.Handle);
        }

        /// <summary>
        /// Avoid iterating inside xxHashtoID while running the game loop.
        /// </summary>
        /// <returns></returns>
        private PedType GetPedType()
        {
            if (Ped.Model.Hash != localPedHash)
            {
                localPedHash = Ped.Model.Hash;
                Enum.TryParse(((PedHash)localPedHash).ToString(), out pedType);
            }

            return pedType;
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
                localVehicleHash = Ped.CurrentVehicle.Model.Hash;
                localVehicleID = Helpers.VehicleHashtoID((VehicleHash)localVehicleHash);
            }
            return localVehicleID;
        }

        /// <summary>
        /// Get the vehicle radio station.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        private byte GetActiveRadioStation()
        {
            return Convert.ToByte(Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX));
        }

        public ClientState GetClientState()
        {
            var clientState = new ClientState();

            clientState.Health = Convert.ToInt16(Ped.Health);
            clientState.WeaponID = GetWeaponID();
            clientState.PedType = GetPedType();

            if (!Ped.IsInVehicle())
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
                if ((int)Ped.CurrentVehicleSeat() == -1)
                {
                    var v = new Vehicle(Vehicle.Handle);

                    clientState.InVehicle = true;

                    clientState.VehicleSeat = (SPFLib.Enums.VehicleSeat)Ped.CurrentVehicleSeat();

                    clientState.VehicleState = new VehicleState(Vehicle.ID,
                        Vehicle.Position.Serialize(),
                        Vehicle.Velocity.Serialize(),
                        Vehicle.Quaternion.Serialize(),
                        v.CurrentRPM,
                        Vehicle.GetWheelRotation(),
                        0, Convert.ToInt16(v.Health),
                        (byte)v.PrimaryColor, (byte)v.SecondaryColor,
                        GetActiveRadioStation(),
                        GetVehicleID());

                    clientState.VehicleState.Flags |= VehicleFlags.Driver;

                    if (Function.Call<bool>(Hash.IS_HORN_ACTIVE, v.Handle))
                        clientState.VehicleState.Flags |= VehicleFlags.HornPressed;

                    if (v.Health <= 0)
                        clientState.VehicleState.Flags |= VehicleFlags.Exploded;

                    if (Vehicle is NetworkCar)
                    {
                        if (v.LeftHeadLightBroken)
                            clientState.VehicleState.ExtraFlags |= (ushort)DamageFlags.LHeadlight;

                        if (v.RightHeadLightBroken)
                            clientState.VehicleState.ExtraFlags |= (ushort)DamageFlags.RHeadlight;

                        if (v.IsDoorBroken(VehicleDoor.FrontLeftDoor))
                            clientState.VehicleState.ExtraFlags |= (ushort)DamageFlags.LDoor;

                        if (v.IsDoorBroken(VehicleDoor.FrontRightDoor))
                            clientState.VehicleState.ExtraFlags |= (ushort)DamageFlags.RDoor;

                        if (v.IsDoorBroken(VehicleDoor.BackLeftDoor))
                            clientState.VehicleState.ExtraFlags |= (ushort)DamageFlags.BLDoor;

                        if (v.IsDoorBroken(VehicleDoor.BackRightDoor))
                            clientState.VehicleState.ExtraFlags |= (ushort)DamageFlags.BRDoor;

                        if (v.IsDoorBroken(VehicleDoor.Hood))
                            clientState.VehicleState.ExtraFlags |= (ushort)DamageFlags.Hood;
                    }

                    else if (Vehicle is NetworkPlane)
                    {
                        if (Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 2, (int)Control.VehicleFlyUnderCarriage))
                        {
                            var lgState = Function.Call<int>(Hash._GET_VEHICLE_LANDING_GEAR, Vehicle.Handle);
                            clientState.VehicleState.ExtraFlags = (ushort)lgState;
                        }

                        if (Game.IsControlPressed(0, Control.VehicleFlyAttack) || Game.IsControlPressed(0, Control.VehicleFlyAttack2))
                        {
                            var outArg = new OutputArgument();
                            if (Function.Call<bool>(Hash.GET_CURRENT_PED_VEHICLE_WEAPON, Ped.Handle, outArg))
                            {
                                unchecked
                                {
                                    switch ((WeaponHash)outArg.GetResult<int>())
                                    {
                                        case (WeaponHash)0xCF0896E0:
                                            clientState.VehicleState.Flags |= VehicleFlags.PlaneShoot;
                                            break;

                                        case (WeaponHash)0xE2822A29:
                                            clientState.VehicleState.Flags |= VehicleFlags.PlaneGun;
                                            break;
                                    }
                                }
                            }
                        }
                    }

                    else if (Vehicle is NetworkBicycle)
                    {
                        if (Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 2, (int)Control.VehiclePushbikePedal))
                        {
                            if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 2, (int)Control.ReplayPreviewAudio))
                                clientState.VehicleState.ExtraFlags = (ushort)BicycleState.TuckPedaling;
                            else clientState.VehicleState.ExtraFlags = (ushort)BicycleState.Pedaling;
                        }

                        else
                        {
                            if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 2, (int)Control.ReplayPreviewAudio))
                                clientState.VehicleState.ExtraFlags = (ushort)BicycleState.TuckCruising;
                            else clientState.VehicleState.ExtraFlags = (ushort)BicycleState.Cruising;
                        }
                    }
                }

                // client is in a remote vehicle, get its handle 
                else if ((Vehicle = EntityManager.VehicleFromLocalHandle(Ped.CurrentVehicle.Handle)) != null)
                {
                    clientState.InVehicle = true;
                    clientState.VehicleSeat = (SPFLib.Enums.VehicleSeat)Ped.CurrentVehicleSeat();
                    clientState.VehicleState = new VehicleState(Vehicle.ID);     
                }
            }

            ResetClientFlags();

            return clientState;
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
            if ((Vehicle == null || vehicle.Handle != Vehicle.Handle))
            {     
                // check if the vehicle already exists in the session so we don't create a clone
                Vehicle = EntityManager.VehicleFromLocalHandle(vehicle.Handle);

                // if not, create a NetworkVehicle instance from the players current vehicle
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

        /// <summary>
        /// Checks local client actions and sets the corrosponding flags to be sent to the server
        /// </summary>
        internal void UpdateUserCommands()
        {
            if (Ped.IsDead)
                SetClientFlag(ClientFlags.Dead);

            if (Ped.IsInMeleeCombat)
            {
                UI.UIManager.UISubtitleProxy("melee");
                SetClientFlag(ClientFlags.Punch);
            }

            if (Ped.IsRagdoll && !Ped.IsDead)
            {
                SetClientFlag(ClientFlags.Ragdoll);
            }

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

            if (Ped.IsInMeleeCombat)
                SetClientFlag(ClientFlags.Punch);
        }

        internal void Update()
        {
            UpdateUserCommands();

            if (Ped.IsShooting && Ped.Weapons.Current.AmmoInClip > 0)
                UpdateWeaponDamage();

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

            if (Game.IsControlJustPressed(0, Control.VehicleExit))
            {
                var closestVehicle = Ped.GetClosestVehicle(2.223f);

                if (closestVehicle != null)
                {
                    NetworkVehicle veh = closestVehicle.Handle == Vehicle?.Handle ? 
                        Vehicle : EntityManager.VehicleFromLocalHandle(closestVehicle.Handle);

                    if (veh != null && !Ped.IsInVehicle(closestVehicle) && veh.Handle != Ped.CurrentVehicle?.Handle)
                    {
                        if (veh is NetworkCar)
                        {
                            var bones = new int[]
                            {
                            Function.Call<int>((Hash)0xFB71170B7E76ACBA , veh.Handle, "handle_dside_f"), //-1 front left
                            Function.Call<int>((Hash)0xFB71170B7E76ACBA , veh.Handle, "handle_pside_f"), //0 front right
                            Function.Call<int>((Hash)0xFB71170B7E76ACBA , veh.Handle, "handle_dside_r"), //1 back left                     
                            Function.Call<int>((Hash)0xFB71170B7E76ACBA , veh.Handle, "handle_pside_r") //2 back right                     
                            };

                            var closestBone = bones.OrderBy(x => Function.Call<GTA.Math.Vector3>((Hash)0x44A8FCB8ED227738, veh.Handle, x).DistanceTo(Ped.Position)).First();

                            Ped.Task.ClearAll();

                            Function.Call(Hash.TASK_ENTER_VEHICLE, Ped.Handle, veh.Handle, -1, (Array.IndexOf(bones, closestBone) - 1), 0.0f, 3, 0);
                        }

                        else
                        {
                            var bones = new int[]
                                {
                                 //   Function.Call<int>((Hash)0xFB71170B7E76ACBA , veh.Handle, "door_dside_f"), //-1 front left
                                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , veh.Handle, "door_pside_f"), //0 front right
                                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , veh.Handle, "door_dside_r"), //1 back left                     
                                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , veh.Handle, "door_pside_r") //2 back right                     
                                };

                            var closestBone = bones.OrderBy(x => Function.Call<GTA.Math.Vector3>((Hash)0x44A8FCB8ED227738, veh.Handle, x).DistanceTo(Ped.Position)).First();

                            Ped.Task.ClearAll();

                            Function.Call(Hash.TASK_ENTER_VEHICLE, Ped.Handle, veh.Handle, -1, (Array.IndexOf(bones, closestBone)), 0.0f, 3, 0);

                        }
                    }
                }
            }
        }
    }
}
