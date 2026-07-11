namespace FanFlow.Ports.Output;

/// <summary>
/// Stores a processed image under a deterministic key in the public asset bucket. Saving to an
/// existing key overwrites it. Returns the public URL the asset can be reached at.
/// </summary>
public interface IImageStorage
{
    Task<string> SaveAsync(string key, byte[] content, string contentType);
}
