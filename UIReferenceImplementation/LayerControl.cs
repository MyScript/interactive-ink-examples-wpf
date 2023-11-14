// Copyright @ MyScript. All rights reserved.

using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class LayerControl : Label
    {
        public Renderer Renderer { get; set; }
        public ImageLoader ImageLoader { get; set; }

        /// <summary>Redraw the Layer Control </summary>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (Renderer != null)
            {
                int x = 0;
                int y = 0;
                int width = (int)Math.Round(ActualWidth);
                int height = (int)Math.Round(ActualHeight);
                float pixelsPerDip = (float)DisplayResolution.GetPixelsPerDip(this);
                Canvas canvas = new Canvas(drawingContext, pixelsPerDip, ImageLoader);

                Renderer.DrawModel(x, y, width, height, canvas);
                Renderer.DrawCaptureStrokes(x, y, width, height, canvas);
            }
        }

        /// <summary> Force to redraw the Layer Control </summary>
        public void Update()
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.InvalidateVisual();
            }));
        }
    }
}
