using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

internal sealed class GitHubUpdateService : IUpdateService, IDisposable
{
    private static readonly Uri LatestReleaseApi = new(
        "https://api.github.com/repos/pitifulbug/ra2-toolkit/releases/latest");
    private static readonly Uri LatestReleasePage = new(
        "https://github.com/pitifulbug/ra2-toolkit/releases/latest");

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
                Timeout = TimeSpan.FromSeconds(12)
            };
            ownsClient = true;
        }
        else
        {
            this.client = client;
        }

        if (!this.client.DefaultRequestHeaders.UserAgent.Any())
            this.client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("ra2-toolkit", "1.0.4"));
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream, cancellationToken: cancellationToken);

        var tag = document.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var latest))
            throw new InvalidDataException("GitHub 返回了无效的版本号。");

        var releaseUri = LatestReleasePage;
        if (document.RootElement.TryGetProperty("html_url", out var uriProperty) &&
            Uri.TryCreate(uriProperty.GetString(), UriKind.Absolute, out var candidate) &&
            candidate.Scheme == Uri.UriSchemeHttps &&
            candidate.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            releaseUri = candidate;

        return new UpdateCheckResult(latest > currentVersion, latest, releaseUri);
    }

    public void Dispose()
    {
        if (ownsClient)
            client.Dispose();
    }
}
