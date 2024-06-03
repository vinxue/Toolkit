import sys
import os


def remove_n_chars(input_filename, n):
    """
    This function removes a specified number of characters from the beginning of each line in a file and saves the modified content to a new file.

    Args:
      input_filename: The name of the file to be processed.
      n: The number of characters to remove from the beginning of each line.
    """
    # Generate the output filename with a modified suffix
    base, ext = os.path.splitext(input_filename)  # Separate filename and extension
    output_filename = f"{base}_modified{ext}"

    with open(input_filename, 'r') as f_in:
        with open(output_filename, 'w') as f_out:
            for line in f_in:
                # Check if line is empty (including leading/trailing whitespaces)
                if not line.strip():
                    f_out.write(line)  # Write the blank line as is
                    continue
                modified_line = line[n:]
                f_out.write(modified_line)


def main():
    if len(sys.argv) != 3:
        print("Usage: %s [input_file] [number]" % (sys.argv[0]))
        sys.exit(1)

    num = int(sys.argv[2])
    remove_n_chars(sys.argv[1], num)


if __name__ == "__main__":
    main()
