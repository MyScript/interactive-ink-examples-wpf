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
        public delegate void NotificationDelegate(string url, BitmapSource image);

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

        public BitmapSource getImage(string url, string mimeType, NotificationDelegate onLoaded)
        {
            if (_cache.ContainsKey(url))
                return _cache[url];

            // Asynchronous loading
            // Performed on the UI thread, not on a task created locally, to avoid issues 
            // with bitmaps created on another thread than the one used for rendering
            var action = new Action(() =>   {
                                                BitmapSource image_ = loadImage(url, mimeType);
                                                if (image_ != null)
                                                {
                                                    _cache[url] = image_;
                                                    onLoaded?.Invoke(url, image_);
                                                }
                                            });

            Application.Current.Dispatcher.BeginInvoke(action);

            return null;
        }

        private BitmapSource loadImage(string url, string mimeType)
        {
            if (mimeType.StartsWith("image/"))
            {
                try
                {
                    var path = getFilePath(url);
                    var uri = new Uri(path);

                    // Do not use "new BitmapImage(uri)", else "File.Delete(path)" fails
                    var image_ = new BitmapImage();

                    image_.BeginInit();
                    image_.CacheOption = BitmapCacheOption.OnLoad;
                    image_.UriSource = uri;
                    image_.EndInit();

                    System.IO.File.Delete(path);

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

        private string getFilePath(string url)
        {
            var filePath = System.IO.Path.Combine(_cacheDirectory, url);
            var fullFilePath = System.IO.Path.GetFullPath(filePath);
            var folderPath = System.IO.Path.GetDirectoryName(fullFilePath);

            System.IO.Directory.CreateDirectory(folderPath);
            _editor.Part.Package.ExtractObject(url, fullFilePath);

            return fullFilePath;
        }
    }
}
