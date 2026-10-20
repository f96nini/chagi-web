using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;

namespace ITF_Taekwondo_Gradering
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        HomeBackCallback? _homeBack;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            _homeBack ??= new HomeBackCallback(this);
            OnBackPressedDispatcher.AddCallback(this, _homeBack);
        }

        // MAUI 9 processes its legacy back lifecycle before consulting the AndroidX
        // dispatcher. Both Android entry points must reach the same home policy.
        public override void OnBackPressed()
            => (_homeBack ??= new HomeBackCallback(this)).HandleOnBackPressed();

        sealed class HomeBackCallback(MainActivity activity) : OnBackPressedCallback(true)
        {
            bool _handling;

            public override async void HandleOnBackPressed()
            {
                if (_handling) return;
                _handling = true;
                try
                {
                    if (NavigationUiService.IsHome && !NavigationUiService.IsNavigating)
                        activity.Finish();
                    else
                        await NavigationUiService.GoHomeAsync();
                }
                catch (Exception ex) { AppDiagnostics.Log(ex, "Android bakåt"); }
                finally { _handling = false; }
            }
        }
    }
}
