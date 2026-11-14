namespace PathfindingVisualiser.Search;

/// <summary>Factory for the algorithms offered in the UI, in display order.</summary>
public static class Algorithms
{
    public static readonly string[] Names =
    {
        "A*",
        "Dijkstra",
        "Greedy best-first",
        "Breadth-first (BFS)",
        "Bidirectional BFS",
        "Depth-first (DFS)",
    };

    public static SearchAlgorithm Create(string name) => name switch
    {
        "A*" => new AStar(),
        "Dijkstra" => new Dijkstra(),
        "Greedy best-first" => new GreedyBestFirst(),
        "Breadth-first (BFS)" => new BreadthFirst(),
        "Bidirectional BFS" => new BidirectionalSearch(),
        "Depth-first (DFS)" => new DepthFirst(),
        _ => throw new ArgumentException($"Unknown algorithm '{name}'."),
    };

    /// <summary>Accepts loose names on the command line, e.g. "astar", "bfs", "bds".</summary>
    public static string Normalise(string loose)
    {
        string key = loose.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("*", "star");
        return key switch
        {
            "astar" or "a" => "A*",
            "dijkstra" => "Dijkstra",
            "greedy" or "greedybestfirst" or "gbfs" => "Greedy best-first",
            "bfs" or "breadthfirst" => "Breadth-first (BFS)",
            "bds" or "bidirectional" or "bidirectionalbfs" => "Bidirectional BFS",
            "dfs" or "depthfirst" => "Depth-first (DFS)",
            _ => throw new ArgumentException($"Unknown algorithm '{loose}'. Try: astar, dijkstra, greedy, bfs, bds, dfs."),
        };
    }
}
