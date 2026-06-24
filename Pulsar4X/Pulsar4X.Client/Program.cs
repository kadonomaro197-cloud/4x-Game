using System;
using System.Diagnostics;
using System.IO;
using Pulsar4X.Client;

// Route all Console and Trace output to a log file so every session is recordable.
var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Pulsar4X", "Pulsar4X");
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
