using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace BlazorInputFile {
    class FileUploadControllerFeatureProvider : ControllerFeatureProvider {
        protected override bool IsController(TypeInfo typeInfo) {
            return typeof(FileUploadController).IsAssignableFrom(typeInfo) || base.IsController(typeInfo);
        }
    }
}
