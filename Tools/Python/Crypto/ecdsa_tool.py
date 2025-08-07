#!/usr/bin/env python3

import sys
import argparse
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature, encode_dss_signature

"""
ECDSA-384:
# Generate ECDSA-384 key pair
openssl ecparam -genkey -name secp384r1 -noout -out private_key.pem
openssl ec -in private_key.pem -pubout -out public_key.pem

# Hash the file using SHA-384 and generate a signature
openssl dgst -sha384 -sign private_key.pem -out signature.bin example.txt

# Verify the signature ((DER encoded))
openssl dgst -sha384 -verify public_key.pem -signature signature.bin example.txt
"""

def sign(private_key_path, data_path):
    # Load private key
    with open(private_key_path, "rb") as key_file:
        private_key = serialization.load_pem_private_key(key_file.read(), password=None)

    # Load data
    with open(data_path, "rb") as data_file:
        data = data_file.read()

    # Sign data
    signature = private_key.sign(data, ec.ECDSA(hashes.SHA384()))

    # Save the signature (DER encoded) to SIGNATURE.bin. It's used for verification by OpenSSL
    with open("SIGNATURE.bin", "wb") as sig_file:
        sig_file.write(signature)

    # Decode signature to R/S format
    r, s = decode_dss_signature(signature)
    r_bytes = r.to_bytes(48, byteorder='big')
    s_bytes = s.to_bytes(48, byteorder='big')

    # Write signature and data to .sig file
    with open(data_path + ".sig", "wb") as sig_file:
        sig_file.write(r_bytes + s_bytes + data)

def verify(public_key_path, signed_data_path):
    # Load public key
    with open(public_key_path, "rb") as key_file:
        public_key = serialization.load_pem_public_key(key_file.read())

    # Load signed data
    with open(signed_data_path, "rb") as signed_file:
        signed_data = signed_file.read()

    # Extract signature and data
    r_bytes = signed_data[:48]
    s_bytes = signed_data[48:96]
    data = signed_data[96:]

    r = int.from_bytes(r_bytes, byteorder='big')
    s = int.from_bytes(s_bytes, byteorder='big')
    signature = encode_dss_signature(r, s)

    # Verify signature
    try:
        public_key.verify(signature, data, ec.ECDSA(hashes.SHA384()))
        print("Signature is valid.")
    except Exception as e:
        print("Signature is invalid:", e)

def main():
    parser = argparse.ArgumentParser(description="ECDSA384/SHA384 signing and verification tool")
    parser.add_argument("-s", "--sign", nargs=2, metavar=("PRIVATE_KEY", "DATA"), help="Sign data with ECDSA384 private key")
    parser.add_argument("-v", "--verify", nargs=2, metavar=("PUBLIC_KEY", "SIGNED_DATA"), help="Verify signed data with ECDSA384 public key")

    args = parser.parse_args()

    if args.sign:
        sign(args.sign[0], args.sign[1])
    elif args.verify:
        verify(args.verify[0], args.verify[1])
    else:
        parser.print_help()

if __name__ == "__main__":
    main()
