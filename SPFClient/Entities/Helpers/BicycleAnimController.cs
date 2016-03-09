﻿using SPFClient.Types;
using GTA.Native;
using GTA;

namespace SPFClient.Entities
{
    public sealed class BicyleAnimController
    {
        private class Anims
        {
            #region road front

            public static readonly Animation TuckFreewheel = new Animation("veh@bicycle@roadfront@base", "tuck_freewheel_char");

            public static readonly Animation TuckPedal = new Animation("veh@bicycle@roadfront@base", "tuck_pedal_char");

            public static readonly Animation CruiseFreewheel = new Animation("veh@bicycle@roadfront@base", "cruise_freewheel_char");   


       //     public static readonly Animation WheeliePedal = new Animation("veh@bicycle@cruiserfront@base", "wheelie_pedal_char");

            public static readonly Animation CruisePedal = new Animation("veh@bicycle@roadfront@base", "cruise_pedal_char");


      //      public static readonly Animation PunchWalkingNoTarget = new Animation("melee@unarmed@streamed_core", "walking_punch_no_target");

          //  public static readonly Animation SkydiveJump = new Animation("move_jump", "jump_launch_l_to_skydive");

          //  public static readonly Animation Skydive = new Animation("skydive@freefall", "free_right");

            #endregion
        }
       
        private BicycleState currentState;
        private Animation currentAnimation;
        private Ped ped;

        public BicyleAnimController(Ped ped)
        {
            this.ped = ped;
        }

        public void SetBicycleState(BicycleState state)
        {
            currentState = state;
            currentAnimation = AnimationFromState(state);
        }

        private void PlayAnimation(Animation animation, int flags = 1)
        {
            Function.Call(Hash.TASK_PLAY_ANIM, ped.Handle,
             animation.Dictionary, animation.Name, 8f, -4.0f, -1, flags, 0, 0, 0, 0);
        }


        public void StopAllAnimations()
        {
            ped.Task.ClearAll();
        }

        public void Update()
        {
            if (currentAnimation == null || ped.IsRagdoll)
                return;
            if (!currentAnimation.IsPlayingOn(ped))
                PlayAnimation(currentAnimation);
        }

        public Animation AnimationFromState(BicycleState state)
        {
            switch (currentState)
            {
                case BicycleState.Cruising:
                    return Anims.CruiseFreewheel;
                case BicycleState.TuckCruising:
                    return Anims.TuckFreewheel;
                case BicycleState.Pedaling:
                    return Anims.CruisePedal;
                case BicycleState.TuckPedaling:
                    return Anims.TuckPedal;                  
                default: return null;
            }
        }
    }
}
