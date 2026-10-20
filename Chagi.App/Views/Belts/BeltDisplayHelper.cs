using Microsoft.Maui.Graphics;

namespace ITF_Taekwondo_Gradering;

static class BeltDisplayHelper
{
    public static BeltAppearance GetAppearance(string beltName)
    {
        var beltColor = Colors.White;
        Color? stripe1 = null;
        Color? stripe2 = null;

        switch (beltName)
        {
            case "10 Kup-1":
                beltColor = Colors.White;
                break;
            case "10 Kup-2":
                beltColor = Colors.White;
                stripe1 = Colors.Black;
                break;
            case "9 Kup-1":
                beltColor = Colors.White;
                stripe1 = Colors.Yellow;
                break;
            case "9 Kup-2":
                beltColor = Colors.White;
                stripe1 = Colors.Black;
                stripe2 = Colors.Yellow;
                break;
            case "8 Kup-1":
                beltColor = Colors.Yellow;
                break;
            case "8 Kup-2":
                beltColor = Colors.Yellow;
                stripe1 = Colors.Black;
                break;
            case "7 Kup-1":
                beltColor = Colors.Yellow;
                stripe1 = Colors.Green;
                break;
            case "7 Kup-2":
                beltColor = Colors.Yellow;
                stripe1 = Colors.Black;
                stripe2 = Colors.Green;
                break;
            case "6 Kup-1":
                beltColor = Colors.Green;
                break;
            case "6 Kup-2":
                beltColor = Colors.Green;
                stripe1 = Colors.Black;
                break;
            case "5 Kup-1":
                beltColor = Colors.Green;
                stripe1 = Colors.Blue;
                break;
            case "5 Kup-2":
                beltColor = Colors.Green;
                stripe1 = Colors.Black;
                stripe2 = Colors.Blue;
                break;
        }

        return new BeltAppearance(beltColor, stripe1, stripe2);
    }

    public static void ApplyTo(BeltView beltView, string beltName)
    {
        var appearance = GetAppearance(beltName);
        beltView.BeltColor = appearance.BeltColor;
        beltView.Stripe1Color = appearance.Stripe1Color ?? Colors.Transparent;
        beltView.Stripe2Color = appearance.Stripe2Color ?? Colors.Transparent;
    }
}

readonly record struct BeltAppearance(Color BeltColor, Color? Stripe1Color, Color? Stripe2Color);
