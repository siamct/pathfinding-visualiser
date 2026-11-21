using PathfindingVisualiser.Grid;

namespace PathfindingVisualiser.Search;

/// <summary>
/// A* search. Like Dijkstra, but cells are ordered by cost so far plus an
/// estimate of the remaining distance, which pulls the search towards the goal.
/// </summary>
public sealed class AStar : SearchAlgorithm
{
    public override string Name => "A*";
    public override bool UsesWeights => true;
    public override bool IsOptimal => true;

    protected override IEnumerable<Point> Search(Map map, bool allowDiagonal)
    {
        var gScore = new Dictionary<Point, double> { [map.Start] = 0 };
        var parent = new Dictionary<Point, Point>();
        var open = new PriorityQueue<Point, double>();
        open.Enqueue(map.Start, Heuristic(map.Start, map.End, allowDiagonal));
        Frontier.Add(map.Start);

        while (open.TryDequeue(out var current, out _))
        {
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

            double g = gScore[current];
            foreach (var (next, moveCost) in map.Neighbours(current, allowDiagonal))
            {
                if (Visited.Contains(next))
                    continue;

                double tentative = g + moveCost;
                if (tentative < gScore.GetValueOrDefault(next, double.PositiveInfinity))
                {
                    gScore[next] = tentative;
                    parent[next] = current;
                    double f = tentative + Heuristic(next, map.End, allowDiagonal);
                    open.Enqueue(next, f);
                    Frontier.Add(next);
                }
            }
        }
    }
}
