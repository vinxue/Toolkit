/** @file
  Utility functions to generate the 32-bit checksum value.

  Copyright (c) 2024, Gavin Xue. All rights reserved.<BR>
  SPDX-License-Identifier: BSD-2-Clause

**/

#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <assert.h>

#define MAX_ADDRESS 0xFFFFFFFF

// Function to calculate 32-bit checksum
uint32_t calculate_sum32(const uint32_t *buffer, size_t length) {
    uint32_t sum = 0;
    size_t count;
    size_t total;

    assert(buffer != NULL);
    assert(((size_t)buffer & 0x3) == 0);
    assert((length & 0x3) == 0);
    assert(length <= (MAX_ADDRESS - (size_t)buffer + 1));

    total = length / sizeof(*buffer);
    for (count = 0; count < total; count++) {
        sum += buffer[count];
    }

    return sum;
}

int main(int argc, char *argv[]) {
    if (argc != 2) {
        fprintf(stderr, "Usage: %s <filename>\n", argv[0]);
        return 1;
    }

    const char *filename = argv[1];
    FILE *file = fopen(filename, "rb");
    if (!file) {
        perror("Failed to open file");
        return 1;
    }

    // Get the file size
    fseek(file, 0, SEEK_END);
    long file_size = ftell(file);
    fseek(file, 0, SEEK_SET);

    // Ensure the file size is a multiple of 4 bytes
    if (file_size % 4 != 0) {
        fprintf(stderr, "File size must be a multiple of 4 bytes\n");
        fclose(file);
        return 1;
    }

    // Allocate buffer to read file content
    uint32_t *buffer = (uint32_t *)malloc(file_size);
    if (!buffer) {
        perror("Failed to allocate memory");
        fclose(file);
        return 1;
    }

    // Read file content into buffer
    size_t bytes_read = fread(buffer, 1, file_size, file);
    if (bytes_read != (size_t)file_size) {
        perror("Failed to read file");
        free(buffer);
        fclose(file);
        return 1;
    }

    // Calculate checksum
    uint32_t checksum = calculate_sum32(buffer, file_size);

    // Print the result
   printf("Checksum: 0x%x\n", checksum);

    // Clean up
    free(buffer);
    fclose(file);

    return 0;
}
