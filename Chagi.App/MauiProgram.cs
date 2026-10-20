using Microsoft.Extensions.Logging;
using ITF_Taekwondo_Gradering.Services;
using ITF_Taekwondo_Gradering.ViewModels;

namespace ITF_Taekwondo_Gradering
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            Chagi.Core.Storage.CorePreferences.Current = new MauiPreferenceStore();
            AppDiagnostics.Initialize();
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<QuizService>();
            builder.Services.AddSingleton<QuizViewModel>();

#if WINDOWS
            builder.ConfigureImageSources(services =>
            {
                services.AddService<IFileImageSource, WinUI.EmbeddedFileImageSourceService>();
                // Controls registers this concrete source separately; it takes precedence over IFileImageSource.
                services.AddService<Microsoft.Maui.Controls.FileImageSource, WinUI.EmbeddedFileImageSourceService>();
            });
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
