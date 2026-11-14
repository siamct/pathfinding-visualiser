using PathfindingVisualiser.Grid;

namespace PathfindingVisualiser.Search;

/// <summary>
/// Base class for every search. A search is written as an iterator that yields
/// each cell as it is expanded, which lets the UI animate it one step at a time
/// and also lets the headless exporter run it to completion in a loop.
/// </summary>
public abstract class SearchAlgorithm
{
    public abstract string Name { get; }

    /// <summary>True if the algorithm takes terrain cost into account.</summary>
    public abstract bool UsesWeights { get; }

    /// <summary>True if the algorithm is guaranteed to find the cheapest path (for the costs it considers).</summary>
    public abstract bool IsOptimal { get; }

    /// <summary>Cells that have been expanded, in order.</summary>
    public HashSet<Point> Visited { get; } = new();

    /// <summary>Cells expanded from the goal side (only used by bidirectional search).</summary>
    public HashSet<Point> VisitedReverse { get; } = new();

    /// <summary>Cells discovered but not yet expanded.</summary>
    public HashSet<Point> Frontier { get; } = new();

    /// <summary>The final path from start to end, or empty if none was found.</summary>
    public List<Point> Path { get; } = new();

    public bool Finished { get; protected set; }
    public bool PathFound => Path.Count > 0;

    public void Reset()
    {
        Visited.Clear();
        VisitedReverse.Clear();
        Frontier.Clear();
        Path.Clear();
        Finished = false;
    }

    /// <summary>Run the search, yielding each expanded cell.</summary>
    public IEnumerable<Point> Run(Map map, bool allowDiagonal)
    {
        Reset();
        foreach (var p in Search(map, allowDiagonal))
            yield return p;
        Finished = true;
        Frontier.Clear();
    }

    protected abstract IEnumerable<Point> Search(Map map, bool allowDiagonal);

    /// <summary>Walk the parent links back from <paramref name="end"/> and store the path.</summary>
    protected void BuildPath(Dictionary<Point, Point> parent, Point start, Point end)
    {
        Path.Clear();
        var current = end;
        Path.Add(current);
        while (current != start)
        {
            if (!parent.TryGetValue(current, out current))
            {
                Path.Clear();
                return;
            }
            Path.Add(current);
        }
        Path.Reverse();
    }

    /// <summary>
    /// Octile distance: the shortest possible distance on a grid that allows
    /// diagonal moves. Falls back to Manhattan when diagonals are off.
    /// Multiplied by the minimum cell cost (1) it never overestimates, so A* stays optimal.
    /// </summary>
    protected static double Heuristic(Point a, Point b, bool allowDiagonal)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        if (!allowDiagonal)
            return (dx + dy) * Cell.MinCost;
        int diag = Math.Min(dx, dy);
        int straight = Math.Max(dx, dy) - diag;
        return (straight + diag * 1.4142135623730951) * Cell.MinCost;
    }
}
