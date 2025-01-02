# Define subdirectories
SUBDIRS := $(wildcard */)

# Define build directory
BUILD_DIR := build

# Default target
.PHONY: all
all: $(BUILD_DIR) $(SUBDIRS)

# Create build directory
$(BUILD_DIR):
	mkdir -p $(BUILD_DIR)

# Recursively call subdirectory Makefile and pass build directory
$(SUBDIRS): $(BUILD_DIR)
	$(MAKE) -C $@ BUILD_DIR=$(abspath $(BUILD_DIR))

# Clean target
.PHONY: clean
clean:
	@for dir in $(SUBDIRS); do \
		$(MAKE) -C $$dir clean; \
	done
	rm -rf $(BUILD_DIR)

