using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ImageGo.AspNetCore.Helpers;
using System.Threading.Tasks;
using ImageGo.AspNetCore.Funcs;
using ImageGo.AspNetCore.Models;
using Newtonsoft.Json;
using System.Text;

namespace ImageGo.AspNetCore
{
    public class ImageGoMiddleware
    {


        private readonly RequestDelegate _req;
        private readonly ILogger<ImageGoMiddleware> _logger;
        private readonly IHostingEnvironment _env;
        private readonly IMemoryCache _memoryCache;
        private string rootPath;
        private WatermarkTextModel watermarkText;
        private WatermarkImageModel watermarkImage;
        int[] _statusCodes = new[] { 200 };
        private static readonly string[] suffixes = new string[] {
            ".png",
            ".jpg",
            ".jpeg"
        };

        public ImageGoMiddleware(RequestDelegate req, IHostingEnvironment env, ILogger<ImageGoMiddleware> logger, IMemoryCache memoryCache, string rootPath)
        {
            _req = req;
            _env = env;
            _logger = logger;
            _memoryCache = memoryCache;
            this.rootPath = rootPath;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path;
            rootPath = _env.WebRootPath ?? _env.ContentRootPath;
            // hand to next middleware if we are not dealing with an image
            if (context.Request.Query.Count == 0 || !IsImagePath(path))
            {
                await _req.Invoke(context);
                return;
            }

            // hand to next middleware if we are dealing with an image but it doesn't have any usable resize querystring params
            var resizeParams = GetResizeParams(path, context.Request.Query);
            if (!resizeParams.hasParams)
            {
                await _req.Invoke(context);
                return;
            }
            var ImageGoJsonPath = Path.Combine(rootPath, "ImageGoJson.json");

            // if json file doesn't exist we don't work with watermark
            if (File.Exists(ImageGoJsonPath))
            {
                var watermarks = new WatermarksModel();

                using (StreamReader r = new StreamReader(ImageGoJsonPath))
                {
                    string json = r.ReadToEnd();
                    watermarks = JsonConvert.DeserializeObject<WatermarksModel>(json);
                }

                if (resizeParams.wmtext != 0)
                {
                    if (watermarks.WatermarkTextList.Any())
                    {
                        watermarkText = watermarks.WatermarkTextList.FirstOrDefault(p => p.Key == resizeParams.wmtext);
                    }
                }
                if (resizeParams.wmimage != 0)
                {
                    if (watermarks.WatermarkImageList.Any())
                    {
                        watermarkImage = watermarks.WatermarkImageList.FirstOrDefault(p => p.Key == resizeParams.wmimage);
                    }
                }
            }

            // if we got this far, resize it
            _logger.LogInformation($"Resizing {path.Value} with params {resizeParams}");

            Console.WriteLine($"Resizing {path.Value} with params {resizeParams}");

            if (rootPath.EndsWith($"{Path.DirectorySeparatorChar}wwwroot") && path.Value.StartsWith("wwwroot"))
            {
                rootPath = rootPath.Replace("\\wwwroot", "");
            }
            // get the image location on disk
            var imagePath =
            Path.Combine(
                rootPath,
                path.Value.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));

            // check file lastwrite
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(imagePath);
            if (lastWriteTimeUtc.Year == 1601) // file doesn't exist, pass to next middleware
            {
                await _req.Invoke(context);
                return;
            }

            var imageData = GetImageData(imagePath, resizeParams, lastWriteTimeUtc);

            // write to stream
            context.Response.ContentType = resizeParams.format == "png" ? "image/png" : "image/jpeg";
            context.Response.ContentLength = imageData.Size;

            if(! AddETag(context,(int)imageData.Size))
            {
               await context.Response.Body.WriteAsync(imageData.ToArray(), 0, (int)imageData.Size);
            }
            // cleanup
            imageData.Dispose();

        }

