using System.Text.RegularExpressions;

using Netgum.Domain.Abstractions;

namespace Netgum.Domain.ValueObjects;

public sealed partial record Slug
{
    private Slug(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Slug Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Slug is required.");
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = SlugUnsafeCharacters().Replace(normalized, "-").Trim('-');
        normalized = RepeatedHyphen().Replace(normalized, "-");

        if (normalized.Length is < 3 or > 80)
        {
            throw new DomainException("Slug must be between 3 and 80 characters.");
        }

        return new Slug(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("[^a-z0-9-]+", RegexOptions.Compiled)]
    private static partial Regex SlugUnsafeCharacters();

    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex RepeatedHyphen();
}
