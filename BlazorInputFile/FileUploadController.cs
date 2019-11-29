using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorInputFile {
    [AllowAnonymous]
    internal class FileUploadController : Controller {
        public const string UploadRoute = "_content/" + nameof(BlazorInputFile) + "/{handlerId}";
        public const string UploadRouteName = nameof(BlazorInputFile) + "UploadRoute";

        private readonly FileUploadService _fileUploadService;

        public FileUploadController(FileUploadService fileUploadService) {
            _fileUploadService = fileUploadService;
        }

        [HttpPost(UploadRoute, Name = UploadRouteName)]
        [DisableRequestSizeLimit]
        [DisableFormValueModelBinding]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Upload(string handlerId) {
            try {
                await _fileUploadService.HandleRequest(Request, handlerId);
                return Ok("Uploaded");
            } catch (Exception e) {
                Console.WriteLine(e);
                return UnprocessableEntity(e.ToString());
            }
        }
    }
}
