using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
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
    internal const string ActionTogglePause = "orbisextract.action.TOGGLE_PAUSE";
    internal const string ActionPause = "orbisextract.action.PAUSE";
    internal const string ActionResume = "orbisextract.action.RESUME";
    internal const string ActionCancel = "orbisextract.action.CANCEL";
    public static event Action? TogglePauseRequested;
    internal static void RequestTogglePause() => TogglePauseRequested?.Invoke();
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
    private static TimeSpan _elapsed;
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
        _elapsed = TimeSpan.Zero;
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

        if (_paused) return;
        _lastFiles = currentFiles;
        _lastTotal = totalFiles;
        _lastProgress = totalFiles > 0 ? (int)Math.Clamp(currentFiles * 100.0 / totalFiles, 0, 100) : 0;
        _lastState = string.IsNullOrWhiteSpace(state) ? "Extraindo arquivos" : state;
        _lastCurrentFile = currentFile;
        NotificationManagerCompat.From(context).Notify(NotificationId, BuildNotification(context, true));
    }

    public static void UpdateElapsed(TimeSpan elapsed)
    {
        _elapsed = elapsed;
        var context = _context;
        if (context is not null && _lastState != "Extração concluída")
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
            .SetContentText($"{_packageName} · {detail}")
            .SetSubText(_destinationName)
            .SetOnlyAlertOnce(true)
            .SetOngoing(ongoing)
            .SetCategory(NotificationCompat.CategoryProgress)
            .SetPriority((int)NotificationPriority.Default)
            .SetVisibility(NotificationCompat.VisibilityPublic)
            .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
            .SetShowWhen(true)
            .SetContentIntent(PendingIntentForStatus(context));

        if (ongoing)
        {
            // MIUI/HyperOS Classic often hides standard notification actions.
            // Place real PendingIntent controls inside the compact content itself.
            var compact = BuildCompactControls(context, detail);
            builder.SetCustomContentView(compact)
                .SetCustomBigContentView(compact)
                .SetStyle(new NotificationCompat.DecoratedCustomViewStyle());
        }
        if (!ongoing)
            builder.SetProgress(0, 0, false);
        return builder.Build();
    }

    private static RemoteViews BuildCompactControls(Context context, string detail)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.notification_extraction);
        views.SetTextViewText(Resource.Id.extraction_title, $"OrbisExtract · {AppLanguage.Text(_lastState)}");
        views.SetTextViewText(Resource.Id.extraction_detail, $"{detail} · {(int)_elapsed.TotalMinutes:00}:{_elapsed.Seconds:00}");
        views.SetProgressBar(Resource.Id.extraction_progress, 100, _lastProgress, _lastTotal <= 0);
        views.SetTextViewText(Resource.Id.extraction_pause, AppLanguage.Text(_paused ? "Retomar" : "Pausar"));
        views.SetTextViewText(Resource.Id.extraction_cancel, AppLanguage.Text("Cancelar"));

        var toggleIntent = new Intent(context, typeof(ExtractionForegroundService));
        toggleIntent.SetAction(ActionTogglePause);
        var togglePending = PendingIntent.GetService(context, 12, toggleIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        views.SetOnClickPendingIntent(Resource.Id.extraction_pause, togglePending);

        var cancelIntent = new Intent(context, typeof(ExtractionForegroundService));
        cancelIntent.SetAction(ActionCancel);
        var cancelPending = PendingIntent.GetService(context, 11, cancelIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        views.SetOnClickPendingIntent(Resource.Id.extraction_cancel, cancelPending);
        return views;
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
        FinishService(context, ActionFinish);
        NotificationManagerCompat.From(context).Cancel(NotificationId);
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
