using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace SDBEditor.Handlers
{
    public static class ClipboardManager
    {
        private static readonly BlockingCollection<ClipboardOperation> _operationQueue = new();
        private static readonly CancellationTokenSource _globalCancellation = new();
        private static bool _workerInitialized = false;
        private static readonly object _initLock = new();

        private const int _maxRetries = 5;
        private static readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(50);

        private class ClipboardOperation
        {
            public string Text { get; }
            public TaskCompletionSource<bool> CompletionSource { get; }
            public CancellationToken CancellationToken { get; }

            public ClipboardOperation(string text, CancellationToken token)
            {
                Text = text;
                CompletionSource = new TaskCompletionSource<bool>();
                CancellationToken = token;
            }
        }

        /// <summary>
        /// Initializes the worker thread
        /// </summary>
        private static void EnsureWorkerInitialized()
        {
            if (_workerInitialized) return;

            lock (_initLock)
            {
                if (_workerInitialized) return;

                Task.Factory.StartNew(ProcessQueue,
                    _globalCancellation.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                _workerInitialized = true;
            }
        }

        private static async Task ProcessQueue()
        {
            foreach (var op in _operationQueue.GetConsumingEnumerable(_globalCancellation.Token))
            {
                if (op.CancellationToken.IsCancellationRequested)
                {
                    op.CompletionSource.TrySetCanceled();
                    continue;
                }

                bool success = false;
                for (int attempt = 1; attempt <= _maxRetries && !success; attempt++)
                {
                    if (op.CancellationToken.IsCancellationRequested)
                    {
                        op.CompletionSource.TrySetCanceled();
                        break;
                    }

                    success = TrySetClipboardText(op.Text);
                    if (!success)
                        await Task.Delay(_retryDelay * attempt, op.CancellationToken);
                }

                op.CompletionSource.TrySetResult(success);
            }
        }

        public static Task<bool> CopyToClipboardAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
                return Task.FromResult(false);

            EnsureWorkerInitialized();

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_globalCancellation.Token, cancellationToken);
            var operation = new ClipboardOperation(text, linkedToken.Token);
            _operationQueue.Add(operation);

            return operation.CompletionSource.Task;
        }

        public static async Task<bool> SafeCopyAsync(string text, int timeoutMs = 5000)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                return await CopyToClipboardAsync(text, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Clipboard operation timed out.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SafeCopyAsync error: {ex.Message}");
                return false;
            }
        }

        public static bool SafeCopy(string text)
        {
            _ = SafeCopyAsync(text); // fire-and-forget
            return true;
        }

        public static void Shutdown()
        {
            try
            {
                _globalCancellation.Cancel();
                _operationQueue.CompleteAdding();
            }
            catch { }
        }

        // WIN32 Clipboard Interop

        private const uint CF_UNICODETEXT = 13;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, IntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        private const uint GMEM_MOVEABLE = 0x0002;

        private static bool TrySetClipboardText(string text)
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                    return false;

                if (!EmptyClipboard())
                {
                    CloseClipboard();
                    return false;
                }

                var bytes = (text.Length + 1) * 2; // Unicode = 2 bytes per char + null terminator
                IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (IntPtr)bytes);
                if (hGlobal == IntPtr.Zero)
                {
                    CloseClipboard();
                    return false;
                }

                IntPtr target = GlobalLock(hGlobal);
                if (target == IntPtr.Zero)
                {
                    CloseClipboard();
                    return false;
                }

                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * 2, 0); // null terminator
                GlobalUnlock(hGlobal);

                if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                {
                    CloseClipboard();
                    return false;
                }

                CloseClipboard();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
