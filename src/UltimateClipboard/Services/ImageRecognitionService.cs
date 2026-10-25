using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace UltimateClipboard.Services;

public class ImageAnalysisResult
{
    public string OcrText { get; set; } = string.Empty;
    public List<string> VisualTags { get; set; } = new();
    public List<string> AllSearchTags { get; set; } = new();
    public string DisplaySummary { get; set; } = string.Empty;
}

/// <summary>
/// On-device intelligent image recognition and OCR service.
/// 1. Windows Native OCR: reads text inside screenshots (e.g. Discord messages, code, documents).
/// 2. Local AI Classifier (MobileNetV2): recognizes visual objects (e.g. cats, dogs, cars, laptops).
/// 100% on-device: runs completely local with zero cloud requests.
/// </summary>
public class ImageRecognitionService : IDisposable
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private InferenceSession? _session;
    private string[]? _labels;
    private bool _initialized;
    private bool _disposed;

    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        // Cats and felines
        { "tabby", new[] { "cat", "tabby cat", "kitten", "feline", "pet" } },
        { "tiger cat", new[] { "cat", "tiger cat", "kitten", "feline", "pet" } },
        { "persian cat", new[] { "cat", "persian cat", "kitten", "feline", "pet" } },
        { "siamese cat", new[] { "cat", "siamese cat", "kitten", "feline", "pet" } },
        { "egyptian cat", new[] { "cat", "egyptian cat", "kitten", "feline", "pet" } },
        { "lynx", new[] { "cat", "wildcat", "feline" } },
        { "cheetah", new[] { "cat", "big cat", "feline" } },
        { "lion", new[] { "cat", "lion", "big cat", "feline" } },
        { "tiger", new[] { "cat", "tiger", "big cat", "feline" } },

        // Dogs and canines
        { "golden retriever", new[] { "dog", "golden retriever", "puppy", "canine", "pet" } },
        { "labrador retriever", new[] { "dog", "labrador", "puppy", "canine", "pet" } },
        { "german shepherd", new[] { "dog", "german shepherd", "puppy", "canine", "pet" } },
        { "beagle", new[] { "dog", "beagle", "puppy", "canine", "pet" } },
        { "bulldog", new[] { "dog", "bulldog", "puppy", "canine", "pet" } },
        { "poodle", new[] { "dog", "poodle", "puppy", "canine", "pet" } },
        { "pug", new[] { "dog", "pug", "puppy", "canine", "pet" } },
        { "chihuahua", new[] { "dog", "chihuahua", "puppy", "canine", "pet" } },
        { "corgi", new[] { "dog", "corgi", "puppy", "canine", "pet" } },
        { "husky", new[] { "dog", "husky", "puppy", "canine", "pet" } },

        // Technology & Computers
        { "laptop", new[] { "laptop", "computer", "pc", "tech", "screen" } },
        { "notebook", new[] { "laptop", "computer", "pc", "notebook" } },
        { "desktop computer", new[] { "computer", "pc", "desktop", "monitor" } },
        { "monitor", new[] { "screen", "monitor", "display", "computer" } },
        { "screen", new[] { "screen", "display", "monitor" } },
        { "cellular telephone", new[] { "phone", "smartphone", "mobile", "cellphone" } },
        { "computer keyboard", new[] { "keyboard", "typing", "computer", "keys" } },
        { "mouse", new[] { "mouse", "computer mouse", "hardware" } },

        // Vehicles
        { "sports car", new[] { "car", "sports car", "vehicle", "automobile" } },
        { "convertible", new[] { "car", "convertible", "vehicle", "automobile" } },
        { "minivan", new[] { "van", "car", "vehicle", "automobile" } },
        { "cab", new[] { "taxi", "car", "vehicle" } },
        { "bicycle", new[] { "bike", "bicycle", "cycle" } },
        { "motorcycle", new[] { "motorcycle", "bike", "motorbike" } },
        { "airplane", new[] { "plane", "airplane", "aircraft", "flight" } }
    };

    /// <summary>
    /// Performs full image analysis: Windows native OCR for text extraction + Onnx AI for visual object detection.
    /// </summary>
    public async Task<ImageAnalysisResult> AnalyzeImageAsync(byte[]? imageData)
    {
        var result = new ImageAnalysisResult();
        if (imageData is null || imageData.Length == 0)
            return result;

        // 1. Run Windows Native OCR
        try
        {
            result.OcrText = await ExtractOcrTextAsync(imageData);
        }
        catch
        {
            result.OcrText = string.Empty;
        }

        // 2. Run Visual Object Recognition (AI Model)
        try
        {
            result.VisualTags = await RecognizeVisualObjectsAsync(imageData);
        }
        catch
        {
            result.VisualTags = new List<string>();
        }

        // 3. Aggregate all search tags
        var allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add visual tags
        foreach (var tag in result.VisualTags)
            allTags.Add(tag);

        // Add OCR words
        if (!string.IsNullOrWhiteSpace(result.OcrText))
        {
            var words = result.OcrText.Split(new[] { ' ', '\r', '\n', '\t', ',', '.', ':', ';', '!', '?' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var w in words)
            {
                if (w.Length >= 2)
                    allTags.Add(w);
            }
        }

        result.AllSearchTags = allTags.ToList();

        // 4. Build friendly display summary
        var summaryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.OcrText))
        {
            // Truncate OCR text for header preview
            string firstLine = result.OcrText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            firstLine = firstLine.Trim();
            if (firstLine.Length > 45)
                firstLine = firstLine[..45] + "...";

            if (!string.IsNullOrEmpty(firstLine))
                summaryParts.Add($"\"{firstLine}\"");
        }

        if (result.VisualTags.Count > 0)
        {
            summaryParts.Add(string.Join(", ", result.VisualTags.Take(2)));
        }

        result.DisplaySummary = string.Join(" • ", summaryParts);
        return result;
    }

    /// <summary>
    /// Uses Windows built-in OCR (OcrEngine) to extract text directly from image bytes.
    /// </summary>
    public async Task<string> ExtractOcrTextAsync(byte[] imageData)
    {
        return await Task.Run(async () =>
        {
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(imageData);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                    writer.DetachStream();
                }
                stream.Seek(0);

                var decoder = await BitmapDecoder.CreateAsync(stream);
                using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                // Use user profile languages or fall back to English / first available
                var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (ocrEngine is null)
                {
                    var enLang = new Language("en");
                    if (OcrEngine.IsLanguageSupported(enLang))
                    {
                        ocrEngine = OcrEngine.TryCreateFromLanguage(enLang);
                    }
                    else if (OcrEngine.AvailableRecognizerLanguages.Count > 0)
                    {
                        ocrEngine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
                    }
                }

                if (ocrEngine is null)
                    return string.Empty;

                var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
                return ocrResult.Text ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    /// <summary>
    /// Returns all recognized search tags (visual tags + OCR words).
    /// </summary>
    public async Task<List<string>> RecognizeContentAsync(byte[]? imageData)
    {
        if (imageData is null || imageData.Length == 0)
            return new List<string>();

        var analysis = await AnalyzeImageAsync(imageData);
        return analysis.AllSearchTags;
    }

    /// <summary>
    /// Analyzes an image byte array asynchronously and returns recognized object labels and keywords.
    /// </summary>
    public async Task<List<string>> RecognizeVisualObjectsAsync(byte[] imageData)
    {
        return await Task.Run(() =>
        {
            try
            {
                EnsureInitialized();
                if (_session is null || _labels is null || _labels.Length == 0)
                    return new List<string>();

                using var stream = new MemoryStream(imageData);
                using var original = new Bitmap(stream);

                // Preprocess: Resize to 224x224 and normalize into NCHW tensor
                var tensor = PreprocessBitmap(original);

                // Run inference
                string inputName = _session.InputMetadata.Keys.First();
                var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

                using var results = _session.Run(inputs);
                var outputTensor = results.First().AsTensor<float>();
                var probabilities = Softmax(outputTensor.ToArray());

                // Find top predictions
                var topPredictions = probabilities
                    .Select((score, index) => (score, index))
                    .OrderByDescending(x => x.score)
                    .Take(5)
                    .Where(x => x.score >= 0.05f) // At least 5% confidence
                    .ToList();

                var recognizedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var (score, index) in topPredictions)
                {
                    if (index >= 0 && index < _labels.Length)
                    {
                        string label = _labels[index].Trim().ToLowerInvariant();
                        recognizedTags.Add(label);

                        // Check for synonyms and parent categories
                        foreach (var kvp in Synonyms)
                        {
                            if (label.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var syn in kvp.Value)
                                    recognizedTags.Add(syn);
                            }
                        }

                        // Generic pet/animal heuristics
                        if (label.Contains("cat") || label.Contains("kitten") || label.Contains("feline"))
                        {
                            recognizedTags.Add("cat");
                            recognizedTags.Add("pet");
                            recognizedTags.Add("animal");
                        }
                        if (label.Contains("dog") || label.Contains("puppy") || label.Contains("hound") || label.Contains("terrier"))
                        {
                            recognizedTags.Add("dog");
                            recognizedTags.Add("pet");
                            recognizedTags.Add("animal");
                        }
                    }
                }

                return recognizedTags.ToList();
            }
            catch
            {
                return new List<string>();
            }
        });
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        InitLock.Wait();
        try
        {
            if (_initialized) return;

            string baseDir = AppContext.BaseDirectory;
            string modelPath = FindFile(baseDir, "mobilenetv2-7.onnx");
            string labelsPath = FindFile(baseDir, "imagenet_classes.txt");

            if (File.Exists(modelPath))
            {
                var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    IntraOpNumThreads = 2
                };
                _session = new InferenceSession(modelPath, options);
            }

            if (File.Exists(labelsPath))
            {
                _labels = File.ReadAllLines(labelsPath);
            }

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static string FindFile(string baseDir, string fileName)
    {
        string direct = Path.Combine(baseDir, fileName);
        if (File.Exists(direct)) return direct;

        string inAssets = Path.Combine(baseDir, "Assets", "Models", fileName);
        if (File.Exists(inAssets)) return inAssets;

        string inWorkspace = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Documents", "AI-Projects", "UltimateClipboard", "src", "UltimateClipboard", "Assets", "Models", fileName);
        if (File.Exists(inWorkspace)) return inWorkspace;

        return direct;
    }

    private static DenseTensor<float> PreprocessBitmap(Bitmap original)
    {
        const int targetSize = 224;
        using var resized = new Bitmap(targetSize, targetSize, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(original, 0, 0, targetSize, targetSize);
        }

        var tensor = new DenseTensor<float>(new[] { 1, 3, targetSize, targetSize });

        const float meanR = 0.485f, meanG = 0.456f, meanB = 0.406f;
        const float stdR = 0.229f, stdG = 0.224f, stdB = 0.225f;

        var data = resized.LockBits(
            new Rectangle(0, 0, targetSize, targetSize),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                int stride = data.Stride;

                for (int y = 0; y < targetSize; y++)
                {
                    byte* row = ptr + (y * stride);
                    for (int x = 0; x < targetSize; x++)
                    {
                        float b = row[x * 3] / 255.0f;
                        float g = row[x * 3 + 1] / 255.0f;
                        float r = row[x * 3 + 2] / 255.0f;

                        tensor[0, 0, y, x] = (r - meanR) / stdR;
                        tensor[0, 1, y, x] = (g - meanG) / stdG;
                        tensor[0, 2, y, x] = (b - meanB) / stdB;
                    }
                }
            }
        }
        finally
        {
            resized.UnlockBits(data);
        }

        return tensor;
    }

    private static float[] Softmax(float[] values)
    {
        float max = values.Max();
        float[] exp = values.Select(v => MathF.Exp(v - max)).ToArray();
        float sum = exp.Sum();
        return exp.Select(v => v / sum).ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
