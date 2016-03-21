using System;

namespace SPFLib.Enums
{
    [Flags]
    public enum DamageFlags
    {
        LWindow = 1,
        RWindow = 2,
        BLWindow = 4,
        BRWindow = 8,
        LDoor = 16,
        RDoor = 32,
        BLDoor = 64,
        BRDoor = 128,
        LHeadlight = 256,
        RHeadlight = 512,
        Hood = 1024,
        LRearHeadlight = 2046,
        RRearHeadlight = 4092
    }
}
