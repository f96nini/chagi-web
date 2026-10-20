using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using ITF_Taekwondo_Gradering.Graphics;

namespace ITF_Taekwondo_Gradering
{
    public class BeltView : ContentView
    {
        public static readonly BindableProperty BeltColorProperty = BindableProperty.Create(
            nameof(BeltColor), typeof(Color), typeof(BeltView), Colors.White, propertyChanged: OnAppearanceChanged);

        public static readonly BindableProperty Stripe1ColorProperty = BindableProperty.Create(
            nameof(Stripe1Color), typeof(Color), typeof(BeltView), Colors.Transparent, propertyChanged: OnAppearanceChanged);

        public static readonly BindableProperty Stripe2ColorProperty = BindableProperty.Create(
            nameof(Stripe2Color), typeof(Color), typeof(BeltView), Colors.Transparent, propertyChanged: OnAppearanceChanged);

        public static readonly BindableProperty HeightScaleProperty = BindableProperty.Create(
            nameof(HeightScale), typeof(double), typeof(BeltView), 1d, propertyChanged: OnLayoutChanged);

        GraphicsView _gfx;

        public Color BeltColor
        {
            get => (Color)GetValue(BeltColorProperty);
            set => SetValue(BeltColorProperty, value);
        }

        public Color Stripe1Color
        {
            get => (Color)GetValue(Stripe1ColorProperty);
            set => SetValue(Stripe1ColorProperty, value);
        }

        public Color Stripe2Color
        {
            get => (Color)GetValue(Stripe2ColorProperty);
            set => SetValue(Stripe2ColorProperty, value);
        }

        public double HeightScale
        {
            get => (double)GetValue(HeightScaleProperty);
            set => SetValue(HeightScaleProperty, value);
        }

        public BeltView()
        {
            HorizontalOptions = LayoutOptions.Start;
            WidthRequest = 60;
            _gfx = new GraphicsView
            {
                HorizontalOptions = LayoutOptions.Start
            };

            Content = _gfx;
            UpdateLayoutSize(WidthRequest);
            BuildVisuals();
        }

        static void OnAppearanceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is BeltView bv)
                bv.BuildVisuals();
        }

        static void OnLayoutChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is BeltView bv)
            {
                bv.UpdateLayoutSize(bv.Width > 0 ? bv.Width : bv.WidthRequest);
                bv.BuildVisuals();
            }
        }

        void BuildVisuals()
        {
            // Create RatioRectangleDrawable with requested colors. Use N=12 for stripe width.
            float n = 8f;
            var extra1 = Stripe1Color.Equals(Colors.Transparent) ? (Color?)null : Stripe1Color;
            var extra2 = Stripe2Color.Equals(Colors.Transparent) ? (Color?)null : Stripe2Color;

            var drawable = new RatioRectangleDrawable(BeltColor, n, extra1, extra2)
            {
                HeightScale = (float)HeightScale
            };
            _gfx.Drawable = drawable;
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width > 0)
            {
                UpdateLayoutSize(width);
            }
        }

        void UpdateLayoutSize(double width)
        {
            if (width <= 0)
                return;

            var height = width * HeightScale / 5.0;
            HeightRequest = height;
            _gfx.WidthRequest = width;
            _gfx.HeightRequest = height;
        }
    }
}
