﻿using System;
using SPFLib.Enums;
using SPFClient.Types;
using System.Collections.Generic;
using GTA.Native;
using GTA.Math;
using GTA;

namespace SPFClient.Entities
{
    public sealed class MovementController
    {
        private Dictionary<WeaponHash, string> weaponAnimSets = new Dictionary<WeaponHash, string>
        {
            { WeaponHash.AdvancedRifle, "weapons@submg@advanced_rifle" },
            { WeaponHash.APPistol, "weapons@pistol@ap_pistol" },
            { WeaponHash.AssaultRifle, "weapons@rifle@" },
            { WeaponHash.AssaultShotgun, "weapons@rifle@" },
            { WeaponHash.AssaultSMG, "weapons@submg@assault_smg" },
            { WeaponHash.Bat, "weapons@melee_2h" },
            { WeaponHash.BullpupRifle, "anim@weapons@submg@bullpup_rifle" },
            { WeaponHash.BullpupShotgun, "weapons@rifle@" },
            { WeaponHash.BZGas, "weapons@projectile@grenade_str" },
            { WeaponHash.CarbineRifle, "weapons@rifle@hi@assault_rifle" },
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
       //     { WeaponHash.Gusenberg, "weapons@machinegun@gusenberg" },
            { WeaponHash.Hammer, "weapons@melee_1h" },
            { WeaponHash.Hatchet, "melee@holster" },
            { WeaponHash.HeavyPistol, "weapons@pistol@pistol" },
            { WeaponHash.HeavyShotgun, "weapons@pistol@pistol" },
            { WeaponHash.HeavySniper, "weapons@rifle@" },
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
            { WeaponHash.SniperRifle, "weapons@rifle@" },
            { WeaponHash.SpecialCarbine, "anim@weapons@rifle@lo@spcarbine" },
            { WeaponHash.StunGun, "weapon@w_pi_stungun" }

        };

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

            public static readonly Animation Walk2Run = new Animation("move_m@generic", "walktorun_right");

            public static readonly Animation RunArmed = new Animation("move_action@p_m_one@armed@core", "run");

            public static readonly Animation Sprint = new Animation("move_m@generic", "sprint");

            public static readonly Animation SprintArmed = new Animation("move_action@p_m_one@armed@core", "sprint");

            public static readonly Animation QuickStop1 = new Animation("move_m@generic", "wstop_quick_l_0");

            public static readonly Animation QuickStop2 = new Animation("move_m@generic", "rstop_quick_l");

            public static readonly SequenceAnim Jump = new SequenceAnim(
                new Animation("move_jump", "jump_launch_r"), new Animation("move_jump", "land_stop_l")
                );

            public static readonly SequenceAnim Roll90Deg = new SequenceAnim(
               new Animation("move_strafe@roll", "combatroll_fwd_p1_90"), new Animation("move_strafe@roll", "combatroll_fwd_p2_90")
               );

            public static readonly Animation AimStun = new Animation("weapons@first_person@aim_scope@generic@pistol@stun_gun@", "w_fire_recoil");
        }
    
        private ClientFlags mFlags;
        private Animation currentAnimation;
        private Animation upperAnimation;
        private Vector3 pAngles, pPosition;
        private readonly ulong pAddress;
        private static ActiveTask pTask;
        private WeaponHash lastWeaponHash;
        private Ped ped;

        public MovementController(Ped ped)
        {
            this.pAddress = MemoryAccess.GetEntityAddress(ped.Handle);
            this.pPosition = new Vector3();
            this.pAngles = new Vector3();
            this.ped = ped;
            this.mFlags = 0;
        }

        public void UpdateAnimationFlags(ClientFlags flags, ActiveTask activeTask)
        {
            if (ped.IsAlive)
            {
                pTask = activeTask;
                currentAnimation = AnimationFromFlags(flags);
                MemoryAccess.WriteInt16(pAddress + Offsets.CLocalPlayer.Stance, (short)activeTask);
                mFlags = flags;
            }
        }

        public void UpdateLocalAngles(Vector3 pos, Vector3 angles)
        {
            pPosition = pos;
            pAngles = angles;
        }

