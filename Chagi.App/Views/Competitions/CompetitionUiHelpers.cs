using System.Globalization;
using System.Text;
using ITF_Taekwondo_Gradering.Models.Ranking;
using Microsoft.Maui.Controls.Shapes;

namespace ITF_Taekwondo_Gradering;

static class CompetitionUiHelpers
{
    public static Border CreateCard(View content, Thickness? padding = null)
    {
        return new Border
        {
            Stroke = Color.FromArgb("#D9E2EC"),
            StrokeThickness = 1,
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = padding ?? 12,
            Content = content
        }.Assign(border =>
        {
            border.SetAppThemeColor(Border.BackgroundColorProperty, ThemeColors.CardBackgroundLight, ThemeColors.CardBackgroundDark);
            border.SetAppTheme<Brush>(
                Border.StrokeProperty,
                new SolidColorBrush(ThemeColors.CardStrokeLight),
                new SolidColorBrush(ThemeColors.CardStrokeDark));
        });
    }

    public static Image CreateMedalIcon(int placement, double size)
    {
        return new Image
        {
            Source = placement switch
            {
                1 => "gold.png",
                2 => "silver.png",
                3 => "bronze.png",
                _ => "bronze.png"
            },
            WidthRequest = size,
            HeightRequest = size,
            Aspect = Aspect.AspectFill,
            Clip = new EllipseGeometry
            {
                Center = new Point(size / 2, size / 2),
                RadiusX = size / 2,
                RadiusY = size / 2
            },
            VerticalOptions = LayoutOptions.Center
        };
    }

    public static View CreatePlacementBadge(int placement)
    {
        if (placement is >= 1 and <= 3)
            return CreateMedalIcon(placement, 22);

        return new Border
        {
            WidthRequest = 22,
            HeightRequest = 22,
            Padding = 0,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#9FB3C8"),
            StrokeShape = new Ellipse(),
            Content = new Label
            {
                Text = placement > 0 ? placement.ToString(CultureInfo.InvariantCulture) : "–",
                FontSize = 11,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            }
        };
    }

    public static View BuildBeltIcons(string beltGroup)
    {
        var belts = ParseBeltColors(beltGroup);
        var layout = new HorizontalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center
        };

        if (belts.Count == 0)
        {
            belts =
            [
                Colors.White,
                Colors.Yellow,
                Colors.Green,
                Colors.Blue,
                Colors.Red
            ];
        }

        foreach (var beltColor in belts)
        {
            layout.Children.Add(BuildSingleBeltIcon(beltColor));
        }

        return layout;
    }

    public static string ToDisciplineLabel(CompetitionDisciplineE discipline)
    {
        return discipline switch
        {
            CompetitionDisciplineE.Tul => "Tul",
            CompetitionDisciplineE.Sparring => "Sparring",
            CompetitionDisciplineE.Specialtekniker => "Spec",
            CompetitionDisciplineE.Kross => "Kross",
            _ => "Okänd"
        };
    }

    public static string BuildCompetitionTitle(DateTime date, string competitionName)
    {
        var formattedDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(competitionName)
            ? formattedDate
            : $"{formattedDate} {competitionName}";
    }

    static View BuildSingleBeltIcon(Color beltColor)
    {
        return new BeltView
        {
            WidthRequest = 30,
            HeightScale = 2,
            BeltColor = beltColor,
            Stripe1Color = Colors.Transparent,
            Stripe2Color = Colors.Transparent,
            VerticalOptions = LayoutOptions.Center
        };
    }

    static List<Color> ParseBeltColors(string beltGroup)
    {
        if (string.IsNullOrWhiteSpace(beltGroup))
            return new List<Color>();

        var colors = new List<Color>();
        foreach (var segment in beltGroup.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var color = ResolveSingleBeltColor(Normalize(segment).ToUpperInvariant());
            if (color != null)
            {
                colors.Add(color);
            }
        }

        return colors;
    }

    static Color? ResolveSingleBeltColor(string beltGroup)
    {
        if (beltGroup.Contains("VIT", StringComparison.Ordinal))
            return Colors.White;

        if (beltGroup.Contains("GUL", StringComparison.Ordinal))
            return Colors.Yellow;

        if (beltGroup.Contains("GRON", StringComparison.Ordinal))
            return Colors.Green;

        if (beltGroup.Contains("BLA", StringComparison.Ordinal))
            return Colors.Blue;

        if (beltGroup.Contains("ROD", StringComparison.Ordinal))
            return Colors.Red;

        if (beltGroup.Contains("SVART", StringComparison.Ordinal) ||
            beltGroup.Contains("DAN", StringComparison.Ordinal))
            return Colors.Black;

        return null;
    }

    static string Normalize(string value)
    {
        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark &&
                (char.IsLetterOrDigit(ch) || ch == ' '))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }
}

static class ViewExtensions
{
    public static T Assign<T>(this T view, Action<T> configure)
        where T : View
    {
        configure(view);
        return view;
    }
}
