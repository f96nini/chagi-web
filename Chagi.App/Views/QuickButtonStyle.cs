namespace ITF_Taekwondo_Gradering;

static class QuickButtonStyle
{
    static readonly Color Background = Color.FromRgba(255, 255, 255, 128);
    static readonly Color Text = Color.FromArgb("#102A43");
    static readonly Color Border = Color.FromRgba(255, 255, 255, 168);

    public static void Apply(Button button)
    {
        button.CornerRadius = 26;
        button.Padding = new Thickness(16);
        button.FontSize = 22;
        button.FontAttributes = FontAttributes.Bold;
        button.BorderWidth = 1;
        button.Shadow = new Shadow
        {
            Brush = Brush.Black,
            Opacity = 0.18f,
            Radius = 18,
            Offset = new Point(0, 8)
        };
        ApplyColors(button);
    }

    public static void ApplyColors(Button button)
    {
        // Replace any theme bindings left by quiz answer feedback.
        button.SetAppThemeColor(Button.BackgroundColorProperty, Background, Background);
        button.SetAppThemeColor(Button.TextColorProperty, Text, Text);
        button.SetAppThemeColor(Button.BorderColorProperty, Border, Border);
    }
}
