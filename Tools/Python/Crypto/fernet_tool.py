#!/usr/bin/env python3

import argparse
from cryptography.fernet import Fernet

def encrypt_hex_string(hex_string):
    hex_bytes = bytes.fromhex(hex_string)
    fernet_key = Fernet.generate_key()
    fernet = Fernet(fernet_key)
    ciphertext = fernet.encrypt(hex_bytes)
    decrypted_bytes = fernet.decrypt(ciphertext)
    decrypted_text = decrypted_bytes.hex()
    if decrypted_text == hex_string:
        print("Hex string encryption and decryption successful.")
    else:
        print("Error: Hex string decryption failed.")
    print(f"Fernet Key: {fernet_key.decode()}")
    print(f"Encrypted data: {ciphertext.decode()}")

def encrypt_string(string):
    plaintext_bytes = string.encode()
    fernet_key = Fernet.generate_key()
    fernet = Fernet(fernet_key)
    ciphertext = fernet.encrypt(plaintext_bytes)
    decrypted_bytes = fernet.decrypt(ciphertext)
    decrypted_text = decrypted_bytes.decode()
    if decrypted_text == string:
        print("String encryption and decryption successful.")
    else:
        print("Error: String decryption failed.")
    print(f"Fernet Key: {fernet_key.decode()}")
    print(f"Encrypted data: {ciphertext.decode()}")

def decrypt_with_key(fernet_key, ciphertext):
    fernet = Fernet(fernet_key)
    decrypted_bytes = fernet.decrypt(ciphertext.encode())
    try:
        decrypted_text = decrypted_bytes.decode()
        print(f"Decrypted text: {decrypted_text}")
    except UnicodeDecodeError:
        decrypted_text = decrypted_bytes.hex()
        print(f"Decrypted hex: {decrypted_text}")

def main():
    parser = argparse.ArgumentParser(description="Encrypt and decrypt using Fernet.")
    parser.add_argument("-x", "--hex", help="Hex string to encrypt and decrypt.")
    parser.add_argument("-s", "--string", help="String to encrypt and decrypt.")
    parser.add_argument("-d", "--decrypt", nargs=2, metavar=("FERNET_KEY", "CIPHERTEXT"), help="Decrypt using Fernet key and ciphertext.")

    args = parser.parse_args()

    if args.hex:
        encrypt_hex_string(args.hex)
    elif args.string:
        encrypt_string(args.string)
    elif args.decrypt:
        decrypt_with_key(args.decrypt[0], args.decrypt[1])
    else:
        parser.print_help()

if __name__ == "__main__":
    main()
