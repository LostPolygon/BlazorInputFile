using Microsoft.Extensions.DependencyInjection;

namespace BlazorInputFile {
    public static class BlazorInputFileMvcBuilderExtensions {
        public static IMvcBuilder AddBlazorInputFile(this IMvcBuilder mvcBuilder) {
            mvcBuilder.AddApplicationPart(typeof(FileUploadController).Assembly);
            mvcBuilder.ConfigureApplicationPartManager(manager => {
                manager.FeatureProviders.Add(new FileUploadControllerFeatureProvider());
            });
            return mvcBuilder;
        }
    }
}
