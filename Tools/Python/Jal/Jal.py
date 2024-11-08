import argparse

def generate_jal_opcode(pc, target_address, rd=1):
    # Calculate the offset (immediate value) as the difference between target address and current PC
    offset = target_address - pc
    
    # Check if the offset is within the valid range (-2^20 to 2^20-1)
    if offset < -(1 << 20) or offset >= (1 << 20):
        raise ValueError("Offset out of range for JAL instruction")
    
    # Decompose the offset into fields
    imm_20 = (offset >> 20) & 0x1
    imm_10_1 = (offset >> 1) & 0x3FF
    imm_11 = (offset >> 11) & 0x1
    imm_19_12 = (offset >> 12) & 0xFF
    
    # Combine the fields to generate the opcode
    opcode = (imm_20 << 31) | (imm_19_12 << 12) | (imm_11 << 20) | (imm_10_1 << 21) | (rd << 7) | 0x6F
    
    return opcode

def parse_jal_opcode(opcode):
    # Extract each field
    imm_20 = (opcode >> 31) & 0x1
    imm_10_1 = (opcode >> 21) & 0x3FF
    imm_11 = (opcode >> 20) & 0x1
    imm_19_12 = (opcode >> 12) & 0xFF
    
    # Combine into a 21-bit immediate value
    imm = (imm_20 << 20) | (imm_19_12 << 12) | (imm_11 << 11) | (imm_10_1 << 1)
    print(f"Opcode Immediate hex value: {imm:x}")

    # Sign extension
    if imm_20 == 1:
        imm -= (1 << 21)
    
    return imm

def main():
    # Create argument parser
    parser = argparse.ArgumentParser(description="RISC-V JAL opcode generator and parser.")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("-e", "--encode", action="store_true", help="Generate a RISC-V JAL opcode.")
    group.add_argument("-d", "--decode", action="store_true", help="Parse a RISC-V JAL opcode.")
    parser.add_argument("pc_or_opcode", type=lambda x: int(x, 16), help="The current PC value or the JAL opcode in hexadecimal format.")
    parser.add_argument("target_address", type=lambda x: int(x, 16), nargs='?', help="The target address in hexadecimal format (required for encoding).")
    
    # Parse command line arguments
    args = parser.parse_args()
    
    if args.encode:
        if args.target_address is None:
            parser.error("The target address is required for encoding.")
        # Generate JAL opcode
        opcode = generate_jal_opcode(args.pc_or_opcode, args.target_address)
        print(f"Generated JAL opcode: 0x{opcode:08X}")
    elif args.decode:
        # Parse immediate value
        imm_value = parse_jal_opcode(args.pc_or_opcode)
        print(f"Immediate value: {imm_value}")

if __name__ == "__main__":
    main()