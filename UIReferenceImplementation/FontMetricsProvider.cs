// Copyright @ MyScript. All rights reserved.

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
    internal static class FontFamilies
    {
        public static string MyScriptInter      { get { return _myScriptInter ?? _defaultFamily; }      private set { _myScriptInter = value; } }
        public static string MyScriptInterBold  { get { return _myScriptInterBold ?? _defaultFamily; }  private set { _myScriptInterBold = value; } }
        public static string StixRegular        { get { return _stixRegular ?? _defaultStixFamily; }    private set { _stixRegular = value; } }
        public static string StixItalic         { get { return _stixItalic ?? _defaultStixFamily; }     private set { _stixItalic = value; } }

        private const string _defaultFamily = "Segoe UI";
        private const string _defaultStixFamily = "STIX";

        private const string _fontFolder = "fonts";

        private static bool _initialized = false;
        private static string _myScriptInter;
        private static string _myScriptInterBold;
        private static string _stixRegular;
        private static string _stixItalic;

        private static string RegisterFontFamily(string filename, string name, string defaultFamily)
        {
            var localPath = System.IO.Path.Combine(_fontFolder, filename);
            var fullPath = System.IO.Path.GetFullPath(localPath);

            if (System.IO.File.Exists(fullPath))
                return fullPath + "#" + name;

            return defaultFamily;
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            MyScriptInter = RegisterFontFamily("MyScriptInter-Regular.otf", "MyScriptInter", _defaultFamily);
            MyScriptInterBold = RegisterFontFamily("MyScriptInter-Bold.otf", "MyScriptInter", _defaultFamily);
            StixRegular = RegisterFontFamily("STIXGeneral.ttf", "STIXGeneral", _defaultStixFamily);
            StixItalic = RegisterFontFamily("STIX-Italic.otf", "STIX", _defaultStixFamily);

            _initialized = true;
        }
    }

    public class FontMetricsProvider : IFontMetricsProvider
    {
        private float _dpiX;
        private float _dpiY;
        private float _pixelsPerDip;

        public static void Initialize()
        {
            FontFamilies.Initialize();
        }

        public FontMetricsProvider(float dpiX, float dpiY, float pixelsPerDip)
        {
            _dpiX = dpiX;
            _dpiY = dpiY;
            _pixelsPerDip = pixelsPerDip;
        }

        public static string ToPlatformFontFamily(string family, string style, int weight)
        {
            if (family == "MyScriptInter")
                return (ToPlatformFontWeight(weight) == FontWeights.Bold) ? FontFamilies.MyScriptInterBold : FontFamilies.MyScriptInter;
            else if (family == "STIX")
                return (ToPlatformFontStyle(style) == FontStyles.Italic) ? FontFamilies.StixItalic : FontFamilies.StixRegular;

            return family;
        }

        public static FontWeight ToPlatformFontWeight(int weight)
        {
            var fontWeight = FontWeight.FromOpenTypeWeight(weight);

            if (weight >= 700)
                fontWeight = FontWeights.Bold;
            else if (weight >= 400)
                fontWeight = FontWeights.Normal;
            else
                fontWeight = FontWeights.Light;

            return fontWeight;
        }

        public static FontStyle ToPlatformFontStyle(string style)
        {
            var fontStyle = FontStyles.Normal;

            if (style.Equals("italic"))
                fontStyle = FontStyles.Italic;
            else if (style.Equals("oblique"))
                fontStyle = FontStyles.Oblique;

            return fontStyle;
        }

        private static float px2mm(float px, float dpi)
        {
            return 25.4f * (px / dpi);
        }

        private static float mm2px(float mmm, float dpi)
        {
            return (mmm / 25.4f) * dpi;
        }

        private class FontKey
        {
            public FontFamily FontFamily { get; }
            public float FontSize { get; }
            public FontWeight FontWeight { get; }
            public FontStretch FontStretch{ get; }
            public FontStyle FontStyle { get; }

            public FontKey(FontFamily fontFamily, float fontSize, FontWeight fontWeight, FontStretch fontStretch, FontStyle fontStyle)
            {
                this.FontFamily = fontFamily;
                this.FontSize = fontSize;
                this.FontWeight = fontWeight;
                this.FontStretch = fontStretch;
                this.FontStyle = fontStyle;
            }

            public override bool Equals(object obj)
            {
                if (obj.GetType() != this.GetType())
                    return false;

                FontKey other = (FontKey)obj;
                return  (   (this.FontFamily.Equals(other.FontFamily))
                        &&  (this.FontSize == other.FontSize)
                        &&  (this.FontWeight == other.FontWeight)
                        &&  (this.FontStretch == other.FontStretch)
                        &&  (this.FontStyle == other.FontStyle) );
            }

            public override int GetHashCode()
            {
                return FontFamily.GetHashCode() ^ FontSize.GetHashCode() ^ FontWeight.GetHashCode() ^ FontStretch.GetHashCode() ^ FontStyle.GetHashCode();
            }
        }
        private Dictionary<FontKey, Dictionary<string, GlyphMetrics>> cache = new Dictionary<FontKey, Dictionary<string, GlyphMetrics>>();

        private FontKey FontKeyFromStyle(MyScript.IInk.Graphics.Style style)
        {
            var fontFamily = new FontFamily(ToPlatformFontFamily(style.FontFamily, style.FontStyle, style.FontWeight));
            var fontSize = mm2px(style.FontSize, _dpiY);
            var fontWeight = ToPlatformFontWeight(style.FontWeight);
            var fontStretch = FontStretches.Normal;
            var fontStyle = ToPlatformFontStyle(style.FontStyle);
            return new FontKey(fontFamily, fontSize, fontWeight, fontStretch, fontStyle);
        }

        private GlyphMetrics GetGlyphMetrics(FontKey fontKey, string glyphLabel)
        {
            Dictionary<string, GlyphMetrics> fontCache = null;
            if (!cache.TryGetValue(fontKey, out fontCache))
            {
                fontCache = new Dictionary<string, GlyphMetrics>();
                cache[fontKey] = fontCache;
            }

            GlyphMetrics value = null;
            if (!fontCache.TryGetValue(glyphLabel, out value))
            {
                var typeFace = new Typeface(fontKey.FontFamily, fontKey.FontStyle, fontKey.FontWeight, fontKey.FontStretch);
                var formattedChar = new FormattedText
                                    (
                                        glyphLabel, System.Globalization.CultureInfo.CurrentCulture,
                                        FlowDirection.LeftToRight, typeFace, fontKey.FontSize, Brushes.Black,
                                        _pixelsPerDip
                                    );

                formattedChar.TextAlignment = TextAlignment.Left;

                var geometry = formattedChar.BuildGeometry(new System.Windows.Point(0.0f, 0.0f));
                var rect = geometry.Bounds;

                // For glyph without geometry (space)
                if (rect.IsEmpty)
                    rect = new Rect(0.0, 0.0, formattedChar.WidthIncludingTrailingWhitespace, formattedChar.Height);

                var rectX = (float)rect.X;
                var rectY = (float)rect.Y;
                var rectW = (float)rect.Width;
                var rectH = (float)rect.Height;

                var leftBearing = -(float)(rect.X);
                var rightBearing = 0.0f;

                var glyphX = px2mm(rectX, _dpiX);
                var glyphY = px2mm(rectY, _dpiY);
                var glyphW = px2mm(rectW, _dpiX);
                var glyphH = px2mm(rectH, _dpiY);
                var glyphRect = new Rectangle(glyphX, glyphY, glyphW, glyphH);
                var glyphLeftBearing = px2mm(leftBearing, _dpiX);
                var glyphRightBearing = px2mm(rightBearing, _dpiX);

                value = new GlyphMetrics(glyphRect, glyphLeftBearing, glyphRightBearing);
                fontCache[glyphLabel] = value;
            }

            return new GlyphMetrics(new Rectangle(value.BoundingBox.X, value.BoundingBox.Y, value.BoundingBox.Width, value.BoundingBox.Height), value.LeftSideBearing, value.RightSideBearing);
        }

        private void GetGlyphMetrics(MyScript.IInk.Text.Text text, TextSpan[] spans, List<GlyphMetrics> glyphMetrics)
        {
            // Process glyph one by one to generate one box per glyph
            for (int s = 0; s < spans.Length; ++s)
            {
                var textSpan = spans[s];
                var fontKey = FontKeyFromStyle(textSpan.Style);

                for (int j = textSpan.BeginPosition; j < textSpan.EndPosition; ++j)
                {
                    var glyphLabel = text.GetGlyphLabelAt(j);
                    var glyphMetrics_ = GetGlyphMetrics(fontKey, glyphLabel);

                    glyphMetrics.Add(glyphMetrics_);
                }
            }

            if (glyphMetrics.Count == 0)
                return;

            // Draw text to get data for glyphs
            FormattedText formattedText;
            {
                var firstStyle = spans[0].Style;
                var firstFontKey = FontKeyFromStyle(firstStyle);
                var firstFontTypeFace = new Typeface(firstFontKey.FontFamily, firstFontKey.FontStyle, firstFontKey.FontWeight, firstFontKey.FontStretch);

                formattedText = new FormattedText
                                    (
                                        text.Label, System.Globalization.CultureInfo.CurrentCulture,
                                        FlowDirection.LeftToRight, firstFontTypeFace, firstFontKey.FontSize,
                                        Brushes.Black, _pixelsPerDip
                                    );

                formattedText.TextAlignment = TextAlignment.Left;

                for (int s = 0; s < spans.Length; ++s)
                {
                    var textSpan = spans[s];
                    var charIndex = textSpan.BeginPosition;
                    var charCount = textSpan.EndPosition - textSpan.BeginPosition;
                    var fontKey = FontKeyFromStyle(textSpan.Style);
                    var fontTypeFace = new Typeface(fontKey.FontFamily, fontKey.FontStyle, fontKey.FontWeight, fontKey.FontStretch);

                    formattedText.SetFontFamily(fontKey.FontFamily, charIndex, charCount);
                    formattedText.SetFontSize(fontKey.FontSize, charIndex, charCount);
                    formattedText.SetFontWeight(fontKey.FontWeight, charIndex, charCount);
                    formattedText.SetFontStretch(fontKey.FontStretch, charIndex, charCount);
                    formattedText.SetFontStyle(fontKey.FontStyle, charIndex, charCount);
                    formattedText.SetFontTypeface(fontTypeFace, charIndex, charCount);
                }
            }

            var drawing = new DrawingGroup();
            {
                var ctx = drawing.Open();
                ctx.DrawText(formattedText, new System.Windows.Point(0.0, 0.0));
                ctx.Close();
            }

            // Apply baseline and offsets of glyphs to bounding boxes
            float baseline = (float)formattedText.Baseline;
            WalkDrawingForText(drawing, glyphMetrics, baseline);
        }

        private static void WalkDrawingForText(Drawing drawing, List<GlyphMetrics> glyphMetrics, ref int idx, ref float x, float baseline, float dpiX, float dpiY)
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
                        WalkDrawingForText(child, glyphMetrics, ref idx, ref x, baseline, dpiX, dpiY);
                }
            }
        }

        private void WalkDrawingForText(Drawing drawing, List<GlyphMetrics> glyphMetrics, float baseline)
        {
            int glyphIdx = 0;
            float glyphX = 0.0f;
            WalkDrawingForText(drawing, glyphMetrics, ref glyphIdx, ref glyphX, baseline, _dpiX, _dpiY);
        }

        public Rectangle[] GetCharacterBoundingBoxes(MyScript.IInk.Text.Text text, TextSpan[] spans)
        {
            var glyphMetrics = new List<GlyphMetrics>();
            var rectangles = new List<Rectangle>();

            GetGlyphMetrics(text, spans, glyphMetrics);

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
            GetGlyphMetrics(text, spans, glyphMetrics);
            return glyphMetrics.ToArray();
        }
    }
}
