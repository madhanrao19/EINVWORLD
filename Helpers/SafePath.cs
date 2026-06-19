using System.IO;
using System.Linq;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Helpers for safely combining a trusted base folder with untrusted, user-supplied path
    /// segments (e.g. route values) while preventing directory-traversal attacks.
    /// </summary>
    public static class SafePath
    {
        /// <summary>
        /// Combines <paramref name="baseFolder"/> with the user-supplied <paramref name="segments"/> and
        /// guarantees the resolved path stays inside the base folder. Rejects empty segments and any segment
        /// containing directory separators or traversal sequences (e.g. "..", "..%2f"), defeating
        /// path-traversal. Returns <c>true</c> and the canonical <paramref name="fullPath"/> when safe.
        /// </summary>
        public static bool TryResolve(string baseFolder, out string fullPath, params string[] segments)
        {
            fullPath = string.Empty;

            if (string.IsNullOrWhiteSpace(baseFolder) || segments is null || segments.Length == 0)
            {
                return false;
            }

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment) ||
                    segment.Contains("..", System.StringComparison.Ordinal) ||
                    segment.IndexOf('/') >= 0 ||
                    segment.IndexOf('\\') >= 0 ||
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    return false;
                }
            }

            var baseFull = Path.GetFullPath(baseFolder);
            var basePrefix = baseFull.EndsWith(Path.DirectorySeparatorChar)
                ? baseFull
                : baseFull + Path.DirectorySeparatorChar;

            var combined = Path.GetFullPath(Path.Combine(new[] { baseFolder }.Concat(segments).ToArray()));

            // Final canonical check: the resolved path must live under the base folder.
            if (!combined.StartsWith(basePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = combined;
            return true;
        }
    }
}
