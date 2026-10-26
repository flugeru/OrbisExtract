using Android.App;
using Android.Content;
using Android.OS;

namespace OrbisExtract.App.Android;

/// <summary>
/// Keeps the process important to Android while a user-started PKG extraction is active.
/// The extraction pipeline remains owned by the app process so SAF document permissions
/// and the selected destination stay valid.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class ExtractionForegroundService : Service
{
    private PowerManager.WakeLock? _wakeLock;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        AndroidNotificationHelper.Initialize(ApplicationContext);
        var notification = AndroidNotificationHelper.BuildForegroundNotification(
            intent?.GetStringExtra("packageName") ?? "PKG",
            intent?.GetStringExtra("destinationName") ?? "Pasta selecionada");

        StartForeground(AndroidNotificationHelper.NotificationId, notification);

        try
        {
            var powerManager = (PowerManager?)GetSystemService(PowerService);
            if (powerManager is not null && (_wakeLock is null || !_wakeLock.IsHeld))
            {
                _wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, "OrbisExtract:Extraction");
                _wakeLock.SetReferenceCounted(false);
                _wakeLock.Acquire();
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("OrbisExtract", $"Não foi possível adquirir o wake lock: {ex.Message}");
        }

        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        try
        {
            if (_wakeLock?.IsHeld == true)
                _wakeLock.Release();
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("OrbisExtract", $"Falha ao liberar wake lock: {ex.Message}");
        }
        finally
        {
            _wakeLock?.Dispose();
            _wakeLock = null;
            base.OnDestroy();
        }
    }
}
