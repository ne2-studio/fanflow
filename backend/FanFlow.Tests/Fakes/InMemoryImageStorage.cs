using FanFlow.Ports.Output;

namespace FanFlow.Tests.Fakes;

public class InMemoryImageStorage : IImageStorage
{
    public List<(string Key, byte[] Content, string ContentType)> SavedImages { get; } = new();

    public Task<string> SaveAsync(string key, byte[] content, string contentType)
    {
        SavedImages.Add((key, content, contentType));
        return Task.FromResult($"https://cdn.test/{key}");
    }
}
