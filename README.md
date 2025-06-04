# POCSync.MAUI

This repository contains a MAUI application that demonstrates offline/online data synchronisation. The solution depends on several external projects from the `POCSynchronisation` backend repository and therefore cannot be built on its own without those sources.

## Prerequisites

- **.NET 9 SDK** – The projects target `net9.0` so the .NET 9 (preview) SDK must be installed.
- **MAUI Workload** – Install the MAUI workload for .NET 9 (e.g. `dotnet workload install maui`).
- **Android SDK/Emulator or device** – required to run the mobile application. `adb` must be available if you want to use the port reverse helper.

## External dependencies

Two projects located in another repository are required by the solution:

- `Domain`
- `Application`

These projects must exist in the paths used in the solution (`..\..\..\..\source\repos\POCSynchronisation\src\Domain` and `..\..\..\..\source\repos\POCSynchronisation\src\Application`). Clone the original backend repository or adjust the project references in `POCSync.MAUI.sln` and the individual `.csproj` files if your folder structure is different.

Without those projects the solution will not compile.

## Configuration

The MAUI app reads configuration values from `POCSync.MAUI/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "data3.db3"
  },
  "DbPwd": "{T878T$+u@&F`3l73%^.`EAa-1HuCXk",
  "ApiUrl": "https://localhost:7199"
}
```

Update the connection string, database password and `ApiUrl` to match your environment. The API URL should point to the running backend server. When debugging locally on Android you can either expose the API through tools such as ngrok or use the `apply_reverse.bat` script to set up `adb reverse` so the device can reach `https://localhost:7199`.

## Typical commands

Build the solution:

```bash
# restores workloads and builds
dotnet build POCSync.MAUI.sln
```

Run the MAUI application (for example on Android):

```bash
dotnet maui run -f net9.0-android
```

If you run the backend locally and your Android device or emulator cannot access `https://localhost:7199`, execute `apply_reverse.bat` to forward the port:

```batch
apply_reverse.bat
```

This calls `adb reverse tcp:7199 tcp:7199` for each connected device so the mobile app can talk to the local API server.

