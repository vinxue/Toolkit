#!/usr/bin/env python

import sys
import struct

'''
The script is for below similar hex string format:
00000000 : 00 00 00 00 00 00 00 00 - 00 00 00 00 00 00 00 00   ................
00000000 : 00 00 00 00 00 00 00 00 - 00 00 00 00               ............

'''

def main():
    if len(sys.argv) != 3:
       print ('Usage: %s [input file] [out file]' % (sys.argv[0]))
       return

    try:
        input_file = open(sys.argv[1], 'r', encoding='utf8')
    except IOError:
        print('Open file failed.\n')
        return

    out_file = open(sys.argv[2], 'wb')

    line = input_file.readline()

    while line:
        start_offset = 11
        line = line.strip()
        line = line.replace("- ", "")
        # print(line)

        if line[9] != ':':
            print('Unrecognized format.\n')
            input_file.close()
            out_file.close()
            return

        for index in range(0, 48, 3):
            if line[start_offset + index:start_offset + index + 2] == '  ':
                break

            hexbyte = struct.pack('B', int(line[start_offset + index:start_offset + index + 2], 16))
            out_file.write(hexbyte)

        line = input_file.readline()

    input_file.close()
    out_file.close()
    print ('Convert successfully.\n')

if __name__ == "__main__":
    main()
