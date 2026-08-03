#!/bin/sh

# Note that if this script cannot execute (directory or file not found error when running ansible)
# then set this script to unix format from dos format (dos2unix tool can be used - apt install dos2unix)
#
#
# Get the device path, file-system type, and UUID for each block device that doesn't host the root file system, sorted ascendingly by the size of the block device.
# This provides the information about the swap and data volume that other roles require to set them up appropriately.
# Example output:
# /dev/nvme1n1    swap   d613acbb-31b8-4114-91c8-f3c1fcf194a4
# /dev/nvme0n1    xfs    ef81865a-5188-484f-84e7-3a6fdc712f4a
get_list () {
    local ROOT_PARTITION_PATH ROOT_BLOCK_DEVICE_NAME
    # determine the device path of the partition hosting the root file system
    ROOT_PARTITION_PATH="$(mount | grep 'on / type' | cut -d ' ' -f 1)"
    # determine the device path of the parent block device containing the root partition
    ROOT_BLOCK_DEVICE_NAME="$(lsblk -no pkname "${ROOT_PARTITION_PATH}")"
    # list all block devices except those on the root device, sorted by size
    lsblk --noheadings --sort SIZE -o PATH,FSTYPE,UUID --bytes -e7 | grep -v "${ROOT_BLOCK_DEVICE_NAME}"
}

get_number_of_disks () {
    get_list | wc -l
}

if [ "$(get_number_of_disks)" != "1" ]
then
    echo "Unable to determine disk information; there do not appear to be a swap and/or data disk as expected" >&2
    exit 2
fi

if [ "${1}" = "swap" ]
then
    TRIM=head
elif [ "${1}" = "data" ]
then
    TRIM=tail
else
    echo "The first command line option needs to be 'swap' or 'data'" >&2
    exit 2
fi

if [ "${2}" = "path" ]
then
    OFFSET=1
elif [ "${2}" = "fs" ]
then
    OFFSET=2
elif [ "${2}" = "uuid" ]
then
    OFFSET=3
else
    echo "The second command line option needs to be 'path', 'fs', or 'uuid'" >&2
    exit 2
fi

get_list | ${TRIM} -n 1 | awk '{ print $'${OFFSET}' }'
