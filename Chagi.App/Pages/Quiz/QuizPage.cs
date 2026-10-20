using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;

namespace ITF_Taekwondo_Gradering;

public class QuizPage : ContentPage
{
    const int AnswerOptionCount = 8;

    RadioButton Rb5, Rb10, Rb20, RbAll;
    VerticalStackLayout ChooseCountLayout;
    QuizBeltHeaderView BeltHeaderLayout;
    Grid QuestionLayout;
    ScrollView AnswersScrollView;
    Label LblProgress;
    BeltView QuestionBeltView;
    Label LblQuestionBelt;
    Label LblQuestion;
    readonly List<Button> _answerButtons = new();
    Button BtnNext;
    Button BtnFinish;

    List<QuizQuestion> _questions = new();
    int _selectedCount;
    int _currentIndex;
    int _score;
    bool _questionAnswered;
    List<int> _userAnswers = new();
    bool _loadingQuestions;
    bool _questionsLoaded;

    public QuizPage()
    {
        Title = "Quiz";

        BeltHeaderLayout = new QuizBeltHeaderView();
        LblProgress = BeltHeaderLayout.Progress;
        QuestionBeltView = BeltHeaderLayout.QuestionBelt;
        LblQuestionBelt = BeltHeaderLayout.QuestionRank;

        Rb5 = CreateCountRadioButton("5");
        Rb10 = CreateCountRadioButton("10");
        Rb20 = CreateCountRadioButton("20");
        RbAll = CreateCountRadioButton("ALLA");

        var chooseLabel = new Label
        {
            Text = "Välj antal frågor:",
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var radioRow = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Center,
            AlignItems = FlexAlignItems.Center,
            AlignContent = FlexAlignContent.Start,
            Children = { Rb5, Rb10, Rb20, RbAll }
        };
        foreach (var radio in new[] { Rb5, Rb10, Rb20, RbAll })
            FlexLayout.SetShrink(radio, 0);

        var startBtn = new Button
        {
            Text = "Starta Quiz",
            HorizontalOptions = LayoutOptions.Fill
        };
        QuickButtonStyle.Apply(startBtn);
        startBtn.Clicked += OnStartQuizClicked;

        ChooseCountLayout = new VerticalStackLayout
        {
            Spacing = 14,
            VerticalOptions = LayoutOptions.Center,
            Children = { chooseLabel, radioRow, startBtn }
        };

        LblQuestion = new Label
        {
            Text = "Fråga kommer här",
            FontAttributes = FontAttributes.Bold,
            FontSize = 22,
            TextColor = Colors.White,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var answersLayout = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Center,
            AlignItems = FlexAlignItems.Center,
            AlignContent = FlexAlignContent.SpaceAround
        };

        for (var i = 0; i < AnswerOptionCount; i++)
        {
            var answerButton = new Button
            {
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(4),
                LineBreakMode = LineBreakMode.WordWrap
            };
            QuickButtonStyle.Apply(answerButton);
            FlexLayout.SetShrink(answerButton, 0);
            answerButton.Clicked += OnAnswerClicked;
            _answerButtons.Add(answerButton);
            answersLayout.Children.Add(answerButton);
        }

        AnswersScrollView = new ScrollView
        {
            Content = answersLayout
        };
        AnswersScrollView.SizeChanged += (_, _) =>
        {
            // Spread answer rows across the viewport, allowing taller content to scroll.
            answersLayout.MinimumHeightRequest = Math.Max(0, AnswersScrollView.Height);
            if (AnswersScrollView.Width <= 8) return;

            foreach (var button in _answerButtons)
                button.MaximumWidthRequest = AnswersScrollView.Width - 8;
        };

        BtnNext = new Button
        {
            Text = "Nästa",
            HorizontalOptions = LayoutOptions.End
        };
        BtnNext.Clicked += OnNextClicked;

        BtnFinish = new Button
        {
            Text = "Avsluta",
            HorizontalOptions = LayoutOptions.Start
        };
        BtnFinish.Clicked += OnFinishClicked;

        var navGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        navGrid.Add(BtnFinish, 0, 0);
        navGrid.Add(BtnNext, 1, 0);

        QuestionLayout = new Grid
        {
            IsVisible = false,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 10
        };

        QuestionLayout.Add(LblQuestion, 0, 0);
        // Only answers scroll. The auto-sized question row always stays above them.
        QuestionLayout.Add(AnswersScrollView, 0, 1);
        QuestionLayout.Add(navGrid, 0, 2);

        var contentHost = new Grid();
        contentHost.Add(ChooseCountLayout);
        contentHost.Add(QuestionLayout);

        var root = new Grid
        {
            Padding = new Thickness(20, 8, 20, 20),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 10
        };

        root.Add(BeltHeaderLayout, 0, 0);
        root.Add(contentHost, 0, 1);

        Content = new AppBackgroundView { Children = { root } };

        UpdateSelectedBeltDisplay();
    }

