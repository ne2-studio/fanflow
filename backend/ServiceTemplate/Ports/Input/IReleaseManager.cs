using CSharpFunctionalExtensions;

namespace ServiceTemplate.Ports.Input;

/// <summary>
/// Authoring surface for releases (React Admin). A release owns exactly one auto-managed
/// landing page — there is no separate landing-page entity or explicit publish action.
/// </summary>
public interface IReleaseManager
{
    /// <summary>
    /// Creates a release and its landing page (Spotify is the only supported destination in MVP).
    /// </summary>
    /// <returns>
    /// The created release, or a failure: "invalid_destination" (non-Spotify link, not idempotent)
    /// or "slug_taken" (not idempotent).
    /// </returns>
    Task<Result<ReleaseDto>> CreateAsync(CreateReleaseRequest request);

    /// <summary>
    /// Updates any subset of a release's authored fields; re-triggers landing page regeneration
    /// and republish.
    /// </summary>
    /// <returns>
    /// The updated release, or a failure: "release_not_found" (idempotent) or "invalid_destination"
    /// (not idempotent).
    /// </returns>
    Task<Result<ReleaseDto>> UpdateAsync(string releaseId, UpdateReleaseRequest request);

    /// <summary>
    /// Lists releases owned by the current tenant/user.
    /// </summary>
    /// <returns>A result containing the releases (an empty list is a valid, idempotent result).</returns>
    Task<Result<IEnumerable<ReleaseSummaryDto>>> ListAsync();

    /// <returns>The release, or a failure: "release_not_found" (idempotent).</returns>
    Task<Result<ReleaseDto>> GetAsync(string releaseId);

    /// <summary>
    /// Soft-deletes the release: unpublishes its landing page but keeps historical tracking data.
    /// </summary>
    /// <returns>A result indicating success, or a failure: "release_not_found" (idempotent).</returns>
    Task<Result> DeleteAsync(string releaseId);
}
