using System.Text.RegularExpressions;

namespace UVBStoler;

public static partial class MessageParser
{
    // Matches: data-post="uvb76logs/XXXXX"
    [GeneratedRegex(@"data-post=""[^/]+/(\d+)""", RegexOptions.Compiled)]
    private static partial Regex PostIdRegex();

    // Matches message text block
    [GeneratedRegex(@"<div class=""tgme_widget_message_text[^""]*""[^>]*>(.*?)</div>", RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex MessageTextRegex();

    // Extracts WORD from: НЖТИ XXXXX СЛОВО XXXX XXXX
    [GeneratedRegex(@"НЖТИ\s+\d{5}\s+(\S+)\s+\d{4}\s+\d{4}", RegexOptions.Compiled)]
    private static partial Regex WordRegex();

    // Strip HTML tags
    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    public record ParsedMessage(string PostId, string Word);

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

            var wordMatch = WordRegex().Match(plainText);
            if (!wordMatch.Success)
                continue;

            results.Add(new ParsedMessage(postId, wordMatch.Groups[1].Value));
        }

        return results;
    }
}
