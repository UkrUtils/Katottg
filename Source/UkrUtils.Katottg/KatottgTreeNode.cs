namespace UkrUtils.Katottg;

/// <summary>
/// Tree node for display (TreeView, a UI tree, nested JSON). Unlike
/// <see cref="KatottgUnit"/>, it contains only the needed branches: the selected objects and their ancestors.
/// </summary>
/// <remarks>
/// The full codifier tree already exists in the objects themselves (<see cref="KatottgDataset.Regions"/> and
/// <see cref="KatottgUnit.Children"/>); it can be walked directly or loaded lazily in a UI.
/// <see cref="KatottgTreeNode"/> is needed when you want to show a <b>part</b> of the tree: search
/// results together with the path to them.
/// </remarks>
public sealed class KatottgTreeNode
{
    private static readonly IReadOnlyList<KatottgTreeNode> s_none = Array.Empty<KatottgTreeNode>().AsReadOnly();

    private KatottgTreeNode(KatottgUnit unit, bool isMatch)
    {
        Unit = unit;
        IsMatch = isMatch;
    }

    /// <summary>Codifier object (code, name, category, <see cref="KatottgUnit.DisplayName"/>…).</summary>
    public KatottgUnit Unit { get; }

    /// <summary>
    /// <c>true</c> — the object was among the selected ones (for example, a search result); <c>false</c> — an ancestor
    /// added for context, or a descendant added via <c>includeDescendants</c>.
    /// </summary>
    public bool IsMatch { get; }

    /// <summary>Child nodes in the order of the official file.</summary>
    public IReadOnlyList<KatottgTreeNode> Children { get; private set; } = s_none;

    /// <summary>This node and all nodes beneath it, depth-first traversal.</summary>
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
    /// Builds a tree from the selected objects and all their ancestors. Roots are regions and cities with
    /// special status; the order is as in the official file; duplicates in the input are ignored.
    /// </summary>
    /// <param name="units">Selected objects, for example search results. They must belong to the same dataset.</param>
    /// <param name="includeDescendants">Also add all descendants of the selected objects (full subtrees).</param>
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
