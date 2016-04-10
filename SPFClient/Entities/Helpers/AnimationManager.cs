using System;
using SPFLib.Enums;
using SPFClient.Types;
using System.Collections.Generic;
using GTA.Native;
using GTA.Math;
using GTA;

namespace SPFClient.Entities
{
    public sealed class AnimationManager
    {
        private Dictionary<WeaponHash, string> weaponAnimSets = new Dictionary<WeaponHash, string>
        {
            { WeaponHash.AdvancedRifle, "weapons@submg@advanced_rifle" },
            { WeaponHash.APPistol, "weapons@pistol@ap_pistol" },
            { WeaponHash.AssaultRifle, "weapons@rifle@" },
            { WeaponHash.AssaultShotgun, "weapons@rifle@hi@assault_rifle" },
            { WeaponHash.AssaultSMG, "weapons@submg@assault_smg" },
            { WeaponHash.Bat, "weapons@melee_2h" },
            { WeaponHash.BullpupRifle, "anim@weapons@submg@bullpup_rifle" },
            { WeaponHash.BullpupShotgun, "weapons@rifle@hi@assault_rifle" },
            { WeaponHash.BZGas, "weapons@projectile@grenade_str" },
            { WeaponHash.CarbineRifle, "weapons@submg@advanced_rifle" },
            { WeaponHash.CombatMG, "weapons@machinegun@combat_mg" },
            { WeaponHash.CombatPDW, "weapons@submg@" },
            { WeaponHash.CombatPistol, "weapons@pistol@combat_pistol" },
            { WeaponHash.Crowbar, "weapons@melee_1h" },
            { WeaponHash.Dagger, "melee@holster" },
            { WeaponHash.FireExtinguisher, "weapons@misc@fire_ext" },
            { WeaponHash.GolfClub, "weapons@melee_2h@golfclub" },
            { WeaponHash.Grenade, "weapons@projectile@grenade_str" },
            { WeaponHash.GrenadeLauncher, "weapons@heavy@grenade_launcher" },
            { WeaponHash.GrenadeLauncherSmoke, "weapons@heavy@grenade_launcher" },
            { WeaponHash.Hammer, "weapons@melee_1h" },
            { WeaponHash.Hatchet, "melee@holster" },
            { WeaponHash.HeavyPistol, "weapons@pistol@pistol" },
            { WeaponHash.HeavyShotgun, "weapons@pistol@pistol" },
            { WeaponHash.HeavySniper, "weapons@rifle@hi@assault_rifle" },
            { WeaponHash.MarksmanPistol, "weapons@pistol@pistol" },
            { WeaponHash.HomingLauncher, "weapons@pistol@pistol" },
            { WeaponHash.Knife, "weapons@first_person@aim_idle@generic@melee@knife@shared@core" },
            { WeaponHash.KnuckleDuster, "weapons@pistol@pistol" },
            { WeaponHash.Railgun, "weapons@rifle@lo@rail_gun" },
            { WeaponHash.MarksmanRifle, "weapons@rifle@hi@assault_rifle" },
            { WeaponHash.MG, "weapons@machinegun@mg" },
            { WeaponHash.MicroSMG, "weapons@submg@" },
            { WeaponHash.Minigun, "weapons@heavy@minigun" },
            { WeaponHash.Molotov, "weapons@projectile@molotov" },
            { WeaponHash.PetrolCan, "weapons@misc@jerrycan@mp_male" },
            { WeaponHash.SniperRifle, "weapons@rifle@hi@assault_rifle" },
            { WeaponHash.SpecialCarbine, "weapons@rifle@hi@assault_rifle" },
            { WeaponHash.StunGun, "weapon@w_pi_stungun" },
            { WeaponHash.RPG, "weapons@heavy@rpg_str" }
        };

        private Ped ped;

        private ClientFlags mFlags;
        private Animation currentAnimation;
        private Animation upperAnimation;
        private Vector3 pAngles, pPosition;

        private readonly ulong pAddress;

        private static ActiveTask pTask;

        private WeaponHash lastWeaponHash;

        Vector3 lastAimPos;
        bool ragdoll, diving;

        DateTime jumpWaiter = new DateTime();

        public AnimationManager(Ped ped)
        {
            this.ped = ped;
            pAddress = MemoryAccess.GetEntityAddress(ped.Handle);
            pPosition = new Vector3();
            pAngles = new Vector3();    
            mFlags = 0;
        }

