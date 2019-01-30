#!/bin/sh
cd /dotnet
echo "Remounting / as rw..."
dev_name=`mount | grep "on / type" | cut -d ' ' -f 0`
echo "/ is $dev_name"
mount -o remount,rw $dev_name /
echo "Dropping you into a shell..."
/bin/sh