using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

internal sealed class GitHubUpdateService : IUpdateService, IDisposable
{
    private static readonly Uri LatestReleasePage = new(
        "https://github.com/pitifulbug/ra2-toolkit/releases/latest");
    private static readonly Uri LatestReleaseApi = new(
        "https://api.github.com/repos/pitifulbug/ra2-toolkit/releases/latest");
    private const string ReleaseTagPathPrefix =
        "/pitifulbug/ra2-toolkit/releases/tag/";

    private readonly HttpClient client;
    private readonly bool ownsClient;

    internal GitHubUpdateService(HttpClient? client = null)
    {
        if (client is null)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All
            };
            this.client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            ownsClient = true;
        }
        else
        {
            this.client = client;
        }

        if (!this.client.DefaultRequestHeaders.UserAgent.Any())
        {
            var version = typeof(GitHubUpdateService).Assembly.GetName().Version?
                .ToString(3) ?? "0.0.0";
            this.client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("ra2-toolkit", version));
        }
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion, CancellationToken cancellationToken)
    {
        Exception? pageError = null;
        try
        {
            return await CheckLatestReleasePageAsync(currentVersion, cancellationToken);
        }
        catch (Exception error) when (CanFallBack(error, cancellationToken))
        {
            pageError = error;
        }

        try
        {
            return await CheckLatestReleaseApiAsync(currentVersion, cancellationToken);
        }
        catch (Exception apiError) when (CanFallBack(apiError, cancellationToken))
        {
            throw new InvalidDataException(
                "无法从 GitHub 最新发布页或备用 API 读取有效版本信息。",
                new AggregateException(pageError!, apiError));
        }
    }

    private async Task<UpdateCheckResult> CheckLatestReleasePageAsync(
        Version currentVersion,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleasePage);
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var releaseUri = response.RequestMessage?.RequestUri;
        if (!TryParseReleasePageUri(releaseUri, out var latest))
        {
            throw new InvalidDataException(
                $"GitHub 最新发布页未重定向到有效的发布标签：{releaseUri}");
        }

        return new UpdateCheckResult(latest > currentVersion, latest, releaseUri!);
    }

    private async Task<UpdateCheckResult> CheckLatestReleaseApiAsync(
        Version currentVersion,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("tag_name", out var tagProperty) ||
            !TryParseVersionTag(tagProperty.GetString(), out var latest))
        {
            throw new InvalidDataException("GitHub 返回了无效的版本号。");
        }

        var releaseUri = LatestReleasePage;
        if (document.RootElement.TryGetProperty("html_url", out var uriProperty) &&
            Uri.TryCreate(uriProperty.GetString(), UriKind.Absolute, out var candidate) &&
            candidate.Scheme == Uri.UriSchemeHttps &&
            candidate.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            releaseUri = candidate;

        return new UpdateCheckResult(latest > currentVersion, latest, releaseUri);
    }

    private static bool TryParseReleasePageUri(Uri? uri, out Version version)
    {
        version = new Version();
        if (uri is null || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith(ReleaseTagPathPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var encodedTag = uri.AbsolutePath[ReleaseTagPathPrefix.Length..].TrimEnd('/');
        if (encodedTag.Length == 0 || encodedTag.Contains('/'))
            return false;

        return TryParseVersionTag(Uri.UnescapeDataString(encodedTag), out version);
    }

    private static bool TryParseVersionTag(string? tag, out Version version) =>
        Version.TryParse(tag?.Trim().TrimStart('v', 'V'), out version!);

    private static bool CanFallBack(Exception error, CancellationToken cancellationToken) =>
        error is HttpRequestException or JsonException or InvalidDataException ||
        error is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    public void Dispose()
    {
        if (ownsClient)
            client.Dispose();
    }
}
