namespace ITF_Taekwondo_Gradering;

public static class ToastService
{
    public static async Task ShowAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
#if ANDROID
                Android.Widget.Toast
                    .MakeText(Android.App.Application.Context, message, Android.Widget.ToastLength.Short)?
                    .Show();
#else
                System.Diagnostics.Debug.WriteLine($"TOAST: {message}");
#endif
            });
        }
        catch
        {
            // ignore toast failures
        }
    }
}
