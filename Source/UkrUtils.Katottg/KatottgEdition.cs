namespace UkrUtils.Katottg;

/// <summary>Information about the codifier edition the dataset was built from.</summary>
/// <param name="Date">Edition date.</param>
/// <param name="Source">Source: usually the name of the official file (for example, <c>katottg-26-11-03.xlsx</c>).</param>
/// <param name="Sha256">SHA-256 of the source file, 64 lowercase hexadecimal digits; <c>null</c> if unknown.</param>
public sealed record KatottgEdition(DateOnly Date, string? Source, string? Sha256)
{
    /// <inheritdoc/>
    public override string ToString() => Source is null ? $"{Date:yyyy-MM-dd}" : $"{Date:yyyy-MM-dd} ({Source})";
}
