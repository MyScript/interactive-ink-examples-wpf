// Copyright MyScript. All right reserved.

using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class ImageLoader
    {
        private Editor _editor;
        private string _cacheDirectory;
        private ConcurrentDictionary<string, BitmapSource> _cache;

        public Editor Editor
        {
            get
            {
                return _editor;
            }
        }

        public ImageLoader(Editor editor, string cacheDirectory)
        {
            _editor = editor;
            _cacheDirectory = System.IO.Path.Combine(cacheDirectory, "tmp/render-cache");
            _cache = new ConcurrentDictionary<string, BitmapSource>();
        }

        public BitmapSource getImage(string url, string mimeType)
        {
            if (!_cache.ContainsKey(url))
                _cache[url] = loadImage(url, mimeType);
            return _cache[url];
        }

        private BitmapSource loadImage(string url, string mimeType)
        {
            if (mimeType.StartsWith("image/"))
            {
                try
                {
                    var path = System.IO.Path.GetFullPath(url);
                    var uri = new Uri(path);

                    var image_ = new BitmapImage();

                    image_.BeginInit();
                    image_.CacheOption = BitmapCacheOption.OnLoad;
                    image_.UriSource = uri;
                    image_.EndInit();

                    return image_;
                }
                catch
                {
                    // Error: use fallback bitmap
                }
            }

            // Fallback 1x1 bitmap
            var dpiX = _editor.Renderer.DpiX;
            var dpiY = _editor.Renderer.DpiY;
            var image = new RenderTargetBitmap(1, 1, dpiX, dpiY, PixelFormats.Default);

            image?.Clear();

            return image;
        }
    }
}
