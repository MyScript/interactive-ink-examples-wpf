// Copyright @ MyScript. All rights reserved.

using MyScript.IInk.Graphics;
using System;
using System.Windows;
using System.Collections.Generic;


namespace MyScript.IInk.UIReferenceImplementation
{
    public class Canvas : ICanvas
    {
        private struct Group
        {
            public Rect Bound { get; }
            public Transform Transform { get; }

            public Group(Rect bound, Transform transform)
            {
                Bound = bound;
                Transform = transform;
            }
        };

        private System.Windows.Media.DrawingContext _drawingContext = null;
        private ImageLoader _imageLoader;
        private float _pixelsPerDip;

        private Transform _transform;
        private System.Windows.Media.Pen _stroke;
        private System.Windows.Media.SolidColorBrush _strokeColor;
        private System.Windows.Media.SolidColorBrush _fillColor;
        private System.Windows.Media.FillRule _fillRule;
        private System.Windows.Media.FontFamily _fontFamily;
        private FontWeight _fontWeight;
        private FontStretch _fontStretch;
        private FontStyle _fontStyle;
        private double _fontSize;
        private float _dropShadow_xOffset;
        private float _dropShadow_yOffset;
        private float _dropShadow_radius;
        private System.Windows.Media.SolidColorBrush _dropShadow_color;

        private Dictionary<string, Group> _groups;
        private Group _activeGroup;

        public Canvas(System.Windows.Media.DrawingContext drawingContext, float pixelsPerDip, ImageLoader imageLoader)
        {
            _drawingContext = drawingContext;
            _pixelsPerDip = pixelsPerDip;

            _imageLoader = imageLoader;

            _transform = new Transform();

            _strokeColor = new System.Windows.Media.SolidColorBrush();
            _stroke = new System.Windows.Media.Pen(_strokeColor, 1);

            _fillColor = new System.Windows.Media.SolidColorBrush();
            _fontStyle = FontStyles.Normal;
            _fontWeight = FontWeights.Normal;
            _fontStretch = FontStretches.Normal;

            _dropShadow_xOffset = 0.5f;
            _dropShadow_yOffset = 0.5f;
            _dropShadow_radius = 2.0f;
            _dropShadow_color = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));

