namespace SosuWeb.Render.Services;

public sealed class ClientRendererVersionOptions
{
    /// <summary>
    /// The latest ClientRenderer release accepted by this server. It is
    /// intentionally configuration-driven so deployment can advance the
    /// server's update policy atomically with a ClientRenderer release.
    /// </summary>
    public string LatestVersion { get; set; } = string.Empty;
}

public static class ClientRendererVersionPolicy
{
    public static bool IsUpdateRequired(string? currentVersion, string? latestVersion)
    {
        if (!TryParseStableVersion(currentVersion, out Version current) ||
            !TryParseStableVersion(latestVersion, out Version latest))
        {
            // Never force an update when either side is not a recognized
            // stable numeric version. This keeps a bad configuration or an
            // older client from being put into an update loop.
            return false;
        }

        return current.CompareTo(latest) < 0;
    }

    public static bool IsVersionSupported(string? currentVersion, string? latestVersion)
    {
        if (!TryParseStableVersion(currentVersion, out Version current) ||
            !TryParseStableVersion(latestVersion, out Version latest))
        {
            // A renderer with no recognizable version must never receive a
            // new render job. This also fails closed when the server's
            // required version is misconfigured.
            return false;
        }

        return current.CompareTo(latest) >= 0;
    }

    private static bool TryParseStableVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(normalized, out Version? parsed) ||
            parsed is null ||
            parsed.Major < 0 || parsed.Minor < 0)
        {
            return false;
        }

        // Normalize omitted build/revision components so 2.0.12 and
        // 2.0.12.0 represent the same release.
        version = new Version(
            parsed.Major,
            parsed.Minor,
            Math.Max(parsed.Build, 0),
            Math.Max(parsed.Revision, 0));
        return true;
    }
}
