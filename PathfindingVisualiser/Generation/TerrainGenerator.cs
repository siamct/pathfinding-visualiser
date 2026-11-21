using PathfindingVisualiser.Grid;

namespace PathfindingVisualiser.Generation;

/// <summary>
/// Fills a map with rolling terrain from Perlin noise. Heights are normalised
/// so the map always uses the full range of movement costs.
/// </summary>
public static class TerrainGenerator
{
    /// <param name="featureSize">Approximate width of a hill in cells. Larger = smoother terrain.</param>
    public static void Generate(Map map, int seed, double featureSize = 14.0)
    {
        var noise = new PerlinNoise(seed);
        double scale = 1.0 / Math.Max(2.0, featureSize);

        var raw = new double[map.Columns, map.Rows];
        double min = double.MaxValue, max = double.MinValue;

        for (int x = 0; x < map.Columns; x++)
        {
            for (int y = 0; y < map.Rows; y++)
            {
                double h = noise.Fractal(x * scale + 0.37, y * scale + 0.11, octaves: 4, persistence: 0.5);
                raw[x, y] = h;
                if (h < min) min = h;
                if (h > max) max = h;
            }
        }

        double range = Math.Max(1e-9, max - min);
        foreach (var cell in map.AllCells())
        {
            double h = (raw[cell.X, cell.Y] - min) / range;
            cell.IsWall = false;
            cell.Height = (float)Shape(h);
        }

        map.EnsureEndpointsValid();
    }

    /// <summary>
    /// Remap normalised noise so roughly the lowest third becomes flat plains
    /// (cost 1) and the rest rises into hills. Without this every cell has a
    /// slightly different cost and the paths all look the same.
    /// </summary>
    private static double Shape(double h)
    {
        const double seaLevel = 0.35;
        if (h <= seaLevel)
            return 0.0;
        double t = (h - seaLevel) / (1.0 - seaLevel);
        return Math.Pow(t, 1.4);
    }
}
