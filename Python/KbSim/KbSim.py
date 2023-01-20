#!/usr/bin/env python

##
# Import Modules
#
import platform
import time
from ctypes import *

def kbFunc():
    if platform.system() == 'Windows':
        while True:
            try:
                # VK_CAPITAL 0x14
                # KEYEVENTF_KEYUP 2
                windll.user32.keybd_event(0x14, 0, 0, 0)
                windll.user32.keybd_event(0x14, 0, 2, 0)

                time.sleep(60)
            except:
                break

if __name__ == '__main__':
    kbFunc()
