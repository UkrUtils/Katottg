namespace UkrUtils.Katottg;

/// <summary>Відомості про редакцію кодифікатора, з якої побудовано набір даних.</summary>
/// <param name="Date">Дата редакції.</param>
/// <param name="Source">Джерело: зазвичай ім’я офіційного файлу (наприклад, <c>katottg-26-11-03.xlsx</c>).</param>
/// <param name="Sha256">SHA-256 вихідного файлу, 64 шістнадцяткові цифри в нижньому регістрі; <c>null</c>, якщо невідомо.</param>
public sealed record KatottgEdition(DateOnly Date, string? Source, string? Sha256)
{
    /// <inheritdoc/>
    public override string ToString() => Source is null ? $"{Date:yyyy-MM-dd}" : $"{Date:yyyy-MM-dd} ({Source})";
}
