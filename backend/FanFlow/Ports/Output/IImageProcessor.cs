namespace FanFlow.Ports.Output;

/// <summary>
/// Center-crops the source image to a square (no stretching) and resizes it to size x size,
/// encoded as WebP.
/// </summary>
public interface IImageProcessor
{
    byte[] ToSquareWebp(byte[] source, int size);
}
