using System;
using System.Collections.Generic;
using System.IO;

namespace Screenbox.Core.Helpers;

/// <summary>
/// Resolves a media path to the most specific configured library root.
/// </summary>
public static class MediaPathGroupingHelper
{
    /// <summary>
    /// Finds the index of the longest root path that contains the media path.
    /// </summary>
    /// <param name="mediaPath">The absolute path of a media file.</param>
    /// <param name="rootPaths">The configured library root paths.</param>
    /// <returns>The matching root index, or <c>-1</c> when no root matches.</returns>
    public static int FindBestMatchingRoot(string mediaPath, IReadOnlyList<string> rootPaths)
    {
        if (string.IsNullOrWhiteSpace(mediaPath) || rootPaths.Count == 0)
        {
            return -1;
        }

        string normalizedMediaPath = NormalizePath(mediaPath);
        int bestMatch = -1;
        int bestMatchLength = -1;

        for (int index = 0; index < rootPaths.Count; index++)
        {
            string rootPath = rootPaths[index];
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                continue;
            }

            string normalizedRootPath = NormalizePath(rootPath);
            if (normalizedRootPath.Length <= bestMatchLength ||
                !IsPathWithinRoot(normalizedMediaPath, normalizedRootPath))
            {
                continue;
            }

            bestMatch = index;
            bestMatchLength = normalizedRootPath.Length;
        }

        return bestMatch;
    }

    /// <summary>
    /// Normalizes separators and removes a trailing directory separator for stable comparisons.
    /// </summary>
    private static string NormalizePath(string path)
    {
        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            normalizedPath = path;
        }

        normalizedPath = normalizedPath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.TrimEndingDirectorySeparator(normalizedPath);
    }

    /// <summary>
    /// Checks path containment using directory boundaries rather than string-prefix semantics.
    /// </summary>
    private static bool IsPathWithinRoot(string mediaPath, string rootPath)
    {
        if (string.Equals(mediaPath, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string rootPrefix = Path.EndsInDirectorySeparator(rootPath)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        return mediaPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
