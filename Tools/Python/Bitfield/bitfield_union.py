#!/usr/bin/env python


class BitfieldUnion:
    def __init__(self, fields):
        self.fields = fields
        self.data = 0

    def get_field(self, field_name):
        start, end = self.fields[field_name]
        mask = (1 << (end - start + 1)) - 1
        shifted_value = self.data >> start
        return shifted_value & mask

    def set_field(self, field_name, value):
        start, end = self.fields[field_name]
        mask = (1 << (end - start + 1)) - 1
        cleared_data = self.data & ~(mask << start)
        shifted_value = value << start
        self.data = cleared_data | shifted_value

    def get_value(self):
        return self.data

    def set_value(self, value):
        self.data = value

    def value(self, **kwargs):
        for field_name, value in kwargs.items():
            self.set_field(field_name, value)

    def __getitem__(self, field_name):
        return self.get_field(field_name)

    def __setitem__(self, field_name, value):
        self.set_field(field_name, value)

    def __str__(self):
        return f"BitfieldUnion:\n" + "\n".join(
            f"  {field_name}: {hex(self.get_field(field_name))}"
            for field_name in self.fields
        )


'''
from bitfield_union import BitfieldUnion

# Define fields for MSR_IA32_APIC_BASE_REGISTER
msr_fields = {
    "Reserved1": (0, 7),
    "BSP": (8, 8),
    "Reserved2": (9, 9),
    "EXTD": (10, 10),
    "EN": (11, 11),
    "ApicBase": (12, 31),
    "ApicBaseHi": (32, 63),
}

# Create an instance of BitfieldUnion
msr = BitfieldUnion(msr_fields)

# Set multiple fields at once
msr.value(BSP=1, ApicBase=0x32)

# Print the register contents
print(msr)

'''
