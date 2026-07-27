using System;
using System.Reflection;
using System.Threading.Tasks;
using RideBuilder.Affiliate.TestSupport;
using Xunit;

namespace RideBuilder.Affiliate.Tests;

// Pins the version-can't-drift guarantee: the wire version is the assembly's informational version, so it
// can never diverge from the package version the way a hand-synced constant can.
public class SdkInfoTests
{
    [Fact]
    public void Type_is_dotnet_sdk() => Assert.Equal("dotnet_sdk", SdkInfo.Type);

    [Fact]
    public void Version_is_non_empty() => Assert.False(string.IsNullOrWhiteSpace(SdkInfo.Version));

    [Fact]
    public void Version_equals_the_assembly_informational_version_without_build_metadata()
    {
        var informational = typeof(RideBuilderClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var expected = informational.Split('+')[0];

        Assert.Equal(expected, SdkInfo.Version);
    }

    [Fact]
    public async Task Register_sends_the_resolved_type_and_version_on_the_wire()
    {
        var handler = new RecordingHandler((200, "{\"integrationId\":\"i\",\"status\":\"connected\"}"));
        using var client = TestClient.Create(new RideBuilderClientOptions { ApiKey = "test-key", MaxRetries = 0 }, handler);

        await client.RegisterAsync();

        Assert.Contains($"\"version\":\"{SdkInfo.Version}\"", handler.Requests[0].Body);
        Assert.Contains("\"type\":\"dotnet_sdk\"", handler.Requests[0].Body);
    }
}
