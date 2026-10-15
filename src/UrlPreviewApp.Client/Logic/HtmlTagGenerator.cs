using System.Text;
using UrlPreviewApp.Client.Models;

namespace UrlPreviewApp.Client.Logic;

/// <summary>
/// Rebuilds the metadata-related markup of a page's <c>&lt;head&gt;</c> from the raw head elements,
/// preserving the original attributes (<c>property</c> vs <c>name</c>, repeated tags, order).
/// </summary>
public static class HtmlTagGenerator
{
    private static readonly HashSet<string> IncludedTags = new(StringComparer.OrdinalIgnoreCase) { "meta", "link", "base" };

    public static string Generate(PageMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(metadata.Title))
            lines.Add($"<title>{Escape(metadata.Title)}</title>");

        foreach (var tag in metadata.HeadTags.Where(t => IncludedTags.Contains(t.Name)))
        {
            var builder = new StringBuilder("<").Append(tag.Name.ToLowerInvariant());
            foreach (var (name, value) in tag.Attributes)
            {
                builder.Append(' ').Append(name);
                if (value is not null)
                    builder.Append("=\"").Append(Escape(value)).Append('"');
            }

            lines.Add(builder.Append('>').ToString());
        }

        return string.Join('\n', lines);
    }

    private static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }
}
