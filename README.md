# HeartRateBroadcaster - OPPO Watch X 心率广播器

> 将 OPPO Watch X 变成标准 BLE 心率带，让任何运动 App 都能接收心率数据

## 项目简介

这是一个为 **OPPO Watch X**（以及所有内置心率传感器的 Android 手表/手环）开发的极简心率广播应用。通过手表内置的光学心率传感器（PPG）读取实时心率数据，并以 **BLE GATT 外围设备（Peripheral）**模式广播标准的蓝牙心率服务。

你的手表将变成一根虚拟的「心率胸带」，任何支持标准 BLE 心率协议的运动 App、码表、健身软件都能直接发现并接收心率数据。

**无需配对、无需连接、无需安装接收端、无账号、无云端。**

## 兼容的接收端

以下 App/设备可以直接搜索并接收本应用广播的心率数据：

- **骑行码表**：Wahoo、Garmin、黑鸟码表、行者码表
- **运动健身**：Zwift、TrainerRoad、Peloton
- **跑步软件**：Strava、Nike Run Club、Keep（部分版本）
- **调试工具**：nRF Connect（推荐用于测试）
- **其他**：任何支持 Bluetooth LE Heart Rate Profile 的设备

## 功能特性

- 通过 `SensorManager.TYPE_HEART_RATE` 读取光学心率传感器
- 以 BLE 外围设备模式广播标准 Heart Rate Service（0x180D）
- 数据格式符合 Bluetooth SIG 官方规范
- 支持息屏/锁屏后台持续运行（Foreground Service + WakeLock）
- 极简 UI：一个开关 + 实时心率数值
- 模拟数据模式：当传感器不可用时自动切换，方便调试 BLE 链路

## 硬件/系统要求

| 项目 | 要求 |
|------|------|
| 设备 | OPPO Watch X 或其他带 PPG 心率传感器的 Android 手表/手环 |
| 系统 | Android 11+（API 30+） |
| 蓝牙 | BLE 4.2+ 且支持 Advertising |
| 架构 | `armeabi-v7a`（`android-arm`） |

## 技术栈

| 技术 | 说明 |
|------|------|
| .NET 10 MAUI | 跨平台移动应用框架 |
| C# | 全部代码均为 C# |
| Android API | SensorManager、BluetoothGattServer、BluetoothLeAdvertiser |

## 项目结构

```
HeartRateBroadcaster/
  HeartRateBroadcaster.csproj          # 项目文件
  MauiProgram.cs                       # MAUI 应用入口
  MainPage.xaml / .xaml.cs             # 主界面（开关 + 心率显示）
  Platforms/Android/
    AndroidManifest.xml                # Android 权限声明
    MainActivity.cs                    # 入口 Activity + 权限申请
    MainApplication.cs                 # MAUI Application
    Services/
      HeartRateService.cs              # 前台服务 + 心率传感器读取
      BleHrAdvertiserService.cs        # BLE GATT 外围设备广播
```

## BLE 服务详情

| 项目 | UUID |
|------|------|
| Heart Rate Service | `0000180D-0000-1000-8000-00805F9B34FB` |
| Heart Rate Measurement | `00002A37-0000-1000-8000-00805F9B34FB` |
| Body Sensor Location | `00002A38-0000-1000-8000-00805F9B34FB`（值 = 0x02 / Wrist） |
| CCCD Descriptor | `00002902-0000-1000-8000-00805F9B34FB` |

**数据格式**：UINT8（1字节 BPM）+ Sensor Contact Detected 标志位

## 编译

### 环境要求

- Visual Studio 2022（安装 .NET MAUI 工作负载）
- Android SDK Platform 30+
- ADB（Android Debug Bridge）

### 命令行编译

```bash
# 还原依赖
dotnet restore HeartRateBroadcaster.csproj

# 编译 Release APK
dotnet publish HeartRateBroadcaster.csproj \
  -f net10.0-android \
  -c Release \
  -p:RuntimeIdentifier=android-arm
```

输出路径：`bin/Release/net10.0-android/android-arm/publish/com.oppo.hrbroadcast-Signed.apk`

## 部署

### 连接 ADB

OPPO Watch X 没有 USB 调试口，使用 WiFi ADB：

```bash
# 手表端：设置 → 关于 → 版本号 连点 7 次 → 开发者选项 → 开启 WiFi 调试
# 记录手表 IP 和端口
adb connect <手表IP>:5555
adb devices
```

### 安装 APK

```bash
adb install -r "bin/Release/net10.0-android/android-arm/publish/com.oppo.hrbroadcast-Signed.apk"
```

### 启动应用

```bash
adb shell am start -n com.oppo.hrbroadcast/crc64060ca0a0331b2a80.MainActivity
```

### 授权权限（如未弹出权限窗口）

```bash
adb shell pm grant com.oppo.hrbroadcast android.permission.BODY_SENSORS
adb shell pm grant com.oppo.hrbroadcast android.permission.BLUETOOTH_ADVERTISE
adb shell pm grant com.oppo.hrbroadcast android.permission.BLUETOOTH_CONNECT
adb shell pm grant com.oppo.hrbroadcast android.permission.ACCESS_FINE_LOCATION
```

## 使用说明

1. 手表上打开 **HR Broadcast** 应用
2. 将开关拨到 **ON**
3. 应用自动启动前台服务、激活心率传感器、开始 BLE 广播
4. 在接收端（手机/码表）打开运动 App，搜索心率设备
5. 找到 **HR Broadcast**，连接后即可看到实时心率
6. 拨回 **OFF** 停止广播

## 查看日志

```bash
# 过滤应用日志
adb logcat -s HeartRateService BleHrAdvertiser *:S

# 所有日志
adb logcat | findstr "HRBroadcast"
```

## 常见问题

### 心率显示 "--"
- 检查 BODY_SENSORS 权限是否已授权
- 部分 ColorOS 版本使用非标准心率传感器，查看 `adb logcat` 中的传感器列表
- 传感器不可用时应用会自动切换到模拟数据模式（60-120 BPM），用于测试 BLE 链路

### BLE 搜不到设备
- 确认手表蓝牙已开启
- 确认接收端蓝牙已开启且支持 BLE Heart Rate
- 部分设备需要等待 5-10 秒才能发现广播

### 后台被杀
- ColorOS：设置 → 电池 → 应用耗电管理 → HR Broadcast → 允许后台运行
- 或将应用锁定在最近任务

### 接收端无法连接
- 本应用作为 **Peripheral（外围设备）** 广播，接收端必须是 **Central（中心设备）**
- 确保接收端支持标准 BLE Heart Rate Service（0x180D）
- 推荐用 **nRF Connect** 测试连通性

## 排错流程

```
打开应用 → 开关 ON → 查看 adb logcat
    ↓
显示 "Heart rate sensor found"? 
    ↓ 是
显示 "Advertising started successfully"?
    ↓ 是
用 nRF Connect 扫描，看到 "HR Broadcast"?
    ↓ 是
连接后收到 Heart Rate 数据?
    ↓ 是
✅ 可以配合运动 App 使用了
```

## License

MIT License - 自由使用，自由修改。

> 本项目为个人开发者工具，与 OPPO 官方无关。
