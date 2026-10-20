namespace ITF_Taekwondo_Gradering.Models
{
    public class Question
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public string CorrectAnswer { get; set; } = string.Empty;
        public int AssociatedBeltLevel { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}
