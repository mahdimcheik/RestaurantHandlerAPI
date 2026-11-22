using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Ganss.Xss;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Nettoyage du texte enrichi à l'écriture (ADR-0014). Liste blanche seulement : ce qui n'y
/// figure pas est retiré avec son contenu, pas échappé. Calé sur le HTML sémantique de Quill 2
/// (<c>getSemanticHTML</c>), que produit <c>p-editor</c> côté front.
/// </summary>
public sealed class RichTextSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "strong", "b", "em", "i", "u", "s",
        "h1", "h2", "h3", "ul", "ol", "li", "blockquote", "pre", "code", "a",
    };

    private readonly HtmlSanitizer sanitizer;
    private readonly HtmlParser parser = new();

    public RichTextSanitizer()
    {
        sanitizer = new HtmlSanitizer(
            new HtmlSanitizerOptions
            {
                AllowedTags = AllowedTags,
                AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "href", "class",
                },
                AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "http", "https", "mailto",
                },
                UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
                AllowedCssProperties = new HashSet<string>(),
            }
        );
        // Une ancre sans adresse ou à schéma refusé perd son href : on garde alors le texte seul.
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement element)
                return;

            var classes = element.ClassList.Where(c => c.StartsWith("ql-", StringComparison.Ordinal)).ToArray();
            element.RemoveAttribute("class");
            if (classes.Length > 0)
                element.SetAttribute("class", string.Join(' ', classes));

            if (element.LocalName == "a")
            {
                if (element.HasAttribute("href"))
                {
                    element.SetAttribute("rel", "noopener noreferrer");
                    element.SetAttribute("target", "_blank");
                }
                else
                {
                    e.ReplacementNodes.Add(element.Owner!.CreateTextNode(element.TextContent));
                }
            }
        };
    }

    /// <summary>HTML nettoyé, ou null si rien de lisible ne reste.</summary>
    public string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var clean = sanitizer.Sanitize(html).Trim();
        return TextLength(clean) == 0 ? null : clean;
    }

    /// <summary>Nombre de caractères de texte, balises exclues : c'est ce que borne
    /// <see cref="HardCode.CARD_DESCRIPTION_MAX_LENGTH"/>.</summary>
    public int TextLength(string? html) =>
        string.IsNullOrEmpty(html)
            ? 0
            : (parser.ParseDocument($"<body>{html}</body>").Body?.TextContent.Trim().Length ?? 0);

    /// <summary>Début du texte, sans balises, pour la tuile d'une carte.</summary>
    public string? Excerpt(string? html, int maxLength = 140)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        var text = string.Join(
            ' ',
            (parser.ParseDocument($"<body>{html}</body>").Body?.TextContent ?? "")
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }
}
