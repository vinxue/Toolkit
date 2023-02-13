#!/usr/bin/env python
# -*- coding: UTF-8 -*-

##
# Import Modules
#
import platform
import ctypes
import argparse

'''
Enables an application to inform the system that it is in use, thereby preventing the system
from entering sleep or turning off the display while the application is running.
https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate

For Windows OS only.
'''
class SystemActivity:
    ES_CONTINUOUS = 0x80000000
    ES_DISPLAY_REQUIRED = 0x00000002
    ES_SYSTEM_REQUIRED = 0x00000001

    def __init__(self):
        pass

    def Activity(self):
        ctypes.windll.kernel32.SetThreadExecutionState(SystemActivity.ES_CONTINUOUS |
                                                       SystemActivity.ES_DISPLAY_REQUIRED |
                                                       SystemActivity.ES_SYSTEM_REQUIRED)

    def Normal(self):
        ctypes.windll.kernel32.SetThreadExecutionState(SystemActivity.ES_CONTINUOUS)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('-e', '--enable', action='store_true', help='Enable System in activity state')
    parser.add_argument('-d', '--disable', action='store_true', help='Restore System in normal state')

    args = parser.parse_args()

    SystemState = None

    if platform.system() == 'Windows':
        SystemState = SystemActivity()

        if (args.enable):
            print ("Activity\n")
            SystemState.Activity()
        # elif (args.disable):
        else:
            print ("Normal\n")
            SystemState.Normal()

if __name__ == '__main__':
    main()
