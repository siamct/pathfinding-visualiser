namespace PathfindingVisualiser.Generation;

/// <summary>
/// Classic 2D Perlin noise (Ken Perlin's improved noise, 2002) with a seeded
/// permutation table. Returns values roughly in [-1, 1].
/// </summary>
public sealed class PerlinNoise
{
    private readonly int[] _perm = new int[512];

    public PerlinNoise(int seed)
    {
        var p = Enumerable.Range(0, 256).ToArray();
        var rng = new Random(seed);
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++)
            _perm[i] = p[i & 255];
    }

    public double Noise(double x, double y)
    {
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);

        double u = Fade(xf);
        double v = Fade(yf);

        int aa = _perm[_perm[xi] + yi];
        int ab = _perm[_perm[xi] + yi + 1];
        int ba = _perm[_perm[xi + 1] + yi];
        int bb = _perm[_perm[xi + 1] + yi + 1];

        double x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        double x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v);
    }

    /// <summary>
    /// Several octaves of noise added together (fractal Brownian motion).
    /// Each octave doubles the frequency and multiplies the amplitude by <paramref name="persistence"/>.
    /// </summary>
    public double Fractal(double x, double y, int octaves, double persistence)
    {
        double total = 0, amplitude = 1, frequency = 1, maxValue = 0;
        for (int i = 0; i < octaves; i++)
        {
            total += Noise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2;
        }
        return total / maxValue;
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    private static double Lerp(double a, double b, double t) => a + t * (b - a);

    private static double Grad(int hash, double x, double y)
    {
        // Pick one of four diagonal gradient directions from the hash.
        return (hash & 3) switch
        {
            0 => x + y,
            1 => -x + y,
            2 => x - y,
            _ => -x - y,
        };
    }
}
