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
      zip -r ../OTD.LEDSandbox-$f-0.6.x.zip *
    )
  done
)