using System;

namespace SPFLib.Enums
{
    [Flags]
    public enum VDamageFlags
    {
        LWindowBroken = 1,
        RWindowBroken = 2,
        BLWindowBroken = 4,
        BRWindowBroken = 8,
        LDoorBroken = 16,
        RDoorBroken = 32,
        BLDoorBroken = 64,
        BRDoorBroken = 128,
        LHeadlightBroken = 256,
        RHeadlightBroken = 512,
        HoodBroken = 1024
    }
}
