namespace ITF_Taekwondo_Gradering;

static class RadioButtonAppearance
{
    static readonly BindableProperty IndicatorColorProperty = BindableProperty.CreateAttached(
        "IndicatorColor", typeof(Color), typeof(RadioButtonAppearance), Colors.White,
        propertyChanged: (bindable, _, _) => Update((RadioButton)bindable));

    public static void Apply(RadioButton radio, Color light, Color dark)
    {
        radio.SetAppThemeColor(IndicatorColorProperty, light, dark);
        radio.HandlerChanged += (_, _) => Update(radio);
        radio.Loaded += (_, _) => Update(radio);
#if IOS || MACCATALYST
        radio.CheckedChanged += (_, _) => Update(radio);
#endif
    }

    static void Update(RadioButton radio)
    {
        var color = (Color)radio.GetValue(IndicatorColorProperty);
#if ANDROID
        if (radio.Handler?.PlatformView is Android.Widget.RadioButton nativeRadio)
            nativeRadio.ButtonTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Argb(
                (int)(color.Alpha * 255), (int)(color.Red * 255), (int)(color.Green * 255), (int)(color.Blue * 255)));
#elif WINDOWS
        if (radio.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.RadioButton nativeRadio)
        {
            var nativeColor = Windows.UI.Color.FromArgb((byte)(color.Alpha * 255),
                (byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255));
            foreach (var state in new[] { "", "PointerOver", "Pressed" })
            {
                var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(nativeColor);
                nativeRadio.Resources["RadioButtonOuterEllipseStroke" + state] = brush;
                nativeRadio.Resources["RadioButtonOuterEllipseCheckedStroke" + state] = brush;
                nativeRadio.Resources["RadioButtonCheckGlyphFill" + state] = brush;
                nativeRadio.Resources["RadioButtonOuterEllipseCheckedFill" + state] =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
        }
#elif IOS || MACCATALYST
        var brush = new SolidColorBrush(color);
        var root = ((IVisualTreeElement)radio).GetVisualChildren().OfType<View>().FirstOrDefault();
        if (root?.FindByName<Microsoft.Maui.Controls.Shapes.Ellipse>(RadioButton.UncheckedButton) is { } circle)
            circle.Stroke = brush;
        if (root?.FindByName<Microsoft.Maui.Controls.Shapes.Ellipse>(RadioButton.CheckedIndicator) is { } indicator)
        {
            indicator.Stroke = brush;
            indicator.Fill = brush;
        }
#endif
    }
}
