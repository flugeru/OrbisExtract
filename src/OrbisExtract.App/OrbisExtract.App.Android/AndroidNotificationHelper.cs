using System;
using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace OrbisExtract.App.Android;

public static class AndroidNotificationHelper
{
    private const string CHANNEL_ID = "orbis_extract_channel";
    private const string NOTIFICATION_TAG = "OrbisExtractProgress";
    private static int _notificationId = 1;
    private static ApplicationContext? _context;

    public static void Initialize(ApplicationContext context)
    {
        _context = context;
        CreateNotificationChannel();
    }

    public static void StartExtraction(string packageName, string destinationName, long totalFiles, long totalBytes)
    {
        if (_context == null) return;

        var notification = new NotificationCompat.Builder(_context, CHANNEL_ID)
            .SetSmallIcon(Resource.Mipmap.AppIcon)
            .SetContentTitle($"Extração: {packageName}")
            .SetSubText($"Destino: {destinationName}")
            .SetProgress(100, 0, totalFiles > 0)
            .SetOngoing(true)
            .SetShowsWhenCancelled(true)
            .Build();

        var service = new ExtractionNotificationService();
        service.StartForeground(_notificationId++, notification);
        
        // Envia broadcast para atualizar a notificação sem criar nova instância do serviço
        var intent = new Intent(_context, typeof(ExtractionNotificationService));
        intent.SetAction(Android.Content.Intent.ActionBroadcast);
        intent.PutExtra("tag", NOTIFICATION_TAG);
        _context.SendBroadcast(intent);
    }

    public static void UpdateProgress(long currentFiles, long totalFiles, string state, string? currentFile = null)
    {
        if (_context == null) return;

        var progress = totalFiles > 0 ? (int)(currentFiles * 100.0 / totalFiles) : 0;
        
        var notification = new NotificationCompat.Builder(_context, CHANNEL_ID)
            .SetSmallIcon(Resource.Mipmap.AppIcon)
            .SetContentTitle(state)
            .SetSubText(currentFile ?? $"Progresso: {progress}%")
            .SetProgress(100, progress, totalFiles > 0)
            .SetOngoing(true)
            .SetShowsWhenCancelled(true)
            .Build();

        var nm = NotificationManagerCompat.From(_context);
        nm.Notify(_notificationId++, notification);
        
        // Envia broadcast para atualizar a notificação sem criar nova instância do serviço
        var intent = new Intent(_context, typeof(ExtractionNotificationService));
        intent.SetAction(Android.Content.Intent.ActionBroadcast);
        intent.PutExtra("tag", NOTIFICATION_TAG);
        _context.SendBroadcast(intent);
    }

    public static void CompleteExtraction(long filesExtracted, long bytesExtracted)
    {
        if (_context == null) return;

        var notification = new NotificationCompat.Builder(_context, CHANNEL_ID)
            .SetSmallIcon(Resource.Mipmap.AppIcon)
            .SetContentTitle("Extração Concluída")
            .SetSubText($"{filesExtracted} arquivos extraídos")
            .SetProgress(0, 0, false)
            .SetOngoing(false)
            .SetAutoCancel(true)
            .Build();

        var nm = NotificationManagerCompat.From(_context);
        nm.Notify(_notificationId++, notification);
        
        // Envia broadcast para atualizar a notificação sem criar nova instância do serviço
        var intent = new Intent(_context, typeof(ExtractionNotificationService));
        intent.SetAction(Android.Content.Intent.ActionBroadcast);
        intent.PutExtra("tag", NOTIFICATION_TAG);
        _context.SendBroadcast(intent);
    }

    public static void CancelExtraction()
    {
        if (_context == null) return;

        var nm = NotificationManagerCompat.From(_context);
        nm.Cancel(_notificationId, NOTIFICATION_TAG);
        
        // Envia broadcast para atualizar a notificação sem criar nova instância do serviço
        var intent = new Intent(_context, typeof(ExtractionNotificationService));
        intent.SetAction(Android.Content.Intent.ActionBroadcast);
        intent.PutExtra("tag", NOTIFICATION_TAG);
        _context.SendBroadcast(intent);
    }

    public static void ShowError(string message)
    {
        if (_context == null) return;

        var notification = new NotificationCompat.Builder(_context, CHANNEL_ID)
            .SetSmallIcon(Resource.Mipmap.AppIcon)
            .SetContentTitle("Erro na Extração")
            .SetSubText(message)
            .SetProgress(0, 0, false)
            .SetOngoing(false)
            .SetAutoCancel(true)
            .Build();

        var nm = NotificationManagerCompat.From(_context);
        nm.Notify(_notificationId++, notification);
        
        // Envia broadcast para atualizar a notificação sem criar nova instância do serviço
        var intent = new Intent(_context, typeof(ExtractionNotificationService));
        intent.SetAction(Android.Content.Intent.ActionBroadcast);
        intent.PutExtra("tag", NOTIFICATION_TAG);
        _context.SendBroadcast(intent);
    }

    private static void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(CHANNEL_ID, "OrbisExtract - Extração", 
                NotificationImportance.High);
            channel.EnableVibration(false);
            channel.SetSound(null, null);
            
            var nm = GetNotificationManager();
            nm.CreateNotificationChannel(channel);
        }
    }

    private static NotificationManagerCompat GetNotificationManager() => 
        _context != null ? NotificationManagerCompat.From(_context) : throw new InvalidOperationException("Contexto não inicializado");
}
