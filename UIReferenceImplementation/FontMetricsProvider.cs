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
    public class FontMetricsProvider : IFontMetricsProvider2
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

        private static void GetGlyphMetrics_(MyScript.IInk.Text.Text text, TextSpan[] spans, List<GlyphMetrics> glyphMetrics, float dpiX, float dpiY)
        {
            var drawing = new DrawingGroup();
            var ctx = drawing.Open();

            float baseline = 0.0f;
            int spanCount = 0;
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

                var typeFace = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);

                // Process glyph one by one to generate one box per glyph
                var label = "";

                for (int j = textSpan.BeginPosition; j < textSpan.EndPosition; ++j)
                {
                    var glyphLabel = text.GetGlyphLabelAt(j);

                    var formattedText = new FormattedText
                                        (
                                            glyphLabel, System.Globalization.CultureInfo.CurrentCulture,
                                            FlowDirection.LeftToRight, typeFace, fontSize, Brushes.Black
                                        );

                    formattedText.TextAlignment = TextAlignment.Left;

                    var geometry = formattedText.BuildGeometry(new System.Windows.Point(0.0f, 0.0f));
                    var rect = geometry.Bounds;

                    // For glyph without geometry (space)
                    if (rect.IsEmpty)
                        rect = new Rect(0.0, 0.0, formattedText.Width, formattedText.Height);
                         
                    var rectX = (float)rect.X;
                    var rectY = (float)rect.Y;
                    var rectW = (float)rect.Width;
                    var rectH = (float)rect.Height;

                    var leftBearing = -(float)(rect.X);
                    var rightBearing = 0.0f;

                    var glyphX = px2mm(rectX, dpiX);
                    var glyphY = px2mm(rectY, dpiY);
                    var glyphW = px2mm(rectW, dpiX);
                    var glyphH = px2mm(rectH, dpiY);
                    var glyphRect = new Rectangle(glyphX, glyphY, glyphW, glyphH);
                    var glyphLeftBearing = px2mm(leftBearing, dpiX);
                    var glyphRightBearing = px2mm(rightBearing, dpiX);

                    glyphMetrics.Add(new GlyphMetrics(glyphRect, glyphLeftBearing, glyphRightBearing));

                    label += glyphLabel;
                }

                // Draw current span
                {
                    var formattedText = new FormattedText
                                        (
                                            label, System.Globalization.CultureInfo.CurrentCulture,
                                            FlowDirection.LeftToRight, typeFace, fontSize, Brushes.Black
                                        );

                    formattedText.TextAlignment = TextAlignment.Left;

                    if (spanCount == 0)
                        baseline = (float)formattedText.Baseline;

                    ctx.DrawText(formattedText, new System.Windows.Point(0.0, 0.0));
                }

                ++spanCount;
            }

            ctx.Close();

            // Apply baseline and offsets of glyphs to bounding boxes
            if (glyphMetrics.Count > 0)
                WalkDrawingForText(drawing, glyphMetrics, baseline, dpiX, dpiY);
        }

        private static void WalkDrawingForText_(Drawing drawing, List<GlyphMetrics> glyphMetrics, ref int idx, ref float x, float baseline, float dpiX, float dpiY)
        {
            var glyphs = drawing as GlyphRunDrawing;

            if (idx >= glyphMetrics.Count)
                return;

            if (glyphs != null)
            {
                var glyphRun = glyphs.GlyphRun;
                var glyphCount = glyphRun.AdvanceWidths.Count;
                var charCount = glyphRun.ClusterMap.Count;

                for (int g = 0; g < charCount; ++g)
                {
                    if ( (g > 0) && (glyphRun.ClusterMap[g] == glyphRun.ClusterMap[g-1]) )
                    {
                        // Current character shares its glyph with the previous character (ligature)

                    #if false
                        // Recompute box of previous glyph
                        var i = glyphRun.GlyphIndices[glyphRun.ClusterMap[g-1]];

                        var rectX = (float)(glyphRun.GlyphTypeface.LeftSideBearings[i] * glyphRun.FontRenderingEmSize);
                        var rectY = (float)(glyphRun.GlyphTypeface.BottomSideBearings[i] * glyphRun.FontRenderingEmSize);
                        var rectW = (float)((glyphRun.GlyphTypeface.AdvanceWidths[i] - glyphRun.GlyphTypeface.LeftSideBearings[i] - glyphRun.GlyphTypeface.RightSideBearings[i]) * glyphRun.FontRenderingEmSize);
                        var rectH = (float)((glyphRun.GlyphTypeface.AdvanceHeights[i] - glyphRun.GlyphTypeface.TopSideBearings[i] - glyphRun.GlyphTypeface.BottomSideBearings[i]) * glyphRun.FontRenderingEmSize);

                        var leftBearing = -(float)(rectX);
                        var rightBearing = 0.0f;

                        var glyphX = px2mm(rectX, dpiX);
                        var glyphY = px2mm(rectY, dpiY);
                        var glyphW = px2mm(rectW, dpiX);
                        var glyphH = px2mm(rectH, dpiY);
                        var glyphRect = new Rectangle(glyphX, glyphY, glyphW, glyphH);
                        var glyphLeftBearing = px2mm(leftBearing, dpiX);
                        var glyphRightBearing = px2mm(rightBearing, dpiX);

                        x -= (float)glyphRun.AdvanceWidths[glyphRun.ClusterMap[g-1]];
                        glyphMetrics[idx-1] = new GlyphMetrics(glyphRect, glyphLeftBearing, glyphRightBearing);
                        glyphMetrics[idx-1].BoundingBox.Y -= px2mm(baseline, dpiY);
                        glyphMetrics[idx-1].BoundingBox.X += px2mm(x, dpiX);
                        x += (float)glyphRun.AdvanceWidths[glyphRun.ClusterMap[g-1]];
                    #endif

                        // => Use previous box for current character
                        glyphMetrics[idx] = glyphMetrics[idx-1];

                    }
                    else
                    {
                        glyphMetrics[idx].BoundingBox.Y -= px2mm(baseline, dpiY);
                        glyphMetrics[idx].BoundingBox.X += px2mm(x, dpiX);
                        x += (float)glyphRun.AdvanceWidths[glyphRun.ClusterMap[g]];
                    }

                    ++idx;
                }
            }
            else
            {
                var group = drawing as DrawingGroup;

                if (group != null)
                {
                    foreach (var child in group.Children)
                        WalkDrawingForText_(child, glyphMetrics, ref idx, ref x, baseline, dpiX, dpiY);
                }
            }
        }

        private static void WalkDrawingForText(Drawing drawing, List<GlyphMetrics> glyphMetrics, float baseline, float dpiX, float dpiY)
        {
            int glyphIdx = 0;
            float glyphX = 0.0f;
            WalkDrawingForText_(drawing, glyphMetrics, ref glyphIdx, ref glyphX, baseline, dpiX, dpiY);
        }

        public Rectangle[] GetCharacterBoundingBoxes(MyScript.IInk.Text.Text text, TextSpan[] spans)
        {
            var glyphMetrics = new List<GlyphMetrics>();
            var rectangles = new List<Rectangle>();

            GetGlyphMetrics_(text, spans, glyphMetrics, dpiX, dpiY);

            foreach (var metrics in glyphMetrics)
                rectangles.Add(metrics.BoundingBox);

            return rectangles.ToArray();
        }

        public float GetFontSizePx(MyScript.IInk.Graphics.Style style)
        {
            return style.FontSize;
        }

        public bool SupportsGlyphMetrics()
        {
            return true;
        }

        public GlyphMetrics[] GetGlyphMetrics(MyScript.IInk.Text.Text text, TextSpan[] spans)
        {
            var glyphMetrics = new List<GlyphMetrics>();
            GetGlyphMetrics_(text, spans, glyphMetrics, dpiX, dpiY);
            return glyphMetrics.ToArray();
        }
    }
}
