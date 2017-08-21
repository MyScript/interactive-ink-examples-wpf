// Copyright MyScript. All right reserved.

using MyScript.IInk.Text;
using MyScript.IInk.Graphics;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System;
using System.Threading;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class FontMetricsProvider : IFontMetricsProvider
    {
        private float dpiX;
        private float dpiY;

        public FontMetricsProvider(float dpiX, float dpiY)
        {
            this.dpiX = dpiX;
            this.dpiY = dpiY;
        }

        private static float px2mm(float px, float dpi)
        {
            return 25.4f * (px / dpi);
        }

        private static float mm2px(float mmm, float dpi)
        {
            return (mmm / 25.4f) * dpi;
        }

        public Rectangle[] GetCharacterBoundingBoxes(MyScript.IInk.Text.Text text, TextSpan[] spans)
        {
            List<Rectangle> rectangles = new List<Rectangle>();
            if (Thread.CurrentThread == Application.Current.Dispatcher.Thread)
                GetCharacterBoundingBoxes_(text, spans, rectangles, dpiX, dpiY);
            else
                Application.Current.Dispatcher.BeginInvoke(new Action(() => { GetCharacterBoundingBoxes_(text, spans, rectangles, dpiX, dpiY); })).Wait();
            return rectangles.ToArray();
        }

        private static void GetCharacterBoundingBoxes_(MyScript.IInk.Text.Text text, TextSpan[] spans, List<Rectangle> rectangles, float dpiX, float dpiY)
        {
            var firstStyle = spans.First().Style;
            var textBlock = new TextBlock();

            textBlock.FontFamily = new FontFamily(firstStyle.FontFamily);
            textBlock.Padding = new Thickness(0.0);
            textBlock.Margin = new Thickness(0.0);
            textBlock.TextWrapping = TextWrapping.NoWrap;
            textBlock.HorizontalAlignment = HorizontalAlignment.Left;
            textBlock.VerticalAlignment = VerticalAlignment.Top;

            foreach (var textSpan in spans)
            {
                var fontFamily = new FontFamily(textSpan.Style.FontFamily);
                var fontSize = mm2px(textSpan.Style.FontSize, dpiY);
                var fontWeight = FontWeight.FromOpenTypeWeight(textSpan.Style.FontWeight);
                var fontStretch = FontStretches.Normal;
                var fontStyle = FontStyles.Normal;

                if (textSpan.Style.FontStyle.Equals("italic"))
                    fontStyle =  FontStyles.Italic;
                else if (textSpan.Style.FontStyle.Equals("oblique"))
                    fontStyle =  FontStyles.Oblique;

                if (textSpan.Style.FontWeight >= 700)
                    fontWeight = FontWeights.Bold;
                else if (textSpan.Style.FontWeight >= 400)
                    fontWeight = FontWeights.Normal;
                else
                    fontWeight = FontWeights.Light;

                // Process glyph one by one to generate one box per glyph
                for (int j = textSpan.BeginPosition; j < textSpan.EndPosition; ++j)
                {
                    var textRun = new Run(text.GetGlyphLabelAt(j));

                    textRun.FontFamily = fontFamily;
                    textRun.FontSize = fontSize;
                    textRun.FontWeight = fontWeight;
                    textRun.FontStyle = fontStyle;
                    textRun.FontStretch = fontStretch;

                    textBlock.Inlines.Add(textRun);
                }
            }

            textBlock.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            textBlock.Arrange(new Rect(textBlock.DesiredSize));

            var baseline = (float)textBlock.BaselineOffset;
            var d = VisualTreeHelper.GetDrawing(textBlock);
            WalkDrawingForText(d, rectangles, baseline, dpiX, dpiY);
        }

        private static void WalkDrawingForText(Drawing d, List<Rectangle> rectangles, float baseline, float dpiX, float dpiY)
        {
            var glyphs = d as GlyphRunDrawing;

            if (glyphs != null)
            {
                // Use the bound from "glyphs.GlyphRun.BuildGeometry()" which seems
                // to be the best fitting one.
                // (instead of other bounds from "glyphs.Bounds",
                // "glyphs.GlyphRun.ComputeAlignmentBox" or "glyphs.GlyphRun.ComputeInkBoundingBox)"
                var geometry = glyphs.GlyphRun.BuildGeometry();
                var rect = geometry.Bounds;

                if (rect.IsEmpty)
                {
                    // For glyph without geometry (space)
                    rect = new Rect(0.0, 0.0, 0.0, 0.0);
                }
                         
                var rectX = (float)rect.X;
                var rectY = (float)rect.Y;
                var rectW = (float)rect.Width;
                var rectH = (float)rect.Height;

                rectY -= baseline;

                var rect_ = new Rectangle(px2mm(rectX, dpiX), px2mm(rectY, dpiY), px2mm(rectW, dpiX), px2mm(rectH, dpiY));
                rectangles.Add(rect_);
            }
            else
            {
                var g = d as DrawingGroup;

                if (g != null)
                {
                    foreach (var child in g.Children)
                        WalkDrawingForText(child, rectangles, baseline, dpiX, dpiY);
                }
            }
        }

        public float GetFontSizePx(MyScript.IInk.Graphics.Style style)
        {
            return style.FontSize;
        }
    }
}
