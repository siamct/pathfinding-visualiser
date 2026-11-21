using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace PathfindingVisualiser
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // PathfindingVisualiser --export out.png astar terrain 2024
            // PathfindingVisualiser --stats terrain 2024
            if (args.Length > 0 && args[0] == "--export") return Export(args);
            if (args.Length > 0 && args[0] == "--stats") return Stats(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static Map MakeMap(string kind, int seed)
        {
            var map = new Map(81, 51);
            if (kind == "maze") MazeGenerator.Generate(map, seed);
            else TerrainGenerator.Generate(map, seed);
            return map;
        }

        private static Algorithm Parse(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "astar": return Algorithm.AStar;
                case "dijkstra": return Algorithm.Dijkstra;
                case "greedy": return Algorithm.Greedy;
                case "bfs": return Algorithm.BreadthFirst;
                case "bds": return Algorithm.Bidirectional;
                case "dfs": return Algorithm.DepthFirst;
            }
            throw new ArgumentException("unknown algorithm " + name);
        }

        private static int Export(string[] a)
        {
            if (a.Length < 5) { Console.WriteLine("usage: --export out.png astar|dijkstra|greedy|bfs|bds|dfs terrain|maze seed"); return 1; }
            var map = MakeMap(a[3], int.Parse(a[4]));
            var search = Search.Create(Parse(a[2]), map);
            search.RunToEnd();
            using (var bmp = new Bitmap(980, 620))
            {
                using (var g = Graphics.FromImage(bmp))
                    GridView.Render(g, bmp.Size, map, search);
                bmp.Save(a[1], ImageFormat.Png);
            }
            Console.WriteLine("{0}: visited {1}, steps {2}, cost {3:0.0}", search.Name, search.Expanded, search.Path.Count - 1, search.PathCost());
            return 0;
        }

        private static int Stats(string[] a)
        {
            string kind = a.Length > 1 ? a[1] : "terrain";
            int seed = a.Length > 2 ? int.Parse(a[2]) : 2024;
            Console.WriteLine("| Algorithm | Cells visited | Path steps | Path cost |");
            Console.WriteLine("|---|---|---|---|");
            foreach (Algorithm alg in Enum.GetValues(typeof(Algorithm)))
            {
                var s = Search.Create(alg, MakeMap(kind, seed));
                s.RunToEnd();
                Console.WriteLine("| {0} | {1:N0} | {2} | {3:0.0} |", s.Name, s.Expanded, s.Path.Count - 1, s.PathCost());
            }
            return 0;
        }
    }
}
