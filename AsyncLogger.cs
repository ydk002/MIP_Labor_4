using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Labor3
{
    public class AsyncLogger : IDisposable
    {
        private readonly string _logFilePath;
        private readonly ConcurrentQueue<string> _messageQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _writeTask;
        private readonly SemaphoreSlim _signalNewMessage;

        public AsyncLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
            _messageQueue = new ConcurrentQueue<string>();
            _cancellationTokenSource = new CancellationTokenSource();
            _signalNewMessage = new SemaphoreSlim(0);

            // Start the background writing task
            _writeTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
        }

        /// <summary>
        /// Logs a message asynchronously without waiting for it to be written
        /// </summary>
        public void LogEvent(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formattedMessage = $"[{timestamp}] {message}";
            
            _messageQueue.Enqueue(formattedMessage);
            _signalNewMessage.Release(); // Signal that a new message is available
        }

        /// <summary>
        /// Background task that processes the queue and writes to file
        /// </summary>
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            using (StreamWriter writer = new StreamWriter(_logFilePath, true))
            {
                writer.AutoFlush = true;

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Wait for a signal that a new message is available
                        await _signalNewMessage.WaitAsync(cancellationToken);

                        // Process all messages in the queue
                        while (_messageQueue.TryDequeue(out string message))
                        {
                            await writer.WriteLineAsync(message);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Log errors to debug output (not to file to avoid recursion)
                        System.Diagnostics.Debug.WriteLine($"AsyncLogger error: {ex.Message}");
                    }
                }

                // Write any remaining messages before shutdown
                while (_messageQueue.TryDequeue(out string message))
                {
                    await writer.WriteLineAsync(message);
                }
            }
        }

        /// <summary>
        /// Disposes resources and ensures all messages are written
        /// </summary>
        public void Dispose()
        {
            // Signal cancellation
            _cancellationTokenSource.Cancel();

            // Wait for the write task to complete
            try
            {
                _writeTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions
            }

            _signalNewMessage?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}