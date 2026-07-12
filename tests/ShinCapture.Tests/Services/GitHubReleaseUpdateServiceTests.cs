using System.Net;
using System.Net.Http;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class GitHubReleaseUpdateServiceTests
{
    [Theory]
    [InlineData("v1.3.8", "1.3.7", true)]
    [InlineData("1.3.8", "1.3.7", true)]
    [InlineData("v1.3.7", "1.3.7", false)]
    [InlineData("v1.3.6", "1.3.7", false)]
    public void TryCreateUpdate_ReturnsOnlyVersionsNewerThanInstalled(
        string tagName,
        string installedVersion,
        bool expected)
    {
        bool result = GitHubReleaseUpdateService.TryCreateUpdate(
            tagName,
            "https://example.test/releases/v1.3.8",
            Version.Parse(installedVersion),
            out ReleaseUpdateInfo? update);

        Assert.Equal(expected, result);
        Assert.Equal(expected, update is not null);
    }

    [Theory]
    [InlineData("")]
    [InlineData("vnext")]
    [InlineData("v1.3.8-preview")]
    public void TryCreateUpdate_RejectsInvalidOrPrereleaseTag(string tagName)
    {
        bool result = GitHubReleaseUpdateService.TryCreateUpdate(
            tagName,
            "https://example.test/releases/v1.3.8",
            new Version(1, 3, 7),
            out ReleaseUpdateInfo? update);

        Assert.False(result);
        Assert.Null(update);
    }

    [Fact]
    public void TryCreateUpdate_RequiresReleaseUrl()
    {
        bool result = GitHubReleaseUpdateService.TryCreateUpdate(
            "v1.3.8",
            "",
            new Version(1, 3, 7),
            out ReleaseUpdateInfo? update);

        Assert.False(result);
        Assert.Null(update);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNewerPublishedRelease()
    {
        using HttpClient client = new(new StaticJsonHandler(
            """
            {"tag_name":"v1.3.8","html_url":"https://github.com/dwshin0907/shincapture/releases/tag/v1.3.8"}
            """));
        GitHubReleaseUpdateService service = new(client);

        ReleaseUpdateInfo? update = await service.CheckForUpdateAsync(new Version(1, 3, 7));

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 3, 8), update.Version);
        Assert.Equal(
            "https://github.com/dwshin0907/shincapture/releases/tag/v1.3.8",
            update.ReleaseUrl);
    }

    private sealed class StaticJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }
}
