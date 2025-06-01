@echo off
for /f "tokens=1" %%i in ('adb devices ^| findstr "device" ^| findstr /v "List"') do (
    echo Applying reverse to device: %%i
    adb -s %%i reverse tcp:7199 tcp:7199
)