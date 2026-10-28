using Android.App;
using Android.Content;
using Android.OS;

namespace OrbisExtract.App.Android;

/// <summary>
/// Mantém o processo do aplicativo em primeiro plano durante a extração.
/// O trabalho de extração continua no fluxo existente, enquanto este serviço
/// impede que o Android trate o aplicativo como um processo comum em segundo plano.
/// </summary>
[Service(
    Name = "com.tatsuya.orbisextract.ExtractionForegroundService",
    Exported = false,
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public sealed class ExtractionForegroundService : Service
{
    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var action = intent?.Action;
        if (action == AndroidNotificationHelper.ActionTogglePause)
        {
            AndroidNotificationHelper.RequestTogglePause();
            return StartCommandResult.Sticky;
        }
        if (action == AndroidNotificationHelper.ActionPause)
        {
            AndroidNotificationHelper.RequestPause();
            return StartCommandResult.Sticky;
        }
        if (action == AndroidNotificationHelper.ActionResume)
        {
            AndroidNotificationHelper.RequestResume();
            return StartCommandResult.Sticky;
        }
        if (action == AndroidNotificationHelper.ActionFinish || action == AndroidNotificationHelper.ActionCancel)
        {
            if (action == AndroidNotificationHelper.ActionCancel)
                AndroidNotificationHelper.RequestCancellation();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                StopForeground(StopForegroundFlags.Remove);
            else
                StopForeground(true);
            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }

        if (action == AndroidNotificationHelper.ActionStart)
        {
            // Android exige que o serviço entre em foreground imediatamente.
            StartForeground(AndroidNotificationHelper.NotificationId,
                AndroidNotificationHelper.BuildNotification(ApplicationContext, true));
        }
        return StartCommandResult.Sticky;
    }
}
