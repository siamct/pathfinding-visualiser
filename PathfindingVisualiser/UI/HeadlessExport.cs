using System.Drawing.Imaging;
using PathfindingVisualiser.Generation;
using PathfindingVisualiser.Grid;
using PathfindingVisualiser.Search;

namespace PathfindingVisualiser.UI;

/// <summary>
/// Runs a search without a window and saves the result as a PNG.
/// Usage: --export out.png [algorithm] [terrain|maze|window] [seed]
/// "window" renders the whole main window instead of just the grid.
/// </summary>
public static class HeadlessExport
{
    public static int Run(string[] args)
    {
        try
        {
            string outPath = args[0];

            if (args.Length > 2 && args[2].Equals("window", StringComparison.OrdinalIgnoreCase))
                return ExportWindow(outPath,
                    args.Length > 1 ? Algorithms.Normalise(args[1]) : "A*",
                    args.Length > 3 ? int.Parse(args[3]) : 12345);

            string algorithmName = args.Length > 1 ? Algorithms.Normalise(args[1]) : "A*";
            string kind = args.Length > 2 ? args[2].ToLowerInvariant() : "terrain";
            int seed = args.Length > 3 ? int.Parse(args[3]) : 12345;

            var map = new Map(81, 51);
            if (kind == "maze")
                MazeGenerator.Generate(map, seed);
            else
                TerrainGenerator.Generate(map, seed, 14);

            var search = Algorithms.Create(algorithmName);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var _ in search.Run(map, allowDiagonal: true)) { }
            sw.Stop();

            using var panel = new GridPanel { Map = map, Search = search };
            using var bmp = panel.RenderToBitmap(new Size(81 * 11 + 2, 51 * 11 + 2));
            bmp.Save(outPath, ImageFormat.Png);

            int visited = search.Visited.Count + search.VisitedReverse.Count;
            string path = search.PathFound
                ? $"path {search.Path.Count - 1} steps, cost {map.PathCost(search.Path):0.0}"
                : "no path";
            Console.WriteLine($"{search.Name} on {kind} (seed {seed}): visited {visited}, {path}, {sw.ElapsedMilliseconds} ms -> {outPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Export failed: {ex.Message}");
            return 1;
        }
    }

    private static int ExportWindow(string outPath, string algorithmName, int seed)
    {
        ApplicationConfiguration.Initialize();
        // The window is shown briefly so every control gets laid out, then rendered with DrawToBitmap.
        using var form = new MainForm { StartPosition = FormStartPosition.Manual, Location = new Point(40, 40) };
        form.Show();
        Application.DoEvents();
        form.RunToCompletion(algorithmName, seed);
        Application.DoEvents();

        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));

        // DrawToBitmap on the form alone leaves nested layout panels blank, so draw
        // every child control into the same bitmap at its own position.
        var clientOrigin = form.PointToScreen(Point.Empty);
        var offset = new Point(clientOrigin.X - form.Location.X, clientOrigin.Y - form.Location.Y);
        DrawChildren(form, bmp, offset);

        bmp.Save(outPath, ImageFormat.Png);
        Console.WriteLine($"Window screenshot ({algorithmName}, seed {seed}) -> {outPath}");
        return 0;
    }

    private static void DrawChildren(Control parent, Bitmap target, Point formOffset)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible || child.Width <= 0 || child.Height <= 0)
                continue;
            var screen = child.PointToScreen(Point.Empty);
            var parentScreen = parent.FindForm()!.PointToScreen(Point.Empty);
            var at = new Point(screen.X - parentScreen.X + formOffset.X, screen.Y - parentScreen.Y + formOffset.Y);
            child.DrawToBitmap(target, new Rectangle(at, child.Size));
            DrawChildren(child, target, formOffset);
        }
    }
}
