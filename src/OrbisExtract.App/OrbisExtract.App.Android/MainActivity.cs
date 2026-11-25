using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Avalonia;
using Avalonia.Android;
using OrbisExtract.App.Shared;
using OrbisExtract.App.Updates;

namespace OrbisExtract.App.Android;

[Activity(
    Label = "OrbisExtract",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private const string CHANNEL_ID = "orbis_extract_channel";
    private PlaySupportBilling? supportBilling;
    internal static MainActivity? Current { get; private set; }
    internal PlaySupportBilling? SupportBilling => supportBilling;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        if (IsInstalledFromPlayStore()) supportBilling = PlaySupportBilling.Create(this);
        UpdateChecker.IsPlayInstall = IsInstalledFromPlayStore;
        UpdateChecker.GetInstalledVersion = () => PackageManager?.GetPackageInfo(PackageName ?? "", 0)?.VersionName ?? "1.0.5";
        UpdateChecker.OpenAndroidUrl = url => StartActivity(new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url)));
        base.OnCreate(savedInstanceState);

        // Cria o canal de notificação imediatamente para evitar erros em versões recentes do Android
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            CreateNotificationChannel();

        // Inicializa o helper de notificações
        AppNotificationHelper.Initialize();

        // Android 13+ exige consentimento em tempo de execução para exibir notificações
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            RequestNotificationPermission();
    }

    internal bool ShareProject(string title, string url)
    {
        using var intent = new Intent(Intent.ActionSend);
        intent.SetType("text/plain");
        intent.PutExtra(Intent.ExtraText, url);
        using var chooser = Intent.CreateChooser(intent, title);
        try { StartActivity(chooser); return true; }
        catch (ActivityNotFoundException) { return false; }
    }

    internal bool RequestSupporterMention(string subject, string body)
    {
        var uri = "mailto:fluugell@gmail.com?subject=" + System.Uri.EscapeDataString(subject) +
            "&body=" + System.Uri.EscapeDataString(body);
        using var intent = new Intent(Intent.ActionSendto, global::Android.Net.Uri.Parse(uri));
        try { StartActivity(intent); return true; }
        catch (ActivityNotFoundException) { return false; }
    }

    private bool IsPlayInstall30(string packageName)
    {
#pragma warning disable CA1416 // Called only when SDK >= 30.
        return PackageManager?.GetInstallSourceInfo(packageName)?.InstallingPackageName == "com.android.vending";
#pragma warning restore CA1416
    }

    private bool IsInstalledFromPlayStore()
    {
        var packageName = PackageName;
        if (string.IsNullOrEmpty(packageName) || PackageManager is null) return false;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            return IsPlayInstall30(packageName);
#pragma warning disable CA1422 // Only reached on Android 29 or earlier.
        return PackageManager.GetInstallerPackageName(packageName) == "com.android.vending";
#pragma warning restore CA1422
    }

    private void RequestNotificationPermission()
    {
#pragma warning disable CA1416 // Called only when SDK >= 33.
        RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, 1001);
#pragma warning restore CA1416
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (supportBilling is not null) _ = supportBilling.RefreshAsync();
    }

    protected override void OnDestroy()
    {
        supportBilling?.Stop();
        if (Current == this) Current = null;
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
#pragma warning disable CA1416 // Called only when SDK >= 26.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                CHANNEL_ID,
                "OrbisExtract - Extração",
                NotificationImportance.High
            );
            channel.EnableVibration(false);
            channel.SetSound(null, null);
            
            var nm = GetNotificationManager();
            nm.CreateNotificationChannel(channel);
        }
#pragma warning restore CA1416
    }

    private NotificationManagerCompat GetNotificationManager() => 
        NotificationManagerCompat.From(this) ?? throw new System.InvalidOperationException("Notification manager unavailable.");
}
