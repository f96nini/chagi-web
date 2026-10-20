namespace ITF_Taekwondo_Gradering
{
    public static class QuizSession
    {
        public static int LastScore { get; set; }
        public static int TotalQuestions { get; set; }
        public static List<QuizQuestion>? LastQuestions { get; set; }
        public static List<int>? LastUserAnswers { get; set; }

        // Control flags used when navigating back to quiz
        public static bool RestartSame { get; set; }
        public static bool StartNew { get; set; }
    }
}
