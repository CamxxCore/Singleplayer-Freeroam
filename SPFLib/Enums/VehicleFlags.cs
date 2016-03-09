using System;

namespace SPFLib.Enums
{
    [Flags]
    public enum VehicleFlags
    {     
        Exploded = 1,
        DoorsLocked = 4,
        Driver = 8,
        PlaneShoot = 16,
        PlaneGun = 32
    }
}
