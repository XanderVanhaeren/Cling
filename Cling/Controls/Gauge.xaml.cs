using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Cling.Controls
{
    /// <summary>
    /// Semicircular OpenCL-style speedometer gauge. The visual tree is built in code-behind
    /// so the arc, ticks and needle geometry stay deterministic regardless of host sizing.
    /// </summary>
    public partial class Gauge : UserControl
    {
        // Arc sweep (math degrees, CCW positive). Left end -> right end.
        private const double ArcStartAngle = 135.0;
        private const double ArcEndAngle = 45.0;

        public Gauge()
        {
            InitializeComponent();
            BuildGauge();
        }

        private double _value = 0.35;

        /// <summary>Normalized value in the range [0,1].</summary>
        public double Value
        {
            get => _value;
            set => _value = Math.Clamp(value, 0d, 1d);
        }

        /// <summary>Dial diameter in device-independent pixels.</summary>
        public double Diameter { get; set; } = 260d;

        /// <summary>When true a centered label is drawn over the pivot.</summary>
        public bool ShowLabel { get; set; }

        /// <summary>Text shown when <see cref="ShowLabel"/> is true.</summary>
        public string LabelText { get; set; } = "CL!ng";

        private void BuildGauge()
        {
            double diameter = Diameter > 0 ? Diameter : ActualWidth;
            if (diameter <= 0) return;

            double cx = diameter / 2d;
            double cy = diameter / 2d;
            double r = diameter * 0.46d;

            var canvas = this.FindName("Canvas") as Canvas;
            if (canvas == null) return;

            // --- Dark dial background -------------------------------------------------
            var bg = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = Brushes.DarkSlateGray,
                Stroke = Brushes.DimGray
            };
            Canvas.SetLeft(bg, 0);
            Canvas.SetTop(bg, 0);
            canvas.Children.Add(bg);

            // --- Gradient stroke for the arc -----------------------------------------
            var gradient = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString("#4caf50"),
                (Color)ColorConverter.ConvertFromString("#e53935"),
                0d);
            gradient.StartPoint = new Point(-r * 0.7071d, -r * 0.7071d);
            gradient.EndPoint = new Point(r * 0.7071d, -r * 0.7071d);

            // --- Arc (fine polyline so geometry is deterministic) --------------------
            var arc = new Polyline { Stroke = gradient, StrokeThickness = diameter * 0.028d };
            const int steps = 60;
            for (int i = 0; i <= steps; i++)
            {
                double theta = ArcStartAngle - ((ArcEndAngle - ArcStartAngle) * i / steps);
                arc.Points.Add(new Point(
                    cx + r * Math.Cos(theta * Math.PI / 180d),
                    cy - r * Math.Sin(theta * Math.PI / 180d)));
            }
            canvas.Children.Add(arc);

            // --- Tick marks ----------------------------------------------------------
            const int tickCount = 11;
            for (int i = 0; i < tickCount; i++)
            {
                double theta = ArcStartAngle - ((ArcEndAngle - ArcStartAngle) * i / (tickCount - 1));
                double outerR = r * 1.06d;
                double innerR = r * 0.92d;
                var tick = new Line
                {
                    X1 = cx + innerR * Math.Cos(theta * Math.PI / 180d),
                    Y1 = cy - innerR * Math.Sin(theta * Math.PI / 180d),
                    X2 = cx + outerR * Math.Cos(theta * Math.PI / 180d),
                    Y2 = cy - outerR * Math.Sin(theta * Math.PI / 180d),
                    Stroke = Brushes.LightGray,
                    StrokeThickness = diameter * 0.012d,
                    StrokeLineJoin = PenLineJoin.Round
                };
                canvas.Children.Add(tick);
            }

            // --- Needle --------------------------------------------------------------
            double needleLen = r * 0.85d;
            double needleAngle = ArcStartAngle - ((ArcEndAngle - ArcStartAngle) * _value);
            var needle = new Line
            {
                X1 = cx,
                Y1 = cy,
                X2 = cx + needleLen * Math.Cos(needleAngle * Math.PI / 180d),
                Y2 = cy - needleLen * Math.Sin(needleAngle * Math.PI / 180d),
                Stroke = Brushes.Black,
                StrokeThickness = diameter * 0.016d,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(needle);

            // --- Needle tail behind the pivot ----------------------------------------
            var tail = new Line
            {
                X1 = cx,
                Y1 = cy,
                X2 = cx,
                Y2 = cy + diameter * 0.05d,
                Stroke = Brushes.Black,
                StrokeThickness = diameter * 0.014d,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(tail);

            // --- Pivot ---------------------------------------------------------------
            var pivot = new Ellipse
            {
                Width = diameter * 0.065d,
                Height = diameter * 0.065d,
                Fill = Brushes.Black
            };
            Canvas.SetLeft(pivot, cx - pivot.Width / 2d);
            Canvas.SetTop(pivot, cy - pivot.Height / 2d);
            canvas.Children.Add(pivot);

            var hub = new Ellipse
            {
                Width = diameter * 0.035d,
                Height = diameter * 0.035d,
                Fill = Brushes.White
            };
            Canvas.SetLeft(hub, cx - hub.Width / 2d);
            Canvas.SetTop(hub, cy - hub.Height / 2d);
            canvas.Children.Add(hub);

            // --- Optional center label ----------------------------------------------
            if (ShowLabel)
            {
                var label = new TextBlock
                {
                    Text = LabelText,
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = diameter * 0.13d
                };
                canvas.Children.Add(label);
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (Diameter <= 0 && ActualWidth > 0) Diameter = ActualWidth;
            BuildGauge();
        }
    }
}
