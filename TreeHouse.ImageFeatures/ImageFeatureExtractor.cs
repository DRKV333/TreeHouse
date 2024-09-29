using System.Reflection;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TreeHouse.ImageFeatures;

public class ImageFeatureExtractor : IDisposable
{
    public const int FeaturesDim = 2048;

    private const int InputDim = 224;
    private const int ChannelStride = InputDim * InputDim;
    private const int BatchStride = 3 * ChannelStride;

    private static readonly float[] ImageNetMean = [123.675f, 116.28f, 103.53f];
    
    private static readonly float[] ImageNetStdDev = [58.395f, 57.12f, 57.375f];

    private readonly int batchSize;

    private readonly SessionOptions options;

    private readonly InferenceSession session;

    private readonly RunOptions runOptions;

    private readonly OrtValue inputTensor;

    private readonly OrtValue outputTensor;

    private bool disposedValue = false;

    public ImageFeatureExtractor(int batchSize, bool logging = false)
    {
        this.batchSize = batchSize;

        string modelPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "resnet_features.onnx");

        options = new SessionOptions();

        if (logging)
            options.LogSeverityLevel = 0;

        options.AppendExecutionProvider_CPU();
        options.AddFreeDimensionOverrideByName("N", batchSize);

        session = new InferenceSession(modelPath, options);

        runOptions = new RunOptions();

        inputTensor = OrtValue.CreateAllocatedTensorValue(
            OrtAllocator.DefaultInstance,
            TensorElementType.Float,
            [batchSize, 3, InputDim, InputDim]
        );

        outputTensor = OrtValue.CreateAllocatedTensorValue(
            OrtAllocator.DefaultInstance,
            TensorElementType.Float,
            [batchSize, FeaturesDim]
        );
    }

    public async Task SetInputImage(Stream stream, int batchIdx)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);

        if (batchIdx < 0 || batchIdx >= batchSize)
            throw new ArgumentOutOfRangeException(nameof(batchIdx));

        using Image<Rgb24> image = await Image.LoadAsync<Rgb24>(stream);
        PreprocessImage(image);
        ImageToInputTensor(image, batchIdx);
    }

    public async Task<IReadOnlyList<float[]>> GetFeatures()
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);

        try
        {
            await session.RunAsync(
                runOptions,
                inputNames: [ "data" ],
                inputValues: [ inputTensor ],
                outputNames: [ "features" ],
                outputValues: [ outputTensor ]
            );
        }
        finally
        {
            // TODO: Remove this once microsoft/onnxruntime#22237 is resolved
            await Task.Yield();
        }

        return OutputTensorToArrays();
    }

    private IReadOnlyList<float[]> OutputTensorToArrays()
    {
        ReadOnlySpan<float> outputData = outputTensor.GetTensorDataAsSpan<float>();

        List<float[]> features = new(batchSize);

        for (int i = 0; i < batchSize; i++)
        {
            float[] featureArray = new float[FeaturesDim];
            outputData.Slice(i * FeaturesDim, FeaturesDim).CopyTo(featureArray);
            features.Add(featureArray);
        }

        return features;
    }

    private static void PreprocessImage(Image<Rgb24> image)
    {
        int targetWidth;
        int targetHeight;

        int cropStartX;
        int cropStartY;

        if (image.Width > image.Height)
        {
            targetWidth = (int)MathF.Round(image.Width * InputDim / (float)image.Height);
            targetHeight = InputDim;

            cropStartX = (targetWidth - InputDim) / 2;
            cropStartY = 0;
        }
        else
        {
            targetWidth = InputDim;
            targetHeight = (int)MathF.Round(image.Height * InputDim / (float)image.Width);

            cropStartX = 0;
            cropStartY = (targetHeight - InputDim) / 2;
        }

        image.Mutate(x => x
            .Resize(targetWidth, targetHeight, KnownResamplers.Triangle, true)
            .Crop(new Rectangle(cropStartX, cropStartY, InputDim, InputDim))
        );
    }

    private void ImageToInputTensor(Image<Rgb24> image, int batchIdx)
    {
        image.ProcessPixelRows(accessor =>
        {
            Span<float> inputData = inputTensor.GetTensorMutableDataAsSpan<float>()[(batchIdx * BatchStride)..];
            
            for (int i = 0; i < accessor.Height; i++)
            {
                Span<Rgb24> imageRow = accessor.GetRowSpan(i);

                int rowOffset = i * InputDim;
                Span<float> tensorRowR = inputData[(0 * ChannelStride + rowOffset)..];
                Span<float> tensorRowG = inputData[(1 * ChannelStride + rowOffset)..];
                Span<float> tensorRowB = inputData[(2 * ChannelStride + rowOffset)..];

                for (int j = 0; j < accessor.Width; j++)
                {
                    Rgb24 pix = imageRow[j];

                    tensorRowR[j] = pix.R - ImageNetMean[0] / ImageNetStdDev[0];
                    tensorRowG[j] = pix.G - ImageNetMean[1] / ImageNetStdDev[1];
                    tensorRowB[j] = pix.B - ImageNetMean[2] / ImageNetStdDev[2];
                }
            }
        });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                outputTensor.Dispose();
                inputTensor.Dispose();
                runOptions.Dispose();
                session.Dispose();
                options.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
