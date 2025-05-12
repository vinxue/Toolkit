# The code is ported from: https://github.com/terjew/VSKeyExtractor
import winreg
import ctypes
import ctypes.wintypes
import re

class Product:
    def __init__(self, name, guid, mpc):
        self.name = name
        self.guid = guid
        self.mpc = mpc

products = [
    Product("Visual Studio Express 2012 for Windows Phone", "77550D6B-6352-4E77-9DA3-537419DF564B", "04937"),
    Product("Visual Studio Professional 2012", "77550D6B-6352-4E77-9DA3-537419DF564B", "04938"),
    Product("Visual Studio Ultimate 2012", "77550D6B-6352-4E77-9DA3-537419DF564B", "04940"),
    Product("Visual Studio Premium 2012", "77550D6B-6352-4E77-9DA3-537419DF564B", "04941"),
    Product("Visual Studio Test Professional 2012", "77550D6B-6352-4E77-9DA3-537419DF564B", "04942"),
    Product("Visual Studio Express 2012 for Windows Desktop", "77550D6B-6352-4E77-9DA3-537419DF564B", "05695"),
    Product("Visual Studio 2013 Professional", "E79B3F9C-6543-4897-BBA5-5BFB0A02BB5C", "06177"),
    Product("Visual Studio 2013 Ultimate", "E79B3F9C-6543-4897-BBA5-5BFB0A02BB5C", "06181"),
    Product("Visual Studio 2015 Enterprise", "4D8CFBCB-2F6A-4AD2-BABF-10E28F6F2C8F", "07060"),
    Product("Visual Studio 2015 Professional", "4D8CFBCB-2F6A-4AD2-BABF-10E28F6F2C8F", "07062"),
    Product("Visual Studio 2017 Enterprise", "5C505A59-E312-4B89-9508-E162F8150517", "08860"),
    Product("Visual Studio 2017 Professional", "5C505A59-E312-4B89-9508-E162F8150517", "08862"),
    Product("Visual Studio 2017 Test Professional", "5C505A59-E312-4B89-9508-E162F8150517", "08866"),
    Product("Visual Studio 2019 Enterprise", "41717607-F34E-432C-A138-A3CFD7E25CDA", "09260"),
    Product("Visual Studio 2019 Professional", "41717607-F34E-432C-A138-A3CFD7E25CDA", "09262"),
    Product("Visual Studio 2022 Enterprise", "1299B4B9-DFCC-476D-98F0-F65A2B46C96D", "09660"),
    Product("Visual Studio 2022 Professional", "1299B4B9-DFCC-476D-98F0-F65A2B46C96D", "09662"),
]

class DATA_BLOB(ctypes.Structure):
    _fields_ = [("cbData", ctypes.wintypes.DWORD),
                ("pbData", ctypes.POINTER(ctypes.c_byte))]

CRYPTPROTECT_UI_FORBIDDEN = 0x1
crypt32 = ctypes.WinDLL('Crypt32.dll')
kernel32 = ctypes.WinDLL('Kernel32.dll')

def decrypt_data(encrypted_data):
    encrypted_blob = DATA_BLOB(len(encrypted_data), ctypes.cast(encrypted_data, ctypes.POINTER(ctypes.c_byte)))
    decrypted_blob = DATA_BLOB()

    result = crypt32.CryptUnprotectData(
        ctypes.byref(encrypted_blob),
        None,  # Optional description
        None,  # Optional entropy
        None,  # Reserved
        None,  # Optional prompt structure
        CRYPTPROTECT_UI_FORBIDDEN,
        ctypes.byref(decrypted_blob)
    )

    if not result:
        raise ctypes.WinError()

    decrypted_data = ctypes.string_at(decrypted_blob.pbData, decrypted_blob.cbData).decode('utf-16', errors='ignore')
    kernel32.LocalFree(decrypted_blob.pbData)

    return decrypted_data

def extract_license(product):
    try:
        key_path = f"Licenses\\{product.guid}\\{product.mpc}"
        with winreg.OpenKey(winreg.HKEY_CLASSES_ROOT, key_path) as key:
            encrypted_data, _ = winreg.QueryValueEx(key, "")
            decrypted_data = decrypt_data(encrypted_data)
            match = re.search(r"\w{5}-\w{5}-\w{5}-\w{5}-\w{5}", decrypted_data)
            if match:
                print(f"Found key for {product.name}: {match.group(0)}")
    except FileNotFoundError:
        pass

def main():
    for product in products:
        extract_license(product)

if __name__ == "__main__":
    main()
