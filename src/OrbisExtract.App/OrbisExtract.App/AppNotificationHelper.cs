using System;

namespace OrbisExtract.App.Shared;

public static class AppNotificationHelper
{
    // Esta classe é usada apenas no Android. Em outras plataformas, este método é um placeholder.
    
#if ANDROID
    private static bool _initialized = false;
    
    public static void Initialize()
    {
        if (!_initialized)
        {
            OrbisExtract.App.Android.AndroidNotificationHelper.Initialize(Android.App.Application.Context);
            _initialized = true;
        }
    }

    public static void CompleteExtraction(long filesExtracted, long bytesExtracted)
    {
        OrbisExtract.App.Android.AndroidNotificationHelper.CompleteExtraction(filesExtracted, bytesExtracted);
    }

    public static void CancelExtraction()
    {
        OrbisExtract.App.Android.AndroidNotificationHelper.CancelExtraction();
    }

    public static void ShowError(string message)
    {
        OrbisExtract.App.Android.AndroidNotificationHelper.ShowError(message);
    }
#else
    // Placeholder para outras plataformas
    public static void CompleteExtraction(long filesExtracted, long bytesExtracted)
    {
        System.Diagnostics.Debug.WriteLine($"Notificação (placeholder): {filesExtracted} arquivos, {bytesExtracted} bytes");
    }

    public static void CancelExtraction()
    {
        System.Diagnostics.Debug.WriteLine("Notificação cancelada (placeholder)");
    }

    public static void ShowError(string message)
    {
        System.Diagnostics.Debug.WriteLine($"Erro (placeholder): {message}");
    }
#endif
}
