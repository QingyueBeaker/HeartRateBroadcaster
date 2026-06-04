# OPPO Watch X Heart Rate Broadcaster

.NET MAUI Android App for OPPO Watch X that reads optical heart rate sensor data and broadcasts it via standard BLE GATT Heart Rate Service.

## Features

- **Heart Rate Reading**: Uses Android `SensorManager.TYPE_HEART_RATE` to read PPG sensor data
- **BLE GATT Peripheral**: Broadcasts standard Heart Rate Service (`0x180D`) with Heart Rate Measurement characteristic (`0x2A37`)
- **Data Format**: Compliant with Bluetooth SIG Heart Rate Service specification
- **Background Operation**: Foreground service keeps broadcasting even when screen is off
- **Minimal UI**: Single switch to start/stop, real-time heart rate display

## Project Structure

```
HeartRateBroadcaster/
├── HeartRateBroadcaster.csproj    # Project file
├── MauiProgram.cs                 # MAUI app entry point
├── App.xaml / App.xaml.cs         # Application class
├── AppShell.xaml / AppShell.xaml.cs  # Shell navigation
├── MainPage.xaml / MainPage.xaml.cs  # Main UI (toggle + HR display)
├── Platforms/
│   └── Android/
│       ├── AndroidManifest.xml    # Permissions & service declarations
│       ├── MainActivity.cs        # Activity with permission requests
│       ├── MainApplication.cs     # MAUI application
│       └── Services/
│           ├── HeartRateService.cs      # Foreground service + sensor reading
│           └── BleHrAdvertiserService.cs # BLE GATT peripheral + broadcaster
└── Resources/
    ├── AppIcon/appicon.svg
    ├── Splash/splash.svg
    └── Styles/
        ├── Colors.xaml
        └── Styles.xaml
```

## Prerequisites

### Required

- **Visual Studio 2022** (Windows) or **JetBrains Rider**
- **.NET 8.0 SDK** or later (项目使用 .NET 8，与 .NET MAUI 兼容)
- **.NET MAUI workload** installed
- **Android SDK** with API 34
- **ADB** (Android Debug Bridge) for deployment

### Install .NET MAUI Workload (if not installed)

```bash
dotnet workload install maui-android
```

Or via Visual Studio Installer: modify installation, check ".NET MAUI" workload.

## Build Instructions

### Option 1: Visual Studio (Recommended)

1. Open `HeartRateBroadcaster.csproj` in Visual Studio 2022
2. Set build configuration: `Release | Any CPU`
3. Select target framework: `net8.0-android`
4. Build → Build Solution (Ctrl+Shift+B)
5. Output APK: `bin/Release/net8.0-android/com.oppo.hrbroadcast-Signed.apk`

### Option 2: Command Line

```bash
# Restore packages
dotnet restore HeartRateBroadcaster.csproj

# Build Release APK for armeabi-v7a
dotnet publish HeartRateBroadcaster.csproj \
  -f net8.0-android \
  -c Release \
  -p:AndroidPackageFormat=apk \
  -p:RuntimeIdentifier=android-arm

# Output location:
# bin/Release/net8.0-android/android-arm/publish/com.oppo.hrbroadcast-Signed.apk
```

## ADB Installation

### Connect to OPPO Watch X via ADB

```bash
# 1. Enable Developer Options on watch:
#    Settings > System > About > Tap "Build Number" 7 times
#    Settings > System > Developer Options > Enable "ADB Debugging"

# 2. Connect via WiFi (recommended for daily use):
#    Settings > WLAN > Click connected network > get IP address
adb connect <WATCH_IP_ADDRESS>:5555

# Or connect via USB cable

# 3. Verify connection
adb devices
# Output: <device_id>    device

# 4. Install APK
adb install -r "bin/Release/net8.0-android/com.oppo.hrbroadcast-Signed.apk"

# 5. Launch app
adb shell am start -n com.oppo.hrbroadcast/crc64060ca0a0331b2a80.MainActivity

# 6. View logs
adb logcat -s HeartRateService BleHrAdvertiser *:S
```

### Grant Permissions

First launch will request permissions. If needed, grant manually:

```bash
adb shell pm grant com.oppo.hrbroadcast android.permission.BLUETOOTH_ADVERTISE
adb shell pm grant com.oppo.hrbroadcast android.permission.BLUETOOTH_CONNECT
adb shell pm grant com.oppo.hrbroadcast android.permission.ACCESS_FINE_LOCATION
adb shell pm grant com.oppo.hrbroadcast android.permission.BODY_SENSORS
```

## Usage

1. Open app on watch - you will see "Ready" status and a switch
2. Toggle the switch to **ON**
3. App will:
   - Start foreground service (notification appears)
   - Activate heart rate sensor
   - Begin BLE advertising with standard HR service UUID `0000180D-0000-1000-8000-00805f9b34fb`
4. Any BLE Central device (phone, bike computer, etc.) can discover and receive HR data
5. Toggle OFF to stop broadcasting and release sensor

## BLE Service Details

| Property | Value |
|----------|-------|
| Service UUID | `0000180D-0000-1000-8000-00805f9b34fb` |
| Characteristic UUID | `00002A37-0000-1000-8000-00805f9b34fb` |
| Body Sensor Location | Wrist (value: 0x02) |
| Data Format | UINT8, Sensor contact detected |
| Advertisement | Connectable, includes device name + HR service UUID |

### Heart Rate Data Packet Format (Bluetooth SIG Standard)

```
Byte 0: Flags (0x06)
        - bit 0: 0 = UINT8 heart rate
        - bit 1-2: 1 = Sensor contact supported and detected
Byte 1: Heart Rate Value (BPM), 0-255
```

## Troubleshooting

### App crashes on launch
```bash
# Check logs for exception details
adb logcat -d | grep -i "hrbroadcast\|exception\|fatal"
```

### BLE advertising not starting
- Verify Bluetooth is enabled on watch: `adb shell settings get global bluetooth_on` should return `1`
- Check if advertiser is supported: `adb shell dumpsys bluetooth_manager | grep -i "le advertiser"`
- Some devices require Location services: `adb shell settings put secure location_mode 3`

### Heart rate sensor not found
```bash
# List available sensors
adb shell dumpsys sensorservice | grep -i "heart"
```
- If no heart rate sensor is listed, the watch may use a proprietary API
- The app includes fallback logic for testing (simulated data mode)

### Background service killed
- Disable battery optimization for the app
- Settings > Battery > App Battery Management > HR Broadcast > Allow background activity

## Architecture Notes

### Foreground Service
The app uses Android `ForegroundService` with type `health` to ensure continuous operation. A persistent notification is required by Android 8+ for foreground services.

### BLE Peripheral Mode
The watch acts as a GATT Server, not a client. This is the same mode used by heart rate chest straps (e.g., Polar H10).

### Sensor Reading
Uses standard Android `SensorManager` with `TYPE_HEART_RATE`. The sensor delivers events at `SENSOR_DELAY_NORMAL` frequency (typically 1Hz for HR sensors).

### Wake Lock
A partial wake lock is held during broadcasting to prevent CPU sleep from interrupting BLE advertising.

## Technical Specifications

| Item | Detail |
|------|--------|
| Target Framework | .NET 8.0-android |
| Minimum Android | API 28 (Android 9) |
| Target Android | API 34 (Android 14) |
| CPU Architecture | armeabi-v7a (android-arm) |
| BLE Version | 4.2+ (LE Advertising required) |
| Foreground Service | Type: Health |

## License

MIT License - Free for personal use.
