using System;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using OrbisExtract.App.Localization;

namespace OrbisExtract.App.Android;

/// <summary>Notificações persistentes e integração com o serviço foreground da extração.</summary>
public static class AndroidNotificationHelper
{
    internal const string ChannelId = "orbis_extract_channel";
    internal const int NotificationId = 1;
    internal const string ActionStart = "orbisextract.action.START";
    internal const string ActionFinish = "orbisextract.action.FINISH";
    internal const string ActionPause = "orbisextract.action.PAUSE";
    internal const string ActionResume = "orbisextract.action.RESUME";
    internal const string ActionCancel = "orbisextract.action.CANCEL";
    public static event Action? PauseRequested;
    public static event Action? ResumeRequested;
    internal static void RequestPause() => PauseRequested?.Invoke();
    internal static void RequestResume() => ResumeRequested?.Invoke();
    private static bool _paused;
    public static event Action? CancellationRequested;
    internal static void RequestCancellation() => CancellationRequested?.Invoke();
    private static Context? _context;
    private static int _smallIcon;
    private static string _packageName = "PKG";
    private static string _destinationName = "Destino selecionado";
    private static int _lastProgress;
    private static long _lastFiles;
    private static long _lastTotal;
    private static string _lastState = "Preparando extração";
    private static string? _lastCurrentFile;

    private static int SmallIcon(Context context)
    {
        if (_smallIcon == 0)
            _smallIcon = context.Resources.GetIdentifier("icon", "drawable", context.PackageName);
        return _smallIcon != 0 ? _smallIcon : context.ApplicationInfo!.Icon;
    }

    public static void Initialize(Context context)
    {
        _context = context.ApplicationContext;
        CreateNotificationChannel();
    }

    public static void StartExtraction(string packageName, string destinationName, long totalFiles, long totalBytes)
    {
        var context = _context;
        if (context is null) return;

        _packageName = packageName;
        _destinationName = destinationName;
        _paused = false;
        _lastProgress = 0;
        _lastFiles = 0;
        _lastTotal = totalFiles;
        _lastState = "Preparando extração";
        _lastCurrentFile = null;
        CreateNotificationChannel();

        var intent = new Intent(context, typeof(ExtractionForegroundService));
        intent.SetAction(ActionStart);
        intent.PutExtra("packageName", packageName);
        intent.PutExtra("destinationName", destinationName);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);

        NotificationManagerCompat.From(context).Notify(NotificationId, BuildNotification(context, true));
    }

    public static void UpdateProgress(long currentFiles, long totalFiles, string state, string? currentFile = null)
    {
        var context = _context;
        if (context is null) return;

        _lastFiles = currentFiles;
        _lastTotal = totalFiles;
        _lastProgress = totalFiles > 0 ? (int)Math.Clamp(currentFiles * 100.0 / totalFiles, 0, 100) : 0;
        if (_paused) return;
        _lastState = string.IsNullOrWhiteSpace(state) ? "Extraindo arquivos" : state;
        _lastCurrentFile = currentFile;
        NotificationManagerCompat.From(context).Notify(NotificationId, BuildNotification(context, true));
    }

    internal static Notification BuildNotification(Context context, bool ongoing)
    {
        var detail = _lastCurrentFile is not null
            ? _lastCurrentFile
            : (_lastTotal > 0 ? $"{_lastFiles:N0} {AppLanguage.Text("de")} {_lastTotal:N0} {AppLanguage.Text("arquivos")} · {_lastProgress}%" : $"{_lastFiles:N0} {AppLanguage.Text("arquivos processados")}");
        var builder = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(SmallIcon(context))
            .SetContentTitle(ongoing ? $"OrbisExtract · {AppLanguage.Text(_lastState)}" : AppLanguage.Text(_lastState))
            .SetContentText($"{_packageName} → {_destinationName}")
            .SetSubText(detail)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText($"{_packageName}\n{AppLanguage.Text("Destino:")} {_destinationName}\n{detail}"))
            .SetOnlyAlertOnce(true)
            .SetOngoing(ongoing)
            .SetCategory(NotificationCompat.CategoryProgress)
            .SetPriority((int)NotificationPriority.Low)
            .SetProgress(100, _lastProgress, _lastTotal <= 0)
            .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
            .SetShowWhen(true)
            .SetContentIntent(PendingIntentForStatus(context));

        if (ongoing)
        {
            var pauseIntent = new Intent(context, typeof(ExtractionForegroundService));
            pauseIntent.SetAction(_paused ? ActionResume : ActionPause);
            var pausePending = PendingIntent.GetService(context, _paused ? 13 : 12, pauseIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            builder.AddAction(global::Android.Resource.Drawable.IcMediaPause, _paused ? AppLanguage.Text("Retomar") : AppLanguage.Text("Pausar"), pausePending);
            var cancelIntent = new Intent(context, typeof(ExtractionForegroundService));
            cancelIntent.SetAction(ActionCancel);
            var cancelPending = PendingIntent.GetService(context, 11, cancelIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            builder.AddAction(global::Android.Resource.Drawable.IcMenuCloseClearCancel, AppLanguage.Text("Cancelar"), cancelPending);
        }
        return builder.Build();
    }

    private static PendingIntent PendingIntentForStatus(Context context)
    {
        var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName);
        intent ??= new Intent(context, typeof(MainActivity));
        return PendingIntent.GetActivity(context, 10, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    internal static void SetPaused(bool paused)
    {
        _paused = paused;
        var context = _context;
        if (context is null) return;
        _lastState = paused ? "Pausado" : "Extraindo";
        NotificationManagerCompat.From(context).Notify(NotificationId, BuildNotification(context, true));
    }

    public static void CompleteExtraction(long filesExtracted, long bytesExtracted)
    {
        var context = _context;
        if (context is null) return;
        _paused = false;
        _lastState = "Extração concluída";
        _lastFiles = filesExtracted;
        _lastTotal = filesExtracted;
        _lastProgress = 100;
        _lastCurrentFile = $"{filesExtracted:N0} {AppLanguage.Text("arquivos")} · {bytesExtracted / 1024d / 1024d:N1} MiB";
        var notification = BuildNotification(context, false);
        FinishService(context, ActionFinish);
        NotificationManagerCompat.From(context).Notify(NotificationId, notification);
    }

    public static void CancelExtraction()
    {
        var context = _context;
        if (context is null) return;
        NotificationManagerCompat.From(context).Cancel(NotificationId);
        FinishService(context, ActionFinish);
    }

    public static void ShowError(string message)
    {
        var context = _context;
        if (context is null) return;
        _lastState = "Falha na extração";
        _lastCurrentFile = message;
        NotificationManagerCompat.From(context).Notify(NotificationId, BuildNotification(context, false));
        FinishService(context, ActionFinish);
    }

    private static void FinishService(Context context, string action)
    {
        var intent = new Intent(context, typeof(ExtractionForegroundService));
        intent.SetAction(action);
        context.StartService(intent);
    }

    private static void CreateNotificationChannel()
    {
        var context = _context;
        if (context is null || Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var channel = new NotificationChannel(ChannelId, AppLanguage.Text("OrbisExtract · Extração"), NotificationImportance.Low)
        {
            Description = AppLanguage.Text("Progresso da extração de pacotes PKG em segundo plano")
        };
        channel.EnableVibration(false);
        channel.SetSound(null, null);
        NotificationManagerCompat.From(context).CreateNotificationChannel(channel);
    }
}
