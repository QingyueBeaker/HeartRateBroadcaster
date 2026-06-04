HeartRateBroadcaster
Turn your OPPO Watch X into a standard BLE heart rate strap that any sports app can receive
Overview
HeartRateBroadcaster is a minimal Android app for the OPPO Watch X (and any Android watch with a built-in PPG heart rate sensor). It reads real-time heart rate data from the optical sensor and broadcasts it via BLE GATT Peripheral mode using the standard Bluetooth Heart Rate Service.
Your watch becomes a virtual HR chest strap. Any sports app, bike computer, or fitness software that supports the standard BLE heart rate protocol can discover and receive HR data directly - no pairing, no companion app, no account, no cloud.
Compatible Receivers
The following apps/devices can directly discover and receive HR data from this app:
•Bike Computers: Wahoo, Garmin, Bryton, Xplova
•Indoor Training: Zwift, TrainerRoad, Peloton
•Running/Fitness: Strava, Nike Run Club, Keep
•Debug Tools: nRF Connect (recommended for testing)
•Others: Any device supporting the Bluetooth LE Heart Rate Profile
Features
•Reads optical heart rate sensor via SensorManager.TYPE_HEART_RATE
•Broadcasts as BLE Peripheral with standard Heart Rate Service (0x180D)
•Data format compliant with official Bluetooth SIG specification
•Background operation with screen off (Foreground Service + WakeLock)
•Minimal UI: one toggle switch + live heart rate value
•Fallback simulation mode when sensor is unavailable (60-120 BPM random)
Requirements
Item	Requirement
Device	OPPO Watch X or any Android watch with PPG heart rate sensor
OS	Android 11+ (API 30+)
Bluetooth	BLE 4.2+ with Advertising support
Architecture	armeabi-v7a (android-arm)
Tech Stack
Technology	Description
.NET 10 MAUI	Cross-platform mobile framework
C#	Entire codebase in C#
Android APIs	SensorManager, BluetoothGattServer, BluetoothLeAdvertiser
Project Structure
HeartRateBroadcaster/
  HeartRateBroadcaster.csproj          # Project file
  MauiProgram.cs                       # MAUI app entry
  MainPage.xaml / .xaml.cs             # Main UI (toggle + HR display)
  Platforms/Android/
    AndroidManifest.xml                # Permission declarations
    MainActivity.cs                    # Entry Activity + permission request
    MainApplication.cs                 # MAUI Application
    Services/
      HeartRateService.cs              # Foreground service + sensor reading
      BleHrAdvertiserService.cs        # BLE GATT peripheral broadcasting
BLE Service Details
Item	UUID
Heart Rate Service	0000180D-0000-1000-8000-00805F9B34FB
Heart Rate Measurement	00002A37-0000-1000-8000-00805F9B34FB
Body Sensor Location	00002A38-0000-1000-8000-00805F9B34FB (value = 0x02 / Wrist)
CCCD Descriptor	00002902-0000-1000-8000-00805F9B34FB
Data format: UINT8 (1 byte BPM) + Sensor Contact Detected flag
Building
Prerequisites
•Visual Studio 2022 with .NET MAUI workload
•Android SDK Platform 30+
•ADB (Android Debug Bridge)
Command Line
# Restore dependencies
dotnet restore HeartRateBroadcaster.csproj

# Build Release APK
dotnet publish HeartRateBroadcaster.csproj \
  -f net10.0-android \
  -c Release \
  -p:RuntimeIdentifier=android-arm
Output: bin/Release/net10.0-android/android-arm/publish/com.oppo.hrbroadcast-Signed.apk
Deployment
Connect ADB
OPPO Watch X has no USB debug port. Use WiFi ADB:
# On watch: Settings → About → Tap Build Number 7x → Developer Options → Enable WiFi Debug
# Note the watch IP and port
adb connect <WATCH_IP>:5555
adb devices
Install APK
adb install -r "bin/Release/net10.0-android/android-arm/publish/com.oppo.hrbroadcast-Signed.apk"
Launch App
adb shell am start -n com.oppo.hrbroadcast/crc64060ca0a0331b2a80.MainActivity
Grant Permissions (if not prompted)
adb shell pm grant com.oppo.hrbroadcast android.permission.BODY_SENSORS
adb shell pm grant com.oppo.hrbroadcast android.permission.BLUETOOTH_ADVERTISE
adb shell pm grant com.oppo.hrbroadcast android.permission.BLUETOOTH_CONNECT
adb shell pm grant com.oppo.hrbroadcast android.permission.ACCESS_FINE_LOCATION
Usage
1.Open the HR Broadcast app on your watch
2.Toggle the switch to ON
3.The app automatically:
–Starts foreground service
–Activates the heart rate sensor
–Begins BLE advertising
4.On your receiving device (phone, bike computer), open a sports app and search for HR devices
5.Find HR Broadcast, connect, and view real-time heart rate
6.Toggle OFF to stop broadcasting
Viewing Logs
# Filter app logs
adb logcat -s HeartRateService BleHrAdvertiser *:S

# All logs
adb logcat | grep "HRBroadcast"
Troubleshooting
Heart rate shows “–”
•Verify BODY_SENSORS permission is granted
•Some ColorOS versions use non-standard HR sensors; check adb logcat for sensor list
•When sensor is unavailable, the app auto-switches to simulation mode (60-120 BPM)
BLE device not discoverable
•Ensure Bluetooth is enabled on the watch
•Ensure the receiver supports BLE Heart Rate
•Some devices need 5-10 seconds to discover the broadcast
Background service killed
•ColorOS: Settings → Battery → App Power Management → HR Broadcast → Allow background
•Or lock the app in recent tasks
Receiver cannot connect
•This app acts as a Peripheral; the receiver must be a Central
•Ensure the receiver supports standard BLE Heart Rate Service (0x180D)
•Use nRF Connect to test connectivity
Debug Flow
Open app → Toggle ON → Check adb logcat
    ↓
Shows "Heart rate sensor found"?
    ↓ Yes
Shows "Advertising started successfully"?
    ↓ Yes
Scan with nRF Connect, see "HR Broadcast"?
    ↓ Yes
Receive Heart Rate data after connecting?
    ↓ Yes
✅ Ready to use with sports apps
License
MIT License - Free to use and modify.
This is a personal developer tool and is not affiliated with OPPO.
