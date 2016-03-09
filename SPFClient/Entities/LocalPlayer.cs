using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using GTA;
using SPFClient.Network;
using SPFLib.Enums;
using GTA.Native;
using GTA.Math;
using SPFLib.Types;

namespace SPFClient.Entities
{
    public sealed class LocalPlayer
    {
        public ClientFlags ClientFlags { get { return clientFlags; } }

        public NetworkVehicle Vehicle { get; private set; }

        internal readonly int ID = Helpers.GetUserID();

        private ClientFlags clientFlags;

        private float weaponDamage = 0.0f;

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

        internal void SetCurrentVehicle(NetworkVehicle vehicle)
        {
            Vehicle = vehicle;
        }

        internal void UpdateWeaponDamage()
        {
            // get range of the current weapon
            var weaponRange = Function.Call<float>((Hash)0x814C9D19DFD69679, Ped.Handle);

            var result = World.Raycast(GameplayCamera.Position,
                GameplayCamera.Position + Helpers.RotationToDirection(GameplayCamera.Rotation) * weaponRange,
                IntersectOptions.Everything);

            if (result.DitHitEntity && NetworkManager.GetClients().Any(x => x.Handle == result.HitEntity.Handle))
            {
                var hitCoords = result.HitEntity.Position.Serialize();
                var dmg = (short)10;
                NetworkSession.CurrentSession.SendWeaponHit(dmg, hitCoords);
            }
        }

        internal float GetCurrentWeaponDamage()
        {
            return MemoryAccess.GetPedWeaponDamage(Ped.Handle);
        }

        /// <summary>
        /// Get wheel rotation at vehicle entity adddress.
        /// </summary>
        /// <returns></returns>
        internal float GetWheelRotation()
        {
            ulong baseAddress = MemoryAccess.GetEntityAddress(Ped.CurrentVehicle.Handle);

            var wheelPtr = MemoryAccess.ReadUInt64(baseAddress + Offsets.CVehicle.WheelsPtr);

            if (baseAddress > wheelPtr)
            {
                wheelPtr = ulong.Parse(baseAddress.ToString("X").Substring(0, 3) + wheelPtr.ToString("X"), System.Globalization.NumberStyles.HexNumber);
            }

            if (wheelPtr > 0)
            {
                return MemoryAccess.GetWheelRotation(wheelPtr, 0);
            }

            return 0.0f;
        }

        /// <summary>
        /// Avoid iterating inside xxHashtoID while running the game loop.
        /// </summary>
        /// <returns></returns>
        internal short GetPedID()
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
        internal short GetWeaponID()
        {
            if ((int)Ped.Weapons.Current.Hash != localWeaponHash)
            {
                localWeaponHash = (int)Ped.Weapons.Current.Hash;
                localWeaponID = Helpers.WeaponHashtoID((WeaponHash)localWeaponHash);
                weaponDamage = GetCurrentWeaponDamage();
            }
            return localWeaponID;
        }

      /*  private void DoFiringPatternFix()
        {
            var baseAddr = MemoryAccess.GetEntityAddress(Ped.Handle);

            var weaponMgr = MemoryAccess.ReadUInt64(baseAddr + Offsets.CLocalPlayer.CWeaponManager);

            var localWeapon = MemoryAccess.ReadUInt64(weaponMgr + Offsets.CWeaponManager.LocalWeaponInstance);

            MemoryAccess.WriteInt32(localWeapon + Offsets.CWeaponInfo.FiringType, 6);
        }*/

        /// <summary>
        /// Avoid iterating inside xxHashtoID while running the game loop.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        internal short GetVehicleID()
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
        internal byte GetRadioStation()
        {
            return Convert.ToByte(Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX));
        }

        internal Color GetVehicleColor()
        {
            OutputArgument outR, outG, outB;
            outR = outG = outB = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_COLOR, Ped.CurrentVehicle.Handle, outR, outG, outB);

            return Color.FromArgb(outR.GetResult<byte>(), outG.GetResult<byte>(), outB.GetResult<byte>());
        }

        internal SPFLib.Enums.VehicleSeat GetVehicleSeat()
        {
            var vehicle = Ped.CurrentVehicle;

            foreach (GTA.VehicleSeat seat in Enum.GetValues(typeof(GTA.VehicleSeat)))
            {
                if (vehicle.GetPedOnSeat(seat) == Ped)
                    return (SPFLib.Enums.VehicleSeat)seat;
            }
            return SPFLib.Enums.VehicleSeat.None;
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

                        UI.Notify("trying to enter " + closestVehicle.Handle.ToString());
                        Ped.Task.ClearAll();

                        // Ped.Task.WarpIntoVehicle(closestVehicle, (GTA.VehicleSeat)(Array.IndexOf(bones, closestBone) - 1));
                        Function.Call(Hash.TASK_ENTER_VEHICLE, Ped.Handle, veh.Handle, -1, (Array.IndexOf(bones, closestBone) - 1), 0.0f, 3, 0);
                        //    Ped.SetIntoVehicle(vehicle, seat);
                        //  Function.Call<int>((Hash)0x814FA8BE5449445D, Ped.Handle);
                    }
                }
            }

            UpdateUserCommands();                    
        }
    }
}
