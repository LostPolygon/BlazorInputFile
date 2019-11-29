using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.JSInterop;

namespace BlazorInputFile {
    internal class RemoteFileListEntryStream : FileListEntryStream {
        private const int RemoteStreamWaitTimeout = 15000;
        private readonly IDisposable _fileUploadHandler;
        private readonly string _uploadRouteUrl;
        private Stream _remoteStream;
        private bool _preparingRemoteStream;
        private TaskCompletionSource<bool> _remoteStreamWaitTask;
        private TaskCompletionSource<bool> _eofWaitTask;
        private long _lastPosition;

        public RemoteFileListEntryStream(
            IJSRuntime jsRuntime,
            ElementReference inputFileElement,
            FileListEntryImpl file,
            FileUploadService fileUploadService,
            IUrlHelper urlHelper
        ) : base(jsRuntime,
            inputFileElement,
            file) {
            _fileUploadHandler =
                fileUploadService.RegisterFileUploadHandler(
                    GetHandlerId(),
                    RemoteStreamReceivedHandler
                );
            _uploadRouteUrl = urlHelper.RouteUrl(FileUploadController.UploadRouteName, new {
                handlerId = GetHandlerId()
            });
            if (_uploadRouteUrl == null)
                throw new InvalidOperationException("Failed to generate upload route");
        }

        public override long Position => _remoteStream?.Position ?? _lastPosition;

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            if (_remoteStreamWaitTask == null) {
                try {
                    await PrepareRemoteStream(cancellationToken);
                } catch (Exception e) {
                    FinishUpload(e);
                    throw;
                }
            }
            else {
                if (_preparingRemoteStream) {
                    while (_remoteStreamWaitTask == null) {
                        await Task.Yield();
                    }

                    await WaitForRemoteStream();
                }
            }

            if (_remoteStream == null)
                throw new IOException("Remote stream is not ready");

            // Sanitize against the client sending arbitrary length files
            int maxBytesToRead = (int) Math.Min(count, Length - Position);
            if (maxBytesToRead == 0) {
                FinishUpload(null);
                return 0;
            }

            int actualBytesRead;
            try {
                actualBytesRead = await _remoteStream.ReadAsync(buffer, offset, maxBytesToRead, cancellationToken);
            } catch (Exception e) {
                IOException ioException = new IOException("Exception while reading from remote stream", e);
                FinishUpload(ioException);
                throw ioException;
            }

            _lastPosition = _remoteStream.Position;
            _file.RaiseOnDataRead();
            return actualBytesRead;
        }

        protected override Task<int> CopyFileDataIntoBuffer(long sourceOffset, byte[] destination, int destinationOffset, int maxBytes, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }

        private async Task PrepareRemoteStream(CancellationToken cancellationToken) {
            try {
                _preparingRemoteStream = true;
                _lastPosition = 0;
                _remoteStreamWaitTask = new TaskCompletionSource<bool>();
                await _jsRuntime.InvokeAsync<string>(
                    "BlazorInputFile.startRemoteUpload",
                    cancellationToken,
                    _inputFileElement,
                    _file.Id,
                    _uploadRouteUrl
                );

                CancellationTokenSource timeoutCts = new CancellationTokenSource(RemoteStreamWaitTimeout);
                CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                linkedCts.Token.Register(() => _remoteStreamWaitTask?.TrySetResult(false));
                await WaitForRemoteStream();
            } finally {
                _preparingRemoteStream = false;
            }
        }

        private async Task WaitForRemoteStream() {
            bool success = await _remoteStreamWaitTask.Task;
            if (!success)
                throw new IOException(
                    "Error getting the remote stream",
                    new TimeoutException("Timed out waiting for remote upload to start")
                );
        }

        private void FinishUpload(Exception remoteStreamReadException) {
            if (remoteStreamReadException == null) {
                _eofWaitTask?.SetResult(true);
            }
            else {
                _eofWaitTask?.SetException(remoteStreamReadException);
            }

            _eofWaitTask = null;
            _remoteStreamWaitTask?.TrySetCanceled();
            _remoteStreamWaitTask = null;
            _remoteStream = null;
        }

        private async Task RemoteStreamReceivedHandler(Stream stream) {
            if (stream == null) {
                _remoteStreamWaitTask.SetException(new IOException("Invalid remote stream"));
                Dispose();
                return;
            }

            // If client tries to upload to a valid handler id when upload wasn't initiated by server
            if (_remoteStreamWaitTask == null)
                throw new InvalidOperationException("Got remote stream when upload wasn't initiated yet");

            if (_remoteStreamWaitTask.Task.IsCompleted)
                throw new InvalidOperationException("Unable to handle a remote stream when another remote stream is active");

            _remoteStream = stream;
            TaskCompletionSource<bool> eofWaitTask = new TaskCompletionSource<bool>();
            _eofWaitTask = eofWaitTask;
            _remoteStreamWaitTask.SetResult(true);
            await eofWaitTask.Task;
        }

        private string GetHandlerId() {
            return _inputFileElement.Id + "." + _file.Id;
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            FinishUpload(null);
            _fileUploadHandler.Dispose();
        }
    }
}
