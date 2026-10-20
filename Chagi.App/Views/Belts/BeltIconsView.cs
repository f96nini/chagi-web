namespace ITF_Taekwondo_Gradering.Views;

public class BeltIconsView : ContentView
{
    public static readonly BindableProperty BeltGroupProperty =
        BindableProperty.Create(
            nameof(BeltGroup),
            typeof(string),
            typeof(BeltIconsView),
            string.Empty,
            propertyChanged: OnBeltGroupChanged);

    public string BeltGroup
    {
        get => (string)GetValue(BeltGroupProperty);
        set => SetValue(BeltGroupProperty, value);
    }

    static void OnBeltGroupChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((BeltIconsView)bindable).Content = CompetitionUiHelpers.BuildBeltIcons((string?)newValue ?? string.Empty);
    }
}
