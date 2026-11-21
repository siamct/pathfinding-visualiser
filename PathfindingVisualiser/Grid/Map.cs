namespace PathfindingVisualiser.Grid;

/// <summary>
/// A rectangular grid of cells with a start and an end point.
/// Provides neighbour lookup for the search algorithms.
/// </summary>
public sealed class Map
{
    private static readonly (int dx, int dy)[] Orthogonal = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    private static readonly (int dx, int dy)[] Diagonal = { (1, 1), (1, -1), (-1, 1), (-1, -1) };
    private const double DiagonalFactor = 1.4142135623730951;

    private Cell[,] _cells;

    public Map(int columns, int rows)
    {
        if (columns < 3 || rows < 3)
            throw new ArgumentException("Map must be at least 3 x 3.");

        Columns = columns;
        Rows = rows;
        _cells = new Cell[columns, rows];
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                _cells[x, y] = new Cell(x, y);

        Start = new Point(columns / 10, rows / 5);
        End = new Point(columns - 1 - columns / 10, rows - 1 - rows / 5);
    }

    public int Columns { get; }
    public int Rows { get; }

    public Point Start { get; set; }
    public Point End { get; set; }

    public Cell this[int x, int y] => _cells[x, y];
    public Cell this[Point p] => _cells[p.X, p.Y];

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows;
    public bool InBounds(Point p) => InBounds(p.X, p.Y);

    public bool IsWalkable(int x, int y) => InBounds(x, y) && !_cells[x, y].IsWall;
    public bool IsWalkable(Point p) => IsWalkable(p.X, p.Y);

    public IEnumerable<Cell> AllCells()
    {
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
                yield return _cells[x, y];
    }

    /// <summary>
    /// Walkable neighbours of a cell together with the cost of moving into them.
    /// Diagonal moves are not allowed to cut through the corner of a wall.
    /// </summary>
    public IEnumerable<(Point cell, double moveCost)> Neighbours(Point p, bool allowDiagonal)
    {
        foreach (var (dx, dy) in Orthogonal)
        {
            int nx = p.X + dx, ny = p.Y + dy;
            if (IsWalkable(nx, ny))
                yield return (new Point(nx, ny), _cells[nx, ny].Cost);
        }

        if (!allowDiagonal)
            yield break;

        foreach (var (dx, dy) in Diagonal)
        {
            int nx = p.X + dx, ny = p.Y + dy;
            if (!IsWalkable(nx, ny))
                continue;
            // Both orthogonal cells must be open, otherwise the path would squeeze between two walls.
            if (!IsWalkable(p.X + dx, p.Y) || !IsWalkable(p.X, p.Y + dy))
                continue;
            yield return (new Point(nx, ny), _cells[nx, ny].Cost * DiagonalFactor);
        }
    }

    /// <summary>Cost of a step from <paramref name="from"/> to an adjacent cell <paramref name="to"/>.</summary>
    public double StepCost(Point from, Point to)
    {
        bool diagonal = from.X != to.X && from.Y != to.Y;
        return this[to].Cost * (diagonal ? DiagonalFactor : 1.0);
    }

    /// <summary>Total cost of walking a path, excluding the start cell.</summary>
    public double PathCost(IReadOnlyList<Point> path)
    {
        double total = 0;
        for (int i = 1; i < path.Count; i++)
            total += StepCost(path[i - 1], path[i]);
        return total;
    }

    public void ClearWalls()
    {
        foreach (var c in AllCells())
            c.IsWall = false;
    }

    public void Flatten()
    {
        foreach (var c in AllCells())
            c.Height = 0f;
    }

    /// <summary>Make sure start and end are inside the map and not on a wall.</summary>
    public void EnsureEndpointsValid()
    {
        Start = ClampToMap(Start);
        End = ClampToMap(End);
        this[Start].IsWall = false;
        this[End].IsWall = false;
    }

    private Point ClampToMap(Point p) =>
        new(Math.Clamp(p.X, 0, Columns - 1), Math.Clamp(p.Y, 0, Rows - 1));
}
