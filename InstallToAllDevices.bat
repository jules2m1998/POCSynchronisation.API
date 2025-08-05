@echo off
setlocal enabledelayedexpansion

REM Set APK path
set APK_PATH=POCSync.MAUI\bin\Release\net9.0-android\com.companyname.pocsync.maui-Signed.apk

REM Check if the APK exists
if not exist "%APK_PATH%" (
    echo ❌ APK not found at path: %APK_PATH%
    exit /b 1
)

REM Get list of connected devices
for /f "skip=1 tokens=1" %%D in ('adb devices') do (
    if "%%D" neq "" (
        echo 📱 Installing APK on device: %%D...
        adb -s %%D install -r "%APK_PATH%"
    )
)

echo ✅ Installation complete on all devices.
pause
