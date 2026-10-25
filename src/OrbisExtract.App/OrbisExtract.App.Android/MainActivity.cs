using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Avalonia;
using Avalonia.Android;

namespace OrbisExtract.App.Android;

[Activity(
    Label = "OrbisExtract.App.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private const string CHANNEL_ID = "orbis_extract_channel";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Cria o canal de notificação imediatamente para evitar erros em versões recentes do Android
        CreateNotificationChannel();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
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
    }

    private NotificationManagerCompat GetNotificationManager() => 
        NotificationManagerCompat.From(this);
}
