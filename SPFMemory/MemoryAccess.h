#pragma once

public ref class MemoryAccess abstract sealed
{
public:
	static MemoryAccess();

	static System::UInt64 GetPlayerAddress(int handle);
	static System::UInt64 GetEntityAddress(int handle);

	static bool SnowEnabled;

	static void SetSnowEnabled(bool enabled);

	static float GetPedWeaponDamage(int handle);

	static float GetWheelRotation(unsigned long long address, int index);

	static void SetWheelRotation(unsigned long long address, int index, float value);

	static float ReadSingle(unsigned long long address);

	static short ReadInt16(unsigned long long address);
	static int ReadInt32(unsigned long long address);
	static long long ReadInt64(unsigned long long address);

	static unsigned short ReadUInt16(unsigned long long address);
	static unsigned int ReadUInt32(unsigned long long address);
	static unsigned long long ReadUInt64(unsigned long long address);

	static System::Byte ReadByte(unsigned long long address);

	static void WriteSingle(unsigned long long address, float value);

	static void WriteInt16(unsigned long long address, short value);
	static void WriteInt32(unsigned long long address, int value);
	static void WriteInt64(unsigned long long address, long value);

	static void WriteUInt16(unsigned long long address, unsigned short value);
	static void WriteUInt32(unsigned long long address, unsigned int value);
	static void WriteUInt64(unsigned long long address, unsigned long value);
	static void WriteByte(unsigned long long address, System::Byte value);

private:
	static System::UInt64(*GetAddressOfEntity)(int handle);
	static System::UInt64(*GetAddressOfPlayer)(int handle);

	static System::UInt64 SnowAddress;

	static System::UInt64 FindPattern(const char *pattern, const char *mask);
};

