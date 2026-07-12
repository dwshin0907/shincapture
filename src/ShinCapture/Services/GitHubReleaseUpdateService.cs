using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ShinCapture.Services;

public sealed record ReleaseUpdateInfo(Version Version, string ReleaseUrl);

public sealed class GitHubReleaseUpdateService
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/dwshin0907/shincapture/releases/latest";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient _httpClient;

    public GitHubReleaseUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public async Task<ReleaseUpdateInfo?> CheckForUpdateAsync(
        Version installedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installedVersion);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using HttpResponseMessage response = await _httpClient
                .GetAsync(LatestReleaseUrl, timeout.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var responseStream = await response.Content
                .ReadAsStreamAsync(timeout.Token)
                .ConfigureAwait(false);
            GitHubReleaseResponse? release = await JsonSerializer
                .DeserializeAsync<GitHubReleaseResponse>(responseStream, cancellationToken: timeout.Token)
                .ConfigureAwait(false);

            return TryCreateUpdate(
                release?.TagName,
                release?.HtmlUrl,
                installedVersion,
                out ReleaseUpdateInfo? update)
                ? update
                : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool TryCreateUpdate(
        string? tagName,
        string? releaseUrl,
        Version installedVersion,
        out ReleaseUpdateInfo? update)
    {
        ArgumentNullException.ThrowIfNull(installedVersion);
        update = null;

        if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(releaseUrl))
            return false;

        string normalizedTag = tagName.Trim();
        if (normalizedTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalizedTag = normalizedTag[1..];

        if (!Version.TryParse(normalizedTag, out Version? availableVersion) ||
            availableVersion <= installedVersion ||
            !Uri.TryCreate(releaseUrl, UriKind.Absolute, out Uri? releaseUri) ||
            (releaseUri.Scheme != Uri.UriSchemeHttps && releaseUri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        update = new ReleaseUpdateInfo(availableVersion, releaseUri.AbsoluteUri);
        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ShinCapture", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }
    }
}
