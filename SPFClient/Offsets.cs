using GTA;

namespace SPFClient
{
    public static class Offsets
    {
        public static class CLocalPlayer
        {
            public static readonly ushort Stance = 0x5D0;
            public static readonly ushort CWeaponManager = 0x1098;
        }

        public static class CWeaponManager
        {
            public static readonly ushort LocalWeaponInstance = 0x20;
        }

        public static class CWeaponInfo
        {
            public static readonly ushort Damage = 0x98;
            public static readonly ushort FiringType = 0x3C;
        }

        public static class CVehicle
        {
            public static readonly ushort WheelsPtr = (ushort)((int)Game.Version > 3 ? 2720 : 2688);
            public static readonly ushort RPM = (ushort)((int)Game.Version > 3 ? 2004 : 1988);
            public static readonly ushort CurrentGear = (ushort)((int)Game.Version > 3 ? 0x7A2 : 0x792);
            public static readonly ushort Steering = 2212;
            public static readonly ushort LightDamage = 0x77C;
        }

        public static class CWheel
        {
            public static readonly ushort Rotation = 0x164;
        }
    }
}