        public bool AddETag(HttpContext context,int size)
        {
            bool is304 =false;
            if (context.Request.Method == "GET")
            {
                if (_statusCodes.Contains(context.Response.StatusCode))
                {
                    //I just serialize the result to JSON, could do something less costly
                    var content = size.ToString();

                    var etag = ETagGenerator.GetETag(context.Request.Path.ToString(), Encoding.UTF8.GetBytes(content));

                    if (context.Request.Headers.Keys.Contains("If-None-Match") && context.Request.Headers["If-None-Match"].ToString() == etag)
                    {
                        context.Response.StatusCode = 304;
                        is304 = true;
                    }
                    context.Response.Headers.Add("ETag", new[] { etag });
                }
            }

            return is304;
        }
        private SKData GetImageData(string imagePath, ResizeParams resizeParams, DateTime lastWriteTimeUtc)
        {
            // check cache and return if cached
            long cacheKey;
            unchecked
            {
                cacheKey = imagePath.GetHashCode() + lastWriteTimeUtc.ToBinary() + resizeParams.ToString().GetHashCode();
            }

            SKData imageData;
            byte[] imageBytes;
            bool isCached = _memoryCache.TryGetValue<byte[]>(cacheKey, out imageBytes);
            if (isCached)
            {
                _logger.LogInformation("Serving from cache");
                return SKData.CreateCopy(imageBytes);
            }

            SKEncodedOrigin origin; // this represents the EXIF orientation
            var bitmap = LoadBitmap(File.OpenRead(imagePath), out origin); // always load as 32bit (to overcome issues with indexed color)

            if (resizeParams.w == 0)
            {
                resizeParams.w = bitmap.Width;
            }
            if (resizeParams.h == 0)
            {
                resizeParams.h = bitmap.Height;
            }

            // if autorotate = true, and origin isn't correct for the rotation, rotate it
            if (resizeParams.autorotate && origin != SKEncodedOrigin.TopLeft)
                bitmap = RotateAndFlip.RotateAndFlipImage(bitmap, origin);

            // if either w or h is 0, set it based on ratio of original image
            if (resizeParams.h == 0)
                resizeParams.h = (int)Math.Round(bitmap.Height * (float)resizeParams.w / bitmap.Width);
            else if (resizeParams.w == 0)
                resizeParams.w = (int)Math.Round(bitmap.Width * (float)resizeParams.h / bitmap.Height);

            // if we need to crop, crop the original before resizing
            if (resizeParams.mode == "crop")
                bitmap = Crop.CropImage(bitmap, resizeParams);

            // store padded height and width
            var paddedHeight = resizeParams.h;
            var paddedWidth = resizeParams.w;

            // if we need to pad, or max, set the height or width according to ratio
            if (resizeParams.mode == "pad" || resizeParams.mode == "max")
            {
                var bitmapRatio = (float)bitmap.Width / bitmap.Height;
                var resizeRatio = (float)resizeParams.w / resizeParams.h;

                if (bitmapRatio > resizeRatio) // original is more "landscape"
                    resizeParams.h = (int)Math.Round(bitmap.Height * ((float)resizeParams.w / bitmap.Width));
                else
                    resizeParams.w = (int)Math.Round(bitmap.Width * ((float)resizeParams.h / bitmap.Height));
            }

            // resize
            var resizedImageInfo = new SKImageInfo(resizeParams.w, resizeParams.h, SKImageInfo.PlatformColorType, bitmap.AlphaType);
            var resizedBitmap = bitmap.Resize(resizedImageInfo, SKFilterQuality.High);

            // optionally pad
            if (resizeParams.mode == "pad")
                resizedBitmap = Padding.PaddingImage(resizedBitmap, paddedWidth, paddedHeight, resizeParams.format != "png");

            // watermarkText
            if (resizeParams.wmtext != 0)
            {
                if (watermarkText != null)
                    resizedBitmap = Watermark.WatermarkText(resizedBitmap, resizeParams, watermarkText);
            }
            // watermarkImage
            if (resizeParams.wmimage != 0)
            {
                if (watermarkImage != null)
                    resizedBitmap = Watermark.WatermarkImage(resizedBitmap, resizeParams, watermarkImage);
            }


            // encode
            var resizedImage = SKImage.FromBitmap(resizedBitmap);
            var encodeFormat = resizeParams.format == "png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
            imageData = resizedImage.Encode(encodeFormat, resizeParams.quality);

            // cache the result
            _memoryCache.Set<byte[]>(cacheKey, imageData.ToArray());

            // cleanup
            resizedImage.Dispose();
            bitmap.Dispose();
            resizedBitmap.Dispose();

            return imageData;
        }

        private SKBitmap LoadBitmap(Stream stream, out SKEncodedOrigin origin)
        {
            using (var s = new SKManagedStream(stream))
            {
                using (var codec = SKCodec.Create(s))
                {
                    origin = codec.EncodedOrigin;
                    var info = codec.Info;
                    var bitmap = new SKBitmap(info.Width, info.Height, SKImageInfo.PlatformColorType, info.IsOpaque ? SKAlphaType.Opaque : SKAlphaType.Premul);

                    IntPtr length;
                    var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels(out length));
                    if (result == SKCodecResult.Success || result == SKCodecResult.IncompleteInput)
                    {
                        return bitmap;
                    }
                    else
                    {
                        throw new ArgumentException("Unable to load bitmap from provided data");
                    }
                }
            }
        }

        private bool IsImagePath(PathString path)
        {
            if (path == null || !path.HasValue)
                return false;

            return suffixes.Any(x => x.EndsWith(x, StringComparison.OrdinalIgnoreCase));
        }

        private ResizeParams GetResizeParams(PathString path, IQueryCollection query)
        {
            ResizeParams resizeParams = new ResizeParams();

            // before we extract, do a quick check for resize params
            resizeParams.hasParams =
                resizeParams.GetType().GetTypeInfo()
                .GetFields().Where(f => f.Name != "hasParams")
                .Any(f => query.ContainsKey(f.Name));

            // if no params present, bug out
            if (!resizeParams.hasParams)
                return resizeParams;

            // extract resize params

            if (query.ContainsKey("format"))
                resizeParams.format = query["format"];
            else
                resizeParams.format = path.Value.Substring(path.Value.LastIndexOf('.') + 1);

            if (query.ContainsKey("autorotate"))
                bool.TryParse(query["autorotate"], out resizeParams.autorotate);

            int quality = 100;
            if (query.ContainsKey("quality"))
                int.TryParse(query["quality"], out quality);
            resizeParams.quality = quality;

            int w = 0;
            if (query.ContainsKey("w"))
                int.TryParse(query["w"], out w);
            resizeParams.w = w;

            int h = 0;
            if (query.ContainsKey("h"))
                int.TryParse(query["h"], out h);
            resizeParams.h = h;

            resizeParams.mode = "max";
            // only apply mode if it's a valid mode and both w and h are specified
            if (h != 0 && w != 0 && query.ContainsKey("mode") && ResizeParams.modes.Any(m => query["mode"] == m))
                resizeParams.mode = query["mode"];

            if (query.ContainsKey("wmtext"))
                resizeParams.wmtext = short.Parse(query["wmtext"]);
            else
                resizeParams.wmtext = 0;

            if (query.ContainsKey("wmimage"))
                resizeParams.wmimage = short.Parse(query["wmimage"]);
            else
                resizeParams.wmimage = 0;

            return resizeParams;
        }
    }
}