        /// <summary>
        /// Set flags for ped animations.
        /// </summary>
        /// <param name="flags">Movement flags.</param>
        /// <param name="activeTask">Task flags.</param>
        public void SetFlags(ClientFlags flags, ActiveTask activeTask)
        {
            if (ped.IsAlive)
            {
                pTask = activeTask;
                currentAnimation = AnimationFromFlags(flags);
                MemoryAccess.WriteInt16(pAddress + Offsets.CLocalPlayer.Stance, (short)activeTask);
                mFlags = flags;
            }
        }

        /// <summary>
        /// Update the player view angles for aiming and shooting animations.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="angles"></param>
        public void UpdateLocalAngles(Vector3 pos, Vector3 angles)
        {
            pPosition = pos;
            pAngles = angles;
        }

        /// <summary>
        /// Play an animation object on the given ped with optional flags.
        /// </summary>
        /// <param name="ped">Target ped.</param>
        /// <param name="animation">Animation.</param>
        /// <param name="flags">Animation Flags.</param>
        public static void PlayAnimation(Ped ped, Animation animation, int flags = 1)
        {
            Function.Call(Hash.TASK_PLAY_ANIM, ped.Handle,
             animation.Dictionary, animation.Name, 8f, -8.0f, -1, flags, 0, 0, 0, 0);
        }

        /// <summary>
        /// Handle ped shooting animations.
        /// </summary>
        private void ShootWeapon()
        {
            var shootPos = pPosition + Helpers.RotationToDirection(pAngles) * 1000;
            lastAimPos = shootPos;

            Function.Call(Hash.SET_PED_SHOOTS_AT_COORD, ped.Handle,
               shootPos.X, shootPos.Y, shootPos.Z, true);

            Function.Call(Hash.TASK_SHOOT_AT_COORD, ped.Handle,
                shootPos.X, shootPos.Y, shootPos.Z, -1,
                Function.Call<int>(Hash.GET_HASH_KEY, "firing_pattern_full_auto"));
        }

        /// <summary>
        /// Hanlde ped aiming animations
        /// </summary>
        private void AimWeapon()
        {
            var aimPos = pPosition + Helpers.RotationToDirection(pAngles) * 1000;

             Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, ped.Handle, 
                 pPosition.X, pPosition.Y, pPosition.Z, 
                 aimPos.X, aimPos.Y, aimPos.Z, 
                 1.0, 0, 0x3f000000, 0x40800000, 0, 1, 0, 
                 Function.Call<int>(Hash.GET_HASH_KEY, "firing_pattern_full_auto"));

            lastAimPos = aimPos;
        }

        /// <summary>
        /// Update ped animations.
        /// </summary>
        public void Update()
        {
            if (ped.IsReloading || Anims.Walk2RunLeft.IsPlayingOn(ped) ||
                Anims.Walk2RunRight.IsPlayingOn(ped) || ped.IsRagdoll)
            {
                return;
            }
     
            if (ClientFlagIsSet(ClientFlags.Ragdoll) && !ped.IsDead)
            {
                if (!ped.IsRagdoll)
                {
                    ped.CanRagdoll = true;
                    Function.Call(Hash.SET_PED_TO_RAGDOLL, ped.Handle, 800, 1500, 2, 1, 1, 0);
                    Function.Call(Hash.APPLY_FORCE_TO_ENTITY, ped.Handle, 1, 2.0, 0.0, -4.0, 0.0, 0.21, 0.0, 
                        Function.Call<int>(Hash.GET_PED_RAGDOLL_BONE_INDEX, ped.Handle, 7), 0, 0, 1, 0, 1);
                    ragdoll = true;
                    return;
                }
            }

            else
            {
                if (ped.IsRagdoll) return;

                if (ragdoll)
                {
                    ped.CanRagdoll = false;
                    ragdoll = false;
                }
            }

            if (ClientFlagIsSet(ClientFlags.Reloading))
            {
                Function.Call(Hash.TASK_RELOAD_WEAPON, ped.Handle, true);

                return;
            }

            #region jumping

            if (jumpWaiter.Ticks > 0 && jumpWaiter > DateTime.Now)
                return;

            if (ClientFlagIsSet(ClientFlags.Jumping) && !Function.Call<bool>(Hash.IS_PED_JUMPING, ped.Handle))
            {
                Function.Call(Hash.TASK_JUMP, ped.Handle, false);
                jumpWaiter = DateTime.Now + TimeSpan.FromMilliseconds(900);
            }

            #endregion

            if (ClientFlagIsSet(ClientFlags.Climbing) && !Function.Call<bool>(Hash.IS_PED_CLIMBING, ped.Handle))
            {
                Function.Call(Hash.TASK_CLIMB, ped.Handle, 0);
            }

            #region parachute

            if (ClientFlagIsSet(ClientFlags.HasParachute) && !Function.Call<bool>((Hash)0xF731332072F5156C, ped.Handle, 0xFBAB5776))
            {
                UI.UIManager.ShowSubtitle("has parachute");
                Function.Call((Hash)0xD0D7B1E680ED4A1A, ped.Handle, 0xFBAB5776, true);
            }

            if (ClientFlagIsSet(ClientFlags.ParachuteOpen) && Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, ped.Handle) != 2)
            {
                if (!Function.Call<bool>((Hash)0xF731332072F5156C, ped.Handle, 0xFBAB5776))
                    Function.Call((Hash)0xD0D7B1E680ED4A1A, ped.Handle, 0xFBAB5776, true);

                UI.UIManager.ShowSubtitle("parachute open");
                Function.Call((Hash)0x16E42E800B472221, ped.Handle);
            }

