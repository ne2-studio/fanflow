namespace FanFlow.Ports.Output;

/// <summary>
/// The internet-facing hostname of the container that serves published release landing pages
/// (the thin Nginx in front of MinIO) — used to build the public release URL shown to authors.
/// </summary>
public interface IPublicSiteSettings
{
    string PublicHostname { get; }
}
