using System;
using System.Collections.Generic;
using PathfindingVisualiser;

// Quick checks for the search code (no test framework, just run it).
// Run with: dotnet run --project tests
public static class SearchTests
{
    static int failed;

    static void Check(bool ok, string what)
    {
        if (!ok) { failed++; Console.WriteLine("FAIL  " + what); }
    }

    static bool ValidPath(Map map, Search s)
    {
        if (s.Path.Count == 0 || s.Path[0] != map.Start || s.Path[s.Path.Count - 1] != map.End) return false;
        for (int i = 1; i < s.Path.Count; i++)
        {
            Cell a = s.Path[i - 1], b = s.Path[i];
            if (b.Wall) return false;
            bool neighbour = false;
            foreach (var n in map.Neighbours(a)) if (n.Key == b) neighbour = true;
            if (!neighbour) return false;
        }
        return true;
    }

    public static int Main()
    {
        int maps = 0;
        foreach (string kind in new[] { "terrain", "maze" })
            foreach (bool diag in new[] { true, false })
                for (int seed = 1; seed <= 25; seed++)
                {
                    var results = new Dictionary<Algorithm, Search>();
                    foreach (Algorithm a in Enum.GetValues(typeof(Algorithm)))
                    {
                        var map = new Map(61, 41) { AllowDiagonal = diag };
                        if (kind == "maze") MazeGenerator.Generate(map, seed); else TerrainGenerator.Generate(map, seed);
                        map.AllowDiagonal = diag;
                        var s = Search.Create(a, map);
                        s.RunToEnd();
                        results[a] = s;
                        if (s.Found) Check(ValidPath(map, s), kind + " seed " + seed + " " + s.Name + " path is not valid");
                    }
                    string tag = kind + " seed " + seed + (diag ? " diag" : "");
                    var astar = results[Algorithm.AStar];
                    var dijkstra = results[Algorithm.Dijkstra];
                    var bfs = results[Algorithm.BreadthFirst];
                    var bds = results[Algorithm.Bidirectional];

                    // all of them agree on whether there's a path at all
                    foreach (var s in results.Values) Check(s.Found == dijkstra.Found, tag + " " + s.Name + " disagrees on whether a path exists");
                    if (!dijkstra.Found) continue;
                    maps++;

                    // A* must find the same cheapest cost as Dijkstra, and not look at more cells
                    Check(Math.Abs(astar.PathCost() - dijkstra.PathCost()) < 1e-6, tag + " A* cost " + astar.PathCost() + " != Dijkstra " + dijkstra.PathCost());
                    Check(astar.Expanded <= dijkstra.Expanded, tag + " A* expanded more than Dijkstra");
                    // nothing beats Dijkstra on cost
                    foreach (var s in results.Values) Check(s.PathCost() >= dijkstra.PathCost() - 1e-6, tag + " " + s.Name + " cheaper than Dijkstra?");
                    // BFS and bidirectional BFS give the fewest steps
                    if (!diag) Check(bfs.Path.Count == bds.Path.Count, tag + " BFS and bidirectional BFS step counts differ");
                    foreach (var s in results.Values) Check(s.Path.Count >= bfs.Path.Count, tag + " " + s.Name + " fewer steps than BFS?");
                }

        // walled-off end -> no path, and nothing crashes
        var boxed = new Map(21, 15);
        boxed.AddBorder();
        foreach (var n in boxed.Neighbours(boxed.End)) n.Key.Wall = true;
        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (dx != 0 || dy != 0) boxed[boxed.End.X + dx, boxed.End.Y + dy].Wall = true;
        foreach (Algorithm a in Enum.GetValues(typeof(Algorithm)))
        {
            var s = Search.Create(a, boxed);
            s.RunToEnd();
            Check(!s.Found && s.Path.Count == 0, "boxed-in end: " + s.Name + " claims a path");
        }

        // no corner cutting between two diagonal walls
        var corner = new Map(5, 5);
        corner[2, 1].Wall = true;
        corner[1, 2].Wall = true;
        bool cuts = false;
        foreach (var n in corner.Neighbours(corner[1, 1])) if (n.Key == corner[2, 2]) cuts = true;
        Check(!cuts, "diagonal move squeezes between two walls");

        // the heap gives things back in order
        var heap = new MinHeap<int>();
        var rnd = new Random(3);
        var nums = new List<int>();
        for (int i = 0; i < 500; i++) { int v = rnd.Next(1000); nums.Add(v); heap.Push(v, v); }
        nums.Sort();
        foreach (int v in nums) Check(heap.Pop() == v, "heap order");

        Console.WriteLine(failed == 0 ? "all checks passed (" + maps + " solvable maps)" : failed + " checks failed");
        return failed == 0 ? 0 : 1;
    }
}
