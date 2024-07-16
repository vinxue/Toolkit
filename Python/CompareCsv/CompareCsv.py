import sys
import csv
import re


def parse_csv_file(filename):
    '''
    name,WIDTH,Offset
    DATA0,1,0
    DATA1,1,1
    '''
    with open(filename, 'r') as csvfile:

        # Create a CSV reader object and skip the header row
        reader = csv.reader(csvfile)
        next(reader)

        # Create an empty dictionary
        data_dict = {}

        # Iterate over each row in the CSV file
        for row in reader:

            # Get the key and value from the current row
            key = row[0]
            value = int(row[2])

            # Add the key-value pair to the dictionary
            data_dict[key] = value

    # Return the dictionary
    return data_dict


def parse_header_file(filename):
    '''
    Data format:
    DATA0                  = 0x00000000, // p:0, w:1, l:1000
    DATA1                  = 0x00000001, // p:0, w:1, l:1001
    DATA2                  = 0x00000002, // p:0, w:1, l:1002
    DATA3                  = 0x00000003, // p:0, w:1, l:1003
    '''
    pattern = r'(\w+)\s+=\s+0x\w+,\s*//.*?l:(\d+)'
    name_map = {}
    with open(filename, 'r') as f:
        for line in f:
            match = re.search(pattern, line)
            if match:
                name = match.group(1)
                bit_offset = int(match.group(2))
                name_map[name] = bit_offset
    return name_map


def compare_dicts(dict1, dict2):
    different_keys = []
    for key, value in dict1.items():
        if key not in dict2:
            different_keys.append((key, value))
        elif dict2[key] != value:
            different_keys.append((key, (value, dict2[key])))
    return different_keys


def main():
    csv_data_dict = parse_csv_file(sys.argv[1])
    # print(csv_data_dict)

    name_map = parse_header_file(sys.argv[2])
    # print(name_map)

    different_keys = compare_dicts(csv_data_dict, name_map)

    if different_keys:
        print("Different Keys:")
        for key, value in different_keys:
            print(f"  - {key}: {value}")
    else:
        print("Same Keys/values")


if __name__ == '__main__':
    sys.exit(main())
