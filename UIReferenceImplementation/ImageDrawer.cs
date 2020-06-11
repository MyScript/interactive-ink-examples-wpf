// Copyright MyScript. All right reserved.

using System;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class ImageDrawer : IImageDrawer
    {
        private static Graphics.Color _defaultBackgroundColor = new Graphics.Color(0xffffffff);

        private RenderTargetBitmap _image;

        public ImageLoader ImageLoader { get; set; }
        public Graphics.Color BackgroundColor { get; set; }

        public ImageDrawer()
        {
            BackgroundColor = _defaultBackgroundColor;
        }

        public void PrepareImage(int width, int height)
        {
            if (_image != null)
                _image = null;

            // Use 96 dpi to match the DIP unit used by WPF DrawingContext
            // (no conversion from DIP to pixel on _image.Render())
            _image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Default);
        }

        public void SaveImage(string path)
        {
            if ((_image != null) && !string.IsNullOrWhiteSpace(path))
            {
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

        public void Invalidate(Renderer renderer, LayerType layers)
        {
            if (_image != null && renderer != null)
                Invalidate(renderer, 0, 0, _image.PixelWidth, _image.PixelHeight, layers);
        }

        public void Invalidate(Renderer renderer, int x, int y, int width, int height, LayerType layers)
        {
            if (_image != null && renderer != null)
            {
                DrawingVisual drawingVisual = new DrawingVisual();

                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    var canvas = new Canvas(drawingContext, this, ImageLoader);
                    var color = (BackgroundColor != null) ? BackgroundColor : _defaultBackgroundColor;

                    canvas.Clear(0, 0, _image.PixelWidth, _image.PixelHeight, color);

                    if (layers.HasFlag(LayerType.MODEL))
                        renderer.DrawModel(x, y, width, height, canvas);

                    if (layers.HasFlag(LayerType.CAPTURE))
                        renderer.DrawCaptureStrokes(x, y, width, height, canvas);
                }

                _image.Render(drawingVisual);
            }
        }
    }
}
