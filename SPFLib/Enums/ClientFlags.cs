using System;

namespace SPFLib.Enums
{
    /// <summary>
    /// User command flags.
    /// </summary>
    [Flags]
    public enum ClientFlags
    {
        Aiming = 1,
        Shooting = 2,
        Running = 4,
        Sprinting = 8,
        Walking = 16,
        Stopped = 32,
        Jumping = 64,
        Diving = 128,
        Punch = 256,
        Ragdoll = 512,
        Dead = 1024,
        Reloading = 2048,
        Climbing = 4096,
        HasParachute = 8192,
        ParachuteOpen = 16384
    }

}
