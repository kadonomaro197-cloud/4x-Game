using System;
using System.Diagnostics;
using System.IO;
using Pulsar4X.Client;

// Route all Console and Trace output to a log file so every session is recordable. Per the developer: keep it
// OUT of %AppData% and in the repo ROOT (next to console_output.txt / launch.bat) so it's easy to find + paste.
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
var logPath = Path.Combine(logDir, "game_log.txt");
using var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
Console.SetOut(logWriter);
Console.SetError(logWriter);
Trace.Listeners.Add(new TextWriterTraceListener(logWriter, "fileLog"));
Trace.AutoFlush = true;

Console.WriteLine($"=== Pulsar4X session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
Console.WriteLine($"Log: {logPath}");

using (var pulsar = new PulsarMainWindow(args))
{
    pulsar.Run();
}

Console.WriteLine($"=== Session ended {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
