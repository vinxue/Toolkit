/** @file
  Utility functions to generate the 32-bit CRC based on ITU-T V.42.

  Copyright (c) 2024, Gavin Xue. All rights reserved.<BR>
  SPDX-License-Identifier: BSD-2-Clause

**/

#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>

uint32_t crc32_table[256];

// Precompute the CRC32 table
void init_crc32_table() {
    for (uint32_t i = 0; i < 256; i++) {
        uint32_t crc = i;
        for (uint32_t j = 0; j < 8; j++) {
            if (crc & 1) {
                crc = (crc >> 1) ^ 0xEDB88320;
            } else {
                crc >>= 1;
            }
        }
        crc32_table[i] = crc;
    }
}

// Compute the CRC32 checksum
uint32_t crc32(const uint8_t *data, size_t length, uint32_t crc) {
    for (size_t i = 0; i < length; i++) {
        uint8_t byte = data[i];
        crc = (crc >> 8) ^ crc32_table[(crc ^ byte) & 0xFF];
    }
    return crc;
}

int main(int argc, char *argv[]) {
    if (argc != 2) {
        fprintf(stderr, "Usage: %s <filename>\n", argv[0]);
        return EXIT_FAILURE;
    }

    // Initialize the CRC32 table
    init_crc32_table();

    // Open the file
    FILE *file = fopen(argv[1], "rb");
    if (!file) {
        perror("fopen");
        return EXIT_FAILURE;
    }

    // Read the file content and compute the CRC32 checksum
    uint8_t buffer[1024];
    size_t bytes_read;
    uint32_t crc_value = 0xFFFFFFFF;

    while ((bytes_read = fread(buffer, 1, sizeof(buffer), file)) > 0) {
        crc_value = crc32(buffer, bytes_read, crc_value);
    }

    // Close the file
    fclose(file);

    // Finalize the CRC value by inverting all the bits
    crc_value ^= 0xFFFFFFFF;

    // Print the CRC32 checksum
    printf("CRC32: %08X\n", crc_value);

    return EXIT_SUCCESS;
}
