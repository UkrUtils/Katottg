namespace UkrUtils.Katottg;

/// <summary>
/// Flat address of a codifier object with the codes of all hierarchy levels. A value (DTO): it does not reference
/// <see cref="KatottgDataset"/>, has value equality, and is convenient for JSON and API responses.
/// Obtain it with <see cref="KatottgUnit.ToAddress"/>.
/// </summary>
/// <remarks>
/// Levels that the object does not have are <c>null</c>: Kyiv and Sevastopol have no district or
/// community, Prypiat and Chornobyl have no district or community, a community has no populated place.
/// </remarks>
public sealed record KatottgAddress
{
    /// <summary>Code of the object itself.</summary>
    public required KatottgCode Code { get; init; }

    /// <summary>Category of the object itself.</summary>
    public required KatottgCategory Category { get; init; }

    /// <summary>Name of the object itself.</summary>
    public required string Name { get; init; }

    /// <summary>Full address: "с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим" (village Andriivka, Andriivka community, Bakhchysarai district, AR Crimea).</summary>
    public required string FullName { get; init; }

    /// <summary>See <see cref="KatottgUnit.IsTemporarilyNotInForce"/>.</summary>
    public required bool IsTemporarilyNotInForce { get; init; }

    /// <summary>Region, AR Crimea, or a city with special status (<c>O</c>/<c>K</c>). Always present.</summary>
    public required KatottgAddressPart Region { get; init; }

    /// <summary>District (<c>P</c>).</summary>
    public KatottgAddressPart? District { get; init; }

    /// <summary>Territorial community, hromada (<c>H</c>).</summary>
    public KatottgAddressPart? Community { get; init; }

    /// <summary>Populated place (<c>M</c>/<c>T</c>/<c>X</c>/<c>C</c>/<c>K</c>); for a city district, the city.</summary>
    public KatottgAddressPart? PopulatedPlace { get; init; }

    /// <summary>City district (<c>B</c>).</summary>
    public KatottgAddressPart? CityDistrict { get; init; }
}

/// <summary>One address level: code, category, name.</summary>
public sealed record KatottgAddressPart
{
    /// <summary>Code of the object at this level.</summary>
    public required KatottgCode Code { get; init; }

    /// <summary>Category.</summary>
    public required KatottgCategory Category { get; init; }

    /// <summary>Official name: "Бахчисарайський".</summary>
    public required string Name { get; init; }

    /// <summary>Name with the category abbreviation: "Бахчисарайський р-н".</summary>
    public required string DisplayName { get; init; }

    internal static KatottgAddressPart From(KatottgUnit unit) => new()
    {
        Code = unit.Code,
        Category = unit.Category,
        Name = unit.Name,
        DisplayName = unit.DisplayName,
    };
}
