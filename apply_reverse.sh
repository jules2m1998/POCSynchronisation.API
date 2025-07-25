#!/bin/bash

# Get all connected Android devices (excluding the header line)
adb devices | grep "device$" | awk '{print $1}' | while read -r device_id; do
    echo "Applying reverse to device: $device_id"
    adb -s "$device_id" reverse tcp:7199 tcp:7199
done