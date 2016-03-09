#include "MemoryAccess.h"
#include <Windows.h>
#include <psapi.h>

static MemoryAccess::MemoryAccess()
{
	uintptr_t address;

	address = FindPattern("\x33\xFF\xE8\x00\x00\x00\x00\x48\x85\xC0\x74\x58", "xxx????xxxxx");
	GetAddressOfEntity = reinterpret_cast<uintptr_t(*)(int)>(*reinterpret_cast<int *>(address + 3) + address + 7);
	address = FindPattern("\xB2\x01\xE8\x00\x00\x00\x00\x33\xC9\x48\x85\xC0\x74\x3B", "xxx????xxxxxxx");
	GetAddressOfPlayer = reinterpret_cast<uintptr_t(*)(int)>(*reinterpret_cast<int *>(address + 3) + address + 7);

	address = FindPattern("\x44\x89\x71\x44\xF3\x0F\x11\x75\x47", "xxxxxxx??");

	if (address) {
		memset((void *)address, 0x90, 4);
	}
}

System::UInt64 MemoryAccess::GetPlayerAddress(int handle)
{
	return GetAddressOfPlayer(handle);
}

System::UInt64 MemoryAccess::GetEntityAddress(int handle)
{
	return GetAddressOfEntity(handle);
}

float MemoryAccess::GetPedWeaponDamage(int handle)
{
	System::UInt64 entity = MemoryAccess::GetEntityAddress(handle);
	System::UInt64 weaponsPtr = *reinterpret_cast<unsigned long long *>(entity + 0x1098);
	System::UInt64 currentWeapon = *reinterpret_cast<unsigned long long *>(weaponsPtr + 0x20);
	return *reinterpret_cast<float *>(currentWeapon + 0x98);
}


float MemoryAccess::GetWheelRotation(unsigned long long address, int index)
{
	unsigned long long wAddress = *reinterpret_cast<unsigned long long *>(address + index * 8);

	return *reinterpret_cast<float *>(wAddress + 0x164);
}

void MemoryAccess::SetWheelRotation(unsigned long long address, int index, float value) {

	unsigned long long wAddress = *reinterpret_cast<unsigned long long *>(address + index * 8);
	*reinterpret_cast<float *>(wAddress + 0x164) = value;
}

void MemoryAccess::WriteSingle(unsigned long long address, float value)
{
	*reinterpret_cast<float *>(address) = value;
}

void MemoryAccess::WriteInt16(unsigned long long address, short value)
{
	*reinterpret_cast<short *>(address) = value;
}

void MemoryAccess::WriteInt32(unsigned long long address, int value)
{
	*reinterpret_cast<int *>(address) = value;
}

void MemoryAccess::WriteInt64(unsigned long long address, long value)
{
	*reinterpret_cast<long *>(address) = value;
}

void MemoryAccess::WriteUInt16(unsigned long long address, unsigned short value)
{
	*reinterpret_cast<unsigned short *>(address) = value;
}

void MemoryAccess::WriteUInt32(unsigned long long address, unsigned int value)
{
	*reinterpret_cast<unsigned int *>(address) = value;
}

void MemoryAccess::WriteUInt64(unsigned long long address, unsigned long value)
{
	*reinterpret_cast<unsigned long *>(address) = value;
}

float MemoryAccess::ReadSingle(unsigned long long address)
{
	return *reinterpret_cast<const float *>(address);
}

short MemoryAccess::ReadInt16(unsigned long long address)
{
	return *reinterpret_cast<const short *>(address);
}

int MemoryAccess::ReadInt32(unsigned long long address)
{
	return *reinterpret_cast<const int *>(address);
}

long long MemoryAccess::ReadInt64(unsigned long long address)
{
	return *reinterpret_cast<const long *>(address);
}

unsigned short MemoryAccess::ReadUInt16(unsigned long long address)
{
	return *reinterpret_cast<const unsigned short *>(address);
}

unsigned int MemoryAccess::ReadUInt32(unsigned long long address)
{
	return *reinterpret_cast<const unsigned int *>(address);
}

unsigned long long MemoryAccess::ReadUInt64(unsigned long long address)
{
	return *reinterpret_cast<const unsigned long *>(address);
}

uintptr_t MemoryAccess::FindPattern(const char *pattern, const char *mask)
{
	MODULEINFO module = {};
	GetModuleInformation(GetCurrentProcess(), GetModuleHandle(nullptr), &module, sizeof(MODULEINFO));

	const char *address = reinterpret_cast<const char *>(module.lpBaseOfDll), *address_end = address + module.SizeOfImage;
	const size_t mask_length = static_cast<size_t>(strlen(mask) - 1);

	for (size_t i = 0; address < address_end; address++)
	{
		if (*address == pattern[i] || mask[i] == '?')
		{
			if (mask[i + 1] == '\0')
			{
				return reinterpret_cast<uintptr_t>(address) - mask_length;
			}

			i++;
		}
		else
		{
			i = 0;
		}
	}

	return 0;
}
