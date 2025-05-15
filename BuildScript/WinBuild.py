import subprocess
import glob

def build_solution(solution_path, platform, build_configuration="Release"):
    print(f"Building {solution_path} for platform {platform}...")
    subprocess.run(["msbuild", "/m", f"/p:Configuration={build_configuration}", f"/p:Platform={platform}", solution_path], check=True)

def build_special_bitviewer(build_configuration="Release"):
    print("Building BitViewer with ACRYLIC_SUPPORT...")
    subprocess.run(["msbuild", "/m", f"/p:Configuration={build_configuration}", "BitViewer", "/p:DefineConstants=ACRYLIC_SUPPORT", "/p:TargetName=BitViewer_A", "/p:Platform=x64"], check=True)

def main():
    # Define the solution paths and platforms
    solution_paths = [
        ("BitViewer", "x64"),
        ("KbSim", "Win32"),
        ("PCIe", "Win32"),
        ("Timer", "Win32"),
        ("ImgConverter", "Win32"),
        ("StopWatch", "Win32"),
        ("IDCard", "Win32"),
        ("HashCalc", "Any CPU"),
        ("End", "End")
    ]

    # Define the build configuration
    build_configuration = "Release"

    # Special build for BitViewer with ACRYLIC_SUPPORT
    build_special_bitviewer()

    # Iterate over each solution and build it
    for solution_path, platform in solution_paths:
        if solution_path != "End":
            build_solution(solution_path, platform)

if __name__ == "__main__":
    main()