              #endregion

                #region diving
                if (ClientFlagIsSet(ClientFlags.Diving))
            {
                if (!Anims.SkydiveJump.IsPlayingOn(ped))
                {
                    if (!diving)
                    {
                        PlayAnimation(ped, Anims.SkydiveJump);
                        diving = true;
                    }

                    else
                    {
                        if (!Anims.Skydive.IsPlayingOn(ped))
                        {
                            PlayAnimation(ped, Anims.Skydive);
                        }
                    }
                }

                return;
            }

            else diving = false;

            #endregion

            if (currentAnimation != null && ped.Weapons.Current.Hash != lastWeaponHash)
            {
                string animSet = "";

                if (weaponAnimSets.TryGetValue(ped.Weapons.Current.Hash, out animSet))
                    upperAnimation = new Animation(animSet, currentAnimation.Name);
                else
                {
                    if (upperAnimation != null)
                    {
                        ped.Task.ClearAnimation(upperAnimation.Dictionary, upperAnimation.Name);
                        upperAnimation = null;
                    }
                }

                lastWeaponHash = ped.Weapons.Current.Hash;
            }

            if (pTask == ActiveTask.MovingCrouched)
            {
                if (ped.Weapons.Current.Hash == WeaponHash.Unarmed || ped.Weapons.Current.IsMeleeOrThrowable())
                {
                    if (!Anims.WalkUnarmedCrouched.IsPlayingOn(ped)) PlayAnimation(ped, Anims.WalkUnarmedCrouched);
                }

                else
                {
                    if (!Anims.WalkStealthASMG.IsPlayingOn(ped)) PlayAnimation(ped, Anims.WalkStealthASMG);
                }
            }

            else if (pTask == ActiveTask.IdleCrouch)
            {
                if (mFlags.HasFlag(ClientFlags.Running) ||
                    mFlags.HasFlag(ClientFlags.Sprinting))
                {
                    if (!Anims.QuickStop2.IsPlayingOn(ped))
                    {
                        PlayAnimation(ped, Anims.QuickStop2);
                    }
                }

                else if (mFlags.HasFlag(ClientFlags.Walking))
                {
                    if (!Anims.QuickStop1.IsPlayingOn(ped))
                    {
                        PlayAnimation(ped, Anims.QuickStop1);
                    }
                }

                else ped.Task.ClearAll();
            }

            else if (pTask == ActiveTask.Respawning)
            {
                if (!Anims.Walk.IsPlayingOn(ped))
                {
                    ped.Task.ClearAll();
                    PlayAnimation(ped, Anims.Walk);
                }
            }

            else
            {
                if (currentAnimation != null && !currentAnimation.IsPlayingOn(ped))
                    PlayAnimation(ped, currentAnimation);

                if (upperAnimation != null && !upperAnimation.IsPlayingOn(ped))
                {
                    PlayAnimation(ped, upperAnimation, 
                        (ClientFlagIsSet(ClientFlags.Running) || ClientFlagIsSet(ClientFlags.Sprinting) || ClientFlagIsSet(ClientFlags.Walking)) ? 48 : 1);
                }
            }

            if (ClientFlagIsSet(ClientFlags.Shooting))
            {
                ShootWeapon();
            }

