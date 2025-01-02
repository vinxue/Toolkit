# Define compiler and compile options
CC := gcc
CFLAGS := -Wall -Wextra -O2

# Define source files and object files
SRCS := $(wildcard *.c)
OBJS := $(SRCS:.c=.o)
TARGET := $(BUILD_DIR)/$(notdir $(CURDIR))

# Default target
.PHONY: all
all: $(TARGET)

# Link object files to create the target
$(TARGET): $(OBJS)
	$(CC) $(CFLAGS) -o $@ $^

# Compile source files
%.o: %.c
	$(CC) $(CFLAGS) -c $< -o $@

# Clean target
.PHONY: clean
clean:
	rm -f $(OBJS) $(TARGET)

