#!/bin/bash

config="Release"

dotnet publish -c $config -o temp $@ || exit 1

# create a folder if it doesn't exist named "build"
if [ ! -d "build" ]; then
  mkdir build
fi

mkdir temp/bin

# copy the files from the temp folder to the build folder

mv temp/OTD.LEDSandbox.dll temp/bin/OTD.LEDSandbox.dll
mv temp/OTD.LEDSandbox.Lib.dll temp/bin/OTD.LEDSandbox.Lib.dll
mv temp/OpenTabletDriver.External.Common.dll temp/bin/OpenTabletDriver.External.Common.dll

# if config is Debug, copy the pdb file
if [ $config = "Debug" ]; then
  mv temp/OTD.LEDSandbox.pdb temp/bin/OTD.LEDSandbox.pdb
  mv temp/OTD.LEDSandbox.Lib.pdb temp/bin/OTD.LEDSandbox.Lib.pdb
  mv temp/OpenTabletDriver.External.Common.pdb temp/bin/OpenTabletDriver.External.Common.pdb
fi

mv temp/SkiaSharp.dll temp/bin/SkiaSharp.dll
mv temp/runtimes temp/bin/runtimes

# nuke conflicting runtimes (x86)

bin_runtime="temp/bin/runtimes"

rm -rf $bin_runtime/win-x86
rm -rf $bin_runtime/win-x64
rm -rf $bin_runtime/linux-x64
rm -rf $bin_runtime/linux-musl-x64

if [ ! -d "build/arm" ]; then
  mkdir build/arm
fi

# copy the files from the temp folder to the build folder

mv temp/bin/* build/arm

# remove temp folder

rm -rf temp