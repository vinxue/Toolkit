import struct

hexstr = '6AA9FC2232CCC2BEB1267DC3FC4105D18537573C70A16B9A7271D023A968D0ED'

hexfile = open('genhex.bin', 'wb')

for i in range(0, len(hexstr), 2):
  hexbyte = struct.pack('B', int(hexstr[i:i+2], 16))
  hexfile.write(hexbyte)

hexfile.close()
