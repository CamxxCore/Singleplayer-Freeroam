using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Native;

namespace SPFClient
{
    public static class VehicleExtensions
    {
        public static ushort GetGearCurr(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7A2 : 0x792);

            return (ushort)(address == 0 ? 0 : MemoryAccess.ReadUInt16(address + offset));
        }

        public static ushort GetGearNext(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7A0 : 0x790);

            return (ushort)(address == 0 ? 0 : MemoryAccess.ReadUInt16(address + offset));
        }

        public static uint GetGears(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7A0 : 0x790);

            return (uint)(address == 0 ? 0 : MemoryAccess.ReadUInt32(address + offset));
        }

        public static void SetGears(this Vehicle vehicle, uint value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7A0 : 0x790);

            MemoryAccess.WriteUInt32(address + offset, value);
        }

        public static void SetGearCurr(this Vehicle vehicle, ushort value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7A0 : 0x790);

            MemoryAccess.WriteUInt16(address + offset, value);
        }

        public static void SetGearNext(this Vehicle vehicle, ushort value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7A2 : 0x792);

            MemoryAccess.WriteUInt16(address + offset, value);
        }

        public static uint GetTopGear(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7A6 : 0x796);

            return address == 0 ? 0 : MemoryAccess.ReadUInt32(address + offset);
        }

        public static float GetCurrentRPM(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7D4 : 0x7C4);

            return address == 0 ? 0 : MemoryAccess.ReadSingle(address + offset);
        }

        public static void SetCurrentRPM(this Vehicle vehicle, float value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7D4 : 0x7C4);

            MemoryAccess.WriteSingle(address + offset, value);
        }

        public static float GetClutch(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7E0 : 0x7D0);

            return address == 0 ? 0 : MemoryAccess.ReadSingle(address + offset);
        }

        public static void SetClutch(this Vehicle vehicle, float value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7E0 : 0x7D0);

            MemoryAccess.WriteSingle(address + offset, value);
        }

        public static float GetTurbo(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7F8 : 0x7D8);

            return address == 0 ? 0 : MemoryAccess.ReadSingle(address + offset);
        }

        public static void SetTurbo(this Vehicle vehicle, float value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7F8 : 0x7D8);

            MemoryAccess.WriteSingle(address + offset, value);
        }

        public static float GetThrottle(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7E4 : 0x7D4);

            return address == 0 ? 0 : MemoryAccess.ReadSingle(address + offset);
        }

        public static void SetThrottle(this Vehicle vehicle, float value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x7E4 : 0x7D4);

            MemoryAccess.WriteSingle(address + offset, value);
        }

        public static float GetThrottleP(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x8B4 : 0x8A4);

            return address == 0 ? 0 : MemoryAccess.ReadSingle(address + offset);
        }

        public static void SetThrottleP(this Vehicle vehicle, float value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x8B4 : 0x8A4);

            MemoryAccess.WriteSingle(address + offset, value);
        }

        public static float GetBrakeP(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x8B8 : 0x8A8);

            return address == 0 ? 0 : MemoryAccess.ReadSingle(address + offset);
        }

        public static void SetBrakeP(this Vehicle vehicle, float value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x8B8 : 0x8A8);

            MemoryAccess.WriteSingle(address + offset, value);
        }

        public static float GetFuelLevel(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x768 : 0x758);

            return address == 0 ? 0 : MemoryAccess.ReadSingle(address + offset);
        }

        public static void SetFuelLevel(this Vehicle vehicle, float value)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0x768 : 0x758);

            MemoryAccess.WriteSingle(address + offset, value);
        }

        public static ulong GetWheelsPtr(this Vehicle vehicle)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0xAA0 : 0xA80);

            return address == 0 ? 0 : MemoryAccess.ReadUInt64(address + offset);
        }

        public static ulong GetWheelPtr(ulong address, int index)
        {
            return MemoryAccess.ReadUInt64(address + (uint)index * 8);
        }

        public static void SetWheelsHealth(this Vehicle vehicle, float health)
        {
            ulong address = MemoryAccess.GetEntityAddress(vehicle.Handle);

            var offset = (ushort)((int)Game.Version > 3 ? 0xAA0 : 0xA80);

            ulong wheelPtr;
            wheelPtr = MemoryAccess.ReadUInt64(address + offset);

            ulong[] wheels = new ulong[6];

            for (uint i = 0; i < 6; i++)
            {
                wheels[i] = MemoryAccess.ReadUInt64(wheelPtr + 0x008 * i);
                if (wheels[i] != 0)
                {
                    MemoryAccess.WriteSingle(wheels[i] + (ushort)((int)Game.Version > 3 ? 0x1E0 : 0x1D0), health);
                }
            }
        }
    }
}
