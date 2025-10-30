using System.Reflection;
using System.Text.RegularExpressions;

namespace Application.Api.Utils;

/// <summary>
/// Utility for filtering comments against a list of banned words.
/// </summary>
public static class WordFilter
{
    private const string ResourceName = "Application.Resources.banned_words.txt";

    // Lazily load and cache the set of banned words from an embedded text resource.
    // This ensures the resource is only read and parsed once, at first access.
    private static readonly Lazy<IReadOnlySet<string>> BannedWordsLazy = new(() =>
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        // Load the embedded resource stream.
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Could not load embedded resource: {ResourceName}");
        }

        // Read and split the file content into a trimmed word set.
        using var reader = new StreamReader(stream);
        string content = reader.ReadToEnd();
        string[] lines = content.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        return new HashSet<string>(lines, StringComparer.OrdinalIgnoreCase);
    });

    private static IReadOnlySet<string> BannedWords => BannedWordsLazy.Value;

    /// <summary>
    /// Checks if a given comment contains any banned words as whole words.
    /// </summary>
    /// <param name="comment">The input comment to check.</param>
    /// <param name="matchedWord">The banned word that was found, if any.</param>
    /// <returns>True if a banned word was found; otherwise, false.</returns>
    public static bool ContainsBannedWords(string comment, out string matchedWord)
    {
        foreach (string word in BannedWords)
        {
            // Use word boundary anchors (\b) to match only whole words.
            // Perform a case-insensitive match.
            string pattern = $@"\b{Regex.Escape(word)}\b";
            if (Regex.IsMatch(comment, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                matchedWord = word;
                return true;
            }
        }

        matchedWord = string.Empty;
        return false;
    }
}
