using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace HeartRateBroadcaster;

[Activity(
    Label = "HR Broadcast",
    Theme = "@style/Maui.MainTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait)]
public class MainActivity : MauiAppCompatActivity
{
    private const int RequestPermissionsCode = 1001;

    private const string PermissionBluetoothAdvertise = "android.permission.BLUETOOTH_ADVERTISE";
    private const string PermissionBluetoothConnect = "android.permission.BLUETOOTH_CONNECT";
    private const string PermissionForegroundService = "android.permission.FOREGROUND_SERVICE";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestRequiredPermissions();
    }

    private void RequestRequiredPermissions()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            return;

        var permissions = new List<string>
        {
            Android.Manifest.Permission.Bluetooth,
            Android.Manifest.Permission.BluetoothAdmin,
            Android.Manifest.Permission.AccessFineLocation,
            Android.Manifest.Permission.AccessCoarseLocation,
            Android.Manifest.Permission.BodySensors,
        };

        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            permissions.Add(PermissionBluetoothAdvertise);
            permissions.Add(PermissionBluetoothConnect);
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
            permissions.Add(PermissionForegroundService);
        }

        var permissionsToRequest = new List<string>();
        foreach (var permission in permissions)
        {
            if (ContextCompat.CheckSelfPermission(this, permission) != Permission.Granted)
            {
                permissionsToRequest.Add(permission);
            }
        }

        if (permissionsToRequest.Count > 0)
        {
            ActivityCompat.RequestPermissions(this, permissionsToRequest.ToArray(), RequestPermissionsCode);
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }
}
