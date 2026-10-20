namespace ITF_Taekwondo_Gradering;

public sealed class QuizBeltHeaderView : Grid
{
    public BeltView SelectedBelt { get; } = CreateBelt();
    public Label SelectedRank { get; } = new()
    {
        FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
        VerticalOptions = LayoutOptions.Center
    };
    public Label Progress { get; } = new()
    {
        FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
        VerticalOptions = LayoutOptions.Center, IsVisible = false
    };
    public BeltView QuestionBelt { get; } = CreateBelt();
    public Label QuestionRank { get; } = new()
    {
        FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
        VerticalOptions = LayoutOptions.Center, IsVisible = false
    };

    public QuizBeltHeaderView()
    {
        ColumnSpacing = 8;
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        this.Add(new Label
        {
            Text = "Visar frågor för", FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White, VerticalOptions = LayoutOptions.Center
        });
        this.Add(SelectedBelt, 1, 0);
        this.Add(SelectedRank, 2, 0);
        this.Add(Progress, 0, 1);
        this.Add(QuestionBelt, 1, 1);
        this.Add(QuestionRank, 2, 1);
        QuestionBelt.IsVisible = false;
    }

    public void SetSelectedRank(string? rank)
    {
        SelectedRank.Text = string.IsNullOrWhiteSpace(rank) ? "Ingen grad vald" : rank;
        if (!string.IsNullOrWhiteSpace(rank))
            BeltDisplayHelper.ApplyTo(SelectedBelt, rank);
        else
        {
            SelectedBelt.BeltColor = Colors.Transparent;
            SelectedBelt.Stripe1Color = Colors.Transparent;
            SelectedBelt.Stripe2Color = Colors.Transparent;
        }
    }

    static BeltView CreateBelt() => new()
    {
        WidthRequest = 96, HeightScale = 1.25,
        HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center
    };
}
