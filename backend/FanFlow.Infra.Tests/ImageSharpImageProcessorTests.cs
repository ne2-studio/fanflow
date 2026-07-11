using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using FanFlow.Infra;

namespace FanFlow.Infra.Tests;

public class ImageSharpImageProcessorTests
{
    private readonly ImageSharpImageProcessor processor = new();

    [Fact]
    public void ToSquareWebp_ShouldProduceExactlyTheRequestedSquareSize()
    {
        var source = EncodeAsPng(CreateSolidImage(800, 400, Color.Green));

        var result = processor.ToSquareWebp(source, 420);

        using var output = Image.Load(result);
        Assert.Equal(420, output.Width);
        Assert.Equal(420, output.Height);
        Assert.Equal("image/webp", output.Metadata.DecodedImageFormat?.DefaultMimeType);
    }

    [Fact]
    public void ToSquareWebp_ShouldCenterCrop_NotStretch_ALandscapeSource()
    {
        // A wide source where only the vertical center strip is red; if the processor stretched
        // instead of cropping, the whole output would be red instead of just the crop region.
        using var source = new Image<Rgba32>(300, 100);
        var blue = Color.Blue.ToPixel<Rgba32>();
        var red = Color.Red.ToPixel<Rgba32>();
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                source[x, y] = x is >= 100 and < 200 ? red : blue;
            }
        }

        var result = processor.ToSquareWebp(EncodeAsPng(source), 100);

        using var output = Image.Load<Rgba32>(result);
        Assert.Equal(100, output.Width);
        Assert.Equal(100, output.Height);
        // Center crop of a 300x100 source keeps the middle 100x100 (the red strip) entirely.
        Assert.Equal(red, output[50, 50]);
        Assert.Equal(red, output[0, 0]);
        Assert.Equal(red, output[99, 99]);
    }

    private static Image<Rgba32> CreateSolidImage(int width, int height, Color color)
    {
        var image = new Image<Rgba32>(width, height);
        var pixel = color.ToPixel<Rgba32>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image[x, y] = pixel;
            }
        }

        return image;
    }

    private static byte[] EncodeAsPng(Image image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
