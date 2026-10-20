namespace ITF_Taekwondo_Gradering.Models
{
    public abstract class BeltBase
    {
        protected BeltBase(int id, string name, int level, Color color)
        {
            Id = id;
            Name = name;
            Level = level;
            Color = color;
        }

        public int Id { get; init; }
        public string Name { get; init; }
        public int Level { get; init; }
        public Color Color { get; init; }
    }
}
