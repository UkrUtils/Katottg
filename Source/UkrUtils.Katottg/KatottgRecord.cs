namespace UkrUtils.Katottg;

/// <summary>
/// Плаский запис кодифікатора — сировина для побудови <see cref="KatottgDataset"/>.
/// </summary>
/// <param name="Code">Код об’єкта.</param>
/// <param name="ParentCode">Код батьківського об’єкта; <c>null</c> для кореня (області, міста зі спецстатусом).</param>
/// <param name="Category">Категорія об’єкта.</param>
/// <param name="Name">Назва (без пробілів по краях).</param>
public readonly record struct KatottgRecord(KatottgCode Code, KatottgCode? ParentCode, KatottgCategory Category, string Name);
