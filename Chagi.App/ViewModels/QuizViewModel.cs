namespace ITF_Taekwondo_Gradering.ViewModels
{
    public class QuizViewModel : BaseViewModel
    {
        private readonly Services.QuizService _quizService = new();
        private int _selectedBeltLevel;
        private List<Models.Question> _quizQuestions = new();

        private int _currentQuestionIndex;
        public int CurrentQuestionIndex
        {
            get => _currentQuestionIndex;
            set => SetProperty(ref _currentQuestionIndex, value);
        }

        private int _totalQuestions;
        public int TotalQuestions
        {
            get => _totalQuestions;
            set => SetProperty(ref _totalQuestions, value);
        }

        private string _beltDisplayName = "";
        public string BeltDisplayName
        {
            get => _beltDisplayName;
            set => SetProperty(ref _beltDisplayName, value);
        }

        public void InitializeQuiz(int beltLevel)
        {
            _selectedBeltLevel = beltLevel;
            _quizQuestions = _quizService.GetQuestionsForBelt(beltLevel);

            TotalQuestions = _quizQuestions.Count;
            CurrentQuestionIndex = 0;
            BeltDisplayName = Models.ChildBelt.GetBeltNameByLevel(beltLevel);
        }
    }
}
