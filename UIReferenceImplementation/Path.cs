// Copyright @ MyScript. All rights reserved.

using System.Windows;
using System.Windows.Media;

namespace MyScript.IInk.UIReferenceImplementation
{
    class RenderPath : MyScript.IInk.Graphics.IPath
    {
        private PathGeometry _path;
        PathFigure _figure;

        public RenderPath()
        {
            _path = new PathGeometry();
            _figure = null;
        }

        public PathGeometry FinalizeGeometry()
        {
            if (_figure != null)
            {
                _path.Figures.Add(_figure);
                _figure = null;
            }
            return _path;
        }

        public uint UnsupportedOperations
        {
            get { return (uint)MyScript.IInk.Graphics.PathOperation.ARC_OPS; }
        }

        public void MoveTo(float x, float y)
        {
            if (_figure != null)
                _path.Figures.Add(_figure);
            _figure = new PathFigure();
            _figure.StartPoint = new Point(x, y);
        }

        public void LineTo(float x, float y)
        {
            _figure.Segments.Add(new LineSegment(new Point(x, y), false));
        }

        public void CurveTo(float x1, float y1, float x2, float y2, float x, float y)
        {
            _figure.Segments.Add(new BezierSegment(new Point(x1, y1), new Point(x2, y2), new Point(x, y), false));
        }

        public void QuadTo(float x1, float y1, float x, float y)
        {
            _figure.Segments.Add(new QuadraticBezierSegment(new Point(x1, y1), new Point(x, y), false));
        }

        public void ArcTo(float rx, float ry, float phi, bool fA, bool fS, float x, float y)
        {
            _figure.Segments.Add(new ArcSegment(new Point(x, y), new Size(rx, ry), phi, fA, fS ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, false));
        }

        public void ClosePath()
        {
            if (!_figure.IsClosed)
                _figure.Segments.Add(new LineSegment(_figure.StartPoint, false));
            _path.Figures.Add(_figure);
            _figure = null;
        }
    }
}
