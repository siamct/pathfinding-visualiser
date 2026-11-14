using PathfindingVisualiser.UI;

namespace PathfindingVisualiser;

internal static class Program
{
    /// <summary>
    /// Entry point. Normally opens the main window.
    ///
    /// Command line:
    ///   PathfindingVisualiser --export out.png [algorithm] [terrain|maze] [seed]
    /// generates a grid, runs the search to completion and saves a PNG without
    /// opening a window. Used to produce the images in the README.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--export")
        {
            return HeadlessExport.Run(args.Skip(1).ToArray());
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
