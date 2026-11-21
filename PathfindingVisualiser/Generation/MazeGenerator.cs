using PathfindingVisualiser.Grid;

namespace PathfindingVisualiser.Generation;

/// <summary>
/// Perfect maze using recursive backtracking (iterative, with an explicit stack).
/// Passages are carved on odd coordinates, so the maze looks best on grids with
/// odd dimensions. Terrain is flattened so every open cell costs the same.
/// </summary>
public static class MazeGenerator
{
    public static void Generate(Map map, int seed)
    {
        var rng = new Random(seed);

        foreach (var c in map.AllCells())
        {
            c.IsWall = true;
            c.Height = 0f;
        }

        var stack = new Stack<Point>();
        var startCell = new Point(1, 1);
        map[startCell].IsWall = false;
        stack.Push(startCell);

        var directions = new (int dx, int dy)[] { (2, 0), (-2, 0), (0, 2), (0, -2) };

        while (stack.Count > 0)
        {
            var current = stack.Peek();

            // Shuffle the directions and pick the first unvisited neighbour two cells away.
            Shuffle(directions, rng);
            bool moved = false;
            foreach (var (dx, dy) in directions)
            {
                int nx = current.X + dx, ny = current.Y + dy;
                if (nx <= 0 || ny <= 0 || nx >= map.Columns - 1 || ny >= map.Rows - 1)
                    continue;
                if (!map[nx, ny].IsWall)
                    continue;

                // Knock down the wall between the two cells, then move on.
                map[current.X + dx / 2, current.Y + dy / 2].IsWall = false;
                map[nx, ny].IsWall = false;
                stack.Push(new Point(nx, ny));
                moved = true;
                break;
            }

            if (!moved)
                stack.Pop();
        }

        // Start top-left, end bottom-right on the nearest carved cell.
        map.Start = startCell;
        map.End = NearestOpen(map, new Point(map.Columns - 2, map.Rows - 2));
        map.EnsureEndpointsValid();
    }

    private static Point NearestOpen(Map map, Point target)
    {
        if (map.IsWalkable(target))
            return target;

        for (int radius = 1; radius < Math.Max(map.Columns, map.Rows); radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var p = new Point(target.X + dx, target.Y + dy);
                    if (map.IsWalkable(p))
                        return p;
                }
            }
        }
        return target;
    }

    private static void Shuffle<T>(T[] items, Random rng)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
