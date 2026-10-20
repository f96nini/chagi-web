using Microsoft.Maui.Controls;

namespace ITF_Taekwondo_Gradering
{
    public class SummaryPage : ContentPage
    {
        public SummaryPage()
        {
            Title = "Resultat";
            NavigationUiService.UseBackButton(this);

            var selectedRank = Microsoft.Maui.Storage.Preferences.Get("RankSelection", string.Empty);
            var beltHeader = new QuizBeltHeaderView();
            beltHeader.SetSelectedRank(selectedRank);

            var scoreLabel = new Label
            {
                Text = $"Poäng: {QuizSession.LastScore} / {QuizSession.TotalQuestions}",
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center
            };

            double pct = 0;
            if (QuizSession.TotalQuestions > 0)
                pct = 100.0 * QuizSession.LastScore / QuizSession.TotalQuestions;

            var pctLabel = new Label
            {
                Text = $"Procent: {pct:0.##}%",
                HorizontalOptions = LayoutOptions.Center
            };

            // Buttons: restart the same quiz, choose a new quiz, back
            var restartBtn = new Button { Text = "Starta om samma quiz" };
            restartBtn.Clicked += async (s, e) =>
            {
                QuizSession.StartNew = false;
                QuizSession.RestartSame = true;
                await NavigationUiService.GoToAsync("//quiz");
            };

            var newBtn = new Button { Text = "Nytt Quiz" };
            newBtn.Clicked += async (s, e) =>
            {
                QuizSession.RestartSame = false;
                QuizSession.StartNew = true;
                await NavigationUiService.GoToAsync("//quiz");
            };

            var backBtn = new Button { Text = "Till huvudmeny" };
            backBtn.Clicked += async (s, e) =>
                await NavigationUiService.GoToAsync("//MainPage");

            foreach (var button in new[] { restartBtn, newBtn, backBtn })
            {
                QuickButtonStyle.Apply(button);
                button.LineBreakMode = LineBreakMode.WordWrap;
            }

            // Detailed list of questions with results
            var details = new StackLayout { Spacing = 8 };
            if (QuizSession.LastQuestions != null && QuizSession.LastUserAnswers != null)
            {
                for (int i = 0; i < QuizSession.LastQuestions.Count; i++)
                {
                    var q = QuizSession.LastQuestions[i];
                    int chosen = (i < QuizSession.LastUserAnswers.Count) ? QuizSession.LastUserAnswers[i] : -1;
                    var correct = q.CorrectIndex;

                    var qLabel = new Label { Text = $"{i+1}. {q.QuestionText}", FontAttributes = FontAttributes.Bold };
                    string chosenText = (chosen >= 0 && chosen < q.Options.Count) ? q.Options[chosen] : "(Inget val)";
                    string correctText = (correct >= 0 && correct < q.Options.Count) ? q.Options[correct] : "(Okänt)";
                    var ansLabel = new Label { Text = $"Ditt svar: {chosenText}    Rätt svar: {correctText}" };

                    var panel = CompetitionUiHelpers.CreateCard(new VerticalStackLayout
                    {
                        Spacing = 6, Children = { qLabel, ansLabel }
                    });
                    qLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.PrimaryTextLight, ThemeColors.PrimaryTextDark);
                    ansLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.PrimaryTextLight, ThemeColors.PrimaryTextDark);
                    panel.SetAppTheme<Brush>(Border.StrokeProperty,
                        new SolidColorBrush(chosen == correct ? ThemeColors.SuccessBackgroundDark : ThemeColors.WarningBackgroundDark),
                        new SolidColorBrush(chosen == correct ? ThemeColors.SuccessBackgroundLight : Colors.IndianRed));
                    details.Children.Add(panel);
                }
            }

            var detailsLabel = new Label { Text = "Detaljer:", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
            scoreLabel.TextColor = Colors.White;
            pctLabel.TextColor = Colors.White;

            var content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 8, 20, 20), Spacing = 12,
                Children = { beltHeader, scoreLabel, pctLabel, restartBtn, newBtn, backBtn, detailsLabel, details }
            };
            Content = new AppBackgroundView { Children = { new ScrollView { Content = content } } };
        }
    }
}
