using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LLC_Paratranz_Util;

internal class Logger
{
    private const int MaxQueueSize = 1000;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly string _logFilePath;
    private readonly BlockingCollection<string> _logQueue;
    private readonly Task _logWriterTask;
    private StreamWriter _writer;

    public Logger(string logFilePath)
    {
        _logFilePath = logFilePath;

        _logQueue = new BlockingCollection<string>(MaxQueueSize);
        _cancellationTokenSource = new CancellationTokenSource();

        _logWriterTask = Task.Run(() => LogWriterTask(_cancellationTokenSource.Token));
    }

    public StreamWriter Writer =>
        _writer ??= new StreamWriter(
                new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                Encoding.UTF8)
            { AutoFlush = true };

    public void Log(string message)
    {
        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        _logQueue.Add(logMessage);
        Console.WriteLine(logMessage);
    }

    private async Task LogWriterTask(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var logMessage in _logQueue.GetConsumingEnumerable(cancellationToken))
                await Writer.WriteLineAsync(logMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while writing log: {ex.Message}");
        }
    }

    public void StopLogging()
    {
        _cancellationTokenSource.Cancel();
        _logQueue.CompleteAdding();
        _logWriterTask.Wait();
        _writer.Dispose();
    }
}