namespace ITF_Taekwondo_Gradering;

public sealed class AppBackgroundView : Grid
{
    public AppBackgroundView()
    {
        Children.Add(new Image
        {
            Source = "bgtxt.png",
            Aspect = Aspect.AspectFill,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Opacity = 0.98,
            InputTransparent = true,
            ZIndex = -1
        });
    }
}
