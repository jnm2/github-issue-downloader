using System;
using System.IO;
using System.Text;

namespace GitHubIssueDownloader
{
    internal static class PathUtils
    {
        public static string SanitizeFileName(ReadOnlySpan<char> source, int maxLength)
        {
            source = TrimFileName(source, maxLength);

            var sanitized = new StringBuilder();

            var invalidFileNameChars = Path.GetInvalidFileNameChars();

            while (true)
            {
                var nextInvalidPosition = source.IndexOfAny(invalidFileNameChars);
                if (nextInvalidPosition == -1) break;

                sanitized.Append(source[..nextInvalidPosition]);

                sanitized.Append(source[nextInvalidPosition] switch
                {
                    '"' => "'",
                    '?' => "",
                    _ => "-",
                });

                source = source.Slice(nextInvalidPosition + 1);
            }

            sanitized.Append(source);

            return sanitized.ToString();
        }

        public static ReadOnlySpan<char> TrimFileName(ReadOnlySpan<char> source, int maxLength)
        {
            source = source.Trim();

            if (source.Length > maxLength)
            {
                const string suffix = " [...]";

                var trimmedLength = maxLength - suffix.Length;

                if (!char.IsWhiteSpace(source[trimmedLength]))
                {
                    while (trimmedLength > 0 && !char.IsWhiteSpace(source[trimmedLength - 1]))
                        trimmedLength--;

                    if (trimmedLength == 0) trimmedLength = maxLength;
                }

                source = source[0..trimmedLength].TrimEnd().TrimEnd('.').ToString() + suffix;
            }
            else
            {
                source = source.TrimEnd('.');
            }

            return source;
        }
    }
}
