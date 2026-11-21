using System;
using System.Collections.Generic;

namespace PathfindingVisualiser
{
    // Classic 2D Perlin noise (Ken Perlin's improved version), seeded so the
    // same seed always gives the same map.
    public class PerlinNoise
    {
        private readonly int[] p = new int[512];

        public PerlinNoise(int seed)
        {
            var rng = new Random(seed);
            int[] perm = new int[256];
            for (int i = 0; i < 256; i++) perm[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = perm[i]; perm[i] = perm[j]; perm[j] = t;
            }
            for (int i = 0; i < 512; i++) p[i] = perm[i & 255];
        }

        private static double Fade(double t) { return t * t * t * (t * (t * 6 - 15) + 10); }
        private static double Lerp(double a, double b, double t) { return a + t * (b - a); }

        private static double Grad(int hash, double x, double y)
        {
            switch (hash & 3)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                default: return -x - y;
            }
        }

        // roughly -1..1
        public double Noise(double x, double y)
        {
            int xi = (int)Math.Floor(x) & 255, yi = (int)Math.Floor(y) & 255;
            double xf = x - Math.Floor(x), yf = y - Math.Floor(y);
            double u = Fade(xf), v = Fade(yf);

            int aa = p[p[xi] + yi], ab = p[p[xi] + yi + 1];
            int ba = p[p[xi + 1] + yi], bb = p[p[xi + 1] + yi + 1];

            double x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            double x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            return Lerp(x1, x2, v);
        }

        // A few layers of noise on top of each other so the hills look natural.
        public double Octaves(double x, double y, int octaves)
        {
            double total = 0, amp = 1, freq = 1, max = 0;
            for (int i = 0; i < octaves; i++)
            {
                total += Noise(x * freq, y * freq) * amp;
                max += amp;
                amp *= 0.5;
                freq *= 2;
            }
            return total / max;
        }
    }

    public static class TerrainGenerator
    {
        // Height from the noise becomes the cost of the cell (1 = flat, 9 = steep).
        // The very top is turned into rock (walls) so there's something to go round.
        public static void Generate(Map map, int seed, double scale = 0.06, double flatLevel = -0.05, double rockLevel = 0.3)
        {
            var noise = new PerlinNoise(seed);
            map.Clear();
            foreach (Cell c in map.AllCells())
            {
                double h = noise.Octaves(c.X * scale, c.Y * scale, 4);   // about -0.6..0.6
                if (h > rockLevel)
                {
                    c.Wall = true;
                    continue;
                }
                // below sea level-ish it's flat (cost 1), above that it gets steeper
                double n = Math.Max(0, (h - flatLevel) / (rockLevel - flatLevel));   // 0..1
                c.Cost = 1 + (int)Math.Round(Math.Pow(n, 0.8) * (Map.MaxCost - 1));
            }
            map.AddBorder();
            map.ResetEnds();
            map.Start.Cost = 1;
            map.End.Cost = 1;
        }
    }

    public static class MazeGenerator
    {
        // Recursive backtracker (done with a stack so big mazes don't overflow).
        // Cells on odd coordinates are rooms, the ones in between are walls that get knocked down.
        public static void Generate(Map map, int seed)
        {
            var rng = new Random(seed);
            map.Clear();
            foreach (Cell c in map.AllCells()) c.Wall = true;

            int w = map.Width, h = map.Height;
            var stack = new Stack<Cell>();
            Cell first = map[1, 1];
            first.Wall = false;
            stack.Push(first);

            int[,] dirs = { { 2, 0 }, { -2, 0 }, { 0, 2 }, { 0, -2 } };
            var options = new List<int>(4);

            while (stack.Count > 0)
            {
                Cell cur = stack.Peek();
                options.Clear();
                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.X + dirs[i, 0], ny = cur.Y + dirs[i, 1];
                    if (nx > 0 && ny > 0 && nx < w - 1 && ny < h - 1 && map[nx, ny].Wall)
                        options.Add(i);
                }
                if (options.Count == 0)
                {
                    stack.Pop();
                    continue;
                }
                int d = options[rng.Next(options.Count)];
                map[cur.X + dirs[d, 0] / 2, cur.Y + dirs[d, 1] / 2].Wall = false;
                Cell next = map[cur.X + dirs[d, 0], cur.Y + dirs[d, 1]];
                next.Wall = false;
                stack.Push(next);
            }

            // put the start and end on room cells (odd coordinates)
            int midY = (h / 2) | 1;
            if (midY >= h - 1) midY -= 2;
            map.Start = map[1, midY];
            int endX = (w - 2) | 1;
            if (endX >= w - 1) endX -= 2;
            map.End = map[endX, midY];
            map.Start.Wall = false;
            map.End.Wall = false;
        }
    }
}
