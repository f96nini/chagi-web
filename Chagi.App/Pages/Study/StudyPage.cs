using System.Globalization;
using ITF_Taekwondo_Gradering.Models;
using ITF_Taekwondo_Gradering.Models.Language;

namespace ITF_Taekwondo_Gradering;

public class StudyPage : ContentPage
{
    readonly VerticalStackLayout _wordsLayout;
    readonly ChildBelt? _selectedBelt;
    bool _loaded;
    bool _loading;
    public StudyPage()
    {
        Title = "Studera";

        var selectedRank = Microsoft.Maui.Storage.Preferences.Get("RankSelection", string.Empty);
        var selectedBelt = ChildBelt.GetBeltByName(selectedRank);
        _selectedBelt = selectedBelt;

        var selectedBeltView = new BeltView
        {
            WidthRequest = 120,
            HeightScale = 1.25,
            HorizontalOptions = LayoutOptions.Center
        };

        if (!string.IsNullOrWhiteSpace(selectedRank))
        {
            BeltDisplayHelper.ApplyTo(selectedBeltView, selectedRank);
        }
        else
        {
            selectedBeltView.BeltColor = Colors.Transparent;
            selectedBeltView.Stripe1Color = Colors.Transparent;
            selectedBeltView.Stripe2Color = Colors.Transparent;
        }

        var selectedBeltLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(selectedRank) ? "Ingen grad vald" : selectedRank,
            FontAttributes = FontAttributes.Bold,
            FontSize = 22,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var introLabel = new Label
        {
            Text = selectedBelt is null
                ? "Välj ett bälte i Inställningar för att se vilka ord som hör till graden."
                : "Alla ord upp till vald grad visas här. Orden för valt bälte är fetmarkerade.",
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var quizShortcutButton = new Button
        {
            Text = "Gå till Quiz"
        };
        quizShortcutButton.Clicked += async (_, _) =>
        {
            await NavigationUiService.GoToAsync("//quiz");
        };

        var wordsLayout = new VerticalStackLayout
        {
            Spacing = 10
        };

        _wordsLayout = wordsLayout;

        var wordsScroll = new ScrollView
        {
            Content = wordsLayout,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        var root = new Grid
        {
            Padding = 20,
            RowSpacing = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        var headerLayout = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                selectedBeltView,
                selectedBeltLabel,
                introLabel,
                quizShortcutButton
            }
        };

        root.Add(headerLayout, 0, 0);
        root.Add(wordsScroll, 0, 1);

        Content = new AppBackgroundView { Children = { root } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await RankSelectionGuard.EnsureSelectedAsync(this)) return;
        if (_loaded || _loading) return;
        _loading = true;
        try
        {
            await PageWorkService.RunAsync(this, () => PopulateWordsAsync(_wordsLayout, _selectedBelt), "Laddar ord...");
            _loaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("Fel", "Kunde inte läsa orden. Öppna sidan igen för att försöka på nytt.", "OK");
        }
        finally { _loading = false; }
    }

    static async Task PopulateWordsAsync(VerticalStackLayout wordsLayout, ChildBelt? selectedBelt)
    {
        wordsLayout.Children.Clear();
        var groupedWords = await Task.Run(() =>
        {
            var words = new Words().WordsList;
            var filteredWords = selectedBelt is null
                ? words
                : words.Where(word => word.Item3.Level <= selectedBelt.Level).ToList();

            return filteredWords
                .GroupBy(word => word.Item3)
                .OrderByDescending(group => group.Key.Level)
                .ToList();
        });

        foreach (var group in groupedWords)
        {
            var isSelectedBeltGroup = selectedBelt?.Level == group.Key.Level;
            var groupBeltView = new BeltView
            {
                WidthRequest = 120,
                HeightScale = 1.25,
                HorizontalOptions = LayoutOptions.Center
            };
            BeltDisplayHelper.ApplyTo(groupBeltView, group.Key.Name);

            var headerLabel = new Label
            {
                Text = group.Key.Name,
                FontAttributes = FontAttributes.Bold,
                FontSize = 18,
                HorizontalTextAlignment = TextAlignment.Center
            };
            headerLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.PrimaryTextLight, ThemeColors.PrimaryTextDark);

            var border = CompetitionUiHelpers.CreateCard(new VerticalStackLayout
            {
                Spacing = 6
            });

            if (border.Content is VerticalStackLayout cardContent)
            {
                cardContent.Children.Add(groupBeltView);
                cardContent.Children.Add(headerLabel);

                foreach (var (korean, swedish, _) in group.OrderBy(word => word.Item1, StringComparer.Create(CultureInfo.GetCultureInfo("sv-SE"), true)))
                {
                    var wordLabel = new Label
                    {
                        LineBreakMode = LineBreakMode.WordWrap,
                        FormattedText = new FormattedString
                        {
                            Spans =
                            {
                                new Span
                                {
                                    Text = korean,
                                    FontAttributes = isSelectedBeltGroup ? FontAttributes.Bold : FontAttributes.None
                                },
                                new Span
                                {
                                    Text = $" - {swedish}",
                                    FontAttributes = isSelectedBeltGroup ? FontAttributes.Bold : FontAttributes.None
                                }
                            }
                        }
                    };
                    wordLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.PrimaryTextLight, ThemeColors.PrimaryTextDark);
                    cardContent.Children.Add(wordLabel);
                    await Task.Delay(1);
                }
            }

            wordsLayout.Children.Add(border);
        }
    }
}
