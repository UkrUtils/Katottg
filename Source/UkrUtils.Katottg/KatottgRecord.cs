namespace UkrUtils.Katottg;

/// <summary>
/// Flat codifier record — the raw material for building a <see cref="KatottgDataset"/>.
/// </summary>
/// <param name="Code">Object code.</param>
/// <param name="ParentCode">Code of the parent object; <c>null</c> for a root (region, city with special status).</param>
/// <param name="Category">Object category.</param>
/// <param name="Name">Name (without surrounding whitespace).</param>
public readonly record struct KatottgRecord(KatottgCode Code, KatottgCode? ParentCode, KatottgCategory Category, string Name);
