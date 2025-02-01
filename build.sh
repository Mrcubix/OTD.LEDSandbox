#!/bin/bash

dotnet restore

# empty build folder

rm -rf build/*

# build for all platforms

./build-x86.sh $@ || exit 1
./build-arm.sh $@ || exit 1

# remove temp folder

if [ -d "temp" ]; then
  rm -rf temp
fi

if [ ! -d "build" ]; then
  mkdir build
fi

#subshell to build the zip files
(
  cd build

  # create zip files
  for f in *; do
    (
      cd $f
      zip -r ../OTD.LEDSandbox-$f-0.5.x.zip *
    )
  done

  echo "Computing hashes"

  sha256sum OTD.LEDSandbox-arm-0.5.x.zip >> hashes.txt
  sha256sum OTD.LEDSandbox-x86-0.5.x.zip >> hashes.txt
)