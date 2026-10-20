namespace ITF_Taekwondo_Gradering.Views;

public partial class MedalSummaryRowView : ContentView
{
    public static readonly BindableProperty TrophyHeightProperty = BindableProperty.Create(
        nameof(TrophyHeight), typeof(double), typeof(MedalSummaryRowView), 48d);

    public static readonly BindableProperty ShowInlineDetailsProperty = BindableProperty.Create(
        nameof(ShowInlineDetails), typeof(bool), typeof(MedalSummaryRowView), false,
        propertyChanged: (bindable, _, _) => ((MedalSummaryRowView)bindable).OnPropertyChanged(nameof(ShowStackedDetails)));

    public static readonly BindableProperty SummaryProperty =
        BindableProperty.Create(
            nameof(Summary),
            typeof(MedalSummaryRowModel),
            typeof(MedalSummaryRowView),
            default(MedalSummaryRowModel));

    public MedalSummaryRowView()
    {
        InitializeComponent();
    }

    public MedalSummaryRowModel? Summary
    {
        get => (MedalSummaryRowModel?)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public double TrophyHeight
    {
        get => (double)GetValue(TrophyHeightProperty);
        set => SetValue(TrophyHeightProperty, value);
    }

    public bool ShowInlineDetails
    {
        get => (bool)GetValue(ShowInlineDetailsProperty);
        set => SetValue(ShowInlineDetailsProperty, value);
    }

    public bool ShowStackedDetails => !ShowInlineDetails;
}
