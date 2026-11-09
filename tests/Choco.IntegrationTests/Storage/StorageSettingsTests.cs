using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Choco.IntegrationTests.Storage;

public sealed class StorageSettingsTests
{
    [Theory]
    [InlineData("Storage:ServiceUrl", "")]
    [InlineData("Storage:ServiceUrl", "ftp://example.com")]
    [InlineData("Storage:Region", "")]
    [InlineData("Storage:UploadsBucketName", "")]
    [InlineData("Storage:UploadsBucketName", "Uppercase-Bucket")]
    [InlineData("Storage:MediaBucketName", "short")]
    [InlineData("Storage:MediaBucketName", "choco-test-uploads")]
    [InlineData("Storage:AccessKeyId", "")]
    [InlineData("Storage:SecretAccessKey", "")]
    public void InvalidSetting_FailsStartupAndNamesTheSetting(string key, string value)
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            ChocoApiFactory.UseTestSettings(builder, "Host=localhost");
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [key] = value }));
        });

        Action act = () => _ = factory.Services;

        act.Should().Throw<OptionsValidationException>().WithMessage($"*'{key}'*");
    }
}