    static RadioButton CreateCountRadioButton(string text)
    {
        var radio = new RadioButton
        {
            Content = text,
            GroupName = "countGroup",
            FontSize = 18,
            TextColor = Colors.White,
            MinimumHeightRequest = 48
        };

        RadioButtonAppearance.Apply(radio, Colors.White, Colors.White);
        return radio;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await RankSelectionGuard.EnsureSelectedAsync(this)) return;
        UpdateSelectedBeltDisplay();
        if (_loadingQuestions) return;

        if (QuizSession.StartNew)
        {
            QuizSession.StartNew = false;
            QuizSession.RestartSame = false;
            SetQuizStarted(false);
            Rb5.IsChecked = Rb10.IsChecked = Rb20.IsChecked = RbAll.IsChecked = false;
            _selectedCount = 0;
            _currentIndex = 0;
            _score = 0;
            _questionAnswered = false;
            _userAnswers.Clear();
            _questionsLoaded = false;
            await LoadQuestionsAsync();
            return;
        }

        if (!_questionsLoaded && !QuizSession.RestartSame)
            await LoadQuestionsAsync();

        if (QuizSession.RestartSame && QuizSession.LastQuestions != null)
        {
            _questions = CloneQuestions(QuizSession.LastQuestions);
            _selectedCount = _questions.Count;
            _userAnswers = Enumerable.Repeat(-1, _questions.Count).ToList();
            _currentIndex = 0;
            _score = 0;
            QuizSession.RestartSame = false;

            SetQuizStarted(true);
            ShowQuestion(_currentIndex);
            return;
        }

    }

    void SetQuizStarted(bool started)
    {
        ChooseCountLayout.IsVisible = !started;
        QuestionLayout.IsVisible = started;
        LblProgress.IsVisible = started;
        QuestionBeltView.IsVisible = started;
        LblQuestionBelt.IsVisible = started;
        BeltHeaderLayout.RowSpacing = started ? 12 : 0;
    }

    void UpdateSelectedBeltDisplay()
    {
        var selectedRank = Microsoft.Maui.Storage.Preferences.Get("RankSelection", string.Empty);
        BeltHeaderLayout.SetSelectedRank(selectedRank);
    }

    static void Shuffle<T>(List<T> list)
    {
        var rng = new Random();
        for (var n = list.Count; n > 1; n--)
        {
            var k = rng.Next(n);
            (list[n - 1], list[k]) = (list[k], list[n - 1]);
        }
    }

    int GetButtonIndex(Button button) => _answerButtons.IndexOf(button);

    void ResetAnswerButtons()
    {
        foreach (var button in _answerButtons)
        {
            button.IsEnabled = true;
            QuickButtonStyle.ApplyColors(button);
        }
    }

    void ShowAnswerFeedback(int chosenIndex, int correctIndex)
    {
        for (var i = 0; i < _answerButtons.Count; i++)
        {
            var button = _answerButtons[i];
            button.IsEnabled = false;

            if (i == correctIndex)
            {
                button.SetAppThemeColor(Button.BackgroundColorProperty, ThemeColors.SuccessBackgroundLight, ThemeColors.SuccessBackgroundDark);
                button.SetAppThemeColor(Button.TextColorProperty, ThemeColors.SuccessTextLight, ThemeColors.SuccessTextDark);
            }
            else if (i == chosenIndex)
            {
                button.SetAppThemeColor(Button.BackgroundColorProperty, ThemeColors.WarningBackgroundLight, ThemeColors.WarningBackgroundDark);
                button.SetAppThemeColor(Button.TextColorProperty, ThemeColors.WarningTextLight, ThemeColors.WarningTextDark);
            }
            else
            {
                QuickButtonStyle.ApplyColors(button);
            }
        }
    }

    async System.Threading.Tasks.Task LoadQuestionsAsync()
    {
        if (_loadingQuestions) return;
        _loadingQuestions = true;
        try
        {
            var saved = Microsoft.Maui.Storage.Preferences.Get("RankSelection", string.Empty);
            var selectedBelt = MapRankStringToChildBelt(saved);

            _questions = await PageWorkService.RunBackgroundAsync(this, () =>
            {
                var wordGenerator = new Models.Language.Words();
                var generated = GenerateWordQuestions(wordGenerator.WordsList, selectedBelt);
                Shuffle(generated);
                return Task.FromResult(generated);
            }, "Förbereder quiz...");
            _questionsLoaded = true;
        }
        catch
        {
            _questions = new List<QuizQuestion>();
        }

        finally { _loadingQuestions = false; }
    }

    static Models.ChildBelt? MapRankStringToChildBelt(string rank)
    {
        return rank switch
        {
            "10 Kup-1" => Models.ChildBelt.Kup10_1,
            "10 Kup-2" => Models.ChildBelt.Kup10_2,
            "9 Kup-1" => Models.ChildBelt.Kup9_1,
            "9 Kup-2" => Models.ChildBelt.Kup9_2,
            "8 Kup-1" => Models.ChildBelt.Kup8_1,
            "8 Kup-2" => Models.ChildBelt.Kup8_2,
            "7 Kup-1" => Models.ChildBelt.Kup7_1,
            "7 Kup-2" => Models.ChildBelt.Kup7_2,
            "6 Kup-1" => Models.ChildBelt.Kup6_1,
            "6 Kup-2" => Models.ChildBelt.Kup6_2,
            "5 Kup-1" => Models.ChildBelt.Kup5_1,
            "5 Kup-2" => Models.ChildBelt.Kup5_2,
            _ => null,
        };
    }

    List<QuizQuestion> GenerateWordQuestions(List<Tuple<string, string, Models.ChildBelt>> words, Models.ChildBelt? maxLevel)
    {
        var list = new List<QuizQuestion>();
        var rng = new Random();
        var pool = words.Where(w => maxLevel == null || w.Item3.Level <= maxLevel.Level).ToList();
        if (pool.Count == 0)
        {
            return list;
        }

        var allKorean = words.Select(w => w.Item1).Distinct().ToList();
        var allSwedish = words.Select(w => w.Item2).Distinct().ToList();

        foreach (var word in pool)
        {
            var askForSwedish = rng.Next(2) == 0;
            var correct = askForSwedish ? word.Item2 : word.Item1;
            var questionText = askForSwedish
                ? $"Vad betyder '{word.Item1}'?"
                : $"Vilket koreanskt ord betyder '{word.Item2}'?";

            var primarySource = (askForSwedish ? pool.Select(w => w.Item2) : pool.Select(w => w.Item1))
                .Where(value => value != correct)
                .Distinct()
                .ToList();
            Shuffle(primarySource);

            var options = new List<string> { correct };
            foreach (var option in primarySource)
            {
                if (options.Count >= AnswerOptionCount)
                {
                    break;
                }

                options.Add(option);
            }

            var fallbackSource = (askForSwedish ? allSwedish : allKorean)
                .Where(value => value != correct && !options.Contains(value))
                .ToList();
            Shuffle(fallbackSource);

            foreach (var option in fallbackSource)
            {
                if (options.Count >= AnswerOptionCount)
                {
                    break;
                }

                options.Add(option);
            }

            var shuffledOptions = options.ToList();
            Shuffle(shuffledOptions);

            list.Add(new QuizQuestion
            {
                QuestionText = questionText,
                Options = shuffledOptions,
                CorrectIndex = shuffledOptions.IndexOf(correct),
                AssociatedBeltName = word.Item3.Name
            });
        }

        return list;
    }

    async void OnStartQuizClicked(object? sender, EventArgs e)
    {
        if (Rb5.IsChecked)
        {
            _selectedCount = 5;
        }
        else if (Rb10.IsChecked)
        {
            _selectedCount = 10;
        }
        else if (Rb20.IsChecked)
        {
            _selectedCount = 20;
        }
        else if (RbAll.IsChecked)
        {
            _selectedCount = _questions.Count;
        }

        if (_selectedCount <= 0)
        {
            await DisplayAlert("Fel", "Välj antal frågor först.", "OK");
            return;
        }

        Shuffle(_questions);
        if (_selectedCount < _questions.Count)
        {
            _questions = _questions.GetRange(0, _selectedCount);
        }

        _currentIndex = 0;
        _score = 0;
        _questionAnswered = false;
        _userAnswers = Enumerable.Repeat(-1, _questions.Count).ToList();

        SetQuizStarted(true);
        ShowQuestion(_currentIndex);
    }

    void ShowQuestion(int index)
    {
        if (index < 0 || index >= _questions.Count)
        {
            return;
        }

        var question = _questions[index];
        LblProgress.Text = $"{index + 1} / {_questions.Count}";
        SemanticProperties.SetDescription(LblProgress, $"Fråga {index + 1} av {_questions.Count}");
        LblQuestionBelt.Text = question.AssociatedBeltName;
        BeltDisplayHelper.ApplyTo(QuestionBeltView, question.AssociatedBeltName);
        LblQuestion.Text = question.QuestionText;

        for (var i = 0; i < _answerButtons.Count; i++)
        {
            _answerButtons[i].Text = i < question.Options.Count ? question.Options[i] : string.Empty;
            _answerButtons[i].IsVisible = i < question.Options.Count;
        }

        _questionAnswered = false;
        ResetAnswerButtons();
        BtnNext.IsEnabled = false;
        _ = AnswersScrollView.ScrollToAsync(0, 0, false);
    }

    void OnAnswerClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || _questionAnswered)
        {
            return;
        }

        var question = _questions[_currentIndex];
        var chosenIndex = GetButtonIndex(button);
        if (chosenIndex < 0)
        {
            return;
        }

        if (_userAnswers.Count != _questions.Count)
        {
            _userAnswers = Enumerable.Repeat(-1, _questions.Count).ToList();
        }

        _userAnswers[_currentIndex] = chosenIndex;
        if (chosenIndex == question.CorrectIndex)
        {
            _score++;
        }

        ShowAnswerFeedback(chosenIndex, question.CorrectIndex);
        _questionAnswered = true;
        BtnNext.IsEnabled = true;
    }

    async void OnNextClicked(object? sender, EventArgs e)
    {
        _currentIndex++;
        if (_currentIndex >= _questions.Count)
        {
            await NavigateToSummaryAsync();
            return;
        }

        ShowQuestion(_currentIndex);
    }

    async void OnFinishClicked(object? sender, EventArgs e)
    {
        await NavigateToSummaryAsync();
    }

    async System.Threading.Tasks.Task NavigateToSummaryAsync()
    {
        QuizSession.LastScore = _score;
        QuizSession.TotalQuestions = _questions.Count;
        QuizSession.LastQuestions = CloneQuestions(_questions);
        QuizSession.LastUserAnswers = new List<int>(_userAnswers);

        await NavigationUiService.GoToAsync("summary");
    }

    static List<QuizQuestion> CloneQuestions(List<QuizQuestion> source)
    {
        var list = new List<QuizQuestion>();
        foreach (var question in source)
        {
            list.Add(new QuizQuestion
            {
                QuestionText = question.QuestionText,
                CorrectIndex = question.CorrectIndex,
                Options = new List<string>(question.Options),
                AssociatedBeltName = question.AssociatedBeltName
            });
        }

        return list;
    }
}
