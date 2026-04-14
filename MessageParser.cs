using System.Text.RegularExpressions;

namespace UVBStealer;

public static partial class MessageParser
{
    // Extracts each WORD from segments like: СЛОВО XXXX XXXX
    [GeneratedRegex(@"([А-ЯЁ]{2,})\s+\d{4}\s+\d{4}", RegexOptions.Compiled)]
    private static partial Regex WordSegmentRegex();

    public static List<string> ExtractWords(string plainText)
    {
        if (!plainText.Contains("НЖТИ"))
            return [];

        var matches = WordSegmentRegex().Matches(plainText);
        return matches.Select(m => m.Groups[1].Value).ToList();
    }
}
