using System.ComponentModel;
using Android.Content;
using Android.OS;
using HeartRateBroadcaster.Platforms.Android.Services;

namespace HeartRateBroadcaster;

public partial class MainPage : ContentPage
{
    private bool _isBroadcasting;
    private HeartRateServiceConnection? _serviceConnection;
    private HeartRateUpdateReceiver? _heartRateReceiver;
    private BleStatusReceiver? _bleStatusReceiver;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RegisterReceivers();
        RequestBatteryWhitelist(); 
    }

    private void RequestBatteryWhitelist()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var ctx = Android.App.Application.Context;
            var pm = ctx.GetSystemService(Android.Content.Context.PowerService) as Android.OS.PowerManager;
            var pkg = ctx.PackageName;
            if (pm != null && !pm.IsIgnoringBatteryOptimizations(pkg))
            {
                var intent = new Android.Content.Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                intent.SetData(Android.Net.Uri.Parse("package:" + pkg));
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                ctx.StartActivity(intent);
            }
        }
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        UnregisterReceivers();
    }

    private void RegisterReceivers()
    {
        _heartRateReceiver = new HeartRateUpdateReceiver(OnHeartRateUpdated);
        _bleStatusReceiver = new BleStatusReceiver(OnBleStatusUpdated);

        var context = Android.App.Application.Context;
        context.RegisterReceiver(_heartRateReceiver, new IntentFilter(HeartRateService.ActionHeartRateUpdated));
        context.RegisterReceiver(_bleStatusReceiver, new IntentFilter(BleHrAdvertiserService.ActionBleStatusUpdated));
    }

    private void UnregisterReceivers()
    {
        var context = Android.App.Application.Context;
        if (_heartRateReceiver != null)
        {
            context.UnregisterReceiver(_heartRateReceiver);
            _heartRateReceiver = null;
        }
        if (_bleStatusReceiver != null)
        {
            context.UnregisterReceiver(_bleStatusReceiver);
            _bleStatusReceiver = null;
        }
    }

    private void OnSwitchToggled(object? sender, ToggledEventArgs e)
    {
        if (e.Value)
        {
            StartBroadcasting();
        }
        else
        {
            StopBroadcasting();
        }
    }

    private void StartBroadcasting()
    {
        if (_isBroadcasting) return;

        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(HeartRateService));
        intent.SetAction(HeartRateService.ActionStart);

        _serviceConnection = new HeartRateServiceConnection();
        context.BindService(intent, _serviceConnection!, Bind.AutoCreate);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }

        _isBroadcasting = true;
        StatusLabel.Text = "Broadcasting...";
        StatusLabel.TextColor = Colors.Lime;
        SwitchLabel.Text = "Broadcast ON";
        DetailLabel.Text = "Broadcasting HR via BLE";
    }

    private void StopBroadcasting()
    {
        if (!_isBroadcasting) return;

        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(HeartRateService));
        intent.SetAction(HeartRateService.ActionStop);
        context.StopService(intent);

        if (_serviceConnection != null)
        {
            context.UnbindService(_serviceConnection);
            _serviceConnection = null;
        }

        _isBroadcasting = false;
        HeartRateValue.Text = "--";
        StatusLabel.Text = "Stopped";
        StatusLabel.TextColor = Color.FromArgb("#8E8E93");
        SwitchLabel.Text = "Broadcast OFF";
        DetailLabel.Text = "Tap switch to start broadcasting";
        BleStatusLabel.Text = "BLE: Idle";
    }

    private void OnHeartRateUpdated(int heartRate)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            HeartRateValue.Text = heartRate.ToString();
        });
    }

    private void OnBleStatusUpdated(string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BleStatusLabel.Text = $"BLE: {status}";
        });
    }
}

public class HeartRateUpdateReceiver : BroadcastReceiver
{
    private readonly Action<int> _callback;

    public HeartRateUpdateReceiver(Action<int> callback)
    {
        _callback = callback;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == HeartRateService.ActionHeartRateUpdated)
        {
            var hr = intent.GetIntExtra("heart_rate", 0);
            if (hr > 0)
            {
                _callback(hr);
            }
        }
    }
}
public class BleStatusReceiver : BroadcastReceiver
{
    private readonly Action<string> _callback;

    public BleStatusReceiver(Action<string> callback)
    {
        _callback = callback;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == BleHrAdvertiserService.ActionBleStatusUpdated)
        {
            var status = intent.GetStringExtra("status") ?? "Unknown";
            _callback(status);
        }
    }
}

public class HeartRateServiceConnection : Java.Lang.Object, IServiceConnection
{
    public void OnServiceConnected(ComponentName? name, IBinder? service)
    {
    }

    public void OnServiceDisconnected(ComponentName? name)
    {
    }
}
