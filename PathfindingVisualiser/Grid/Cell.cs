namespace PathfindingVisualiser.Grid;

/// <summary>
/// One square of the map. Terrain height is kept as a value in [0, 1] and the
/// movement cost is derived from it, so a cell on high ground is more expensive
/// to walk into than one on low ground.
/// </summary>
public sealed class Cell
{
    public const int MinCost = 1;
    public const int MaxCost = 9;

    public Cell(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }

    public bool IsWall { get; set; }

    private float _height;

    /// <summary>Terrain height, clamped to [0, 1].</summary>
    public float Height
    {
        get => _height;
        set => _height = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Cost of stepping into this cell (1 on flat ground, up to 9 on the highest ground).</summary>
    public int Cost => MinCost + (int)Math.Round(Height * (MaxCost - MinCost));

    public Point Location => new(X, Y);
}
