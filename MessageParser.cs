using System.Text.RegularExpressions;

namespace UVBStealer;

public static partial class MessageParser
{
    // Matches: data-post="uvb76logs/XXXXX"
    [GeneratedRegex(@"data-post=""[^/]+/(\d+)""", RegexOptions.Compiled)]
    private static partial Regex PostIdRegex();

    // Matches message text block
    [GeneratedRegex(@"<div class=""tgme_widget_message_text[^""]*""[^>]*>(.*?)</div>", RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex MessageTextRegex();

    // Extracts each WORD from segments like: СЛОВО XXXX XXXX
    [GeneratedRegex(@"([А-ЯЁ]{2,})\s+\d{4}\s+\d{4}", RegexOptions.Compiled)]
    private static partial Regex WordSegmentRegex();

    // Strip HTML tags
    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    public record ParsedMessage(string PostId, List<string> Words);

    public static List<ParsedMessage> ParseLatestWords(string html)
    {
        var results = new List<ParsedMessage>();

        // Split by message widget blocks
        var messageBlocks = html.Split("tgme_widget_message_wrap", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in messageBlocks)
        {
            var postMatch = PostIdRegex().Match(block);
            if (!postMatch.Success)
                continue;

            var postId = postMatch.Groups[1].Value;

            var textMatch = MessageTextRegex().Match(block);
            if (!textMatch.Success)
                continue;

            var rawText = textMatch.Groups[1].Value;
            var plainText = HtmlTagRegex().Replace(rawText, " ");

            // Only process messages containing НЖТИ
            if (!plainText.Contains("НЖТИ"))
                continue;

            var wordMatches = WordSegmentRegex().Matches(plainText);
            if (wordMatches.Count == 0)
                continue;

            var words = wordMatches.Select(m => m.Groups[1].Value).ToList();
            results.Add(new ParsedMessage(postId, words));
        }

        return results;
    }
}
