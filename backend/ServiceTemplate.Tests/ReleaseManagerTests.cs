using Microsoft.Extensions.Logging.Abstractions;
using ServiceTemplate.Application;
using ServiceTemplate.Ports.Input;
using ServiceTemplate.Tests.Fakes;

namespace ServiceTemplate.Tests;

public class ReleaseManagerTests
{
    private static readonly Guid GeneratedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string CurrentUserId = "user-1";

    private readonly InMemoryReleaseRepository repository;
    private readonly SpyReleasePublisher publisher;
    private readonly ReleaseManager releaseManager;

    public ReleaseManagerTests()
    {
        repository = new InMemoryReleaseRepository();
        publisher = new SpyReleasePublisher();

        releaseManager = new ReleaseManager(
            NullLogger<ReleaseManager>.Instance,
            repository,
            publisher,
            new StaticSlugGenerator(),
            new StaticIdGenerator(GeneratedId),
            new StaticClock(),
            new StaticCurrentUserProvider(CurrentUserId));
    }

    private static CreateReleaseRequest ValidRequest(string title = "Run To Me") => new(
        title, "New single out now", "A great song.", "https://img/cover.jpg", "https://img/bg.jpg", "Listen now",
        [new DestinationLinkDto("Spotify", "https://open.spotify.com/track/123")]);

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

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenSlugIsAlreadyTaken()
    {
        await releaseManager.CreateAsync(ValidRequest());

        var releaseManager2 = new ReleaseManager(
            NullLogger<ReleaseManager>.Instance, repository, publisher, new StaticSlugGenerator(),
            new StaticIdGenerator(Guid.NewGuid()), new StaticClock(), new StaticCurrentUserProvider("user-2"));

        var result = await releaseManager2.CreateAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("slug_taken", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ShouldChangeFieldsAndRepublish()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, "Updated headline", null, null, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated headline", result.Value.Headline);
        Assert.Equal("Run To Me", result.Value.Title);
        Assert.Equal(2, publisher.PublishedReleases.Count);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenReleaseNotFound()
    {
        var result = await releaseManager.UpdateAsync(Guid.NewGuid().ToString(), new UpdateReleaseRequest(
            "New title", null, null, null, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("release_not_found", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenDestinationIsNotSpotify()
    {
        var created = await releaseManager.CreateAsync(ValidRequest());

        var result = await releaseManager.UpdateAsync(created.Value.Id, new UpdateReleaseRequest(
            null, null, null, null, null, null, [new DestinationLinkDto("YouTube", "https://youtube.com/x")]));

        Assert.True(result.IsFailure);
        Assert.Equal("invalid_destination", result.Error);
    }

    [Fact]
    public async Task ListAsync_ShouldOnlyReturnCurrentTenantsNonDeletedReleases()
    {
        await releaseManager.CreateAsync(ValidRequest());

        var otherTenantManager = new ReleaseManager(
            NullLogger<ReleaseManager>.Instance, repository, publisher, new StaticSlugGenerator(),
            new StaticIdGenerator(Guid.NewGuid()), new StaticClock(), new StaticCurrentUserProvider("user-2"));
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

        var otherTenantManager = new ReleaseManager(
            NullLogger<ReleaseManager>.Instance, repository, publisher, new StaticSlugGenerator(),
            new StaticIdGenerator(Guid.NewGuid()), new StaticClock(), new StaticCurrentUserProvider("user-2"));

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
