using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Ballast.Atlantis.Web.Components.BlazorNativeFileUpload {
    // NOTE: bits taken from FormFeature
    internal class StreamedMultipartFormReader {
        private readonly MultipartReader _multipartReader;

        public HttpRequest Request{ get; }
        public CancellationToken CancellationToken { get; }

        public StreamedMultipartFormReader(HttpRequest request, FormOptions formOptions, CancellationToken cancellationToken) {
            Request = request;
            CancellationToken = cancellationToken;

            string boundary = GetBoundary(ContentType, formOptions.MultipartBoundaryLengthLimit);
            _multipartReader = new MultipartReader(boundary, Request.Body)
            {
                HeadersCountLimit = formOptions.MultipartHeadersCountLimit,
                HeadersLengthLimit = formOptions.MultipartHeadersLengthLimit,
                BodyLengthLimit = formOptions.MultipartBodyLengthLimit,
            };
        }

        private MediaTypeHeaderValue ContentType
        {
            get
            {
                MediaTypeHeaderValue mt;
                MediaTypeHeaderValue.TryParse(Request.ContentType, out mt);
                return mt;
            }
        }

        public async Task<(MultipartSection section, ContentDispositionHeaderValue contentDisposition)> GetNextSection() {
            if (Request.ContentLength.GetValueOrDefault(0) == 0)
                return (null, null);

            MultipartSection section = await _multipartReader.ReadNextSectionAsync(CancellationToken);
            if (section == null)
                return (null, null);

            // Parse the content disposition here and pass it further to avoid reparsings
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out ContentDispositionHeaderValue contentDisposition))
                throw new InvalidDataException("Form section has invalid Content-Disposition value: " + section.ContentDisposition);

            return (section, contentDisposition);
        }

        public static async Task<KeyValuePair<string, string>> HandleFormDispositionSection(MultipartSection section, ContentDispositionHeaderValue contentDisposition) {
            FormMultipartSection formDataSection = new FormMultipartSection(section, contentDisposition);

            // Content-Disposition: form-data; name="key"
            //
            // value

            // Do not limit the key name length here because the multipart headers length limit is already in effect.
            string key = formDataSection.Name;
            string value = await formDataSection.GetValueAsync();
            return new KeyValuePair<string, string>(key, value);
        }

        private static bool HasMultipartFormContentType(MediaTypeHeaderValue contentType)
        {
            // Content-Type: multipart/form-data; boundary=----WebKitFormBoundarymx2fSWqWSd0OxQqq
            return contentType != null && contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase);
        }

        private bool HasFormDataContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="key";
            return contentDisposition != null && contentDisposition.DispositionType.Equals("form-data")
                && StringSegment.IsNullOrEmpty(contentDisposition.FileName) && StringSegment.IsNullOrEmpty(contentDisposition.FileNameStar);
        }

        private bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
            return contentDisposition != null && contentDisposition.DispositionType.Equals("form-data")
                && (!StringSegment.IsNullOrEmpty(contentDisposition.FileName) || !StringSegment.IsNullOrEmpty(contentDisposition.FileNameStar));
        }

        // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
        // The spec says 70 characters is a reasonable limit.
        private static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
        {
            StringSegment boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary);
            if (StringSegment.IsNullOrEmpty(boundary))
            {
                throw new InvalidDataException("Missing content-type boundary.");
            }
            if (boundary.Length > lengthLimit)
            {
                throw new InvalidDataException($"Multipart boundary length limit {lengthLimit} exceeded.");
            }
            return boundary.ToString();
        }
    }
}
