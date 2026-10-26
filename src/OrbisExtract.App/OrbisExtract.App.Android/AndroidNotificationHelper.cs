using System;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace OrbisExtract.App.Android;

/// <summary>Notificações persistentes e integração com o serviço foreground da extração.</summary>
public static class AndroidNotificationHelper
{
    internal const string ChannelId = "orbis_extract_channel";
    internal const int NotificationId = 1;
    internal const string ActionStart = "orbisextract.action.START";
    internal const string ActionFinish = "orbisextract.action.FINISH";
    internal const string ActionCancel = "orbisextract.action.CANCEL";
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
        _lastState = string.IsNullOrWhiteSpace(state) ? "Extraindo arquivos" : state;
        _lastCurrentFile = currentFile;
        NotificationManagerCompat.From(context).Notify(NotificationId, BuildNotification(context, true));
    }

    internal static Notification BuildNotification(Context context, bool ongoing)
    {
        var detail = _lastCurrentFile is not null
            ? _lastCurrentFile
            : (_lastTotal > 0 ? $"{_lastFiles:N0} de {_lastTotal:N0} arquivos · {_lastProgress}%" : $"{_lastFiles:N0} arquivos processados");
        var builder = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(SmallIcon(context))
            .SetContentTitle(ongoing ? $"OrbisExtract · {_lastState}" : _lastState)
            .SetContentText($"{_packageName} → {_destinationName}")
            .SetSubText(detail)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText($"{_packageName}\nDestino: {_destinationName}\n{detail}"))
            .SetOnlyAlertOnce(true)
            .SetOngoing(ongoing)
            .SetCategory(NotificationCompat.CategoryProgress)
            .SetPriority((int)NotificationPriority.Low)
            .SetProgress(100, _lastProgress, _lastTotal <= 0)
            .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
            .SetShowWhen(true);

        if (ongoing)
            builder.AddAction(0, "Em andamento", PendingIntentForStatus(context));

        return builder.Build();
    }

    private static PendingIntent PendingIntentForStatus(Context context)
    {
        var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName);
        intent ??= new Intent(context, typeof(MainActivity));
        return PendingIntent.GetActivity(context, 10, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    public static void CompleteExtraction(long filesExtracted, long bytesExtracted)
    {
        var context = _context;
        if (context is null) return;
        _lastState = "Extração concluída";
        _lastFiles = filesExtracted;
        _lastTotal = filesExtracted;
        _lastProgress = 100;
        _lastCurrentFile = $"{filesExtracted:N0} arquivos · {bytesExtracted / 1024d / 1024d:N1} MiB";
        var notification = BuildNotification(context, false);
        NotificationManagerCompat.From(context).Notify(NotificationId, notification);
        FinishService(context, ActionFinish);
    }

    public static void CancelExtraction()
    {
        var context = _context;
        if (context is null) return;
        FinishService(context, ActionCancel);
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
        var channel = new NotificationChannel(ChannelId, "OrbisExtract · Extração", NotificationImportance.Low)
        {
            Description = "Progresso da extração de pacotes PKG em segundo plano"
        };
        channel.EnableVibration(false);
        channel.SetSound(null, null);
        NotificationManagerCompat.From(context).CreateNotificationChannel(channel);
    }
}
