using System;
using System.Diagnostics;
using System.IO;
using Pulsar4X.Client;

// Route all Console and Trace output to the session log so every session is recordable. Per the developer: keep
// it OUT of %AppData% and in the repo ROOT (next to console_output.txt / launch.bat) so it's easy to find + paste.
// The log ROLLS OVER into read-sized pages under a game_logs/ folder (game_log_000.txt, _001, …) so a whole
// session can be read start-to-finish without hitting a "file too large" wall — see RotatingLogWriter.cs.
// Walk up from the running .exe to the repo root (the folder holding .git or launch.bat); fall back to the exe
// folder, then %AppData% if neither is found.
string logDir;
try
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, "launch.bat")))
        dir = dir.Parent;
    logDir = dir?.FullName ?? AppContext.BaseDirectory;
}
catch
{
    logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pulsar4X", "Pulsar4X");
}
Directory.CreateDirectory(logDir);
var logFolder = Path.Combine(logDir, "game_logs");          // the rolling pages live here
var fallbackLogPath = Path.Combine(logDir, "game_log.txt"); // single-file fallback if rotation can't start
var realConsole = Console.Out;                              // capture BEFORE redirect (rotating writer's fault sink)

TextWriter logWriter;
string logTarget;
try
{
    var rotating = new RotatingLogWriter(logFolder, "game_log_", realConsole);
    logWriter = rotating;
    logTarget = rotating.LogDirectory + " (rolling pages game_log_000.txt, _001, …)";
}
catch
{
    // Rotation couldn't start (folder perms, etc.) — fall back to the original single-file log so we ALWAYS log.
    logWriter = new StreamWriter(fallbackLogPath, append: false) { AutoFlush = true };
    logTarget = fallbackLogPath;
}

using (logWriter)
{
    Console.SetOut(logWriter);
    Console.SetError(logWriter);
    Trace.Listeners.Add(new TextWriterTraceListener(logWriter, "fileLog"));
    Trace.AutoFlush = true;

    Console.WriteLine($"=== Pulsar4X session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    Console.WriteLine($"Log: {logTarget}");

    using (var pulsar = new PulsarMainWindow(args))
    {
        pulsar.Run();
    }

    Console.WriteLine($"=== Session ended {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
}
