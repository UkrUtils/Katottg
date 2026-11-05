namespace UkrUtils.Katottg;

/// <summary>
/// Пласка адреса об’єкта кодифікатора з кодами всіх рівнів ієрархії. Значення (DTO): не посилається
/// на <see cref="KatottgDataset"/>, має рівність за значенням, зручне для JSON і відповідей API.
/// Отримати: <see cref="KatottgUnit.ToAddress"/>.
/// </summary>
/// <remarks>
/// Рівні, яких в об’єкта немає, дорівнюють <c>null</c>: у Києві та Севастополі немає району й
/// громади, у Прип’яті й Чорнобилі — району й громади, у громади — населеного пункту.
/// </remarks>
public sealed record KatottgAddress
{
    /// <summary>Код самого об’єкта.</summary>
    public required KatottgCode Code { get; init; }

    /// <summary>Категорія самого об’єкта.</summary>
    public required KatottgCategory Category { get; init; }

    /// <summary>Назва самого об’єкта.</summary>
    public required string Name { get; init; }

    /// <summary>Повна адреса: «с. Андріївка, Андріївська громада, Бахчисарайський р-н, АР Крим».</summary>
    public required string FullName { get; init; }

    /// <summary>Див. <see cref="KatottgUnit.IsTemporarilyNotInForce"/>.</summary>
    public required bool IsTemporarilyNotInForce { get; init; }

    /// <summary>Область, АР Крим або місто зі спеціальним статусом (<c>O</c>/<c>K</c>). Завжди є.</summary>
    public required KatottgAddressPart Region { get; init; }

    /// <summary>Район (<c>P</c>).</summary>
    public KatottgAddressPart? District { get; init; }

    /// <summary>Територіальна громада (<c>H</c>).</summary>
    public KatottgAddressPart? Community { get; init; }

    /// <summary>Населений пункт (<c>M</c>/<c>T</c>/<c>X</c>/<c>C</c>/<c>K</c>); для району в місті — місто.</summary>
    public KatottgAddressPart? PopulatedPlace { get; init; }

    /// <summary>Район у місті (<c>B</c>).</summary>
    public KatottgAddressPart? CityDistrict { get; init; }
}

/// <summary>Один рівень адреси: код, категорія, назва.</summary>
public sealed record KatottgAddressPart
{
    /// <summary>Код об’єкта цього рівня.</summary>
    public required KatottgCode Code { get; init; }

    /// <summary>Категорія.</summary>
    public required KatottgCategory Category { get; init; }

    /// <summary>Офіційна назва: «Бахчисарайський».</summary>
    public required string Name { get; init; }

    /// <summary>Назва зі скороченням категорії: «Бахчисарайський р-н».</summary>
    public required string DisplayName { get; init; }

    internal static KatottgAddressPart From(KatottgUnit unit) => new()
    {
        Code = unit.Code,
        Category = unit.Category,
        Name = unit.Name,
        DisplayName = unit.DisplayName,
    };
}
