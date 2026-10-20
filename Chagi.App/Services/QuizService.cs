namespace ITF_Taekwondo_Gradering.Services
{
    public class QuizService
    {
        private readonly List<Models.Question> _allQuestions;

        public QuizService()
        {
            _allQuestions = InitializeQuestions();
        }

        /// <summary>
        /// Gets all questions up to and including the selected belt level
        /// </summary>
        public List<Models.Question> GetQuestionsForBelt(int selectedBeltLevel)
        {
            return _allQuestions
                .Where(q => q.AssociatedBeltLevel <= selectedBeltLevel)
                .OrderBy(q => Random.Shared.Next())
                .ToList();
        }

        /// <summary>
        /// Gets questions by belt level (optional - for analysis)
        /// </summary>
        public Dictionary<int, List<Models.Question>> GetQuestionsByBeltLevel(int selectedBeltLevel)
        {
            return _allQuestions
                .Where(q => q.AssociatedBeltLevel <= selectedBeltLevel)
                .GroupBy(q => q.AssociatedBeltLevel)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Gets total question count for a belt (including all previous belts)
        /// </summary>
        public int GetTotalQuestionsForBelt(int beltLevel)
        {
            return _allQuestions.Count(q => q.AssociatedBeltLevel <= beltLevel);
        }

        private List<Models.Question> InitializeQuestions()
        {
            // This would typically come from a database or JSON file
            // For now, return empty list - data will be populated from existing system
            return new List<Models.Question>();
        }
    }
}
