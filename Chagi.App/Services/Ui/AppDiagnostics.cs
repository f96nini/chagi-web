using System.Diagnostics;

namespace ITF_Taekwondo_Gradering;

internal static class AppDiagnostics
{
    static readonly object Sync = new();
    static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()), "Ohanterat fel");
        TaskScheduler.UnobservedTaskException += (_, args) => Log(args.Exception, "Oobserverad bakgrundsuppgift");
#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, args) => Log(args.Exception, "Android-fel");
#endif
    }

    public static void Log(Exception exception, string context)
    {
        Debug.WriteLine($"{context}: {exception}");
        Append("navigation-errors.log", $"{context}{Environment.NewLine}{exception}");
    }

    public static void RecordNavigation(string message) => Append("navigation-events.log", message);
    public static void RecordDataSync(string message) => Append("data-sync-events.log", message);

    static void Append(string fileName, string message)
    {
        try
        {
            lock (Sync)
            {
                var path = Path.Combine(FileSystem.AppDataDirectory, fileName);
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                    File.Move(path, path + ".previous", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch (Exception logError) { Debug.WriteLine(logError); }
    }
}
