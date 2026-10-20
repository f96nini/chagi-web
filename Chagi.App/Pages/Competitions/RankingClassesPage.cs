using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering;

public class RankingClassesPage : ContentPage, IQueryAttributable
{
    readonly Label _status = new() { FontSize = 14 };

    public RankingClassesPage()
    {
        Title = "Klasser";
        NavigationUiService.UseBackButton(this);

        var layout = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };

        layout.Children.Add(new Label
        {
            Text = "Välj vilka klasser som ska visas",
            FontAttributes = FontAttributes.Bold,
            FontSize = 18
        });

        foreach (var ageClass in Enum.GetValues<RankingAgeClassE>())
        {
            var checkBox = new CheckBox
            {
                IsChecked = RankingClassFilterService.IsEnabled(ageClass),
                VerticalOptions = LayoutOptions.Center
            };

            checkBox.CheckedChanged += (_, args) =>
            {
                try
                {
                    RankingClassFilterService.SetEnabled(ageClass, args.Value);
                    RefreshStatus();
                }
                catch (Exception ex)
                {
                    AppDiagnostics.Log(ex, "Spara klassurval");
                    checkBox.IsChecked = RankingClassFilterService.IsEnabled(ageClass);
                    _status.Text = "Kunde inte spara klassurvalet. Försök igen.";
                }
            };

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 10
            };

            row.Add(checkBox, 0, 0);
            row.Add(new Label
            {
                Text = RankingClassFilterService.ToDisplayName(ageClass),
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 16
            }, 1, 0);

            layout.Children.Add(row);
        }

        layout.Children.Add(_status);
        RefreshStatus();

        Content = new ScrollView
        {
            Content = layout
        };
    }

    void RefreshStatus()
    {
        var count = RankingClassFilterService.GetEnabledClasses().Count;
        _status.Text = count == 0
            ? "Inga klasser valda. Välj en klass för att visa resultat."
            : $"{count} klasser valda. Urvalet sparas automatiskt.";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _ = query;
    }
}
