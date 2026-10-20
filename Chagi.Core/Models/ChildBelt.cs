namespace ITF_Taekwondo_Gradering.Models
{
    public sealed class ChildBelt
    {
        ChildBelt(int id, string name, int level, string colorHex)
        {
            Id = id; Name = name; Level = level; ColorHex = colorHex;
        }

        public int Id { get; }
        public string Name { get; }
        public int Level { get; }
        public string ColorHex { get; }

        public static readonly ChildBelt Kup10_1 = new(1, "10 Kup-1", 1, "#FFFFFF");
        public static readonly ChildBelt Kup10_2 = new(2, "10 Kup-2", 2, "#FFFFFF");
        public static readonly ChildBelt Kup9_1 = new(3, "9 Kup-1", 3, "#FFEB3B");
        public static readonly ChildBelt Kup9_2 = new(4, "9 Kup-2", 4, "#FFEB3B");
        public static readonly ChildBelt Kup8_1 = new(5, "8 Kup-1", 5, "#FFEB3B");
        public static readonly ChildBelt Kup8_2 = new(6, "8 Kup-2", 6, "#FFEB3B");
        public static readonly ChildBelt Kup7_1 = new(7, "7 Kup-1", 7, "#FFEB3B");
        public static readonly ChildBelt Kup7_2 = new(8, "7 Kup-2", 8, "#FFEB3B");
        public static readonly ChildBelt Kup6_1 = new(9, "6 Kup-1", 9, "#00FF00");
        public static readonly ChildBelt Kup6_2 = new(10, "6 Kup-2", 10, "#00FF00");
        public static readonly ChildBelt Kup5_1 = new(11, "5 Kup-1", 11, "#00FF00");
        public static readonly ChildBelt Kup5_2 = new(12, "5 Kup-2", 12, "#00FF00");

        public static IReadOnlyList<ChildBelt> AllBelts { get; } = new[]
        {
            Kup10_1,
            Kup10_2,
            Kup9_1,
            Kup9_2,
            Kup8_1,
            Kup8_2,
            Kup7_1,
            Kup7_2,
            Kup6_1,
            Kup6_2,
            Kup5_1,
            Kup5_2,
        };

        public static ChildBelt? GetBeltByName(string name)
        {
            return AllBelts.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.Ordinal));
        }

        public static ChildBelt? GetBeltByLevel(int level)
        {
            return AllBelts.FirstOrDefault(b => b.Level == level);
        }

        public static string GetBeltNameByLevel(int level)
        {
            return GetBeltByLevel(level)?.Name ?? "Okänd";
        }
    }
}
