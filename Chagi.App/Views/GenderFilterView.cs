using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using Microsoft.Maui.Controls.Shapes;

namespace ITF_Taekwondo_Gradering;

public sealed class GenderFilterView : Border
{
    readonly Dictionary<GenderFilterMode, Button> _buttons = new();

    public static readonly BindableProperty SelectedGenderProperty = BindableProperty.Create(
        nameof(SelectedGender), typeof(GenderFilterMode), typeof(GenderFilterView), ResultsFilterSettings.DefaultGender,
        validateValue: (_, value) => value is GenderFilterMode gender && Enum.IsDefined(gender),
        propertyChanged: (view, _, _) => ((GenderFilterView)view).OnSelectionChanged());

    public GenderFilterMode SelectedGender
    {
        get => (GenderFilterMode)GetValue(SelectedGenderProperty);
        set => SetValue(SelectedGenderProperty, value);
    }

    public event EventHandler? SelectionChanged;

    public GenderFilterView()
    {
        HorizontalOptions = LayoutOptions.Start;
        Padding = 4;
        StrokeThickness = 0;
        StrokeShape = new RoundRectangle { CornerRadius = 14 };
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColors.SurfaceLight, ThemeColors.CardBackgroundDark);
        var buttons = new HorizontalStackLayout { Spacing = 0 };
        foreach (var (gender, text) in new[]
        {
            (GenderFilterMode.Male, "Herr"), (GenderFilterMode.Female, "Dam"), (GenderFilterMode.All, "H/D")
        })
        {
            var button = new Button
            {
                Text = text, FontSize = 14, CornerRadius = 12, Padding = new Thickness(8, 10),
                MinimumHeightRequest = 44, MinimumWidthRequest = 44, AutomationId = $"Gender{gender}"
            };
            button.Clicked += (_, _) => SelectedGender = gender;
            _buttons.Add(gender, button);
            buttons.Add(button);
        }
        Content = buttons;
        RefreshStyles();
    }

    void OnSelectionChanged()
    {
        RefreshStyles();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    void RefreshStyles()
    {
        foreach (var (gender, button) in _buttons)
        {
            var selected = gender == SelectedGender;
            var background = selected ? Color.FromArgb("#2F6B5F") : Colors.Transparent;
            button.SetAppThemeColor(Button.BackgroundColorProperty, background, background);
            button.SetAppThemeColor(Button.TextColorProperty,
                selected ? Colors.White : ThemeColors.PrimaryTextLight, selected ? Colors.White : ThemeColors.PrimaryTextDark);
            SemanticProperties.SetDescription(button, selected ? $"{button.Text}, valt" : button.Text);
        }
    }
}
