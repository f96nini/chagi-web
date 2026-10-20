using Microsoft.Maui.Graphics;
using Microsoft.Maui.Devices;

namespace ITF_Taekwondo_Gradering.Graphics
{
    public sealed class RatioRectangleDrawable : IDrawable
    {
        public Color FillColor { get; set; }

        public Color? ExtraColor1 { get; set; }
        public Color? ExtraColor2 { get; set; }

        public float HeightScale { get; set; } = 1f;

        /// <summary>
        /// Bredden N för de lodräta strecken.
        /// Samma N används även som marginal.
        /// </summary>
        public float N { get; set; } = 6f;

        /// <summary>
        /// true = exakt 1 fysisk pixel.
        /// false = 1 MAUI-enhet / device independent pixel.
        /// </summary>
        public bool UsePhysicalPixelBorder { get; set; } = true;

        public RatioRectangleDrawable(
            Color fillColor,
            float n,
            Color? extraColor1 = null,
            Color? extraColor2 = null)
        {
            FillColor = fillColor;
            N = n;
            ExtraColor1 = extraColor1;
            ExtraColor2 = extraColor2;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            const float ratio = 5f; // liggande 1:5 => höjd:bredd = 1:5
            var effectiveRatio = ratio / Math.Max(HeightScale, 0.1f);

            // Räkna ut största möjliga rektangel med förhållandet 1:5
            // som ryms i GraphicsView.
            float rectWidth = dirtyRect.Width;
            float rectHeight = rectWidth / effectiveRatio;

            if (rectHeight > dirtyRect.Height)
            {
                rectHeight = dirtyRect.Height;
                rectWidth = rectHeight * effectiveRatio;
            }

            float x = dirtyRect.X + (dirtyRect.Width - rectWidth) / 2f;
            float y = dirtyRect.Y + (dirtyRect.Height - rectHeight) / 2f;

            var rect = new RectF(x, y, rectWidth, rectHeight);

            // 1 fysisk pixel på Android/iOS med hänsyn till skärmdensitet.
            float borderThickness = UsePhysicalPixelBorder
                ? 1f / (float)DeviceDisplay.MainDisplayInfo.Density
                : 1f;
            // Ensure border is visible even when very thin on some platforms/DPIs
            if (borderThickness < 1f)
            {
                borderThickness = 1f;
            }

            // Fyll huvudrektangeln.
            canvas.FillColor = FillColor;
            canvas.FillRectangle(rect);

            // Första extra färgen:
            // lodrätt streck, bredd N, marginal N från vänster.
            if (ExtraColor1 is not null)
            {
                var stripe1 = new RectF(
                    rect.Left + N,
                    rect.Top,
                    N,
                    rect.Height);

                canvas.FillColor = ExtraColor1;
                canvas.FillRectangle(stripe1);
            }

            // Andra extra färgen:
            // lodrätt streck, bredd N, marginal N till höger om första strecket.
            if (ExtraColor2 is not null)
            {
                var stripe2 = new RectF(
                    rect.Left + N + N + N, // vänstermarginal + stripe1-bredd + mellanrum
                    rect.Top,
                    N,
                    rect.Height);

                canvas.FillColor = ExtraColor2;
                canvas.FillRectangle(stripe2);
            }

            // Rita svart border sist så att den alltid ligger överst.
            // Inset med halva stroke-bredden så att linjen hamnar inuti canvasen.
            var borderRect = new RectF(
                rect.Left + borderThickness / 2f,
                rect.Top + borderThickness / 2f,
                rect.Width - borderThickness,
                rect.Height - borderThickness);

            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = borderThickness;
            canvas.DrawRectangle(borderRect);
        }
    }
}
