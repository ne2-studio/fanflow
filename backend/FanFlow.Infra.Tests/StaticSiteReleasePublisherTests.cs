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
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == BucketName &&
                r.Key == "run-to-me.html" &&
                r.ContentType == "text/html" &&
                r.ContentBody.Contains("Run To Me") &&
                r.ContentBody.Contains("The Artist") &&
                r.ContentBody.Contains("https://connect.facebook.net/en_US/fbevents.js") &&
                r.ContentBody.Contains($"fbq('init', \"{PixelId}\")") &&
                r.ContentBody.Contains("var contentName = \"the-artist-run-to-me\"") &&
                r.ContentBody.Contains("fbq('track', 'PageView', { content_name: contentName }, { eventID: pageViewEventId })") &&
                r.ContentBody.Contains("id=\"bg\"") &&
                r.ContentBody.Contains("url('https://img/cover.jpg')") &&
                !r.ContentBody.Contains("bg.jpg")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldRenderSpotifyDeepLinkAttributes_ForSpotifyLink()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.ContentBody.Contains("data-app-uri=\"spotify:track:123:play\"") &&
                r.ContentBody.Contains("data-android-intent=\"intent://open.spotify.com/track/123:play#Intent;scheme=https;package=com.spotify.music;end\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldOmitDeepLinkAttributes_WhenUrlShapeIsUnrecognized()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());
        var release = SampleRelease() with { Links = [new DestinationLink("Spotify", "https://open.spotify.com/")] };

        await publisher.PublishAsync(release);

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                !r.ContentBody.Contains("data-app-uri=\"") &&
                !r.ContentBody.Contains("data-android-intent=\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldPlaceHoneypotLink_AsFirstElementInBody_BeforeAnyOtherAnchor()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                IsFirstElementInBody(r.ContentBody, "href=\"/trap/") &&
                r.ContentBody.IndexOf("href=\"/trap/", StringComparison.Ordinal) <
                    r.ContentBody.IndexOf("class=\"cta\"", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldMakeCoverArt_ClickableToThePrimaryDestination()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.ContentBody.Contains("<a id=\"a\"") &&
                r.ContentBody.Contains("href=\"https://open.spotify.com/track/123\" class=\"cta\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldIncludeSpotifyLogo_ForSpotifyDestination()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r.ContentBody.Contains("<svg viewBox=\"0 0 496 512\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldInlinePlayOverlaySvg_OnCoverArt_WhenALinkExists()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.ContentBody.Contains("<svg class=\"play-overlay\"") &&
                !r.ContentBody.Contains("play-button-overlay")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldOmitPlayOverlay_WhenReleaseHasNoLinks()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());
        var release = SampleRelease() with { Links = [] };

        await publisher.PublishAsync(release);

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => !r.ContentBody.Contains("<svg class=\"play-overlay\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldReadFbpAndFbcCookies_AndAttachThemToThePageViewBeacon()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.ContentBody.Contains("fbCookie('_fbp')") &&
                r.ContentBody.Contains("fbCookie('_fbc')") &&
                r.ContentBody.Contains("fetch('/pv/' + slug + '?' + pvParams.join('&')")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldShareOneEventIdBetweenThePixelPageViewAndTheCapiPageViewBeacon()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.ContentBody.Contains("var pageViewEventId = newEventId('pv');") &&
                r.ContentBody.Contains("{ eventID: pageViewEventId }") &&
                r.ContentBody.Contains("pvParams.push('eid=' + encodeURIComponent(pageViewEventId));")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldGenerateASeparateEventId_ForTheDestinationClickBeacon()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.ContentBody.Contains("var clickEventId = newEventId('click');") &&
                r.ContentBody.Contains("clickParams.push('eid=' + encodeURIComponent(clickEventId));")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldDeriveFbcFromFbclid_WhenNoFbcCookieExists()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.ContentBody.Contains("new URLSearchParams(location.search).get('fbclid')") &&
                r.ContentBody.Contains("'fb.1.' + Date.now() + '.' + fbclid")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldReReadFbpAndFbc_AtDestinationClickTime()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

        await publisher.PublishAsync(SampleRelease());

        await s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.ContentBody.Contains("var clickParams = fbTrackingParams();") &&
                r.ContentBody.Contains("'/out/' + slug + '/' + destination + '?dwell=' + dwell +")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnpublishAsync_ShouldDeleteObject_ForSlugKey()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var publisher = new StaticSiteReleasePublisher(s3Client, BucketName, new SlugGenerator());

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

    /// <summary>
    /// True when the first tag opened after &lt;body&gt; is the one containing <paramref name="marker"/>
    /// (used to assert the honeypot link is the very first element, ahead of any real content).
    /// </summary>
    private static bool IsFirstElementInBody(string html, string marker)
    {
        var bodyOpenEnd = html.IndexOf('>', html.IndexOf("<body", StringComparison.Ordinal)) + 1;
        var firstTagStart = html.IndexOf('<', bodyOpenEnd);
        return html.IndexOf(marker, firstTagStart, StringComparison.Ordinal) - firstTagStart < 80;
    }
}
