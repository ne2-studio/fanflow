using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;
using FanFlow.Infra;
using FanFlow.Ports.Output;

namespace FanFlow.Infra.Tests;

public class StaticSiteReleasePublisherTests
{
    private const string BucketName = "releases";

    private const string PixelId = "123456789012345";

    private static Release SampleRelease() => new(
        Guid.NewGuid(),
        "user-1",
        "run-to-me",
        "The Artist",
        "Run To Me",
        "New single out now",
        "A great song.",
        "https://img/cover.jpg",
        "Listen now",
        PixelId,
        [new DestinationLink("Spotify", "https://open.spotify.com/track/123")],
        ReleaseStatus.Published,
        DateTime.UtcNow,
        DateTime.UtcNow);

    [Fact]
    public async Task PublishAsync_ShouldPutRenderedHtml_UnderSlugKey()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName);

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == BucketName &&
                r.Key == "run-to-me.html" &&
                r.ContentType == "text/html" &&
                r.ContentBody.Contains("Run To Me") &&
                r.ContentBody.Contains("The Artist") &&
                r.ContentBody.Contains($"https://www.facebook.com/tr?id={PixelId}") &&
                r.ContentBody.Contains("class=\"backdrop\"") &&
                r.ContentBody.Contains("url('https://img/cover.jpg')") &&
                !r.ContentBody.Contains("bg.jpg")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnpublishAsync_ShouldDeleteObject_ForSlugKey()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName);

        await publisher.UnpublishAsync("run-to-me");

        await s3Client.Received(1).DeleteObjectAsync(BucketName, "run-to-me.html", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureBucketAsync_ShouldCreateBucketAndSetPublicReadPolicy()
    {
        var s3Client = Substitute.For<IAmazonS3>();

        await StaticSiteReleasePublisher.EnsureBucketAsync(s3Client, BucketName);

        await s3Client.Received(1).PutBucketAsync(BucketName, Arg.Any<CancellationToken>());
        await s3Client.Received(1).PutBucketPolicyAsync(
            BucketName,
            Arg.Is<string>(policy => policy.Contains("s3:GetObject") && policy.Contains(BucketName)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureBucketAsync_ShouldSwallowBucketAlreadyOwnedByYou_AndStillSetPolicy()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client.PutBucketAsync(BucketName, Arg.Any<CancellationToken>())
            .Returns<Task<PutBucketResponse>>(_ => throw new AmazonS3Exception("already owned")
            {
                ErrorCode = "BucketAlreadyOwnedByYou"
            });

        await StaticSiteReleasePublisher.EnsureBucketAsync(s3Client, BucketName);

        await s3Client.Received(1).PutBucketPolicyAsync(BucketName, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
