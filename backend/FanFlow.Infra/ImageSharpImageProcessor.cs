using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using FanFlow.Ports.Output;

namespace FanFlow.Infra;

public class ImageSharpImageProcessor : IImageProcessor
{
    public byte[] ToSquareWebp(byte[] source, int size)
    {
        using var image = Image.Load(source);

        var cropSize = Math.Min(image.Width, image.Height);
        var cropRectangle = new Rectangle(
            (image.Width - cropSize) / 2,
            (image.Height - cropSize) / 2,
            cropSize,
            cropSize);

        image.Mutate(x => x.Crop(cropRectangle).Resize(size, size));

        using var output = new MemoryStream();
        image.Save(output, new WebpEncoder());
        return output.ToArray();
    }
}
