#!/bin/bash

dotnet restore

# empty build folder

rm -rf build/*

# build for all platforms

./build-x86.sh $@ || exit 1
./build-arm64.sh $@ || exit 1

# remove temp folder

if [ -d "temp" ]; then
  rm -rf temp
fi

if [ ! -d "build" ]; then
  mkdir build
fi

cd build

# create a zip file

for f in *; do
    zip -r OTD.CustomLED-$f.zip $f
done

cd ..