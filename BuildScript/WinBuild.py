import subprocess
import concurrent.futures

# Add MSBuild to PATH. e.g.:
# set PATH=C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin;%PATH%

# Define the solution paths and platforms
solution_paths = [
    ("BitViewer", "x64"),
    ("KbSim", "Win32"),
    ("PCIe", "Win32"),
    ("Timer", "Win32"),
    ("ImgConverter", "Win32"),
    ("StopWatch", "Win32"),
    ("IDCard", "Win32"),
    ("ClockApp", "Any CPU"),
    ("TimeTracker", "Any CPU"),
    ("SecKit", "Any CPU"),
    ("End", "End")
]

def build_solution(solution_path, platform, build_configuration="Release"):
    print(f"Building {solution_path} for platform {platform}...")
    subprocess.run(["msbuild", "/m", f"/p:Configuration={build_configuration}", f"/p:Platform={platform}", solution_path], check=True)

def build_special_bitviewer(build_configuration="Release"):
    print("Building BitViewer with ACRYLIC_SUPPORT...")
    subprocess.run(["msbuild", "/m", f"/p:Configuration={build_configuration}", "BitViewer", "/p:DefineConstants=ACRYLIC_SUPPORT", "/p:TargetName=BitViewer_A", "/p:Platform=x64"], check=True)

def main():
    # Define the build configuration
    build_configuration = "Release"

    try:
        # Special build for BitViewer with ACRYLIC_SUPPORT
        build_special_bitviewer()

        # Use ThreadPoolExecutor to build solutions concurrently
        with concurrent.futures.ThreadPoolExecutor() as executor:
            futures = [
                executor.submit(build_solution, solution_path, platform, build_configuration)
                for solution_path, platform in solution_paths if solution_path != "End"
            ]
            # Wait for all futures to complete
            for future in concurrent.futures.as_completed(futures):
                # This will raise an exception if the future encountered an exception
                future.result()
    except Exception as e:
        print(f"Build failed: {e}")
        exit(1)

if __name__ == "__main__":
    main()
