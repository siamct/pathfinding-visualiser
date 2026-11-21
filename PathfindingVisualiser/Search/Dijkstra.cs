using PathfindingVisualiser.Grid;

namespace PathfindingVisualiser.Search;

/// <summary>
/// Dijkstra's algorithm. Always expands the cheapest known cell next, so the
/// first time the end is expanded the path to it is the cheapest possible.
/// </summary>
public sealed class Dijkstra : SearchAlgorithm
{
    public override string Name => "Dijkstra";
    public override bool UsesWeights => true;
    public override bool IsOptimal => true;

    protected override IEnumerable<Point> Search(Map map, bool allowDiagonal)
    {
        var dist = new Dictionary<Point, double> { [map.Start] = 0 };
        var parent = new Dictionary<Point, Point>();
        var queue = new PriorityQueue<Point, double>();
        queue.Enqueue(map.Start, 0);
        Frontier.Add(map.Start);

        while (queue.TryDequeue(out var current, out var d))
        {
            // A cell can be queued more than once with different costs. Skip stale entries.
            if (Visited.Contains(current))
                continue;

            Visited.Add(current);
            Frontier.Remove(current);
            yield return current;

            if (current == map.End)
            {
                BuildPath(parent, map.Start, map.End);
                yield break;
            }

            foreach (var (next, moveCost) in map.Neighbours(current, allowDiagonal))
            {
                if (Visited.Contains(next))
                    continue;

                double nd = d + moveCost;
                if (nd < dist.GetValueOrDefault(next, double.PositiveInfinity))
                {
                    dist[next] = nd;
                    parent[next] = current;
                    queue.Enqueue(next, nd);
                    Frontier.Add(next);
                }
            }
        }
    }
}
