using ITF_Taekwondo_Gradering.Services.Images;

namespace ITF_Taekwondo_Gradering.WinUI;

public sealed class EmbeddedFileImageSourceService : FileImageSourceService
{
    readonly EmbeddedImageStore _images = new(typeof(MauiProgram).Assembly);
    readonly StreamImageSourceService _streams = new();

    public override async Task<IImageSourceServiceResult<Microsoft.UI.Xaml.Media.ImageSource>?> GetImageSourceAsync(
        IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default)
    {
        if (imageSource is not IFileImageSource file || string.IsNullOrEmpty(file.File) || !_images.Contains(file.File))
            return await base.GetImageSourceAsync(imageSource, scale, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        // Keep bindings intact; MAUI decodes a fresh assembly stream instead of a disk file.
        var fileName = file.File;
        try
        {
            var source = Microsoft.Maui.Controls.ImageSource.FromStream(() => _images.Open(fileName, scale));
            var result = await _streams.GetImageSourceAsync((IImageSource)source, scale, cancellationToken);
            return result == null ? null : new ImageSourceServiceResult(result.Value, true, result.Dispose);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AppDiagnostics.Log(exception, $"Inbyggd bild: {fileName}");
            throw;
        }
    }
}
