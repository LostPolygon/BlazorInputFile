using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ballast.Atlantis.Web.Components.BlazorNativeFileUpload;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace BlazorInputFile {
    internal class FileUploadService{
        private readonly IOptions<FormOptions> _formOptions;
        private readonly ConcurrentDictionary<string, FileUploadHandler> _registeredInputFiles =
            new ConcurrentDictionary<string, FileUploadHandler>();

        public delegate Task FileUploadHandlerDelegate(Stream stream);

        public FileUploadService(IOptions<FormOptions> formOptions) {
            _formOptions = formOptions;
            _formOptions = Options.Create(new FormOptions {
                BufferBody = false,
                KeyLengthLimit = formOptions.Value.KeyLengthLimit,
                MemoryBufferThreshold = formOptions.Value.MemoryBufferThreshold,
                ValueCountLimit = 1,
                ValueLengthLimit = formOptions.Value.ValueLengthLimit,
                BufferBodyLengthLimit = formOptions.Value.BufferBodyLengthLimit,
                MultipartBodyLengthLimit = long.MaxValue,
                MultipartBoundaryLengthLimit = formOptions.Value.MultipartBoundaryLengthLimit,
                MultipartHeadersCountLimit = formOptions.Value.MultipartHeadersCountLimit,
                MultipartHeadersLengthLimit = formOptions.Value.MultipartHeadersLengthLimit
            });
        }

        public IDisposable RegisterFileUploadHandler(string id, FileUploadHandlerDelegate handler) {
            FileUploadHandler fileUploadHandler =
                new FileUploadHandler(handler, () => _registeredInputFiles.TryRemove(id, out _));
            if (!_registeredInputFiles.TryAdd(id, fileUploadHandler))
                throw new InvalidOperationException($"Id '{id}' is already registered");

            return fileUploadHandler;
        }

        public async Task HandleRequest(HttpRequest request, string handlerId) {
            if (!_registeredInputFiles.TryGetValue(handlerId, out FileUploadHandler registeredInputFile))
                throw new InvalidDataException($"Invalid handler id {handlerId}");

            StreamedMultipartFormReader multipartFormReader =
                new StreamedMultipartFormReader(request, _formOptions.Value, CancellationToken.None);

            (MultipartSection section, ContentDispositionHeaderValue contentDisposition) =
                await multipartFormReader.GetNextSection();

            bool fileFound = false;
            while (section != null) {
                if (!fileFound && contentDisposition.IsFileDisposition()) {
                    fileFound = true;
                    await HandleFileDispositionSection(registeredInputFile, section, contentDisposition);
                }
                else
                {
                    throw new InvalidDataException("Unexpected content-disposition for this section: " + section.ContentDisposition);
                }

                (section, contentDisposition) = await multipartFormReader.GetNextSection();
            }

            if (!fileFound) {
                await HandleFileDispositionSection(registeredInputFile, null, null);
            }
        }

        private async Task HandleFileDispositionSection(
            FileUploadHandler fileUploadHandler,
            MultipartSection section,
            ContentDispositionHeaderValue contentDisposition) {
            if (section == null) {
                await fileUploadHandler.Handler(null);
                return;
            }
            /*FileMultipartSection fileSection = new FileMultipartSection(section, contentDisposition);

            string name = fileSection.Name;
            string fileName = fileSection.FileName;

            long baseStreamOffset = section.BaseStreamOffset.GetValueOrDefault(0);*/
            await fileUploadHandler.Handler(section.Body);
        }

        private class FileUploadHandler : IDisposable {
            private Action DisposeAction { get; }
            public FileUploadHandlerDelegate Handler { get; }

            public FileUploadHandler(FileUploadHandlerDelegate handler, Action disposeAction) {
                Handler = handler;
                DisposeAction = disposeAction;
            }

            public void Dispose() {
                DisposeAction();
            }
        }
    }
}
