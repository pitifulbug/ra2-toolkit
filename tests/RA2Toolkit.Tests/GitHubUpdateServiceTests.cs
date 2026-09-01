using System.Net;
using System.Text;
using Xunit;

public sealed class GitHubUpdateServiceTests
{
    private static readonly Uri LatestPage = new(
        "https://github.com/pitifulbug/ra2-toolkit/releases/latest");
    private static readonly Uri LatestApi = new(
        "https://api.github.com/repos/pitifulbug/ra2-toolkit/releases/latest");

    [Theory]
    [InlineData("1.1.9.0", "v1.2.0", true)]
    [InlineData("1.2.0.0", "v1.2.0", false)]
    public async Task Latest_page_redirect_uri_determines_update(
        string currentVersion,
        string tag,
        bool updateAvailable)
    {
        var finalUri = new Uri(
            $"https://github.com/pitifulbug/ra2-toolkit/releases/tag/{tag}");
        using var client = CreateClient((request, call) =>
        {
            Assert.Equal(1, call);
            Assert.Equal(LatestPage, request.RequestUri);
            return Response(HttpStatusCode.OK, finalUri);
        }, out var handler);
        using var service = new GitHubUpdateService(client);

        var result = await service.CheckAsync(
            Version.Parse(currentVersion), CancellationToken.None);

        Assert.Equal(updateAvailable, result.UpdateAvailable);
        Assert.Equal(Version.Parse("1.2.0"), result.LatestVersion);
        Assert.Equal(finalUri, result.ReleaseUri);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Page_failure_falls_back_to_release_api()
    {
        var apiReleaseUri = new Uri(
            "https://github.com/pitifulbug/ra2-toolkit/releases/tag/v1.3.0");
        using var client = CreateClient((request, call) => call switch
        {
            1 => Response(HttpStatusCode.ServiceUnavailable, request.RequestUri),
            2 => JsonResponse(HttpStatusCode.OK,
                """
                {
                  "tag_name": "v1.3.0",
                  "html_url": "https://github.com/pitifulbug/ra2-toolkit/releases/tag/v1.3.0"
                }
                """, request.RequestUri),
            _ => throw new InvalidOperationException("发出了非预期的额外请求。")
        }, out var handler);
        using var service = new GitHubUpdateService(client);

        var result = await service.CheckAsync(
            Version.Parse("1.2.0.0"), CancellationToken.None);

        Assert.True(result.UpdateAvailable);
        Assert.Equal(Version.Parse("1.3.0"), result.LatestVersion);
        Assert.Equal(apiReleaseUri, result.ReleaseUri);
        Assert.Equal(new[] { LatestPage, LatestApi }, handler.RequestUris);
    }

    [Fact]
    public async Task Invalid_page_and_api_tags_report_clear_error()
    {
        using var client = CreateClient((request, call) => call switch
        {
            1 => Response(HttpStatusCode.OK, new Uri(
                "https://github.com/pitifulbug/ra2-toolkit/releases/tag/not-a-version")),
            2 => JsonResponse(HttpStatusCode.OK,
                """{"tag_name":"also-invalid"}""", request.RequestUri),
            _ => throw new InvalidOperationException("发出了非预期的额外请求。")
        }, out var handler);
        using var service = new GitHubUpdateService(client);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckAsync(Version.Parse("1.0.0"), CancellationToken.None));

        Assert.Contains("最新发布页", error.Message, StringComparison.Ordinal);
        Assert.Contains("备用 API", error.Message, StringComparison.Ordinal);
        Assert.IsType<AggregateException>(error.InnerException);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Non_github_final_page_is_rejected_and_api_is_used()
    {
        using var client = CreateClient((request, call) => call switch
        {
            1 => Response(HttpStatusCode.OK,
                new Uri("https://example.test/releases/tag/v99.0.0")),
            2 => JsonResponse(HttpStatusCode.OK,
                """
                {
                  "tag_name": "v1.0.1",
                  "html_url": "https://github.com/pitifulbug/ra2-toolkit/releases/tag/v1.0.1"
                }
                """, request.RequestUri),
            _ => throw new InvalidOperationException("发出了非预期的额外请求。")
        }, out var handler);
        using var service = new GitHubUpdateService(client);

        var result = await service.CheckAsync(
            Version.Parse("1.0.0"), CancellationToken.None);

        Assert.Equal(Version.Parse("1.0.1"), result.LatestVersion);
        Assert.Equal(2, handler.CallCount);
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory,
        out RecordingHandler handler)
    {
        handler = new RecordingHandler(responseFactory);
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    private static HttpResponseMessage Response(
        HttpStatusCode status,
        Uri? finalUri) => new(status)
        {
            RequestMessage = finalUri is null
            ? null
            : new HttpRequestMessage(HttpMethod.Get, finalUri)
        };

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode status,
        string json,
        Uri? finalUri)
    {
        var response = Response(status, finalUri);
        response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return response;
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        private readonly List<Uri?> requestUris = [];

        internal int CallCount => requestUris.Count;
        internal IReadOnlyList<Uri?> RequestUris => requestUris;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            requestUris.Add(request.RequestUri);
            return Task.FromResult(responseFactory(request, requestUris.Count));
        }
    }
}
