using Microsoft.Extensions.DependencyInjection;

namespace BlazorInputFile {
    public static class BlazorInputFileServiceCollectionExtensions {
        public static IServiceCollection AddBlazorInputFile(this IServiceCollection services) {
            return services
                .AddHttpContextAccessor()
                .AddSingleton<FileUploadService>();
        }
    }
}
