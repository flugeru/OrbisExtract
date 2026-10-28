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
            OrbisExtract.App.Android.AndroidNotificationHelper.Initialize(global::Android.App.Application.Context);
            _initialized = true;
        }
    }

    public static event Action? PauseRequested { add => OrbisExtract.App.Android.AndroidNotificationHelper.PauseRequested += value; remove => OrbisExtract.App.Android.AndroidNotificationHelper.PauseRequested -= value; }
    public static event Action? ResumeRequested { add => OrbisExtract.App.Android.AndroidNotificationHelper.ResumeRequested += value; remove => OrbisExtract.App.Android.AndroidNotificationHelper.ResumeRequested -= value; }
    public static void SetPaused(bool paused) => OrbisExtract.App.Android.AndroidNotificationHelper.SetPaused(paused);
    public static event Action? CancellationRequested
    {
        add => OrbisExtract.App.Android.AndroidNotificationHelper.CancellationRequested += value;
        remove => OrbisExtract.App.Android.AndroidNotificationHelper.CancellationRequested -= value;
    }

    public static void StartExtraction(string packageName, string destinationName, long totalFiles, long totalBytes)
    {
        OrbisExtract.App.Android.AndroidNotificationHelper.StartExtraction(packageName, destinationName, totalFiles, totalBytes);
    }

    public static void UpdateProgress(long currentFiles, long totalFiles, string state, string? currentFile = null)
    {
        OrbisExtract.App.Android.AndroidNotificationHelper.UpdateProgress(currentFiles, totalFiles, state, currentFile);
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
    public static event Action? PauseRequested { add { } remove { } }
    public static event Action? ResumeRequested { add { } remove { } }
    public static void SetPaused(bool paused) { }
    public static event Action? CancellationRequested { add { } remove { } }
    public static void StartExtraction(string packageName, string destinationName, long totalFiles, long totalBytes)
    {
        System.Diagnostics.Debug.WriteLine($"Extração iniciada (placeholder): {packageName} -> {destinationName}");
    }

    public static void UpdateProgress(long currentFiles, long totalFiles, string state, string? currentFile = null)
    {
        System.Diagnostics.Debug.WriteLine($"Progresso (placeholder): {state} ({currentFiles}/{totalFiles})");
    }

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
