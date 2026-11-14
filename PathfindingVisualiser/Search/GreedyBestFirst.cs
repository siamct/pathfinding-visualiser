using PathfindingVisualiser.Grid;

namespace PathfindingVisualiser.Search;

/// <summary>
/// Greedy best-first search. Orders cells purely by their estimated distance
/// to the goal. Very fast and very focused, but it ignores the cost so far,
/// so the path it finds is often not the cheapest.
/// </summary>
public sealed class GreedyBestFirst : SearchAlgorithm
{
    public override string Name => "Greedy best-first";
    public override bool UsesWeights => false;
    public override bool IsOptimal => false;

    protected override IEnumerable<Point> Search(Map map, bool allowDiagonal)
    {
        var parent = new Dictionary<Point, Point>();
        var open = new PriorityQueue<Point, double>();
        var seen = new HashSet<Point> { map.Start };
        open.Enqueue(map.Start, Heuristic(map.Start, map.End, allowDiagonal));
        Frontier.Add(map.Start);

        while (open.TryDequeue(out var current, out _))
        {
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
                open.Enqueue(next, Heuristic(next, map.End, allowDiagonal));
                Frontier.Add(next);
            }
        }
    }
}
