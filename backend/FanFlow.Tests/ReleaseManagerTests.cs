using Microsoft.Extensions.Logging.Abstractions;
using FanFlow.Application;
using FanFlow.Ports.Input;
using FanFlow.Tests.Fakes;

namespace FanFlow.Tests;

public class ReleaseManagerTests
{
    private static readonly Guid GeneratedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string CurrentUserId = "user-1";

    private readonly InMemoryReleaseRepository repository;
    private readonly SpyReleasePublisher publisher;
    private readonly StaticImageProcessor imageProcessor;
    private readonly InMemoryImageStorage imageStorage;
    private readonly ReleaseManager releaseManager;

    public ReleaseManagerTests()
    {
        repository = new InMemoryReleaseRepository();
        publisher = new SpyReleasePublisher();
        imageProcessor = new StaticImageProcessor();
        imageStorage = new InMemoryImageStorage();

        releaseManager = new ReleaseManager(
            NullLogger<ReleaseManager>.Instance,
            repository,
            publisher,
            new StaticSlugGenerator(),
            new StaticIdGenerator(GeneratedId),
            new StaticClock(),
            new StaticCurrentUserProvider(CurrentUserId),
            new StaticPublicSiteSettings(),
            imageProcessor,
            imageStorage);
    }

    private static UploadedFile ValidCoverImage() => new("image/jpeg", [1, 2, 3, 4]);

    private static CreateReleaseRequest ValidRequest(string title = "Run To Me") => new(
        "The Artist", title, "New single out now", "A great song.", ValidCoverImage(), "Listen now",
        "123456789012345",
        [new DestinationLinkDto("Spotify", "https://open.spotify.com/track/123")]);

    private ReleaseManager NewManagerFor(string userId, Guid id) => new(
        NullLogger<ReleaseManager>.Instance, repository, publisher, new StaticSlugGenerator(),
        new StaticIdGenerator(id), new StaticClock(), new StaticCurrentUserProvider(userId),
        new StaticPublicSiteSettings(), imageProcessor, imageStorage);

