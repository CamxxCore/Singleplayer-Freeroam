using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using GTA;
using SPFClient.Network;
using SPFLib.Enums;
using GTA.Native;
using GTA.Math;
using SPFClient.Types;
using SPFLib.Types;

namespace SPFClient.Entities
{
    public sealed class LocalPlayer
    {
       // public ClientState[] HistoryBuffer {  get { return historyBuffer; } }

      // private int statesCount = 0;

     //   private ClientState[] historyBuffer = new ClientState[32];

        public ClientFlags ClientFlags { get { return clientFlags; } }

        public NetworkVehicle Vehicle { get; private set; }

        internal readonly int ID = Helpers.GetUserID();

        private ClientFlags clientFlags;

        private float weaponDamage = 0.0f;

        private uint sentPacketCount;

        public uint SentPacketCount {  get { return sentPacketCount; } }

        private int localPedHash, localWeaponHash, localVehicleHash;
        private short localPedID, localWeaponID, localVehicleID;

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
            weaponDamage = GetCurrentWeaponDamage();

            Function.Call((Hash)0x2C2B3493FBF51C71, true);

            Function.Call(Hash.SET_FADE_OUT_AFTER_DEATH, false);

            Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, Ped.Handle, true, true);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 342, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 122, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 134, 1);

            Function.Call(Hash.SET_PED_CONFIG_FLAG, Ped.Handle, 115, 1);
        }

        internal NetworkVehicle NetVehicleFromGameVehicle(Vehicle vehicle)
        {
            var uid = SPFLib.Helpers.GenerateUniqueID();
            var hash = Ped.CurrentVehicle.Model.Hash;

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, hash))
            {
                return new NetCar(Ped.CurrentVehicle, uid);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_HELI, hash))
            {
                return new NetHeli(Ped.CurrentVehicle, uid);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, hash))
            {
                return new NetPlane(Ped.CurrentVehicle, uid);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, hash))
            {
                return new NetBicycle(Ped.CurrentVehicle, uid);
            }

            else return new NetworkVehicle(Ped.CurrentVehicle, uid);
        }

        private void UpdateWeaponDamage()
        {
            // get range of the current weapon
            var weaponRange = Function.Call<float>((Hash)0x814C9D19DFD69679, Ped.Handle);

            var result = World.Raycast(GameplayCamera.Position,
                GameplayCamera.Position + Helpers.RotationToDirection(GameplayCamera.Rotation) * weaponRange,
                IntersectOptions.Everything);

            if (result.DitHitEntity && NetworkManager.ActivePlayers.Any(x => x.Handle == result.HitEntity.Handle))
            {
                var hitCoords = result.HitEntity.Position.Serialize();
                var dmg = (short)GetCurrentWeaponDamage();
                NetworkSession.Client.SendWeaponData(20, hitCoords);
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
        private short GetPedID()
        {
            if (Ped.Model.Hash != localPedHash)
            {
                localPedHash = Ped.Model.Hash;
                localPedID = Helpers.PedHashtoID((PedHash)localPedHash);
            }
            return localPedID;
        }

        /// <summary>
        /// Avoid iterating inside xxHashtoID while running the game loop.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        private short GetWeaponID()
        {
            if ((int)Ped.Weapons.Current.Hash != localWeaponHash)
            {
                localWeaponHash = (int)Ped.Weapons.Current.Hash;
                localWeaponID = Helpers.WeaponHashtoID((WeaponHash)localWeaponHash);
                weaponDamage = GetCurrentWeaponDamage();
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

        /*  public IEnumerable<ClientState> GetHistoryBuffer()
          {
              for (int i = 0; i < statesCount; i++)
                  yield return historyBuffer[i];
          }*/

        public ClientState GetClientState()
        {
            var clientState = new ClientState();

            // dont serialize this information unless we have to
            if (!Ped.IsInVehicle())
            {
                clientState.Position = Ped.Position.Serialize();
                clientState.Velocity = Ped.Velocity.Serialize();
                clientState.Angles = GameplayCamera.Rotation.Serialize();
                clientState.Rotation = Ped.Quaternion.Serialize();
                clientState.MovementFlags = ClientFlags;
                clientState.ActiveTask = GetActiveTask();
                clientState.Health = Convert.ToInt16(Ped.Health);
                clientState.WeaponID = GetWeaponID();
                clientState.PedID = GetPedID();
            }

            else
            {
                if ((int)Ped.CurrentVehicleSeat() == -1)
                {
                    // if the local player is in a vehicle that doesn't exist in the active list...
                    if ((Vehicle == null || Ped.CurrentVehicle.Handle != Vehicle.Handle))
                    {
                        Vehicle = NetworkManager.VehicleFromLocalHandle(Ped.CurrentVehicle.Handle);

                        if (Vehicle == null)
                        {
                            Vehicle = NetVehicleFromGameVehicle(Ped.CurrentVehicle);
                        }
                    }

                    var v = new Vehicle(Vehicle.Handle);

                    clientState.VehicleState = new VehicleState(Vehicle.ID,
                        Vehicle.Position.Serialize(),
                        Vehicle.Velocity.Serialize(),
                        Vehicle.Quaternion.Serialize(),
                        v.CurrentRPM,
                        Vehicle.GetWheelRotation(),
                        v.Steering,
                        0, Convert.ToInt16(v.Health),
                        (byte)v.PrimaryColor, (byte)v.SecondaryColor,
                        GetActiveRadioStation(),
                        GetVehicleID());

                    clientState.VehicleState.Flags |= VehicleFlags.Driver;

                    if (Function.Call<bool>(Hash.IS_HORN_ACTIVE, v.Handle))
                        clientState.VehicleState.Flags |= VehicleFlags.HornPressed;

                    if (v.Health <= 0)
                        clientState.VehicleState.Flags |= VehicleFlags.Exploded;

                    if (Vehicle is NetCar)
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

                    else if (Vehicle is NetPlane)
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

                    else if (Vehicle is NetBicycle)
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

                else
                {
                    Vehicle = NetworkManager.VehicleFromLocalHandle(Ped.CurrentVehicle.Handle);

                    if (Vehicle != null)
                    {
                        clientState.VehicleState = new VehicleState(Vehicle.ID);
                    }
                }

                clientState.InVehicle = true;

                //   state.Health = Convert.ToInt16(localPlayer.Ped.Health);
                clientState.VehicleSeat = (SPFLib.Enums.VehicleSeat)Ped.CurrentVehicleSeat();

                clientState.PedID = GetPedID();
                clientState.WeaponID = GetWeaponID();
            }

            //  for (int i = historyBuffer.Length - 1; i > 0; i--)
            //      historyBuffer[i] = historyBuffer[i - 1];

            //  historyBuffer[0] = clientState;
            //  statesCount = Math.Min(statesCount + 1, historyBuffer.Length);

            sentPacketCount++;
            sentPacketCount %= int.MaxValue;

            ResetClientFlags();

            return clientState;
        }

        /// <summary>
        /// Set an input command for the local client
        /// TODO: Move into a dedicated class
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
        /// Check the status of an input command for the local client
        /// TODO: Move into a dedicated class
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
                SetClientFlag(ClientFlags.Punch);

            if (Ped.IsRagdoll)
                SetClientFlag(ClientFlags.Ragdoll);

            if (Game.Player.IsAiming)
                SetClientFlag(ClientFlags.Aiming);

            if (Game.IsControlPressed(2, Control.Attack))
                SetClientFlag(ClientFlags.Shooting);

            if (Ped.IsRunning)
                SetClientFlag(ClientFlags.Running);

            else if (Ped.IsSprinting)
                SetClientFlag(ClientFlags.Sprinting);

            else if (Ped.IsWalking)
                SetClientFlag(ClientFlags.Walking);

            else if (Ped.IsStopped)
                SetClientFlag(ClientFlags.Stopped);

            if (Function.Call<bool>(Hash.IS_PED_JUMPING, Ped.Handle))
                SetClientFlag(ClientFlags.Jumping);

            else if (Function.Call<bool>(Hash.IS_PED_DIVING, Ped.Handle))
                SetClientFlag(ClientFlags.Diving);

            if (Ped.IsInMeleeCombat)
                SetClientFlag(ClientFlags.Punch);
        }

        internal void Update()
        {
            if (Ped.IsShooting)
                UpdateWeaponDamage();

            if (Vehicle != null && Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, Vehicle.Handle, -1) != Ped.Handle)
                Vehicle.Update();

            if (Game.IsControlJustPressed(0, Control.VehicleExit))
            {
                var closestVehicle = Ped.GetClosestVehicle(5f);

                if (closestVehicle != null)
                {
                    var veh = NetworkManager.VehicleFromLocalHandle(closestVehicle.Handle);

                    if (veh != null && !Ped.IsInVehicle(closestVehicle) && veh.Handle != Ped.CurrentVehicle?.Handle)
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
                }
            }

            UpdateUserCommands();                    
        }
    }
}
