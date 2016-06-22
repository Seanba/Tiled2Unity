#!/bin/sh

# All work is in *this* directory
THIS_DIR=`dirname $0`
pushd $THIS_DIR > /dev/null 2>&1

# Make sure the right unity package is in the build
ruby replace-unitypackage.rb
if [ "$?" != "0" ]; then
	echo "Could not replace the unity package"
	popd > /dev/null 2<&1
	exit 1
fi

# Build the project in Release
xbuild /p:Configuration=Release ../Tiled2UnityMac.sln
if [ "$?" != "0" ]; then
	echo "Error building Tiled2UnityMac.sln"
	popd > /dev/null 2<&1
	exit 1
fi

# Zip it all up
version=$(cat t2u_version.txt)
echo version="$version"

t2u="Tiled2UnityMac-"
ext=".zip"

zipped=$t2u$version$ext
echo Zipping to $zipped

pushd ../Tiled2UnityMac/bin/Release
zip -r ../../../build/$zipped Tiled2UnityMac.app
popd

# Done. Pop the directory.
popd > /dev/null 2<&1