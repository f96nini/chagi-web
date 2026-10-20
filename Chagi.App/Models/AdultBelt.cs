namespace ITF_Taekwondo_Gradering.Models
{
    public sealed class AdultBelt : BeltBase
    {
        AdultBelt(int id, string name, int level, Color color, Color? stripeColor = null)
            : base(id, name, level, color)
        {
            StripeColor = stripeColor;
        }

        public Color? StripeColor { get; }

        public string ColorDescription
        {
            get
            {
                var stripeColor = StripeColor;
                return stripeColor is null
                    ? ToSwedishColorName(Color)
                    : $"{ToSwedishColorName(Color)}/{ToSwedishColorName(stripeColor)}";
            }
        }

        public static readonly AdultBelt Kup10 = new(1, "10 Kup", 1, Color.FromArgb("#FFFFFF"));
        public static readonly AdultBelt Kup9 = new(2, "9 Kup", 2, Color.FromArgb("#FFFFFF"), Color.FromArgb("#FFEB3B"));
        public static readonly AdultBelt Kup8 = new(3, "8 Kup", 3, Color.FromArgb("#FFEB3B"));
        public static readonly AdultBelt Kup7 = new(4, "7 Kup", 4, Color.FromArgb("#FFEB3B"), Color.FromArgb("#00FF00"));
        public static readonly AdultBelt Kup6 = new(5, "6 Kup", 5, Color.FromArgb("#00FF00"));
        public static readonly AdultBelt Kup5 = new(6, "5 Kup", 6, Color.FromArgb("#00FF00"), Color.FromArgb("#0000FF"));
        public static readonly AdultBelt Kup4 = new(7, "4 Kup", 7, Color.FromArgb("#0000FF"));
        public static readonly AdultBelt Kup3 = new(8, "3 Kup", 8, Color.FromArgb("#0000FF"), Color.FromArgb("#FF0000"));
        public static readonly AdultBelt Kup2 = new(9, "2 Kup", 9, Color.FromArgb("#FF0000"));
        public static readonly AdultBelt Kup1 = new(10, "1 Kup", 10, Color.FromArgb("#FF0000"), Color.FromArgb("#000000"));
        public static readonly AdultBelt Dan1 = new(11, "1 Dan", 11, Color.FromArgb("#000000"));
        public static readonly AdultBelt Dan2 = new(12, "2 Dan", 12, Color.FromArgb("#000000"));
        public static readonly AdultBelt Dan3 = new(13, "3 Dan", 13, Color.FromArgb("#000000"));
        public static readonly AdultBelt Dan4 = new(14, "4 Dan", 14, Color.FromArgb("#000000"));
        public static readonly AdultBelt Dan5 = new(15, "5 Dan", 15, Color.FromArgb("#000000"));
        public static readonly AdultBelt Dan6 = new(16, "6 Dan", 16, Color.FromArgb("#000000"));
        public static readonly AdultBelt Dan7 = new(17, "7 Dan", 17, Color.FromArgb("#000000"));
        public static readonly AdultBelt Dan8 = new(18, "8 Dan", 18, Color.FromArgb("#000000"));
        public static readonly AdultBelt Dan9 = new(19, "9 Dan", 19, Color.FromArgb("#000000"));

        public static IReadOnlyList<AdultBelt> AllBelts { get; } = new[]
        {
            Kup10,
            Kup9,
            Kup8,
            Kup7,
            Kup6,
            Kup5,
            Kup4,
            Kup3,
            Kup2,
            Kup1,
            Dan1,
            Dan2,
            Dan3,
            Dan4,
            Dan5,
            Dan6,
            Dan7,
            Dan8,
            Dan9,
        };

        public static AdultBelt? GetBeltByName(string name)
        {
            return AllBelts.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.Ordinal));
        }

        public static AdultBelt? GetBeltByLevel(int level)
        {
            return AllBelts.FirstOrDefault(b => b.Level == level);
        }

        public static string GetBeltNameByLevel(int level)
        {
            return GetBeltByLevel(level)?.Name ?? "Okänd";
        }

        static string ToSwedishColorName(Color color)
        {
            var hex = color.ToArgbHex();
            return hex switch
            {
                "#FFFFFF" => "Vit",
                "#FFEB3B" => "Gul",
                "#00FF00" => "Grön",
                "#0000FF" => "Blå",
                "#FF0000" => "Röd",
                "#000000" => "Svart",
                _ => hex
            };
        }
    }
}
