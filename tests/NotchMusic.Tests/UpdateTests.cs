using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Update;
using NotchMusic.ViewModels;
using Xunit;

namespace NotchMusic.Tests;

public class UpdateTests
{
    private class MockSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; } = new AppSettings();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => "appdata";
        public string GetLyricsCachePath() => "cache";
        public void ClearLyricsCache() { }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0", true, true, false, false)]
    [InlineData("1.0.0", "1.1.0", true, false, true, false)]
    [InlineData("1.0.0", "1.0.1", true, false, false, true)]
    [InlineData("v1.0.0", "v1.2.0", true, false, true, false)]
    [InlineData("1.0.0", "v2.0.0-beta", true, true, false, false)]
    [InlineData("1.0.0", "1.0.0", false, false, false, false)]
    [InlineData("1.2.0", "1.1.0", false, false, false, false)]
    [InlineData("2.0.0", "1.9.9", false, false, false, false)]
    public void VersionHelper_Compare_CorrectlyIdentifiesMajorMinorPatch(
        string current, string remote, bool expectedNewer, bool expectedMajor, bool expectedMinor, bool expectedPatch)
    {
        var (isNewer, isMajor, isMinor, isPatch) = VersionHelper.Compare(current, remote);

        Assert.Equal(expectedNewer, isNewer);
        Assert.Equal(expectedMajor, isMajor);
        Assert.Equal(expectedMinor, isMinor);
        Assert.Equal(expectedPatch, isPatch);
    }

    [Fact]
    public void UpdateService_ParseGitHubReleaseJson_ExtractsFieldsCorrectly()
    {
        var settings = new MockSettingsService();
        var updateService = new UpdateService(settings);

        var githubJson = @"{
            ""tag_name"": ""v1.2.0"",
            ""name"": ""DRANOTCH v1.2.0"",
            ""body"": ""- Added update button\n- Minor bug fixes"",
            ""html_url"": ""https://github.com/dranotch/dranotch/releases/tag/v1.2.0"",
            ""published_at"": ""2026-10-21T12:00:00Z"",
            ""assets"": [
                {
                    ""name"": ""DRANOTCH-Setup-1.2.0.exe"",
                    ""browser_download_url"": ""https://github.com/dranotch/dranotch/releases/download/v1.2.0/DRANOTCH-Setup-1.2.0.exe""
                }
            ]
        }";

        var updateInfo = updateService.ParseUpdateJson(githubJson);

        Assert.NotNull(updateInfo);
        Assert.Equal("1.2.0", updateInfo!.Version);
        Assert.Equal("DRANOTCH v1.2.0", updateInfo.Title);
        Assert.Contains("Added update button", updateInfo.Changelog);
        Assert.Equal("https://github.com/dranotch/dranotch/releases/download/v1.2.0/DRANOTCH-Setup-1.2.0.exe", updateInfo.DownloadUrl);
        Assert.Equal("https://github.com/dranotch/dranotch/releases/tag/v1.2.0", updateInfo.ReleasePageUrl);
    }

    [Fact]
    public void UpdateService_ParseStandaloneJson_ExtractsFieldsCorrectly()
    {
        var settings = new MockSettingsService();
        var updateService = new UpdateService(settings);

        var standaloneJson = @"{
            ""version"": ""2.0.0"",
            ""title"": ""DRANOTCH 2.0.0 Major Release"",
            ""changelog"": ""Major UI and core improvements"",
            ""downloadUrl"": ""https://example.com/DRANOTCH-Setup-2.0.0.exe"",
            ""releasePageUrl"": ""https://example.com/release/2.0.0""
        }";

        var updateInfo = updateService.ParseUpdateJson(standaloneJson);

        Assert.NotNull(updateInfo);
        Assert.Equal("2.0.0", updateInfo!.Version);
        Assert.Equal("DRANOTCH 2.0.0 Major Release", updateInfo.Title);
        Assert.Equal("Major UI and core improvements", updateInfo.Changelog);
        Assert.Equal("https://example.com/DRANOTCH-Setup-2.0.0.exe", updateInfo.DownloadUrl);
    }

    [Fact]
    public void UpdateInfo_Labels_ReturnExpectedIndonesianDescriptions()
    {
        var major = new UpdateInfo { IsMajor = true };
        var minor = new UpdateInfo { IsMinor = true };
        var patch = new UpdateInfo { IsPatch = true };

        Assert.Equal("Pembaruan Mayor (Major)", major.UpdateTypeLabel);
        Assert.Equal("Pembaruan Minor (Minor)", minor.UpdateTypeLabel);
        Assert.Equal("Perbaikan / Patch", patch.UpdateTypeLabel);
    }

    [Fact]
    public async Task SettingsViewModel_CheckUpdateCommand_DetectsNewerVersion()
    {
        var settings = new MockSettingsService();
        var json = @"{
            ""version"": ""9.9.9"",
            ""title"": ""DRANOTCH Future Release"",
            ""changelog"": ""Brand new features"",
            ""downloadUrl"": ""https://example.com/DRANOTCH-Setup-9.9.9.exe"",
            ""releasePageUrl"": ""https://example.com/release""
        }";

        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });
        var client = new HttpClient(handler);
        var updateService = new UpdateService(settings, client);

        var vm = new SettingsViewModel(settings, _ => { }, syncService: null, updateService: updateService);

        vm.CheckUpdateCommand.Execute(null);

        // Allow async command execution
        for (int i = 0; i < 20 && vm.IsCheckingUpdate; i++)
        {
            await Task.Delay(50);
        }

        Assert.True(vm.HasAvailableUpdate);
        Assert.NotNull(vm.AvailableUpdate);
        Assert.Equal("9.9.9", vm.AvailableUpdate!.Version);
        Assert.Contains("Tersedia versi baru", vm.UpdateStatus);
    }
}
