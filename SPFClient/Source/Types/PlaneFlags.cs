using System;

namespace SPFClient.Types
{
    [Flags]
    public enum PlaneFlags
    {
        Flares = 1,
        Ext = 2,
        Shoot = 4,
        LGOpen = 8,
        LGClose = 16,
        FireRocket = 32,
        FireCannon = 64
    }
}
