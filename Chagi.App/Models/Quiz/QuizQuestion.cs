using System.Collections.Generic;

namespace ITF_Taekwondo_Gradering
{
    public class QuizQuestion
    {
        public string QuestionText { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new List<string>(); // Expect 6 options
        public int CorrectIndex { get; set; } = -1; // index 0..5 or -1 unknown
        public string AssociatedBeltName { get; set; } = string.Empty;
    }
}
