namespace Link.Examples._011_Logger_Initialization;

/// <summary>
/// This example shows how to initialize loggers. Loggers are very useful
/// as they output useful information which will help you diagnose possible
/// errors in the application.
/// <br/><br/>
/// Loggers are extensively called by the library, but by default they do
/// nothing. To actually show logs, we need to set-up loggers. Fortunately,
/// this process is very easy and requires very little effort.
/// </summary>
public static class LoggerInitialization
{
    public static void Main()
    {
        BasicLoggerInitialization();
        AdvancedLoggerInitialization();

        // We also can (and should!) use loggers ourselves:
        Log.Info("Useful information that helps during development.");
        Log.Warning("Warning, something might be wrong!");
        Log.Error("Something went definitely wrong!");
    }

    private static void BasicLoggerInitialization()
    {
        // Log useful information to the standard output stream.
        Log.Info = Console.Out.WriteLine;
        
        // Same with warnings...
        Log.Warning = Console.Out.WriteLine;
        
        // Errors get logged to the standard error output stream.
        Log.Error = Console.Error.WriteLine;
    }

    private static void AdvancedLoggerInitialization()
    {
        // Since loggers are simple delegates, we can execute any custom
        // logging logic that we need. For example, we can easily write
        // current time to each message, just like in this example. Or
        // we could write logging information to an external destination,
        // such as a text file or database.
        Log.Info = message => Console.WriteLine($"[{DateTime.Now}] {message}");
    }
}
