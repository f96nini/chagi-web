using Microsoft.Maui.Controls.Shapes;

namespace ITF_Taekwondo_Gradering;

public sealed class MedalLeaderboardLink : Border
{
    public event EventHandler<TappedEventArgs>? Tapped;

    public MedalLeaderboardLink()
    {
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = 8 };
        Padding = new Thickness(12, 10);
        MinimumHeightRequest = 48;
        this.SetAppTheme(Border.StrokeProperty, new SolidColorBrush(Color.FromArgb("#BCCCDC")), new SolidColorBrush(Color.FromArgb("#627D98")));
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#E6EEF5"), Color.FromArgb("#243B53"));
        SemanticProperties.SetDescription(this, "Medaljtoppen. Visa poängtopplistan.");
        var label = new Label { Text = "Medaljtoppen", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#102A43"), Color.FromArgb("#F0F4F8"));
        var arrow = new Label { Text = "›", FontSize = 26, VerticalOptions = LayoutOptions.Center };
        arrow.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#184E77"), Color.FromArgb("#BCCCDC"));
        var content = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 12
        };
        content.Add(label);
        content.Add(arrow, 1, 0);
        Content = content;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, args) => Tapped?.Invoke(this, args);
        GestureRecognizers.Add(tap);
    }
}
