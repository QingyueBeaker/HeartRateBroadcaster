using global::Android.Bluetooth;
using global::Android.Bluetooth.LE;
using global::Android.Content;
using global::Android.OS;
using global::Java.Util;
using global::Android.Runtime;

namespace HeartRateBroadcaster.Platforms.Android.Services;

public class BleHrAdvertiserService : IDisposable
{
    public const string ActionBleStatusUpdated = "com.oppo.hrbroadcast.ACTION_BLE_STATUS";

    public static readonly UUID HrServiceUuid = UUID.FromString("0000180D-0000-1000-8000-00805f9b34fb");
    public static readonly UUID HrMeasurementUuid = UUID.FromString("00002A37-0000-1000-8000-00805f9b34fb");
    public static readonly UUID HrLocationUuid = UUID.FromString("00002A38-0000-1000-8000-00805f9b34fb");
    public static readonly UUID CccdUuid = UUID.FromString("00002902-0000-1000-8000-00805f9b34fb");

    private readonly Context _context;
    private BluetoothManager? _bluetoothManager;
    private BluetoothAdapter? _bluetoothAdapter;
    private BluetoothLeAdvertiser? _advertiser;
    private BluetoothGattServer? _gattServer;
    private BluetoothGattCharacteristic? _hrMeasurementCharacteristic;

    private bool _isAdvertising;
    private int _lastHeartRate;
    private readonly List<BluetoothDevice> _connectedDevices = new();

    public BleHrAdvertiserService(Context context)
    {
        _context = context;
        InitializeBluetooth();
    }

    private void InitializeBluetooth()
    {
        _bluetoothManager = _context.GetSystemService(Context.BluetoothService) as BluetoothManager;
        _bluetoothAdapter = _bluetoothManager?.Adapter;

        if (_bluetoothAdapter == null)
        {
            throw new InvalidOperationException("Bluetooth adapter not available");
        }

        if (!_bluetoothAdapter.IsEnabled)
        {
            _bluetoothAdapter.Enable();
        }
    }

    public void StartAdvertising()
    {
        if (_isAdvertising) return;

        try
        {
            SetupGattServer();
            StartBleAdvertising();
            _isAdvertising = true;
            BroadcastStatus("Advertising");
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("BleHrAdvertiser", $"StartAdvertising failed: {ex.Message}");
            BroadcastStatus($"Error: {ex.Message}");
            throw;
        }
    }

    public void StopAdvertising()
    {
        if (!_isAdvertising) return;

        try
        {
            _advertiser?.StopAdvertising(new BleAdvertiseCallback());
            _gattServer?.Close();
            _gattServer = null;
        }
        catch { }

        _isAdvertising = false;
        _connectedDevices.Clear();
        BroadcastStatus("Idle");
    }

    private void SetupGattServer()
    {
        _gattServer = _bluetoothManager?.OpenGattServer(_context, new HrGattServerCallback(this));

        if (_gattServer == null)
        {
            throw new InvalidOperationException("Failed to open GATT server");
        }

        _hrMeasurementCharacteristic = new BluetoothGattCharacteristic(
            HrMeasurementUuid,
            GattProperty.Notify | GattProperty.Read,
            GattPermission.Read);

        var cccdDescriptor = new BluetoothGattDescriptor(
            CccdUuid,
            GattDescriptorPermission.Read | GattDescriptorPermission.Write);
        // .NET 10 API: 直接用 byte[] 替代 DisabledNotificationValue
        cccdDescriptor.SetValue(new byte[] { 0x00, 0x00 });
        _hrMeasurementCharacteristic.AddDescriptor(cccdDescriptor);

        var bodySensorLocationChar = new BluetoothGattCharacteristic(
            HrLocationUuid,
            GattProperty.Read,
            GattPermission.Read);
        bodySensorLocationChar.SetValue(new byte[] { 2 });

        var hrService = new BluetoothGattService(HrServiceUuid, GattServiceType.Primary);
        hrService.AddCharacteristic(_hrMeasurementCharacteristic);
        hrService.AddCharacteristic(bodySensorLocationChar);

        _gattServer.AddService(hrService);
    }

