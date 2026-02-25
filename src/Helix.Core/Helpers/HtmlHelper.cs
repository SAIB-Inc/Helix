using ReverseMarkdown;

namespace Helix.Core.Helpers;

/// <summary>
/// Converts HTML content to Markdown for LLM-friendly consumption.
/// </summary>
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
    /// Converts HTML content to Markdown, preserving structure (headings, links, lists, bold/italic).
    /// </summary>
    public static string ConvertToMarkdown(string html)
    {
        return string.IsNullOrWhiteSpace(html)
            ? string.Empty
            : MarkdownConverter.Convert(html).Trim();
    }
}
