using System.Reflection;

namespace UrlPreviewApp.Client;

/// <summary>
/// The app version (<c>&lt;Version&gt;</c> in <c>Directory.Build.props</c>), shared by the WASM UI and the server.
/// </summary>
public static class AppVersion
{
    /// <summary>The version as <c>X.Y.Z</c>, e.g. <c>1.0.1</c>; the release tag is <c>v</c> + this value.</summary>
    public static string Current { get; } = Read();

    private static string Read()
    {
        var version = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(AppVersion).Assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";

        // Defensive: drop SourceLink build metadata ("1.0.1+abc123") if it is ever re-enabled.
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }
}