    private void StartBleAdvertising()
    {
        _advertiser = _bluetoothAdapter?.BluetoothLeAdvertiser;

        if (_advertiser == null)
        {
            throw new InvalidOperationException("BLE advertiser not available");
        }

        // .NET 10: 用整型常量替代枚举属性
        var settings = new AdvertiseSettings.Builder()
            .SetAdvertiseMode(AdvertiseMode.LowLatency)   // 枚举，不是 0/1/2
            .SetTxPowerLevel(AdvertiseTx.PowerHigh)        // 枚举，不是 3
            .SetConnectable(true)
            .Build();

        var data = new AdvertiseData.Builder()
            .SetIncludeDeviceName(true)
            .AddServiceUuid(new ParcelUuid(HrServiceUuid))
            .Build();

        var callback = new BleAdvertiseCallback();
        callback.OnStartSuccessAction = () =>
        {
            global::Android.Util.Log.Info("BleHrAdvertiser", "Advertising started successfully");
            BroadcastStatus("Advertising");
        };
        callback.OnStartFailureAction = errorCode =>
        {
            global::Android.Util.Log.Error("BleHrAdvertiser", $"Advertising failed: {errorCode}");
            BroadcastStatus($"Adv Error: {errorCode}");
        };

        _advertiser.StartAdvertising(settings, data, callback);
    }

    public void UpdateHeartRateValue(int heartRate)
    {
        _lastHeartRate = heartRate;

        if (_hrMeasurementCharacteristic == null || _gattServer == null) return;

        byte flags = 0x06;

        byte[] hrData;
        if (heartRate > 255)
        {
            flags |= 0x01;
            hrData = new byte[3];
            hrData[0] = flags;
            hrData[1] = (byte)(heartRate & 0xFF);
            hrData[2] = (byte)((heartRate >> 8) & 0xFF);
        }
        else
        {
            hrData = new byte[2];
            hrData[0] = flags;
            hrData[1] = (byte)heartRate;
        }

        _hrMeasurementCharacteristic.SetValue(hrData);

        foreach (var device in _connectedDevices.ToList())
        {
            try
            {
                _gattServer.NotifyCharacteristicChanged(device, _hrMeasurementCharacteristic, false);
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("BleHrAdvertiser", $"Notify failed for device: {ex.Message}");
                _connectedDevices.Remove(device);
            }
        }

        BroadcastStatus($"Connected:{_connectedDevices.Count} HR:{heartRate}");
    }

    internal void OnDeviceConnected(BluetoothDevice device)
    {
        if (!_connectedDevices.Contains(device))
        {
            _connectedDevices.Add(device);
            BroadcastStatus($"Connected:{_connectedDevices.Count}");
        }
    }

    internal void OnDeviceDisconnected(BluetoothDevice device)
    {
        _connectedDevices.Remove(device);
        BroadcastStatus($"Connected:{_connectedDevices.Count}");
    }

    private void BroadcastStatus(string status)
    {
        var intent = new Intent(ActionBleStatusUpdated);
        intent.PutExtra("status", status);
        _context.SendBroadcast(intent);
    }

    public void Dispose()
    {
        StopAdvertising();
    }
}

public class BleAdvertiseCallback : AdvertiseCallback
{
    public Action? OnStartSuccessAction { get; set; }
    public Action<int>? OnStartFailureAction { get; set; }

    public override void OnStartSuccess(AdvertiseSettings? settingsInEffect)
    {
        OnStartSuccessAction?.Invoke();
    }

    public override void OnStartFailure([global::Android.Runtime.GeneratedEnum] AdvertiseFailure errorCode)
    {
        OnStartFailureAction?.Invoke((int)errorCode);
    }
}

public class HrGattServerCallback : BluetoothGattServerCallback
{
    private readonly BleHrAdvertiserService _service;

    public HrGattServerCallback(BleHrAdvertiserService service)
    {
        _service = service;
    }

    public override void OnConnectionStateChange(BluetoothDevice? device, [global::Android.Runtime.GeneratedEnum] ProfileState status, [global::Android.Runtime.GeneratedEnum] ProfileState newState)
    {
        if (device == null) return;

        if (newState == ProfileState.Connected)
        {
            global::Android.Util.Log.Info("BleHrAdvertiser", $"Device connected: {device.Address}");
            _service.OnDeviceConnected(device);
        }
        else if (newState == ProfileState.Disconnected)
        {
            global::Android.Util.Log.Info("BleHrAdvertiser", $"Device disconnected: {device.Address}");
            _service.OnDeviceDisconnected(device);
        }
    }

    public override void OnDescriptorReadRequest(BluetoothDevice? device, int requestId, int offset, BluetoothGattDescriptor? descriptor)
    {
        global::Android.Util.Log.Debug("BleHrAdvertiser", "Descriptor read request");
    }

    public override void OnDescriptorWriteRequest(BluetoothDevice? device, int requestId, BluetoothGattDescriptor? descriptor, bool preparedWrite, bool responseNeeded, int offset, byte[]? value)
    {
        global::Android.Util.Log.Debug("BleHrAdvertiser", "Descriptor write request");
    }

    public override void OnMtuChanged(BluetoothDevice? device, int mtu)
    {
        global::Android.Util.Log.Info("BleHrAdvertiser", $"MTU changed to {mtu}");
    }
}