using ReverseMarkdown;

namespace Helix.Core.Helpers;

public static class HtmlHelper
{
    private static readonly Converter MarkdownConverter = new(new Config
    {
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true,
        UnknownTags = Config.UnknownTagsOption.Bypass
    });

    /// <summary>
    /// Converts HTML content to Markdown for LLM-friendly consumption.
    /// Preserves structure (headings, links, lists, bold/italic) in a compact format.
    /// </summary>
    public static string ConvertToMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        return MarkdownConverter.Convert(html).Trim();
    }
}
