using System.Net;
using System.Text;
using organizadorCapitulos.Infrastructure.Services;
using Xunit;

namespace OrganizadorCapitulos.Tests;

public class TmdbMetadataServiceTests
{
    private static HttpClient BuildHttpClient(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(responseJson, statusCode);
        var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
        return client;
    }

    [Fact]
    public async Task SearchSeriesAsync_NotConfigured_ReturnsEmpty()
    {
        var svc = new TmdbMetadataService(BuildHttpClient("{}"));
        var result = await svc.SearchSeriesAsync("Breaking Bad");
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchSeriesAsync_ReturnsResults_ParsedCorrectly()
    {
        const string json = """
            {
                "results": [
                    {"id": 1396, "name": "Breaking Bad", "overview": "A show", "first_air_date": "2008-01-20"}
                ]
            }
            """;
        var svc = new TmdbMetadataService(BuildHttpClient(json));
        svc.Configure("fake-api-key");

        var results = await svc.SearchSeriesAsync("Breaking Bad");

        Assert.Single(results);
        Assert.Equal(1396, results[0].Id);
        Assert.Equal("Breaking Bad", results[0].Name);
        Assert.Equal("2008-01-20", results[0].FirstAirDate);
    }

    [Fact]
    public async Task SearchSeriesAsync_EmptyQuery_ReturnsEmpty()
    {
        var svc = new TmdbMetadataService(BuildHttpClient("{}"));
        svc.Configure("fake-api-key");
        var result = await svc.SearchSeriesAsync("   ");
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchSeriesAsync_HttpError_ReturnsEmpty()
    {
        var svc = new TmdbMetadataService(BuildHttpClient("{}", HttpStatusCode.Unauthorized));
        svc.Configure("bad-key");
        var result = await svc.SearchSeriesAsync("test");
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEpisodeTitleAsync_ReturnsTitle()
    {
        const string json = """{"name": "Pilot", "overview": "First episode"}""";
        var svc = new TmdbMetadataService(BuildHttpClient(json));
        svc.Configure("fake-api-key");

        var title = await svc.GetEpisodeTitleAsync(1396, 1, 1);

        Assert.Equal("Pilot", title);
    }

    [Fact]
    public async Task GetEpisodeTitleAsync_NotConfigured_ReturnsNull()
    {
        var svc = new TmdbMetadataService(BuildHttpClient("{}"));
        var title = await svc.GetEpisodeTitleAsync(1396, 1, 1);
        Assert.Null(title);
    }

    [Fact]
    public async Task GetEpisodeTitleAsync_HttpError_ReturnsNull()
    {
        var svc = new TmdbMetadataService(BuildHttpClient("{}", HttpStatusCode.NotFound));
        svc.Configure("fake-api-key");
        var title = await svc.GetEpisodeTitleAsync(1396, 99, 99);
        Assert.Null(title);
    }

    [Fact]
    public async Task SearchMoviesAsync_ReturnsResults_ParsedCorrectly()
    {
        const string json = """
            {
                "results": [
                    {"id": 550, "title": "Fight Club", "overview": "An insomniac office worker...", "release_date": "1999-10-15"}
                ]
            }
            """;
        var svc = new TmdbMetadataService(BuildHttpClient(json));
        svc.Configure("fake-api-key");

        var results = await svc.SearchMoviesAsync("Fight Club");

        Assert.Single(results);
        Assert.Equal(550, results[0].Id);
        Assert.Equal("Fight Club", results[0].Title);
        Assert.Equal("1999-10-15", results[0].ReleaseDate);
    }

    [Fact]
    public async Task SearchMoviesAsync_NotConfigured_ReturnsEmpty()
    {
        var svc = new TmdbMetadataService(BuildHttpClient("{}"));
        var result = await svc.SearchMoviesAsync("Fight Club");
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchSeriesAsync_WithV3Key_AppendsApiKeyToUrl()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new CallbackHttpMessageHandler((req) =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
            };
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };
        var svc = new TmdbMetadataService(client);
        svc.Configure("1234567890abcdef1234567890abcdef"); // 32-character key

        await svc.SearchSeriesAsync("test");

        Assert.NotNull(capturedRequest);
        Assert.Contains("api_key=1234567890abcdef1234567890abcdef", capturedRequest.RequestUri?.Query);
        Assert.Null(capturedRequest.Headers.Authorization);
    }

    [Fact]
    public async Task SearchSeriesAsync_WithV4Key_UsesBearerHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new CallbackHttpMessageHandler((req) =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
            };
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };
        var svc = new TmdbMetadataService(client);
        // Long v4 token
        string v4Token = "eyJhbGciOiJIUzI1NiJ9.eyJhdWQiOiJmYWtlIiwic3ViIjoiZmFrZSJ9.abc123xyz";
        svc.Configure(v4Token);

        await svc.SearchSeriesAsync("test");

        Assert.NotNull(capturedRequest);
        Assert.DoesNotContain("api_key", capturedRequest.RequestUri?.Query);
        Assert.NotNull(capturedRequest.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
        Assert.Equal(v4Token, capturedRequest.Headers.Authorization.Parameter);
    }

    private class CallbackHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _callback;

        public CallbackHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> callback)
        {
            _callback = callback;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_callback(request));
        }
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
