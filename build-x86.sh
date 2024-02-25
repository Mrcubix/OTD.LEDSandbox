#!/bin/bash

config="Release"

dotnet publish -c $config -o temp $@ || exit 1

# create a folder if it doesn't exist named "build"
if [ ! -d "build" ]; then
  mkdir build
fi

mkdir temp/bin

# copy the files from the temp folder to the build folder

mv temp/OTD.CustomLED.dll temp/bin/OTD.CustomLED.dll

# if config is Debug, copy the pdb file
if [ $config = "Debug" ]; then
  mv temp/OTD.CustomLED.pdb temp/bin/OTD.CustomLED.pdb
fi

mv temp/SkiaSharp.dll temp/bin/SkiaSharp.dll
mv temp/runtimes temp/bin/runtimes

# nuke conflicting runtimes (arm64 & musl64)

bin_runtime="temp/bin/runtimes"

rm -rf $bin_runtime/win-arm64
rm -rf $bin_runtime/linux-arm
rm -rf $bin_runtime/linux-arm64
rm -rf $bin_runtime/linux-musl-x64

if [ ! -d "build/x86" ]; then
  mkdir build/x86
fi

# copy the files from the temp folder to the build folder

mv temp/bin/* build/x86

# remove temp folder

rm -rf temp