    [Fact]
    public async Task CreateAsync_ShouldSaveAndPublish_WhenDestinationIsSpotify()
    {
        var result = await releaseManager.CreateAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneratedId.ToString(), result.Value.Id);
        Assert.Equal("run-to-me", result.Value.Slug);
        Assert.Single(publisher.PublishedReleases);
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenDestinationIsNotSpotify()
    {
        var request = ValidRequest() with { Links = [new DestinationLinkDto("AppleMusic", "https://music.apple.com/x")] };

        var result = await releaseManager.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("invalid_destination", result.Error);
        Assert.Empty(publisher.PublishedReleases);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-pixel-id")]
    public async Task CreateAsync_ShouldFail_WhenFacebookPixelIdIsMissingOrNotNumeric(string pixelId)
    {
        var request = ValidRequest() with { FacebookPixelId = pixelId };

        var result = await releaseManager.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("invalid_facebook_pixel_id", result.Error);
        Assert.Empty(publisher.PublishedReleases);
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenCoverImageIsMissing()
    {
        var request = ValidRequest() with { CoverImage = null };

        var result = await releaseManager.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("cover_image_required", result.Error);
        Assert.Empty(publisher.PublishedReleases);
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenCoverImageContentTypeIsNotAllowed()
    {
        var request = ValidRequest() with { CoverImage = new UploadedFile("application/pdf", [1, 2, 3]) };

        var result = await releaseManager.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("invalid_cover_image_type", result.Error);
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenCoverImageIsTooLarge()
    {
        var request = ValidRequest() with { CoverImage = new UploadedFile("image/jpeg", new byte[10 * 1024 * 1024 + 1]) };

        var result = await releaseManager.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("cover_image_too_large", result.Error);
    }

    [Fact]
    public async Task CreateAsync_ShouldProcessAndStoreCoverImage_UnderReleaseIdKey()
    {
        var result = await releaseManager.CreateAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Single(imageStorage.SavedImages);
        Assert.Equal($"{GeneratedId}/cover.webp", imageStorage.SavedImages[0].Key);
        Assert.Equal("image/webp", imageStorage.SavedImages[0].ContentType);
        Assert.Equal($"https://cdn.test/{GeneratedId}/cover.webp", result.Value.CoverImageUrl);
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenSlugIsAlreadyTaken()
    {
        await releaseManager.CreateAsync(ValidRequest());

        var releaseManager2 = NewManagerFor("user-2", Guid.NewGuid());

        var result = await releaseManager2.CreateAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("slug_taken", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ShouldChangeFieldsAndRepublish()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, null, "Updated headline", null, null, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated headline", result.Value.Headline);
        Assert.Equal("Run To Me", result.Value.Title);
        Assert.Equal(2, publisher.PublishedReleases.Count);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenReleaseNotFound()
    {
        var result = await releaseManager.UpdateAsync(Guid.NewGuid().ToString(), new UpdateReleaseRequest(
            null, "New title", null, null, null, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("release_not_found", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenDestinationIsNotSpotify()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, null, null, null, null, null, null, [new DestinationLinkDto("YouTube", "https://youtube.com/x")]));

        Assert.True(result.IsFailure);
        Assert.Equal("invalid_destination", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ShouldChangeFacebookPixelId_WhenValidNumericValueProvided()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, null, null, null, null, null, "999888777666", null));

        Assert.True(result.IsSuccess);
        Assert.Equal("999888777666", result.Value.FacebookPixelId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenFacebookPixelIdIsNotNumeric()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, null, null, null, null, null, "not-numeric", null));

        Assert.True(result.IsFailure);
        Assert.Equal("invalid_facebook_pixel_id", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ShouldKeepExistingFacebookPixelId_WhenNotProvided()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, null, "Updated headline", null, null, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal("123456789012345", result.Value.FacebookPixelId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldKeepExistingCoverImage_WhenNoFileProvided()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());
        var originalCoverImageUrl = created.Value.CoverImageUrl;

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, null, "Updated headline", null, null, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(originalCoverImageUrl, result.Value.CoverImageUrl);
        Assert.Single(imageStorage.SavedImages); // only the create-time save, no re-save on update
    }

    [Fact]
    public async Task UpdateAsync_ShouldOverwriteCoverImage_WhenFileProvided_EvenAfterSlugChangedViaTitleUpdate()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, "A New Title", null, null, null, null, null, null));

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, null, null, null, new UploadedFile("image/png", [9, 9, 9]), null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, imageStorage.SavedImages.Count);
        Assert.All(imageStorage.SavedImages, saved => Assert.Equal($"{GeneratedId}/cover.webp", saved.Key));
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenCoverImageContentTypeIsNotAllowed()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, null, null, null, new UploadedFile("application/pdf", [1, 2, 3]), null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("invalid_cover_image_type", result.Error);
    }

    [Fact]
    public async Task ListAsync_ShouldOnlyReturnCurrentTenantsNonDeletedReleases()
    {
        await releaseManager.CreateAsync(ValidRequest());

        var otherTenantManager = NewManagerFor("user-2", Guid.NewGuid());
        await otherTenantManager.CreateAsync(ValidRequest("Other Song"));

        var result = await releaseManager.ListAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Run To Me", result.Value.Single().Title);
    }

    [Fact]
    public async Task GetAsync_ShouldFail_WhenReleaseBelongsToAnotherTenant()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var otherTenantManager = NewManagerFor("user-2", Guid.NewGuid());

        var result = await otherTenantManager.GetAsync(created.Value.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("release_not_found", result.Error);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteAndUnpublish()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var result = await releaseManager.DeleteAsync(created.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Contains("run-to-me", publisher.UnpublishedSlugs);

        var getResult = await releaseManager.GetAsync(created.Value.Id);
        Assert.True(getResult.IsFailure);
        Assert.Equal("release_not_found", getResult.Error);
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenReleaseNotFound()
    {
        var result = await releaseManager.DeleteAsync(Guid.NewGuid().ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("release_not_found", result.Error);
    }
}