            else if (ClientFlagIsSet(ClientFlags.Aiming))
            {
                AimWeapon();
            }   
        }

        /// <summary>
        /// Get the current animation for the supplied client flags.
        /// </summary>
        /// <param name="flags">Flags.</param>
        /// <returns></returns>
        public Animation AnimationFromFlags(ClientFlags flags)
        {
            if (flags.HasFlag(ClientFlags.Stopped))
            {
                if (flags.HasFlag(ClientFlags.Punch))
                {
                    return Anims.PunchIdle;
                }

                else
                    return new Animation("move_m@generic", "idle");
            }
            if (flags.HasFlag(ClientFlags.Walking))
            {
                if (flags.HasFlag(ClientFlags.Punch))
                    return Anims.PunchWalkingNoTarget;
                else
                {
                    if (ClientFlagIsSet(ClientFlags.Running) || ClientFlagIsSet(ClientFlags.Sprinting))
                    {
                        var vel = ped.Velocity;
                        return vel.X < 0 ? Anims.Run2WalkLeft : Anims.Run2WalkRight;
                    }
                    else
                    return new Animation("move_m@generic", "walk");
                }
            }
            if (flags.HasFlag(ClientFlags.Running))
            {
                if (flags.HasFlag(ClientFlags.Punch))
                    return Anims.PunchRunning;
                else
                {
                    if (ClientFlagIsSet(ClientFlags.Walking))
                    {
                        var vel = ped.Velocity;
                        return vel.X < 0 ? Anims.Walk2RunLeft : Anims.Walk2RunRight;
                    }
                    else
                        return new Animation("move_m@generic", "run");
                }
            }
            if (flags.HasFlag(ClientFlags.Sprinting))
            {
                if (flags.HasFlag(ClientFlags.Punch))
                    return Anims.PunchRunning;
                else
                    return new Animation("move_m@generic", "sprint");
            }

            else return null;
        }

        public bool ClientFlagIsSet(ClientFlags flag)
        {
            return (mFlags & flag) != 0;
        }


        private class Anims
        {
            public static readonly Animation Punch = new Animation("melee@unarmed@streamed_core_fps", "heavy_punch_a_var_1");

            public static readonly Animation PunchIdle = new Animation("melee@unarmed@streamed_core", "short_0_punch");

            public static readonly Animation PunchRunning = new Animation("melee@unarmed@streamed_core_fps", "running_punch");

            public static readonly Animation PunchWalkingNoTarget = new Animation("melee@unarmed@streamed_core", "walking_punch_no_target");

            public static readonly Animation SkydiveJump = new Animation("move_jump", "jump_launch_l_to_skydive");

            public static readonly Animation Skydive = new Animation("skydive@freefall", "free_right");

            #region walk

            public static readonly Animation Walk = new Animation("move_m@generic", "walk");

            public static readonly Animation WalkUnarmedCrouched = new Animation("move_stealth@p_m_zero@unarmed@core", "walk");

            public static readonly Animation WalkBackwardsFP = new Animation("move_strafe@first_person@generic", "walk_bwd_180_loop");

            public static readonly Animation WalkRifle = new Animation("move_weapon@rifle@generic", "walk");

            public static readonly Animation WalkPistol = new Animation("move_weapon@pistol@generic", "walk");

            public static readonly Animation WalkStealthASMG = new Animation("move_stealth@p_m_zero@2h_short@upper", "walk");

            public static readonly Animation WalkMingun = new Animation("move_ballistic_minigun", "walk");

            #endregion

            public static readonly Animation Run = new Animation("move_m@generic", "run");

            public static readonly Animation RunArmed = new Animation("move_action@p_m_one@armed@core", "run");

            public static readonly Animation Sprint = new Animation("move_m@generic", "sprint");

            public static readonly Animation SprintArmed = new Animation("move_action@p_m_one@armed@core", "sprint");

            public static readonly Animation QuickStop1 = new Animation("move_m@multiplayer", "wstop_quick_l_0");

            public static readonly Animation QuickStop2 = new Animation("move_m@multiplayer", "rstop_quick_l");

            public static readonly Animation Run2WalkLeft = new Animation("move_m@multiplayer", "runtowalk_left");

            public static readonly Animation Run2WalkRight = new Animation("move_m@multiplayer", "runtowalk_right");

            public static readonly Animation Walk2RunLeft = new Animation("move_m@multiplayer", "walktorun_left");

            public static readonly Animation Walk2RunRight = new Animation("move_m@multiplayer", "walktorun_right");

            public static readonly Animation AimStun = new Animation("weapons@first_person@aim_scope@generic@pistol@stun_gun@", "w_fire_recoil");
        }
    }
}
