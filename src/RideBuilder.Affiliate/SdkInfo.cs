using System.Collections.Generic;
using System.Reflection;

namespace RideBuilder.Affiliate;

/// <summary>
/// Integration-protocol identity for this SDK, reported on <c>register</c>/<c>heartbeat</c>.
/// <see cref="Version"/> is read from the assembly's <see cref="AssemblyInformationalVersionAttribute"/>
/// (build metadata stripped) so the wire version <b>cannot drift</b> from the package version — unlike the
/// Node SDK's hand-synced <c>SDK_VERSION</c> constant.
/// </summary>
public static class SdkInfo
{
    /// <summary>The SDK <c>type</c> reported to RideBuilder.</summary>
    public const string Type = "dotnet_sdk";

    /// <summary>Capabilities announced on <c>register</c> when the caller passes none.</summary>
    public static readonly IReadOnlyList<string> DefaultCapabilities =
        new[] { "click_capture", "order_events", "refund_events" };

    /// <summary>The SDK version, derived from the assembly (never hand-synced).</summary>
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var asm = typeof(SdkInfo).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            // Strip any "+<gitsha>" build metadata SourceLink/MinVer appends.
            var plus = info!.IndexOf('+');
            return plus >= 0 ? info.Substring(0, plus) : info;
        }

        return asm.GetName().Version?.ToString() ?? "0.0.0";
    }
}
