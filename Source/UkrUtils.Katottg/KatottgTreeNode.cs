namespace UkrUtils.Katottg;

/// <summary>
/// Вузол дерева для відображення (TreeView, дерево в UI, вкладений JSON). На відміну від
/// <see cref="KatottgUnit"/>, містить лише потрібні гілки: вибрані об’єкти та їхніх предків.
/// </summary>
/// <remarks>
/// Повне дерево кодифікатора вже є в самих об’єктах (<see cref="KatottgDataset.Regions"/> і
/// <see cref="KatottgUnit.Children"/>) — його можна обходити напряму або ліниво підвантажувати в UI.
/// <see cref="KatottgTreeNode"/> потрібен, коли треба показати <b>частину</b> дерева: результати
/// пошуку разом із шляхом до них.
/// </remarks>
public sealed class KatottgTreeNode
{
    private static readonly IReadOnlyList<KatottgTreeNode> s_none = Array.Empty<KatottgTreeNode>().AsReadOnly();

    private KatottgTreeNode(KatottgUnit unit, bool isMatch)
    {
        Unit = unit;
        IsMatch = isMatch;
    }

    /// <summary>Об’єкт кодифікатора (код, назва, категорія, <see cref="KatottgUnit.DisplayName"/>…).</summary>
    public KatottgUnit Unit { get; }

    /// <summary>
    /// <c>true</c> — об’єкт був серед вибраних (наприклад, результат пошуку); <c>false</c> — предок,
    /// доданий для контексту, або нащадок, доданий через <c>includeDescendants</c>.
    /// </summary>
    public bool IsMatch { get; }

    /// <summary>Дочірні вузли в порядку офіційного файлу.</summary>
    public IReadOnlyList<KatottgTreeNode> Children { get; private set; } = s_none;

    /// <summary>Цей вузол і всі вузли під ним, обхід у глибину.</summary>
    public IEnumerable<KatottgTreeNode> DescendantsAndSelf()
    {
        var stack = new Stack<KatottgTreeNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (var i = node.Children.Count - 1; i >= 0; i--)
                stack.Push(node.Children[i]);
        }
    }

    /// <inheritdoc/>
    public override string ToString() => IsMatch ? $"* {Unit}" : Unit.ToString();

    /// <summary>
    /// Будує дерево з вибраних об’єктів та всіх їхніх предків. Корені — області й міста зі
    /// спецстатусом; порядок — як в офіційному файлі; дублікати у вхідних даних ігноруються.
    /// </summary>
    /// <param name="units">Вибрані об’єкти, наприклад результати пошуку. Мають належати одному набору даних.</param>
    /// <param name="includeDescendants">Додати також усіх нащадків вибраних об’єктів (повні піддерева).</param>
    public static IReadOnlyList<KatottgTreeNode> Build(IEnumerable<KatottgUnit> units, bool includeDescendants = false)
    {
        ArgumentNullException.ThrowIfNull(units);

        var matches = new HashSet<KatottgUnit>(ReferenceEqualityComparer.Instance);
        foreach (var unit in units)
            matches.Add(unit);
        if (matches.Count == 0)
            return s_none;

        var included = new HashSet<KatottgUnit>(matches, ReferenceEqualityComparer.Instance);
        foreach (var unit in matches)
        {
            for (var a = unit.Parent; a is not null && included.Add(a); a = a.Parent)
            {
            }
            if (includeDescendants)
            {
                foreach (var d in unit.Descendants())
                    included.Add(d);
            }
        }

        // Батьки завжди мають менший індекс, тож один прохід у порядку файлу будує дерево.
        var ordered = included.OrderBy(u => u.Index).ToList();
        var nodes = new Dictionary<KatottgUnit, KatottgTreeNode>(ordered.Count, ReferenceEqualityComparer.Instance);
        var children = new Dictionary<KatottgTreeNode, List<KatottgTreeNode>>(ReferenceEqualityComparer.Instance);
        var roots = new List<KatottgTreeNode>();
        foreach (var unit in ordered)
        {
            var node = new KatottgTreeNode(unit, matches.Contains(unit));
            nodes.Add(unit, node);
            if (unit.Parent is { } parent && nodes.TryGetValue(parent, out var parentNode))
            {
                if (!children.TryGetValue(parentNode, out var list))
                    children[parentNode] = list = [];
                list.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }
        foreach (var (node, list) in children)
            node.Children = list.AsReadOnly();
        return roots.AsReadOnly();
    }
}
