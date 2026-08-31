internal interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken);
}

internal sealed record UpdateCheckResult(
    bool UpdateAvailable,
    Version LatestVersion,
    Uri ReleaseUri);
