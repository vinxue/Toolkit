#!/usr/bin/env python3

import argparse
import os
import sys
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature

"""
ECDSA-384:
# Generate ECDSA-384 key pair
openssl ecparam -genkey -name secp384r1 -noout -out private_key.pem
openssl ec -in private_key.pem -pubout -out public_key.pem

# Hash the file using SHA-384 and generate a signature
openssl dgst -sha384 -sign private_key.pem -out signature.bin example.txt

# Verify the signature
openssl dgst -sha384 -verify public_key.pem -signature signature.bin example.txt
"""

def format_array(data, name="default_array"):
    lines = []
    for i in range(0, len(data), 16):
        line = ', '.join(f'0x{byte:02x}' for byte in data[i:i+16])
        lines.append(line)
    formatted_array = f"unsigned char {name}[] = {{\n    " + ',\n    '.join(lines) + "\n};"
    return formatted_array

def process_public_key(public_key_path):
    # Derive the output binary file name from the public key file name
    base_name = os.path.splitext(public_key_path)[0]
    output_bin_path = f"{base_name}.bin"

    # Load the public key
    with open(public_key_path, 'rb') as key_file:
        public_key = serialization.load_pem_public_key(key_file.read())

    # Extract the public key numbers (X and Y coordinates)
    public_numbers = public_key.public_numbers()
    x = public_numbers.x
    y = public_numbers.y

    # Convert the X and Y coordinates to bytes
    x_bytes = x.to_bytes(48, byteorder='big')
    y_bytes = y.to_bytes(48, byteorder='big')

    # Combine X and Y bytes
    combined_bytes = x_bytes + y_bytes

    # Save the combined bytes to a binary file
    with open(output_bin_path, 'wb') as binary_file:
        binary_file.write(combined_bytes)

    # Convert the binary data to a C array format with 16 bytes per line
    x_array_formatted = format_array(x_bytes, "public_key_x")
    y_array_formatted = format_array(y_bytes, "public_key_y")
    combined_array_formatted = format_array(combined_bytes, "public_key")

    print(x_array_formatted)
    print()
    print(y_array_formatted)
    print()
    print(combined_array_formatted)

def der_to_rs_signature(der_signature):
    r, s = decode_dss_signature(der_signature)
    r_bytes = r.to_bytes(48, byteorder='big')
    s_bytes = s.to_bytes(48, byteorder='big')
    return r_bytes + s_bytes

def process_signature(der_signature_file):
    if not os.path.isfile(der_signature_file):
        print(f"File {der_signature_file} does not exist.")
        sys.exit(1)

    with open(der_signature_file, 'rb') as f:
        der_signature = f.read()

    rs_signature = der_to_rs_signature(der_signature)

    base_name, ext = os.path.splitext(der_signature_file)
    output_file = f"{base_name}_r_s{ext}"
    with open(output_file, 'wb') as f:
        f.write(rs_signature)

    print(f"r/s signature written to {output_file}")

def header_to_binary(input_file_path):
    with open(input_file_path, 'r') as file:
        hex_data = file.read().strip()

    hex_data = hex_data.replace('0x', '').replace(',', '').replace(' ', '')
    binary_data = bytes.fromhex(hex_data)
    output_file_path = os.path.splitext(input_file_path)[0] + '.bin'
    with open(output_file_path, 'wb') as output_file:
        output_file.write(binary_data)

    print(f"Binary data saved to {output_file_path}")

def binary_to_header(input_file_path):
    with open(input_file_path, 'rb') as file:
        binary_data = file.read()

    formatted_array = format_array(binary_data)
    output_file_path = os.path.splitext(input_file_path)[0] + '.h'
    with open(output_file_path, 'w') as header_file:
        header_file.write(formatted_array)

    print(f"Header file saved to {output_file_path}")
    print(formatted_array)

def main():
    parser = argparse.ArgumentParser(description='Process an EC public key or DER signature.')
    parser.add_argument('-p', '--public_key', type=str, help='Path to the public key PEM file')
    parser.add_argument('-s', '--signature', type=str, help='Path to the DER signature file')
    parser.add_argument('-c', '--header2bin', type=str, help='Path to the C header file')
    parser.add_argument('-b', '--bin2header', type=str, help='Path to the binary file to convert to C header')

    args = parser.parse_args()

    if args.public_key:
        process_public_key(args.public_key)
    elif args.signature:
        process_signature(args.signature)
    elif args.header2bin:
        header_to_binary(args.header2bin)
    elif args.bin2header:
        binary_to_header(args.bin2header)
    else:
        parser.print_help()
        sys.exit(1)

if __name__ == '__main__':
    main()
