#!/usr/bin/env python3

import os
import sys
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives import padding

# Default AES key string (should be a 32-byte hexadecimal string)
DEFAULT_AES_KEY_STR = '37ec4cf39c83ebc5d2f9f3bee135983a1399a0dfb5f99f977d147162845e2945'

def encrypt_file(input_filename):
    # Try to get AES key from environment variable, fallback to default if not set
    aes_key_str = os.getenv('AES_KEY', DEFAULT_AES_KEY_STR)
    aes_key = bytes.fromhex(aes_key_str)

    # Generate a random IV (16 bytes)
    iv = os.urandom(16)

    # Read the data from the file to be encrypted
    with open(input_filename, 'rb') as input_file:
        plaintext = input_file.read()

    # Apply PKCS#7 padding
    padder = padding.PKCS7(algorithms.AES.block_size).padder()
    padded_data = padder.update(plaintext) + padder.finalize()

    # Create AES encryptor
    cipher = Cipher(algorithms.AES(aes_key), modes.CBC(iv), backend=default_backend())
    encryptor = cipher.encryptor()

    # Encrypt the data
    ciphertext = encryptor.update(padded_data) + encryptor.finalize()

    # Generate the output file name
    base_name = os.path.basename(input_filename)
    output_filename = os.path.join(os.path.dirname(input_filename), base_name + '.enc')

    # Write the IV and encrypted data to the output file
    with open(output_filename, 'wb') as output_file:
        output_file.write(iv + ciphertext)

    print(f"Encryption complete. Encrypted data saved to {output_filename}.")

def main():
    if len(sys.argv) != 2:
        print("Usage: python aes_enc.py <input_file>")
        sys.exit(1)

    input_filename = sys.argv[1]
    encrypt_file(input_filename)

if __name__ == "__main__":
    main()

