// Copyright @ MyScript. All rights reserved.

using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;

namespace MyScript.IInk.UIReferenceImplementation
{
    // LruImgCache
    public class LruImgCache
    {
        private LinkedList<string> _lru = new LinkedList<string>();
        private Dictionary<string, ImageNode> _cache = new Dictionary<string, ImageNode>();
        private int _maxBytes;
        private int _curBytes;

        // ImageNode
        public struct ImageNode
        {
            public ImageNode(BitmapSource image, int cost)
            {
                Image = image;
                Cost = cost;
            }

            public BitmapSource Image { get; }
            public int Cost { get; }
        }

        public LruImgCache(int maxBytes)
        {
            _maxBytes = maxBytes;
            _curBytes = 0;
        }

        public bool containsBitmap(string url)
        {
            return _cache.ContainsKey(url);
        }

        public BitmapSource getBitmap(string url)
        {
            // Update LRU
            _lru.Remove(url);
            _lru.AddFirst(url);

            return _cache[url].Image;
        }

        public void putBitmap(string url, string mimeType)
        {
            BitmapSource image = loadBitmap(url, mimeType);
            int imageBytes = (int)((image.Format.BitsPerPixel * image.PixelWidth * image.PixelHeight) / 8.0);

            // Too big for cache
            if (imageBytes > _maxBytes)
            {
                // Use fallback (cache it to avoid reloading it each time for size check)
                image = createFallbackBitmap();
                imageBytes = 4;
            }

            // Remove LRUs if max size reached
            while (_curBytes + imageBytes > _maxBytes)
            {
                string lruKey = _lru.Last.Value;
                ImageNode lruNode = _cache[lruKey];
                _curBytes -= lruNode.Cost;
                _cache.Remove(lruKey);
                _lru.RemoveLast();
            }

            // Add to cache
            _cache.Add(url, new ImageNode(image, imageBytes));
            _curBytes += imageBytes;
            _lru.AddFirst(url);
        }

        private BitmapSource loadBitmap(string url, string mimeType)
        {
            if (mimeType.StartsWith("image/"))
            {
                try
                {
                    // Load
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

            // Fallback
            return createFallbackBitmap();
        }

        public static BitmapSource createFallbackBitmap()
        {
            // Fallback 1x1 bitmap
            var dpi = 96;
            var image = new RenderTargetBitmap(1, 1, dpi, dpi, PixelFormats.Default);
            image?.Clear();

            return image;
        }
    }

    // ImageLoader
    public class ImageLoader
    {
        private Editor _editor;
        private LruImgCache _cache;
        private const int CACHE_MAX_BYTES = 200 * 1000000;  // 200M (in Bytes)

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
            _cache = new LruImgCache(CACHE_MAX_BYTES);
        }

        public BitmapSource getImage(string url, string mimeType)
        {
            BitmapSource image = null;

            lock (_cache)
            {
                if (!_cache.containsBitmap(url))
                    _cache.putBitmap(url, mimeType);
                image = _cache.getBitmap(url);
            }

            if (image == null)
                image = LruImgCache.createFallbackBitmap();

            return image;
        }
    }
}
