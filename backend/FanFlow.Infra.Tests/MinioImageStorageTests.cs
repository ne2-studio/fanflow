using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;
using FanFlow.Infra;

namespace FanFlow.Infra.Tests;

public class MinioImageStorageTests
{
    private const string BucketName = "releases";
    private const string PublicHostname = "https://cdn.example.com";

    [Fact]
    public async Task SaveAsync_ShouldPutObject_AndReturnPublicUrl()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var storage = new MinioImageStorage(s3Client, BucketName, PublicHostname);
        var content = new byte[] { 1, 2, 3, 4 };

        var url = await storage.SaveAsync("release-1/cover.webp", content, "image/webp");

        Assert.Equal($"{PublicHostname}/release-1/cover.webp", url);
        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == BucketName &&
                r.Key == "release-1/cover.webp" &&
                r.ContentType == "image/webp"),
            Arg.Any<CancellationToken>());
    }
}
