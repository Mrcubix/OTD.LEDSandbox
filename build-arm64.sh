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

# if config is Debug, copy the pdb file
if [ $config = "Debug" ]; then
  mv temp/OTD.LEDSandbox.pdb temp/bin/OTD.LEDSandbox.pdb
fi

mv temp/SkiaSharp.dll temp/bin/SkiaSharp.dll
mv temp/runtimes temp/bin/runtimes

# nuke conflicting runtimes (x86)

bin_runtime="temp/bin/runtimes"

rm -rf $bin_runtime/win-x86
rm -rf $bin_runtime/win-x64
rm -rf $bin_runtime/linux-x64
rm -rf $bin_runtime/linux-musl-x64

if [ ! -d "build/arm64" ]; then
  mkdir build/arm64
fi

# copy the files from the temp folder to the build folder

mv temp/bin/* build/arm64

# remove temp folder

rm -rf temp