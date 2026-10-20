using ITF_Taekwondo_Gradering.Models.Clubs;

namespace ITF_Taekwondo_Gradering.Views;

public partial class ClubCardView : ContentView
{
    public static readonly BindableProperty SummaryProperty = BindableProperty.Create(
        nameof(Summary),
        typeof(ClubSummaryEntry),
        typeof(ClubCardView));

    public ClubCardView()
    {
        InitializeComponent();
    }

    public ClubSummaryEntry? Summary
    {
        get => (ClubSummaryEntry?)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }
}