            _groups = new Dictionary<string, Group>();
            _activeGroup = new Group(Rect.Empty, new Transform());
        }

        public Transform Transform
        {
            get
            {
                return _transform;
            }
            set
            {
                _transform = value;
            }
        }

        public void SetStrokeColor(Color color)
        {
            var c = System.Windows.Media.Color.FromArgb((byte)color.A, (byte)color.R, (byte)color.G, (byte)color.B);
            _strokeColor = new System.Windows.Media.SolidColorBrush(c);
            _stroke.Brush = _strokeColor;
        }

        public void SetStrokeWidth(float width)
        {
            _stroke.Thickness = width;
        }

        public void SetStrokeLineCap(LineCap lineCap)
        {
            if (lineCap == LineCap.BUTT)
            {
                _stroke.StartLineCap = System.Windows.Media.PenLineCap.Flat;
                _stroke.EndLineCap = System.Windows.Media.PenLineCap.Flat;
                _stroke.DashCap = System.Windows.Media.PenLineCap.Flat;
            }
            else if (lineCap == LineCap.ROUND)
            {
                _stroke.StartLineCap = System.Windows.Media.PenLineCap.Round;
                _stroke.EndLineCap = System.Windows.Media.PenLineCap.Round;
                _stroke.DashCap = System.Windows.Media.PenLineCap.Round;
            }
            else if (lineCap == LineCap.SQUARE)
            {
                _stroke.StartLineCap = System.Windows.Media.PenLineCap.Square;
                _stroke.EndLineCap = System.Windows.Media.PenLineCap.Square;
                _stroke.DashCap = System.Windows.Media.PenLineCap.Square;
            }
        }

        public void SetStrokeLineJoin(LineJoin lineJoin)
        {
            if (lineJoin == LineJoin.BEVEL)
                _stroke.LineJoin = System.Windows.Media.PenLineJoin.Bevel;
            else if (lineJoin == LineJoin.MITER)
                _stroke.LineJoin = System.Windows.Media.PenLineJoin.Miter;
            else if (lineJoin == LineJoin.ROUND)
                _stroke.LineJoin = System.Windows.Media.PenLineJoin.Round;
        }

        public void SetStrokeMiterLimit(float limit)
        {
            _stroke.MiterLimit = limit;
        }

        public void SetStrokeDashArray(float[] array)
        {
            if (_stroke.DashStyle.IsFrozen)
            {
                var dashStyle = _stroke.DashStyle.Clone();
                _stroke.DashStyle = dashStyle;
            }

            _stroke.DashStyle.Dashes.Clear();
            foreach (float f in array)
                _stroke.DashStyle.Dashes.Add(f);
        }

        public void SetStrokeDashOffset(float offset)
        {
            if (_stroke.DashStyle.IsFrozen)
            {
                var dashStyle = _stroke.DashStyle.Clone();
                _stroke.DashStyle = dashStyle;
            }

            _stroke.DashStyle.Offset = offset;
        }

        public void SetFillColor(Color color)
        {
            _fillColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)color.A, (byte)color.R, (byte)color.G, (byte)color.B));
        }

        public void SetFillRule(FillRule rule)
        {
            switch (rule)
            {
                case FillRule.NONZERO:
                    _fillRule = System.Windows.Media.FillRule.Nonzero;
                    break;
                case FillRule.EVENODD:
                    _fillRule = System.Windows.Media.FillRule.EvenOdd;
                    break;
                default:
                    break;
            }
        }

        public void SetDropShadow(float xOffset, float yOffset, float radius, Color color)
        {
            _dropShadow_xOffset = xOffset;
            _dropShadow_yOffset = yOffset;
            _dropShadow_radius = radius;
            _dropShadow_color = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)color.A, (byte)color.R, (byte)color.G, (byte)color.B));
        }

        public void SetFontProperties(string family, float lineHeight, float size, string style, string variant, int weight)
        {
            _fontFamily = new System.Windows.Media.FontFamily(FontMetricsProvider.ToPlatformFontFamily(family, style, weight));
            _fontStyle = FontMetricsProvider.ToPlatformFontStyle(style);
            _fontWeight = FontMetricsProvider.ToPlatformFontWeight(weight);
            _fontStretch = FontStretches.Normal;
            _fontSize = size;
        }

        public void StartGroup(string id, float x, float y, float width, float height, bool clipContent)
        {
            if (clipContent)
            {
                // Save previous
                _groups.Add(id, _activeGroup);

                // Apply transform to clipping rect
                var rect = new Rect(x, y, width, height);
                var transform = new System.Windows.Media.Matrix(    _transform.XX, _transform.XY,
                                                                    _transform.YX, _transform.YY,
                                                                    _transform.TX, _transform.TY );
                rect = Rect.Transform(rect, transform);

                // Push clipping rect
                _activeGroup = new Group(rect, _transform);
                var geometry = new System.Windows.Media.RectangleGeometry(_activeGroup.Bound);
                _drawingContext.PushClip(geometry);
            }
        }

        public void EndGroup(string id)
        {
            // Restore previous (if any)
            if (_groups.ContainsKey(id))
            {
                _activeGroup = _groups[id];
                _groups.Remove(id);

                // Pop clipping rect
                _drawingContext.Pop();
            }
        }

        public void StartItem(string id)
        {
        }

        public void EndItem(string id)
        {
        }

        private void PushRenderStates()
        {
            // Push current transform
            var transform = new System.Windows.Media.MatrixTransform(   _transform.XX, _transform.XY,
                                                                        _transform.YX, _transform.YY,
                                                                        _transform.TX, _transform.TY );
            _drawingContext.PushTransform(transform);
        }

        private void PopRenderStates()
        {
            // Pop current transform
            _drawingContext.Pop();
        }

        /// <summary>Clear canvas with color according to region</summary>
        public void Clear(float x, float y, float width, float height, Color color)
        {
            var color_ = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)color.A, (byte)color.R, (byte)color.G, (byte)color.B));
            var rect = new Rect(x, y, width, height);

            PushRenderStates();
            _drawingContext.DrawRectangle(color_, null, rect);
            PopRenderStates();
        }

        /// <summary>Draw Path to canvas according path</summary>
        public void DrawPath(IPath path)
        {
            var p = (RenderPath)path;

            if (_dropShadow_color.Color.A > 0)
                DrawShadowPath(p, _fillColor.Color.A > 0, _strokeColor.Color.A > 0);

            var draw = (_fillColor.Color.A > 0) || (_strokeColor.Color.A > 0);

            if (draw)
            {
                PushRenderStates();
                var geometry = p.FinalizeGeometry();

                if (_fillColor.Color.A > 0)
                {
                    geometry.FillRule = _fillRule;
                    _drawingContext.DrawGeometry(_fillColor, null, geometry);
                }

                if (_strokeColor.Color.A > 0)
                {
                    foreach (var figure in geometry.Figures)
                    {
                        foreach (var segment in figure.Segments)
                        {
                            segment.IsStroked = true;
                        }
                    }

                    _drawingContext.DrawGeometry(null, _stroke.Clone(), geometry);
                }

                PopRenderStates();
            }
        }

        /// <summary>Draw Rectangle to canvas according to region</summary>
        public void DrawRectangle(float x, float y, float width, float height)
        {
            if (_dropShadow_color.Color.A > 0)
                DrawShadowRect(x, y, width, height, _fillColor.Color.A > 0, _strokeColor.Color.A > 0);

            var draw = (_fillColor.Color.A > 0) || (_strokeColor.Color.A > 0);

            if (draw)
            {
                PushRenderStates();

                var rect = new Rect(x, y, width, height);

                if (_fillColor.Color.A > 0)
                {
                    _drawingContext.DrawRectangle(_fillColor, null, rect);
                }

                if (_strokeColor.Color.A > 0)
                {
                    _drawingContext.DrawRectangle(null, _stroke.Clone(), rect);
                }

                PopRenderStates();
            }
        }

        /// <summary>Draw Line to canvas according coordinates</summary>
        public void DrawLine(float x1, float y1, float x2, float y2)
        {
            if (_strokeColor.Color.A > 0)
            {
                if (_dropShadow_color.Color.A > 0)
                    DrawShadowLine(x1, y1, x2, y2);

                PushRenderStates();
                _drawingContext.DrawLine(_stroke.Clone(), new System.Windows.Point(x1, y1), new System.Windows.Point(x2, y2));
                PopRenderStates();
            }
        }

        public void DrawObject(string url, string mimeType, float x, float y, float width, float height)
        {
            if (_imageLoader == null)
                return;

            var transform = _transform;
            var tl = transform.Apply(x, y);
            var br = transform.Apply(x + width, y + height);
            var screenMin = new Graphics.Point(Math.Min(tl.X, br.X), Math.Min(tl.Y, br.Y));
            var screenMax = new Graphics.Point(Math.Max(tl.X, br.X), Math.Max(tl.Y, br.Y));

            if (_dropShadow_color.Color.A > 0)
                DrawShadowRect(x, y, width, height, _fillColor.Color.A > 0, _strokeColor.Color.A > 0);

            var image = _imageLoader.getImage(url, mimeType);

            if (image == null)
            {
                // image is not ready yet...
                if (_fillColor.Color.A > 0)
                {
                    var rect = new Rect(x, y, width, height);
                    PushRenderStates();
                    _drawingContext.DrawRectangle(_fillColor, null, rect);
                    PopRenderStates();
                }
            }
            else
            {
                // draw the image
                var rect = new Rect(x, y, width, height);
                PushRenderStates();
                _drawingContext.DrawImage(image, rect);
                PopRenderStates();
            }
        }

        /// <summary>Draw Text to canvas according coordinates and label</summary>
        public void DrawText(string label, float x, float y, float xmin, float ymin, float xmax, float ymax)
        {
            if (_fillColor.Color.A > 0)
            {
                // Create formatted text in a particular font at a particular size
                var typeFace = new System.Windows.Media.Typeface(_fontFamily, _fontStyle, _fontWeight, _fontStretch);
                var ft = new System.Windows.Media.FormattedText(
                                    label, System.Globalization.CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight, typeFace, _fontSize, _fillColor,
                                    _pixelsPerDip
                                );

                ft.TextAlignment = TextAlignment.Left;

                // Draw the text
                var baseline = (float)ft.Baseline;
                var origin = new System.Windows.Point(x, y - baseline);

                if (_dropShadow_color.Color.A > 0)
                    DrawShadowText(label, typeFace, origin);

                PushRenderStates();
                _drawingContext.DrawText(ft, origin);
                PopRenderStates();
            }
        }

        public IPath CreatePath()
        {
            return new RenderPath();
        }

        private void DrawShadowPath(RenderPath path, bool fill, bool outline)
        {
            PushRenderStates();

            var geometry = path.FinalizeGeometry().Clone();
            geometry.Transform = new System.Windows.Media.TranslateTransform(_dropShadow_xOffset, _dropShadow_yOffset);

            if (fill)
            {
                geometry.FillRule = _fillRule;
                _drawingContext.DrawGeometry(_dropShadow_color, null, geometry);
            }

            if (outline)
            {
                foreach (var figure in geometry.Figures)
                {
                    foreach (var segment in figure.Segments)
                    {
                        segment.IsStroked = true;
                    }
                }
                var pen = _stroke.Clone();
                pen.Brush = _dropShadow_color;
                _drawingContext.DrawGeometry(null, pen, geometry);
            }
            PopRenderStates();
        }

        private void DrawShadowRect(float x, float y, float width, float height, bool fill, bool outline)
        {
            PushRenderStates();

            var rect = new Rect(x + _dropShadow_xOffset, y + _dropShadow_yOffset, width, height);

            if (fill)
            {
                _drawingContext.DrawRectangle(_dropShadow_color, null, rect);
            }

            if (outline)
            {
                var pen = _stroke.Clone();
                pen.Brush = _dropShadow_color;
                _drawingContext.DrawRectangle(null, pen, rect);
            }

            PopRenderStates();
        }

        private void DrawShadowLine(float x1, float y1, float x2, float y2)
        {
            PushRenderStates();

            var p1 = new System.Windows.Point(x1 + _dropShadow_xOffset, y1 + _dropShadow_yOffset);
            var p2 = new System.Windows.Point(x2 + _dropShadow_xOffset, y2 + _dropShadow_yOffset);
            var pen = _stroke.Clone();
            pen.Brush = _dropShadow_color;
            _drawingContext.DrawLine(pen, p1, p2);

            PopRenderStates();
        }

        private void DrawShadowText(string label, System.Windows.Media.Typeface typeFace, System.Windows.Point origin)
        {
            PushRenderStates();

            // Create formatted text in a particular font at a particular size
            var ft = new System.Windows.Media.FormattedText(
                                label, System.Globalization.CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight, typeFace, _fontSize, _dropShadow_color,
                                _pixelsPerDip
                            );
            ft.TextAlignment = TextAlignment.Left;

            var p = new System.Windows.Point(origin.X + _dropShadow_xOffset, origin.Y + _dropShadow_yOffset);
            _drawingContext.DrawText(ft, p);

            PopRenderStates();
        }

        public void StartDraw(int x, int y, int width, int height)
        {
        }

        public void EndDraw()
        {
        }

        public void BlendOffscreen(uint id, float src_x, float src_y, float src_width, float src_height, float dest_x, float dest_y, float dest_width, float dest_height, Color color)
        {
            throw new NotImplementedException();
        }
    }
}
