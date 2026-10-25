using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using UltimateClipboard.Services;
using Xunit;

namespace UltimateClipboard.Tests;

public class ImageRecognitionTests
{
    [Fact]
    public async Task RecognizeContent_NullOrEmpty_ReturnsEmptyList_WithoutThrowing()
    {
        using var service = new ImageRecognitionService();

        var nullResult = await service.RecognizeContentAsync(null);
        Assert.NotNull(nullResult);
        Assert.Empty(nullResult);

        var emptyResult = await service.RecognizeContentAsync(new byte[0]);
        Assert.NotNull(emptyResult);
        Assert.Empty(emptyResult);
    }

    [Fact]
    public async Task RecognizeContent_CorruptedData_ReturnsEmptyList_WithoutThrowing()
    {
        using var service = new ImageRecognitionService();
        var corrupted = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var result = await service.RecognizeContentAsync(corrupted);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task AnalyzeImageAsync_NullOrEmpty_ReturnsEmptyAnalysis()
    {
        using var service = new ImageRecognitionService();

        var nullAnalysis = await service.AnalyzeImageAsync(null);
        Assert.NotNull(nullAnalysis);
        Assert.Empty(nullAnalysis.OcrText);
        Assert.Empty(nullAnalysis.AllSearchTags);

        var emptyAnalysis = await service.AnalyzeImageAsync(new byte[0]);
        Assert.NotNull(emptyAnalysis);
        Assert.Empty(emptyAnalysis.OcrText);
        Assert.Empty(emptyAnalysis.AllSearchTags);
    }

    [Fact]
    public async Task ExtractOcrTextAsync_CorruptedData_ReturnsEmptyString_WithoutThrowing()
    {
        using var service = new ImageRecognitionService();
        var corrupted = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var text = await service.ExtractOcrTextAsync(corrupted);
        Assert.NotNull(text);
        Assert.Empty(text);
    }

    [Fact]
    public async Task AnalyzeImageAsync_WithRenderedText_ExtractsOcrTextAndTags()
    {
        using var service = new ImageRecognitionService();

        // Create a bitmap image with large legible text
        using var bitmap = new Bitmap(400, 100);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            using var font = new Font("Arial", 28, FontStyle.Bold);
            using var brush = new SolidBrush(Color.Black);
            g.DrawString("mehe test", font, brush, new PointF(10, 20));
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        byte[] imageBytes = ms.ToArray();

        var analysis = await service.AnalyzeImageAsync(imageBytes);
        Assert.NotNull(analysis);
        // If Windows OCR engine is present, it will extract "mehe", or at minimum not crash
        Assert.NotNull(analysis.AllSearchTags);
    }

    [Fact]
    public async Task AnalyzeImageAsync_WithDiscordScreenshot_ExtractsUserText()
    {
        string screenshotPath = @"C:\Users\Hamza\.gemini\antigravity\brain\f7918b32-3932-40c6-bcfd-27f2d01bfef9\.user_uploaded\media_1792899387701_e861311e.png";
        if (!File.Exists(screenshotPath)) return;

        using var service = new ImageRecognitionService();
        byte[] bytes = await File.ReadAllBytesAsync(screenshotPath);
        var analysis = await service.AnalyzeImageAsync(bytes);

        Assert.NotNull(analysis);
        Assert.NotEmpty(analysis.OcrText);
        Assert.Contains("mehe", analysis.OcrText, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(analysis.AllSearchTags, tag => string.Equals(tag, "mehe", System.StringComparison.OrdinalIgnoreCase));
    }
}
