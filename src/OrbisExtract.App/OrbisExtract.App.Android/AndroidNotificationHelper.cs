using System;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace OrbisExtract.App.Android;

/// <summary>
/// Gerencia as notificações nativas de progresso da extração no Android.
/// Usa sempre o mesmo ID de notificação para atualizar a notificação existente
/// em vez de criar novas notificações a cada atualização.
/// </summary>
public static class AndroidNotificationHelper
{
    private const string CHANNEL_ID = "orbis_extract_channel";
    private const int NOTIFICATION_ID = 1;
    private static Context? _context;
    private static int _smallIcon;

    // O nome do recurso é resolvido em runtime para não depender do nome gerado da
    // classe Resource (a entrada no APK é "icon", comprovado em build publicado).
    private static int SmallIcon(Context context)
    {
        if (_smallIcon == 0)
            _smallIcon = context.Resources.GetIdentifier("icon", "drawable", context.PackageName);
        return _smallIcon;
    }

    public static void Initialize(Context context)
    {
        _context = context;
        CreateNotificationChannel();
    }

    public static void StartExtraction(string packageName, string destinationName, long totalFiles, long totalBytes)
    {
        var context = _context;
        if (context is null) return;

        var notification = new NotificationCompat.Builder(context, CHANNEL_ID)
            .SetSmallIcon(SmallIcon(context))
            .SetContentTitle($"Extraindo: {packageName}")
            .SetSubText($"Destino: {destinationName}")
            .SetProgress(100, 0, totalFiles <= 0)
            .SetOngoing(true)
            .Build();

        NotificationManagerCompat.From(context).Notify(NOTIFICATION_ID, notification);
    }

    public static void UpdateProgress(long currentFiles, long totalFiles, string state, string? currentFile = null)
    {
        var context = _context;
        if (context is null) return;

        var progress = totalFiles > 0 ? (int)(currentFiles * 100.0 / totalFiles) : 0;

        var builder = new NotificationCompat.Builder(context, CHANNEL_ID)
            .SetSmallIcon(SmallIcon(context))
            .SetContentTitle(state)
            .SetProgress(100, progress, totalFiles <= 0)
            .SetOngoing(true);

        if (currentFile is not null)
            builder.SetSubText(currentFile);
        else
            builder.SetSubText(totalFiles > 0 ? $"Progresso: {progress}%" : $"{currentFiles} arquivos");

        NotificationManagerCompat.From(context).Notify(NOTIFICATION_ID, builder.Build());
    }

    public static void CompleteExtraction(long filesExtracted, long bytesExtracted)
    {
        var context = _context;
        if (context is null) return;

        var notification = new NotificationCompat.Builder(context, CHANNEL_ID)
            .SetSmallIcon(SmallIcon(context))
            .SetContentTitle("Extração Concluída")
            .SetSubText($"{filesExtracted} arquivos extraídos")
            .SetProgress(0, 0, false)
            .SetOngoing(false)
            .SetAutoCancel(true)
            .Build();

        NotificationManagerCompat.From(context).Notify(NOTIFICATION_ID, notification);
    }

    public static void CancelExtraction()
    {
        var context = _context;
        if (context is null) return;

        NotificationManagerCompat.From(context).Cancel(NOTIFICATION_ID);
    }

    public static void ShowError(string message)
    {
        var context = _context;
        if (context is null) return;

        var notification = new NotificationCompat.Builder(context, CHANNEL_ID)
            .SetSmallIcon(SmallIcon(context))
            .SetContentTitle("Erro na Extração")
            .SetContentText(message)
            .SetProgress(0, 0, false)
            .SetOngoing(false)
            .SetAutoCancel(true)
            .Build();

        NotificationManagerCompat.From(context).Notify(NOTIFICATION_ID, notification);
    }

    private static void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(CHANNEL_ID, "OrbisExtract - Extração",
                NotificationImportance.High);
            channel.SetSound(null, null);

            var context = _context;
            if (context is not null)
                NotificationManagerCompat.From(context).CreateNotificationChannel(channel);
        }
    }
}
