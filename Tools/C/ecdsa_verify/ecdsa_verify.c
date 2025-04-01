#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "mbedtls/include/mbedtls/ecdsa.h"
#include "mbedtls/include/mbedtls/sha512.h"
#include "mbedtls/memory_buffer_alloc.h"
#include <sys/time.h>

#define SIGNATURE_SIZE 96
#define PUBLIC_KEY_SIZE 96

/*
 * CFLAGS="-I$PWD/../ecdsa_verify -DMBEDTLS_CONFIG_FILE='<mbedtls_config.h>'" make no_test
 */
unsigned char memory_buf[10 * 1024];

int main(int argc, char *argv[]) {
    if (argc != 2) {
        fprintf(stderr, "Usage: %s <file_path>\n", argv[0]);
        return 1;
    }

    const char *filename = argv[1];
    int ret;
    //mbedtls_ecdsa_context ecdsa;
    unsigned char hash[48]; // SHA-384 produces a 48-byte hash
    unsigned char signature[SIGNATURE_SIZE];
    unsigned char *data = NULL;
    size_t data_len;

    // ECDSA-384 public key (x and y coordinates concatenated)
    unsigned char public_key[PUBLIC_KEY_SIZE] = {
        0x49, 0x1b, 0xc1, 0x42, 0x0f, 0xed, 0xd3, 0x87, 0xa9, 0x9e, 0x41, 0x7a, 0x6e, 0x28, 0xc1, 0x13,
        0xb0, 0x31, 0x55, 0x03, 0x5c, 0x4e, 0x0f, 0x5e, 0xcf, 0x80, 0x8b, 0x6e, 0xe2, 0xd1, 0x73, 0x7f,
        0x81, 0xe5, 0xc0, 0x94, 0xd4, 0x36, 0x5d, 0xbe, 0xa1, 0xaf, 0xfc, 0x4f, 0x33, 0x00, 0x0e, 0x17,
        0xf6, 0x98, 0x08, 0x99, 0x81, 0x6f, 0x2c, 0x3b, 0x4e, 0x0a, 0x6a, 0x12, 0xca, 0x0c, 0x8e, 0xf0,
        0xca, 0xca, 0x87, 0xaf, 0x47, 0xf0, 0x39, 0xc8, 0x8c, 0xc2, 0xce, 0x91, 0x53, 0x6d, 0x5c, 0x18,
        0xa8, 0x9a, 0xbd, 0xf3, 0x54, 0x3d, 0x4f, 0x95, 0x89, 0x0e, 0xfe, 0x80, 0xbe, 0x1a, 0x79, 0x86
    };

    // Read the binary file
    FILE *file = fopen(filename, "rb");
    if (!file) {
        perror("Failed to open file");
        ret = -1;
        goto cleanup;
    }

    // Get the file size
    fseek(file, 0, SEEK_END);
    long file_size = ftell(file);
    fseek(file, 0, SEEK_SET);

    if (file_size <= SIGNATURE_SIZE) {
        fprintf(stderr, "File size is too small\n");
        ret = -1;
        fclose(file);
        goto cleanup;
    }

    // Allocate memory for data
    data_len = file_size - SIGNATURE_SIZE;
    data = (unsigned char *)malloc(data_len);
    if (!data) {
        perror("Failed to allocate memory");
        ret = -1;
        fclose(file);
        goto cleanup;
    }

    // Read the signature
    if (fread(signature, 1, SIGNATURE_SIZE, file) != SIGNATURE_SIZE) {
        perror("Failed to read signature");
        ret = -1;
        fclose(file);
        goto cleanup;
    }

    // Read the data
    if (fread(data, 1, data_len, file) != data_len) {
        perror("Failed to read data");
        ret = -1;
        fclose(file);
        goto cleanup;
    }

    fclose(file);

    struct timeval start, end;
    long seconds, useconds;
    double mtime;

    gettimeofday(&start, NULL);

    mbedtls_memory_buffer_alloc_init( memory_buf, sizeof(memory_buf) );

    // Compute SHA-384 hash of the data
    mbedtls_sha512(data, data_len, hash, 1); // 1 indicates SHA-384

    // Load the public key into the ECDSA context
    mbedtls_mpi r, s;
    mbedtls_ecp_group grp;
    mbedtls_ecp_point Q;

    mbedtls_mpi_init(&r);
    mbedtls_mpi_init(&s);
    mbedtls_ecp_group_init(&grp);
    mbedtls_ecp_point_init(&Q);

    ret = mbedtls_ecp_group_load(&grp, MBEDTLS_ECP_DP_SECP384R1);
    if (ret != 0) {
        goto cleanup;
    }

    ret = mbedtls_mpi_read_binary(&Q.MBEDTLS_PRIVATE(X), public_key, 48);
    if (ret != 0) {
        goto cleanup;
    }

    ret = mbedtls_mpi_read_binary(&Q.MBEDTLS_PRIVATE(Y), public_key + 48, 48);
    if (ret != 0) {
        goto cleanup;
    }

    ret = mbedtls_mpi_lset(&Q.MBEDTLS_PRIVATE(Z), 1);
    if (ret != 0) {
        goto cleanup;
    }

    // Read r and s from the signature
    ret = mbedtls_mpi_read_binary(&r, signature, 48);
    if (ret != 0) {
        goto cleanup;
    }

    ret = mbedtls_mpi_read_binary(&s, signature + 48, 48);
    if (ret != 0) {
        goto cleanup;
    }

    // Verify the signature
    ret = mbedtls_ecdsa_verify(&grp, hash, sizeof(hash), &Q, &r, &s);
    if (ret != 0) {
        goto cleanup;
    }

    gettimeofday(&end, NULL);

    seconds  = end.tv_sec  - start.tv_sec;
    useconds = end.tv_usec - start.tv_usec;

    mtime = ((seconds) * 1000 + useconds/1000.0) + 0.5;

    printf("Execution time: %f milliseconds\n", mtime);


    printf("Signature verified successfully!\n");

cleanup:
    mbedtls_mpi_free(&r);
    mbedtls_mpi_free(&s);
    mbedtls_ecp_group_free(&grp);
    mbedtls_ecp_point_free(&Q);
    free(data);
    return ret;
}
