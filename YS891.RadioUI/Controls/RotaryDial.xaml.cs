using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace YS891.RadioUI.Controls
{
    /// <summary>
    /// A skeuomorphic rotary knob. Drag it round (angle tracking from the knob
    /// centre) or use the mouse wheel; every ~10° emits one signed detent via
    /// <see cref="DetentChanged"/>, so a fast spin can't flood the consumer.
    /// </summary>
    public partial class RotaryDial
    {
        private const double DegreesPerDetent = 10.0;
        private const double Center = 100.0; // design-space centre

        private bool _dragging;
        private double _lastAngle;
        private double _residual;

        public RotaryDial()
        {
            InitializeComponent();
            BuildTicks();
            MouseLeftButtonDown += OnDown;
            MouseLeftButtonUp += OnUp;
            MouseMove += OnMove;
            MouseWheel += OnWheel;
        }

        /// <summary>Signed detent count: positive clockwise.</summary>
        public event Action<int> DetentChanged;

        private void BuildTicks()
        {
            var brush = (Brush)FindResource("KnobTickBrush");
            for (int i = 0; i < 36; i++)
            {
                double a = i * 10 * Math.PI / 180;
                double rOuter = 86, rInner = 76;
                TickCanvas.Children.Add(new Line
                {
                    X1 = Center + rInner * Math.Cos(a),
                    Y1 = Center + rInner * Math.Sin(a),
                    X2 = Center + rOuter * Math.Cos(a),
                    Y2 = Center + rOuter * Math.Sin(a),
                    Stroke = brush,
                    StrokeThickness = i % 9 == 0 ? 2.4 : 1.2,
                    Opacity = 0.55,
                });
            }
        }

        private double AngleTo(Point p)
            => Math.Atan2(p.Y - Center, p.X - Center) * 180 / Math.PI;

        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            _residual = 0;
            _lastAngle = AngleTo(e.GetPosition(DesignRoot));
            CaptureMouse();
        }

        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;

            double angle = AngleTo(e.GetPosition(DesignRoot));
            double delta = angle - _lastAngle;
            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;
            _lastAngle = angle;

            RotorTransform.Angle += delta; // knob follows the hand exactly

            _residual += delta;
            int detents = (int)(_residual / DegreesPerDetent);
            if (detents != 0)
            {
                _residual -= detents * DegreesPerDetent;
                DetentChanged?.Invoke(detents);
            }
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            int detents = e.Delta / 120;
            if (detents == 0) return;
            RotorTransform.Angle += detents * DegreesPerDetent;
            DetentChanged?.Invoke(detents);
            e.Handled = true;
        }
    }
}
