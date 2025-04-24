#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <mbedtls/aes.h>

void aes_decrypt_file(const char *filename) {
    // AES key (32 bytes for AES-256)
    unsigned char key[32] = {
        0x37, 0xec, 0x4c, 0xf3, 0x9c, 0x83, 0xeb, 0xc5,
        0xd2, 0xf9, 0xf3, 0xbe, 0xe1, 0x35, 0x98, 0x3a,
        0x13, 0x99, 0xa0, 0xdf, 0xb5, 0xf9, 0x9f, 0x97,
        0x7d, 0x14, 0x71, 0x62, 0x84, 0x5e, 0x29, 0x45
    };

    // Open the binary file
    FILE *file = fopen(filename, "rb");
    if (!file) {
        perror("Failed to open file");
        return;
    }

    // Read IV (first 16 bytes)
    unsigned char iv[16];
    if (fread(iv, 1, 16, file) != 16) {
        perror("Failed to read IV");
        fclose(file);
        return;
    }

    // Determine the size of the encrypted data
    fseek(file, 0, SEEK_END);
    long file_size = ftell(file);
    fseek(file, 16, SEEK_SET); // Move back to the start of encrypted data

    size_t encrypted_data_len = file_size - 16;
    unsigned char *encrypted_data = malloc(encrypted_data_len);
    if (!encrypted_data) {
        perror("Failed to allocate memory for encrypted data");
        fclose(file);
        return;
    }

    // Read encrypted data
    if (fread(encrypted_data, 1, encrypted_data_len, file) != encrypted_data_len) {
        perror("Failed to read encrypted data");
        free(encrypted_data);
        fclose(file);
        return;
    }

    fclose(file);

    // Buffer for decrypted data
    unsigned char *decrypted_data = malloc(encrypted_data_len);
    if (!decrypted_data) {
        perror("Failed to allocate memory for decrypted data");
        free(encrypted_data);
        return;
    }

    // Initialize AES context
    mbedtls_aes_context aes;
    mbedtls_aes_init(&aes);

    // Set AES key for decryption
    mbedtls_aes_setkey_dec(&aes, key, 256); // 256 for AES-256

    // Perform AES decryption
    int ret = mbedtls_aes_crypt_cbc(&aes, MBEDTLS_AES_DECRYPT, encrypted_data_len, iv, encrypted_data, decrypted_data);
    if (ret != 0) {
        printf("AES decryption failed\n");
        mbedtls_aes_free(&aes);
        free(encrypted_data);
        free(decrypted_data);
        return;
    }

    // Create output file name with .dec extension
    char *output_filename = malloc(strlen(filename) + 5); // +5 for ".dec" and null terminator
    if (!output_filename) {
        perror("Failed to allocate memory for output filename");
        mbedtls_aes_free(&aes);
        free(encrypted_data);
        free(decrypted_data);
        return;
    }
    sprintf(output_filename, "%s.dec", filename);

    // Write decrypted data to output file
    FILE *output_file = fopen(output_filename, "wb");
    if (!output_file) {
        perror("Failed to open output file");
        free(output_filename);
        mbedtls_aes_free(&aes);
        free(encrypted_data);
        free(decrypted_data);
        return;
    }
    fwrite(decrypted_data, 1, encrypted_data_len, output_file);
    fclose(output_file);

    printf("Decryption complete. Decrypted data saved to %s\n", output_filename);

    // Free resources
    free(output_filename);
    mbedtls_aes_free(&aes);
    free(encrypted_data);
    free(decrypted_data);
}

int main(int argc, char *argv[]) {
    if (argc != 2) {
        fprintf(stderr, "Usage: %s <filename>\n", argv[0]);
        return EXIT_FAILURE;
    }

    const char *filename = argv[1];
    aes_decrypt_file(filename);
    return EXIT_SUCCESS;
}
