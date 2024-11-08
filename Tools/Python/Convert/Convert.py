#!/usr/bin/env python

##
# Import Modules
#
import os
import sys
import subprocess
import argparse

def main():
    if len(sys.argv) != 5:
        print ('Usage: %s [FileName] [DataOffset] [DataSize] [Address]' % (sys.argv[0]))
        sys.exit(1)

    data_offset = int(sys.argv[2], base=16)
    print(data_offset)
    data_size = int(sys.argv[3], base=16)
    print(data_size)
    data_addr = int(sys.argv[4], base=16)
    print(data_addr)

    bin_file = open(sys.argv[1], 'rb')
    bin_size = os.path.getsize(sys.argv[1])
    print(bin_size)
    bin_data = bin_file.read()

    outfilename = 'out.txt'
    if os.path.exists(outfilename):
        os.remove(outfilename)
    out_file = open(outfilename, 'a')

    for i in range(data_size):
        # print(data_offset+i)
        bin_file.seek(data_offset + i)
        data = bin_file.read(1)
        # print(data)
        # 0000000000007000  30                    db      0x30
        outdata = '%016X  %02X                    db      0x%02x\n' % (data_addr+i, int.from_bytes(data, 'big'), int.from_bytes(data, 'big'))
        out_file.write(outdata)

    bin_file.close()
    out_file.close()

if __name__ == '__main__':
    sys.exit(main())
