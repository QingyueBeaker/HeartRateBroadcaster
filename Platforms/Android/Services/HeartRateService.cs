using Android.App;
using Android.Content;
using Android.Hardware;
using Android.OS;
using Android.Runtime;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Java.Util;

namespace HeartRateBroadcaster.Platforms.Android.Services;

[Service]
public class HeartRateService : Service, ISensorEventListener
{
    public const string ActionStart = "com.oppo.hrbroadcast.ACTION_START";
    public const string ActionStop = "com.oppo.hrbroadcast.ACTION_STOP";
    public const string ActionHeartRateUpdated = "com.oppo.hrbroadcast.ACTION_HR_UPDATED";

    private SensorManager? _sensorManager;
    private Sensor? _heartRateSensor;
    private PowerManager.WakeLock? _wakeLock;
    private BleHrAdvertiserService? _bleService;
    private HeartRateServiceBinder? _binder;
    private int _currentHeartRate;
    private bool _isRunning;

    public int CurrentHeartRate => _currentHeartRate;

    public override void OnCreate()
    {
        base.OnCreate();
        _sensorManager = GetSystemService(SensorService) as SensorManager;
        _heartRateSensor = _sensorManager?.GetDefaultSensor(SensorType.HeartRate);

        if (_heartRateSensor == null)
        {
            global::Android.Util.Log.Warn("HeartRateService", "TYPE_HEART_RATE sensor not found! Listing all available sensors...");
            var sensors = _sensorManager?.GetSensorList(SensorType.All);
            if (sensors != null)
            {
                foreach (var s in sensors)
                {
                    global::Android.Util.Log.Info("HeartRateService", $"Sensor: type={s.Type} name={s.Name} vendor={s.Vendor}");
                }
            }
        }
        else
        {
            global::Android.Util.Log.Info("HeartRateService", $"Heart rate sensor found: {_heartRateSensor.Name}");
        }

        // Acquire wake lock to keep CPU running
        var powerManager = GetSystemService(PowerService) as PowerManager;
        _wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "HRBroadcast::WakeLock");
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            StopService();
            return StartCommandResult.NotSticky;
        }

        if (!_isRunning)
        {
            StartForegroundService();
            StartHeartRateMonitoring();
            StartBleAdvertising();
            _isRunning = true;
        }

        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent)
    {
        _binder = new HeartRateServiceBinder(this);
        return _binder;
    }

    public override void OnDestroy()
    {
        StopService();
        base.OnDestroy();
    }

    private void StartForegroundService()
    {
        var channelId = "hr_broadcast_channel";
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                channelId,
                "Heart Rate Broadcast",
                NotificationImportance.Low)
            {
                Description = "Broadcasting heart rate via BLE"
            };
            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            notificationManager?.CreateNotificationChannel(channel);
        }

        Notification.Builder builder;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            builder = new Notification.Builder(this, channelId);
        }
        else
        {
            builder = new Notification.Builder(this);
        }

        var notification = builder
            .SetContentTitle("HR Broadcast")
            .SetContentText("Broadcasting heart rate...")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuInfoDetails)
            .SetOngoing(true)
            .Build();

        StartForeground(1001, notification);
    }

    private void StartHeartRateMonitoring()
    {
        if (_sensorManager == null || _heartRateSensor == null)
        {
            global::Android.Util.Log.Warn("HeartRateService", "Heart rate sensor not available, using simulated data (60-120 BPM)");
            _ = Task.Run(async () =>
            {
                var random = new global::System.Random();
                while (_isRunning)
                {
                    _currentHeartRate = 60 + random.Next(0, 60);

                    var intent = new Intent(ActionHeartRateUpdated);
                    intent.PutExtra("heart_rate", _currentHeartRate);
                    SendBroadcast(intent);

                    _bleService?.UpdateHeartRateValue(_currentHeartRate);

                    await Task.Delay(1000);
                }
            });
            return;
        }

        global::Android.Util.Log.Info("HeartRateService", "Registering heart rate sensor listener");
        _sensorManager.RegisterListener(this, _heartRateSensor, SensorDelay.Normal);
        _wakeLock?.Acquire();
    }

    private void StartBleAdvertising()
    {
        try
        {
            _bleService = new BleHrAdvertiserService(this);
            _bleService.StartAdvertising();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("HeartRateService", $"Failed to start BLE advertising: {ex.Message}");
        }
    }

    private void StopService()
    {
        if (!_isRunning) return;

        _isRunning = false;

        if (_sensorManager != null && _heartRateSensor != null)
        {
            _sensorManager.UnregisterListener(this);
        }

        _wakeLock?.Release();

        try
        {
            _bleService?.StopAdvertising();
            _bleService?.Dispose();
            _bleService = null;
        }
        catch { }

        StopForeground(true);
        StopSelf();
    }

    // ISensorEventListener implementation
    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Sensor?.Type == SensorType.HeartRate && e.Values?.Count > 0)
        {
            _currentHeartRate = (int)e.Values[0];

            // Broadcast HR update to UI
            var intent = new Intent(ActionHeartRateUpdated);
            intent.PutExtra("heart_rate", _currentHeartRate);
            SendBroadcast(intent);

            // Notify BLE service of new HR value
            _bleService?.UpdateHeartRateValue(_currentHeartRate);
        }
    }

    public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy)
    {
        // Not needed for heart rate
    }
}

public class HeartRateServiceBinder : Binder
{
    private readonly HeartRateService _service;

    public HeartRateServiceBinder(HeartRateService service)
    {
        _service = service;
    }

    public HeartRateService GetService() => _service;
}
