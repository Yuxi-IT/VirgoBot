using SkiaSharp;

namespace VirgoBot.Utilities;

public static class ImageCompressor
{
    private const int MaxWidth = 1024;
    private const int MaxHeight = 1024;
    private const int JpegQuality = 75;
    private const int MaxFileSizeBytes = 1_000_000; // 1MB

    /// <summary>
    /// 压缩图片以减小文件大小，适合发送到 LLM API
    /// </summary>
    public static byte[] Compress(byte[] imageBytes)
    {
        try
        {
            using var inputStream = new MemoryStream(imageBytes);
            using var bitmap = SKBitmap.Decode(inputStream);

            if (bitmap == null)
            {
                ColorLog.Warning("IMAGE", "无法解码图片，返回原始数据");
                return imageBytes;
            }

            var (newWidth, newHeight) = CalculateNewSize(bitmap.Width, bitmap.Height);

            // 如果尺寸没变且文件已经很小，直接返回
            if (newWidth == bitmap.Width && newHeight == bitmap.Height && imageBytes.Length <= MaxFileSizeBytes)
            {
                ColorLog.Info("IMAGE", $"图片无需压缩 ({bitmap.Width}x{bitmap.Height}, {imageBytes.Length / 1024}KB)");
                return imageBytes;
            }

            using var resized = bitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.Medium);
            if (resized == null)
            {
                ColorLog.Warning("IMAGE", "图片缩放失败，返回原始数据");
                return imageBytes;
            }

            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

            var compressed = data.ToArray();
            var originalSizeKB = imageBytes.Length / 1024;
            var compressedSizeKB = compressed.Length / 1024;
            var ratio = (1 - (double)compressed.Length / imageBytes.Length) * 100;

            ColorLog.Success("IMAGE",
                $"图片已压缩: {bitmap.Width}x{bitmap.Height} → {newWidth}x{newHeight}, " +
                $"{originalSizeKB}KB → {compressedSizeKB}KB (减少 {ratio:F1}%)");

            return compressed;
        }
        catch (Exception ex)
        {
            ColorLog.Error("IMAGE", $"压缩失败: {ex.Message}，返回原始数据");
            return imageBytes;
        }
    }

    private static (int width, int height) CalculateNewSize(int originalWidth, int originalHeight)
    {
        if (originalWidth <= MaxWidth && originalHeight <= MaxHeight)
            return (originalWidth, originalHeight);

        var widthRatio = (double)MaxWidth / originalWidth;
        var heightRatio = (double)MaxHeight / originalHeight;
        var ratio = Math.Min(widthRatio, heightRatio);

        return ((int)(originalWidth * ratio), (int)(originalHeight * ratio));
    }
}
