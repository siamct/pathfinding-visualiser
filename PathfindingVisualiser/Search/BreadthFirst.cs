using PathfindingVisualiser.Grid;

namespace PathfindingVisualiser.Search;

/// <summary>
/// Breadth-first search. Expands cells in rings around the start. Finds the
/// path with the fewest steps, but treats every step as the same cost, so on
/// terrain it will happily walk straight over a hill.
/// </summary>
public sealed class BreadthFirst : SearchAlgorithm
{
    public override string Name => "Breadth-first (BFS)";
    public override bool UsesWeights => false;
    public override bool IsOptimal => false; // optimal in steps, not in terrain cost

    protected override IEnumerable<Point> Search(Map map, bool allowDiagonal)
    {
        var parent = new Dictionary<Point, Point>();
        var queue = new Queue<Point>();
        var seen = new HashSet<Point> { map.Start };
        queue.Enqueue(map.Start);
        Frontier.Add(map.Start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            Visited.Add(current);
            Frontier.Remove(current);
            yield return current;

            if (current == map.End)
            {
                BuildPath(parent, map.Start, map.End);
                yield break;
            }

            foreach (var (next, _) in map.Neighbours(current, allowDiagonal))
            {
                if (!seen.Add(next))
                    continue;
                parent[next] = current;
                queue.Enqueue(next);
                Frontier.Add(next);
            }
        }
    }
}
