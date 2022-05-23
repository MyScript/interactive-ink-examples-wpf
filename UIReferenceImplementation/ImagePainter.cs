// Copyright @ MyScript. All rights reserved.

using MyScript.IInk.Graphics;
using System;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class ImagePainter : IImagePainter
    {
        private static Graphics.Color _defaultBackgroundColor = new Graphics.Color(0xffffffff);

        private RenderTargetBitmap _image;
        private DrawingVisual _drawingVisual;
        private DrawingContext _drawingContext;

        public ImageLoader ImageLoader { get; set; }
        public Graphics.Color BackgroundColor { get; set; }

        public ImagePainter()
        {
            BackgroundColor = _defaultBackgroundColor;
        }

        public void PrepareImage(int width, int height, float dpi)
        {
            if (_image != null)
                _image = null;

            // Use 96 dpi to match the DIP unit used by WPF DrawingContext
            // (no conversion from DIP to pixel on _image.Render())
            _image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Default);

            _drawingVisual = new DrawingVisual();
            _drawingContext = _drawingVisual.RenderOpen();
        }

        public void SaveImage(string path)
        {
            if ((_image != null) && !string.IsNullOrWhiteSpace(path))
            {
                _drawingContext.Close();
                _drawingContext = null;
                _image.Render(_drawingVisual);

                BitmapEncoder encoder = null;

                var pos = path.LastIndexOf('.');

                if (pos >= 0)
                {
                    var ext = path.Substring(pos)?.ToLower();

                    if (!string.IsNullOrWhiteSpace(ext))
                    {
                        string[] jpgExtensions = MimeTypeF.GetFileExtensions(MimeType.JPEG)?.Split(',');
                        string[] pngExtensions = MimeTypeF.GetFileExtensions(MimeType.PNG)?.Split(',');
                        string[] gifExtensions = MimeTypeF.GetFileExtensions(MimeType.GIF)?.Split(',');

                        if ( (pngExtensions != null) && pngExtensions.Contains(ext) )
                            encoder = new PngBitmapEncoder();
                        else if ( (jpgExtensions != null) && jpgExtensions.Contains(ext) )
                            encoder = new JpegBitmapEncoder();
                        else if ( (gifExtensions != null) && gifExtensions.Contains(ext) )
                            encoder = new GifBitmapEncoder();
                    }
                }

                if (encoder != null)
                {
                    encoder.Frames.Add(BitmapFrame.Create(_image));

                    using (var fileStream = new System.IO.FileStream(path, System.IO.FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }
                }
                else
                {
                    throw new Exception("No bitmap encoder available.");
                }

            }

            _image = null;
        }

        public ICanvas CreateCanvas()
        {
            float pixelsPerDip = (float)DisplayResolution.GetPixelsPerDip(_drawingVisual);
            var canvas = new Canvas(_drawingContext, pixelsPerDip, ImageLoader);
            var color = (BackgroundColor != null) ? BackgroundColor : _defaultBackgroundColor;
            canvas.Clear(0, 0, _image.PixelWidth, _image.PixelHeight, color);
            return canvas;
        }
    }
}
