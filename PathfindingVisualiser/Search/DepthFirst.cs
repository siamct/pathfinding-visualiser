using PathfindingVisualiser.Grid;

namespace PathfindingVisualiser.Search;

/// <summary>
/// Depth-first search. Follows one branch as far as it can before backing up.
/// Included mainly for comparison: it finds a path if one exists, but usually
/// a long and winding one.
/// </summary>
public sealed class DepthFirst : SearchAlgorithm
{
    public override string Name => "Depth-first (DFS)";
    public override bool UsesWeights => false;
    public override bool IsOptimal => false;

    protected override IEnumerable<Point> Search(Map map, bool allowDiagonal)
    {
        var parent = new Dictionary<Point, Point>();
        var stack = new Stack<Point>();
        stack.Push(map.Start);
        Frontier.Add(map.Start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!Visited.Add(current))
                continue;

            Frontier.Remove(current);
            yield return current;

            if (current == map.End)
            {
                BuildPath(parent, map.Start, map.End);
                yield break;
            }

            // Push in reverse so the first neighbour listed is explored first.
            foreach (var (next, _) in map.Neighbours(current, allowDiagonal).Reverse())
            {
                if (Visited.Contains(next))
                    continue;
                if (!parent.ContainsKey(next))
                    parent[next] = current;
                stack.Push(next);
                Frontier.Add(next);
            }
        }
    }
}
