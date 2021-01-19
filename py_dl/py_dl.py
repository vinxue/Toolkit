#!/usr/bin/python3

import os
import sys
import getpass
# Need to install by: pip install requests
import requests

user=input("Username:")
password=getpass.getpass("Password:")

def SplitStr (StrName):
    str_name = StrName.split('/')
    for item in str_name:
        if item.strip():
            Name=item

    return Name

def CreateTargeFolder (Argument):
    folder = SplitStr(Argument)
    print(folder)

    try:
        os.mkdir(folder)
    except Exception as e:
        print(e)
        return False

    return True

def DownloadFile (UrlLink, FileLink):
    folder_name = SplitStr(UrlLink)
    file_name = SplitStr(FileLink)

    print ('Downloading %s...' % (file_name))

    r = requests.get(url=UrlLink + FileLink, auth=(user, password))
    with open(os.path.join(os.path.dirname(os.path.abspath("__file__")), (folder_name + '/' + file_name)),"wb") as f:
        f.write(r.content)

def main():
    if len(sys.argv) != 2:
        print ('Usage : %s [image link]' % (sys.argv[0]))
        sys.exit(1)

    if CreateTargeFolder(sys.argv[1]) != True:
        print ('Create folder failed')
        sys.exit(1)

    DownloadFile (sys.argv[1], "/boot.img.gz")

if __name__ == "__main__":
    main()
