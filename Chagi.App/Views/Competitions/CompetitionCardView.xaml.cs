namespace ITF_Taekwondo_Gradering.Views;

public partial class CompetitionCardView : ContentView
{
    public static readonly BindableProperty CompetitionProperty =
        BindableProperty.Create(
            nameof(Competition),
            typeof(CompetitionCardEntry),
            typeof(CompetitionCardView),
            default(CompetitionCardEntry));

    public CompetitionCardView()
    {
        InitializeComponent();
    }

    public CompetitionCardEntry? Competition
    {
        get => (CompetitionCardEntry?)GetValue(CompetitionProperty);
        set => SetValue(CompetitionProperty, value);
    }
}
