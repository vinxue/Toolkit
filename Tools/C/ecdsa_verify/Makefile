# Compiler and flags
CC = gcc
CFLAGS = -I./mbedtls/include -Wall -Wextra -Os
LDFLAGS = -L./mbedtls/library -lmbedcrypto

# Source files
SOURCES = ecdsa_verify.c

# Target executable
TARGET = $(BUILD_DIR)/$(notdir $(CURDIR))

# Mbed TLS repository URL
MBEDTLS_REPO = https://github.com/Mbed-TLS/mbedtls.git

# Default target
all: mbedtls_build $(TARGET)

# Clone Mbed TLS repository and update submodules
mbedtls_clone:
	if [ ! -d "mbedtls" ]; then \
		git clone $(MBEDTLS_REPO); \
	fi
	cd mbedtls && git checkout v3.6.2 && git submodule update --init

# Build Mbed TLS
mbedtls_build: mbedtls_clone
	cd mbedtls && CFLAGS="-I$(shell pwd) -DMBEDTLS_CONFIG_FILE='<mbedtls_config.h>'" make no_test

# Build target
$(TARGET): $(SOURCES)
	$(CC) $(CFLAGS) -o $(TARGET) $(SOURCES) $(LDFLAGS)

# Clean target
clean:
	rm -f $(TARGET)
	rm -rf mbedtls
