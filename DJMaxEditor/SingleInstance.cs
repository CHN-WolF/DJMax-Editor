using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace DJMaxEditor
{
    // Single-instance coordination: the first process owns a named mutex and hosts a named-pipe
    // server; later processes forward their file arguments to it and exit, so every chart opens
    // as a tab in the already-running editor.
    static class SingleInstance
    {
        private const string MutexName = @"Local\DJMaxEditor.SingleInstance";
        private const string PipeName = "DJMaxEditor.SingleInstance.Pipe";
        private const int MaxPayloadBytes = 64 * 1024;
        private const int PollIntervalMs = 200;

        // The returned mutex must stay referenced (or undisposed) for the whole process lifetime:
        // the named mutex only exists while at least one handle to it is open.
        public static Mutex TryAcquireFirst(out bool isFirstInstance)
        {
            bool createdNew;
            var mutex = new Mutex(false, MutexName, out createdNew);
            isFirstInstance = createdNew;
            return mutex;
        }

        // Payload is newline-separated absolute paths (Windows paths cannot contain newlines);
        // an empty payload means "just bring the running window to the front".
        public static bool TrySendToFirstInstance(string[] paths, int timeoutMs)
        {
            string payload = paths == null || paths.Length == 0
                ? string.Empty
                : string.Join("\n", paths);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            if (bytes.Length > MaxPayloadBytes)
            {
                return false;
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            for (;;)
            {
                try
                {
                    using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    {
                        int remaining = (int)Math.Max(1, (deadline - DateTime.UtcNow).TotalMilliseconds);
                        client.Connect(remaining);
                        client.Write(bytes, 0, bytes.Length);
                        client.Flush();
                        try
                        {
                            client.WaitForPipeDrain();
                        }
                        catch (IOException)
                        {
                            // The server may close its end right after reading; the payload
                            // was already delivered, so this is still a success.
                        }
                        return true;
                    }
                }
                catch (TimeoutException)
                {
                    // First instance is still starting up; retry until the deadline.
                }
                catch (IOException)
                {
                    // Server is between listeners; retry until the deadline.
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    return false;
                }
                Thread.Sleep(PollIntervalMs);
            }
        }

        public static IDisposable StartServer(Action<string[]> onFiles)
        {
            var server = new PipeServer(onFiles);
            server.Start();
            return server;
        }

        private sealed class PipeServer : IDisposable
        {
            private readonly Action<string[]> _onFiles;
            private volatile bool _stop;
            private Thread _thread;

            public PipeServer(Action<string[]> onFiles)
            {
                _onFiles = onFiles;
            }

            public void Start()
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "SingleInstancePipeServer" };
                _thread.Start();
            }

            public void Dispose()
            {
                _stop = true;
            }

            private void Run()
            {
                while (!_stop)
                {
                    try
                    {
                        using (var pipe = new NamedPipeServerStream(
                            PipeName,
                            PipeDirection.In,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous))
                        {
                            var connect = pipe.WaitForConnectionAsync();
                            while (!connect.IsCompleted)
                            {
                                if (_stop)
                                {
                                    return;
                                }
                                Thread.Sleep(PollIntervalMs);
                            }
                            if (connect.IsFaulted || connect.IsCanceled)
                            {
                                continue;
                            }

                            string[] paths = ReadPayload(pipe);
                            try
                            {
                                _onFiles(paths);
                            }
                            catch (Exception ex)
                            {
                                Diagnostics.DiagnosticLog.Exception("singleinstance.callback", ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Diagnostics.DiagnosticLog.Exception("singleinstance.server", ex);
                        if (!_stop)
                        {
                            Thread.Sleep(PollIntervalMs);
                        }
                    }
                }
            }

            private static string[] ReadPayload(NamedPipeServerStream pipe)
            {
                using (var ms = new MemoryStream())
                {
                    var buffer = new byte[4096];
                    int read;
                    while ((read = pipe.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                        if (ms.Length > MaxPayloadBytes)
                        {
                            return new string[0];
                        }
                    }
                    string payload = Encoding.UTF8.GetString(ms.ToArray());
                    return payload.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
        }
    }
}
