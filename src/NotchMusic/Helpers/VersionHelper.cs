using System;

namespace NotchMusic.Helpers;

public static class VersionHelper
{
    public static bool TryParseCleanVersion(string? versionStr, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(versionStr)) return false;

        var clean = versionStr.Trim();
        if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(1).Trim();
        }

        // Remove prerelease tags or build metadata (e.g., "-beta", "+123")
        int dashIdx = clean.IndexOf('-');
        if (dashIdx >= 0) clean = clean.Substring(0, dashIdx);
        int plusIdx = clean.IndexOf('+');
        if (plusIdx >= 0) clean = clean.Substring(0, plusIdx);

        // System.Version requires at least 2 parts (Major.Minor)
        var parts = clean.Split('.');
        if (parts.Length == 1)
        {
            clean += ".0";
        }

        return Version.TryParse(clean, out version!);
    }

    public static (bool isNewer, bool isMajor, bool isMinor, bool isPatch) Compare(string? currentVersionStr, string? remoteVersionStr)
    {
        if (!TryParseCleanVersion(currentVersionStr, out var current) ||
            !TryParseCleanVersion(remoteVersionStr, out var remote))
        {
            return (false, false, false, false);
        }

        if (remote.Major > current.Major)
        {
            return (true, true, false, false);
        }

        if (remote.Major == current.Major)
        {
            if (remote.Minor > current.Minor)
            {
                return (true, false, true, false);
            }

            if (remote.Minor == current.Minor)
            {
                int currentBuild = Math.Max(0, current.Build);
                int remoteBuild = Math.Max(0, remote.Build);
                if (remoteBuild > currentBuild)
                {
                    return (true, false, false, true);
                }

                int currentRev = Math.Max(0, current.Revision);
                int remoteRev = Math.Max(0, remote.Revision);
                if (remoteBuild == currentBuild && remoteRev > currentRev)
                {
                    return (true, false, false, true);
                }
            }
        }

        return (false, false, false, false);
    }
}