        public void PlayAnimation(Animation animation, int flags = 1)
        {
            Function.Call(Hash.TASK_PLAY_ANIM, ped.Handle,
                animation.Dictionary, animation.Name, 8f, -8.0f, -1, flags, 1f, 0, 0, 0);
        }

        private bool TransitionActive()
        {
            return Anims.Walk2Run.IsPlayingOn(ped) || Anims.Roll90Deg.IsPlayingOn(ped);

        }

        private void HandleShootAnim()
        {
            var shootPos = Helpers.SmoothStep(lastAimPos, ped.Position + Helpers.RotationToDirection(pAngles) * 10, 0.5f);

            lastAimPos = shootPos;

            Function.Call(Hash.TASK_SHOOT_AT_COORD, ped.Handle, 
                shootPos.X, 
                shootPos.Y, 
                shootPos.Z, 
                2000, 
                Function.Call<int>(Hash.GET_HASH_KEY, 
                "firing_pattern_full_auto"));
        }

        private void HandleAimAnim()
        {
            var aimPos = Helpers.SmoothStep(lastAimPos, pPosition + Helpers.RotationToDirection(pAngles) * 10, 0.5f);
            Function.Call(Hash.TASK_AIM_GUN_AT_COORD, ped.Handle, aimPos.X, aimPos.Y, aimPos.Z, -1, 0, 0);
            lastAimPos = aimPos;
        }

        public void StopAllAnimations()
        {
            ped.Task.ClearAll();
        }

        Vector3 lastAimPos;
        bool ragdoll, diving;
        DateTime jumpWaiter = new DateTime();

