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
            // Process glyph one by one to generate one box per glyph
            for (int s = 0; s < spans.Length; ++s)
            {
                var textSpan = spans[s];
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

                for (int j = textSpan.BeginPosition; j < textSpan.EndPosition; ++j)
                {
                    var glyphLabel = text.GetGlyphLabelAt(j);

                    var formattedChar = new FormattedText
                                        (
                                            glyphLabel, System.Globalization.CultureInfo.CurrentCulture,
                                            FlowDirection.LeftToRight, typeFace, fontSize, Brushes.Black
                                        );

                    formattedChar.TextAlignment = TextAlignment.Left;

                    var geometry = formattedChar.BuildGeometry(new System.Windows.Point(0.0f, 0.0f));
                    var rect = geometry.Bounds;

                    // For glyph without geometry (space)
                    if (rect.IsEmpty)
                        rect = new Rect(0.0, 0.0, formattedChar.Width, formattedChar.Height);
                         
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
                }
            }

            if (glyphMetrics.Count == 0)
                return;

            // Draw text to get data for glyphs
            float baseline = 0.0f;
            FormattedText formattedText;

            {
                var firstStyle = spans[0].Style;
                var firstFontFamily = new FontFamily(firstStyle.FontFamily);
                var firstFontSize = mm2px(firstStyle.FontSize, dpiY);
                var firstFontWeight = FontWeight.FromOpenTypeWeight(firstStyle.FontWeight);
                var firstFontStretch = FontStretches.Normal;
                var firstFontStyle = FontStyles.Normal;

                if (firstStyle.FontStyle.Equals("italic"))
                    firstFontStyle =  FontStyles.Italic;
                else if (firstStyle.FontStyle.Equals("oblique"))
                    firstFontStyle =  FontStyles.Oblique;

                if (firstStyle.FontWeight >= 700)
                    firstFontWeight = FontWeights.Bold;
                else if (firstStyle.FontWeight >= 400)
                    firstFontWeight = FontWeights.Normal;
                else
                    firstFontWeight = FontWeights.Light;

                var firstFontTypeFace = new Typeface(firstFontFamily, firstFontStyle, firstFontWeight, firstFontStretch);

                formattedText = new FormattedText
                                    (
                                        text.Label, System.Globalization.CultureInfo.CurrentCulture,
                                        FlowDirection.LeftToRight, firstFontTypeFace, firstFontSize,
                                        Brushes.Black
                                    );

                formattedText.TextAlignment = TextAlignment.Left;

                for (int s = 0; s < spans.Length; ++s)
                {
                    var textSpan = spans[s];
                    var charIndex = textSpan.BeginPosition;
                    var charCount = textSpan.EndPosition - textSpan.BeginPosition;

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

                    var fontTypeFace = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);

                    formattedText.SetFontFamily(fontFamily, charIndex, charCount);
                    formattedText.SetFontSize(fontSize, charIndex, charCount);
                    formattedText.SetFontWeight(fontWeight, charIndex, charCount);
                    formattedText.SetFontStretch(fontStretch, charIndex, charCount);
                    formattedText.SetFontStyle(fontStyle, charIndex, charCount);
                    formattedText.SetFontTypeface(fontTypeFace, charIndex, charCount);
                }

                baseline = (float)formattedText.Baseline;
            }

            var drawing = new DrawingGroup();
            {
                var ctx = drawing.Open();
                ctx.DrawText(formattedText, new System.Windows.Point(0.0, 0.0));
                ctx.Close();
            }

            // Apply baseline and offsets of glyphs to bounding boxes
            WalkDrawingForText(drawing, glyphMetrics, baseline, dpiX, dpiY);
        }

        private static void WalkDrawingForText_(Drawing drawing, List<GlyphMetrics> glyphMetrics, ref int idx, ref float x, float baseline, float dpiX, float dpiY)
        {
            // Parse the Drawing tree recursively depending on the real type of each node
            // - node is a DrawingGroup, parse the children
            // - node is a GlyphRunDrawing, retrieve the glyphs data

            if (idx >= glyphMetrics.Count)
                return;

            var glyphs = drawing as GlyphRunDrawing;

            if (glyphs != null)
            {
                var glyphRun = glyphs.GlyphRun;

                if (    (glyphRun.Characters != null) && (glyphRun.ClusterMap != null)
                    &&  (glyphRun.Characters.Count == glyphRun.ClusterMap.Count) )
                {
                    var text = new String(glyphRun.Characters.ToArray());
                    var tee = System.Globalization.StringInfo.GetTextElementEnumerator(text);

                    while (tee.MoveNext())
                    {
                        if (idx >= glyphMetrics.Count)
                            break;

                        var g = tee.ElementIndex;

                        if ( (g > 0) && (idx > 0) && (glyphRun.ClusterMap[g] == glyphRun.ClusterMap[g-1]) )
                        {
                            // Ligature with the previous glyph
                            // The position is not accurate because of glyphs substitution at rendering
                            // but it makes the illusion.
                            var prevGlyphMetrics = glyphMetrics[idx-1];
                            glyphMetrics[idx].BoundingBox.Y -= px2mm(baseline, dpiY);
                            glyphMetrics[idx].BoundingBox.X = prevGlyphMetrics.BoundingBox.X
                                                            + prevGlyphMetrics.BoundingBox.Width
                                                            + prevGlyphMetrics.RightSideBearing
                                                            + glyphMetrics[idx].LeftSideBearing;
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
                    // Fallback in case we have no data for characters and clusters
                    var glyphCount = glyphRun.AdvanceWidths.Count;

                    for (int g = 0; g < glyphCount; ++g)
                    {
                        if (idx >= glyphMetrics.Count)
                            break;

                        glyphMetrics[idx].BoundingBox.Y -= px2mm(baseline, dpiY);
                        glyphMetrics[idx].BoundingBox.X += px2mm(x, dpiX);

                        x += (float)glyphRun.AdvanceWidths[g];
                        ++idx;
                    }
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