        public void Update()
        {
            if (currentAnimation == null || ped.IsRagdoll || TransitionActive())
                return;

            if (ped.Weapons.Current.Hash != lastWeaponHash)
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

            if (ragdoll)
            {
                ped.CanRagdoll = false;
                ragdoll = false;
            }


            #region ragdoll
            if (FlagIsSet(ClientFlags.Ragdoll))
            {
                ragdoll = true;
                ped.CanRagdoll = true;
                Function.Call(Hash.SET_PED_TO_RAGDOLL, ped.Handle, 1000, 2000, 1, 0, 1, 1, 0);
                return;
            }

            #endregion

            #region jumping

            if (jumpWaiter.Ticks > 0 && jumpWaiter > DateTime.Now)
                return;

            if (FlagIsSet(ClientFlags.Jumping) && !Function.Call<bool>(Hash.IS_PED_JUMPING, ped.Handle))
            {
                Function.Call(Hash.TASK_JUMP, ped.Handle, false);
                jumpWaiter = DateTime.Now + TimeSpan.FromMilliseconds(900);
            }

            #endregion

            #region diving
            if (FlagIsSet(ClientFlags.Diving))
            {
                if (!Anims.SkydiveJump.IsPlayingOn(ped))
                {
                    if (!diving)
                    {
                        PlayAnimation(Anims.SkydiveJump);
                        diving = true;
                    }

                    else
                    {
                        if (!Anims.Skydive.IsPlayingOn(ped))
                        {
                            PlayAnimation(Anims.Skydive);
                        }
                    }
                }

                return;
            }

            else diving = false;

            #endregion

            if (pTask == ActiveTask.MovingCrouched)
            {
                if (ped.Weapons.Current.Hash == WeaponHash.Unarmed || ped.Weapons.Current.IsMeleeOrThrowable())
                {
                    // unarmed crouch walk
                    if (!Anims.WalkUnarmedCrouched.IsPlayingOn(ped)) PlayAnimation(Anims.WalkUnarmedCrouched);
                }

                else
                {
                    // armed crouch walk
                    if (!Anims.WalkStealthASMG.IsPlayingOn(ped)) PlayAnimation(Anims.WalkStealthASMG);
                }
            }

            else if (pTask == ActiveTask.IdleCrouch)
            {
                if (mFlags.HasFlag(ClientFlags.Running) ||
                    mFlags.HasFlag(ClientFlags.Sprinting))
                {
                    if (!Anims.QuickStop2.IsPlayingOn(ped))
                    {
                        PlayAnimation(Anims.QuickStop2);
                    }
                }

                else if (mFlags.HasFlag(ClientFlags.Walking))
                {
                    if (!Anims.QuickStop1.IsPlayingOn(ped))
                    {
                        PlayAnimation(Anims.QuickStop1);
                    }
                }

                else
                ped.Task.ClearAll();
            }

            else
            {
                if (!currentAnimation.IsPlayingOn(ped))
                    PlayAnimation(currentAnimation);

                if (upperAnimation != null && !upperAnimation.IsPlayingOn(ped))
                {
                    PlayAnimation(upperAnimation, 33);
                }
            }

            if (FlagIsSet(ClientFlags.Shooting)) HandleShootAnim();

            else if (FlagIsSet(ClientFlags.Aiming)) HandleAimAnim();

            /*      #region stealth

                  if ((pStance == PlayerStance.IdleCrouch || pStance == PlayerStance.MovingCrouched) &&
                      !Function.Call<bool>(Hash.GET_PED_STEALTH_MOVEMENT, ped.Handle))
                  {
                      Function.Call(Hash.SET_PED_STEALTH_MOVEMENT, ped.Handle, 1, 0);
                  }

                  else if ((pStance == PlayerStance.Idle || pStance == PlayerStance.Moving) &&
                      Function.Call<bool>(Hash.GET_PED_STEALTH_MOVEMENT, ped.Handle))
                  {
                      Function.Call(Hash.SET_PED_STEALTH_MOVEMENT, ped.Handle, 0, 0);
                  }

                  #endregion*/


            /*

                        #region running
                        // running
                        if (FlagIsSet(MovementFlags.Running))
                        {
                            running = true;
                            stopped = false;

                            // punch
                            if (FlagIsSet(MovementFlags.Punch))
                            {
                                if (Anims.PunchWalkingNoTarget))
                                    PlayAnimation(Anims.PunchWalkingNoTarget);
                            }

                            // use locally referenced anim set based on current weapon
                            if (animSet != null)
                                PlayMovementAnimation(new Animation(animSet, "run"));
                            else
                                // no specfic animation found. use generic
                                anim = Anims.Run;

                            if (anim))
                                PlayAnimation(anim);

                            if (FlagIsSet(MovementFlags.Shooting)) HandleShootAnim();

                            else if (FlagIsSet(MovementFlags.Aiming)) HandleAimAnim();
                        }
                        #endregion

                        #region sprinting
                        // sprinting
                        else if (FlagIsSet(MovementFlags.Sprinting))
                        {
                            running = true;
                            stopped = false;

                            #region combat roll
                            if (pStance == PlayerStance.CombatRoll)
                            {
                                if (!Anims.Roll90Deg.IsPlayingOn(ped))
                                    Anims.Roll90Deg.StartAnimation(ped);
                            }
                            #endregion

                            #region default
                            // use locally referenced anim set based on current weapon
                            else if (animSet != null)
                                anim = new Animation(animSet, Anims.Sprint.Name);
                            else
                                // no specfic animation found. use generic
                                anim = Anims.Sprint;

                            if (anim))
                                PlayAnimation(anim);
                            #endregion

                            #region weapon

                            if (FlagIsSet(MovementFlags.Shooting)) HandleShootAnim();

                            else if (FlagIsSet(MovementFlags.Aiming)) HandleAimAnim();
                            #endregion
                        }
                        #endregion

                        #region walking
                        // walking
                        else if (FlagIsSet(MovementFlags.Walking))
                        {
                            running = false;
                            stopped = false;

                            if (FlagIsSet(MovementFlags.Aiming))
                            {
                                var aimPos = Helpers.SmoothStep(lastAimPos, ped.Position + Helpers.RotationToDirection(pAngles) * 10, 0.5f);

                                Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, ped.Handle,
                                    pPosition.X,
                                    pPosition.Y,
                                    pPosition.Z,
                                    aimPos.X,
                                    aimPos.Y,
                                    aimPos.Z,
                                    2f,
                                    false,
                                    2f,
                                    0.5f,
                                    true,
                                    0,
                                    0,
                                    Function.Call<int>(Hash.GET_HASH_KEY, "firing_pattern_full_auto")
                                    );
                            }

                            else
                            {

                                #region crouched
                                if (pStance == PlayerStance.MovingCrouched || pStance == PlayerStance.IdleCrouch)
                                {
                                    if (ped.Weapons.Current.Hash == WeaponHash.Unarmed ||
                                        ped.Weapons.Current.IsMeleeOrThrowable())
                                    {
                                        // unarmed crouch walk

                                        if (Anims.WalkUnarmedCrouched))
                                            PlayAnimation(Anims.WalkUnarmedCrouched);
                                    }

                                    else
                                    {
                                        // armed crouch walk

                                        if (Anims.WalkStealthASMG))
                                            PlayAnimation(Anims.WalkStealthASMG);
                                    }
                                }
                                #endregion

                                #region default

                                else
                                {
                                    if (ped.Weapons.Current.Hash == WeaponHash.Unarmed ||
                                        ped.Weapons.Current.IsMeleeOrThrowable())
                                    {
                                        // unarmed walk

                                        // punch
                                        if (FlagIsSet(MovementFlags.Punch))
                                        {
                                            if (Anims.PunchWalkingNoTarget))
                                                PlayAnimation(Anims.PunchWalkingNoTarget);
                                        }

                                        else if (Anims.Walk))
                                            PlayAnimation(Anims.Walk);
                                    }

                                    else if (ped.Weapons.Current.Hash == WeaponHash.Minigun)
                                    {
                                        if (Anims.WalkMingun))
                                        {
                                            PlayAnimation(Anims.WalkMingun);
                                        }
                                    }

                                    else
                                    {

                                          // use locally referenced anim set based on current weapon
                                            if (animSet != null)
                                            anim = new Animation(animSet, Anims.Walk.Name);
                                        else
                                            // no specfic animation found. use generic
                                            anim = Anims.Walk;

                                        if (anim))
                                            PlayAnimation(anim);
                                    }
                                }


                                #endregion

                                #region weapon

                                if (FlagIsSet(MovementFlags.Shooting)) HandleShootAnim();

                                else if (FlagIsSet(MovementFlags.Aiming)) HandleAimAnim();

                                #endregion
                            }
                        }
                        #endregion

                        #region idle
                        // stopped
                        else if (FlagIsSet(MovementFlags.Stopped))
                        {
                            if (Anims.QuickStop1) && !stopped)
                            {
                                if (running)
                                {
                                    PlayAnimation(Anims.QuickStop2);
                                }

                                else
                                {
                                    PlayAnimation(Anims.QuickStop1);
                                }

                                stopped = true;
                            }


                            if (FlagIsSet(MovementFlags.Shooting)) HandleShootAnim();

                            else if (FlagIsSet(MovementFlags.Aiming)) HandleAimAnim();


                            // punch
                            if (FlagIsSet(MovementFlags.Punch))
                            {
                                if (Anims.PunchIdle))
                                    PlayAnimation(Anims.PunchIdle);
                            }

                            else
                            {
                                ped.Task.ClearAll();
                            }
                        }

                        #endregion*/


        }

        public Animation AnimationFromFlags(ClientFlags flags)
        {
            if (FlagIsSet(ClientFlags.Stopped))
            {
                if (FlagIsSet(ClientFlags.Punch))
                    return Anims.PunchIdle;
                else
                    return new Animation("move_m@generic", "idle");
            }
            if (FlagIsSet(ClientFlags.Walking))
            {
                if (FlagIsSet(ClientFlags.Punch))
                    return Anims.PunchWalkingNoTarget;
                else
                    return new Animation("move_m@generic", "walk");
            }
            if (FlagIsSet(ClientFlags.Running))
            {
                if (FlagIsSet(ClientFlags.Punch))
                    return Anims.PunchRunning;
                else
                {
                    if (mFlags.HasFlag(ClientFlags.Walking))
                        return Anims.Walk2Run;
                    else
                    return new Animation("move_m@generic", "run");
                }
            }
            if (FlagIsSet(ClientFlags.Sprinting))
            {
                if (FlagIsSet(ClientFlags.Punch))
                    return Anims.PunchRunning;
                else
                    return new Animation("move_m@generic", "sprint");
            }

            else return null;
        }

        public bool FlagIsSet(ClientFlags flag)
        {
            return (mFlags & flag) != 0;
        }
    }
}