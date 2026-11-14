using System.Diagnostics;
using PathfindingVisualiser.Generation;
using PathfindingVisualiser.Grid;
using PathfindingVisualiser.Search;

namespace PathfindingVisualiser.UI;

public sealed class MainForm : Form
{
    private enum Tool { Wall, Erase, Start, End, Raise, Lower }

    private readonly GridPanel _gridPanel = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _algorithmBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly CheckBox _diagonalBox = new() { Text = "Allow diagonal moves", AutoSize = true, Checked = true };
    private readonly TrackBar _speedBar = new() { Minimum = 1, Maximum = 10, Value = 6, TickFrequency = 1, Width = 180 };
    private readonly Button _runButton = new() { Text = "Run", Width = 56 };
    private readonly Button _stepButton = new() { Text = "Step", Width = 56 };
    private readonly Button _stopButton = new() { Text = "Stop", Width = 56, Enabled = false };

    private readonly NumericUpDown _columnsBox = new() { Minimum = 10, Maximum = 200, Value = 81, Width = 60 };
    private readonly NumericUpDown _rowsBox = new() { Minimum = 10, Maximum = 150, Value = 51, Width = 60 };
    private readonly TrackBar _featureBar = new() { Minimum = 4, Maximum = 40, Value = 14, TickFrequency = 4, Width = 180 };
    private readonly NumericUpDown _brushBox = new() { Minimum = 1, Maximum = 9, Value = 1, Width = 50 };
    private readonly CheckBox _gridLinesBox = new() { Text = "Show grid lines", AutoSize = true, Checked = true };

    private readonly Dictionary<Tool, RadioButton> _toolButtons = new();
    private readonly ToolStripStatusLabel _statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _statsLabel = new();

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Random _rng = new();

    private Map _map;
    private SearchAlgorithm? _search;
    private IEnumerator<Point>? _steps;
    private readonly Stopwatch _stopwatch = new();
    private long _elapsedMs;

    public MainForm()
    {
        Text = "Pathfinding Visualiser";
        MinimumSize = new Size(980, 660);
        ClientSize = new Size(1200, 790);
        StartPosition = FormStartPosition.CenterScreen;

        _map = new Map((int)_columnsBox.Value, (int)_rowsBox.Value);
        TerrainGenerator.Generate(_map, _rng.Next(), _featureBar.Value);

        BuildLayout();
        WireEvents();

        _gridPanel.Map = _map;
        SetStatus("Ready. Draw walls, move the start and end, then press Run.");
        UpdateStats();
    }

    // ---------------------------------------------------------------- layout

    private void BuildLayout()
    {
        var side = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 268,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8),
        };

        side.Controls.Add(BuildAlgorithmGroup());
        side.Controls.Add(BuildMapGroup());
        side.Controls.Add(BuildToolGroup());
        side.Controls.Add(BuildLegend());

        var status = new StatusStrip();
        status.Items.Add(_statusLabel);
        status.Items.Add(_statsLabel);

        Controls.Add(_gridPanel);
        Controls.Add(side);
        Controls.Add(status);

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("Save &image...", null, (_, _) => SaveImage());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("E&xit", null, (_, _) => Close());
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add("&Controls", null, (_, _) => ShowHelp());
        menu.Items.Add(file);
        menu.Items.Add(help);
        MainMenuStrip = menu;
        Controls.Add(menu);
    }

    private GroupBox BuildAlgorithmGroup()
    {
        var group = NewGroup("Search");
        var table = NewTable();

        _algorithmBox.Items.AddRange(Algorithms.Names);
        _algorithmBox.SelectedIndex = 0;

        table.Controls.Add(new Label { Text = "Algorithm", AutoSize = true }, 0, 0);
        table.Controls.Add(_algorithmBox, 0, 1);
        table.Controls.Add(_diagonalBox, 0, 2);
        table.Controls.Add(new Label { Text = "Animation speed", AutoSize = true }, 0, 3);
        table.Controls.Add(_speedBar, 0, 4);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange(new Control[] { _runButton, _stepButton, _stopButton });
        table.Controls.Add(buttons, 0, 5);

        return Finish(group, table);
    }

    private GroupBox BuildMapGroup()
    {
        var group = NewGroup("Map");
        var table = NewTable();

        var size = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        size.Controls.Add(new Label { Text = "Columns", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        size.Controls.Add(_columnsBox);
        size.Controls.Add(new Label { Text = "Rows", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        size.Controls.Add(_rowsBox);
        table.Controls.Add(size, 0, 0);

        table.Controls.Add(new Label { Text = "Terrain feature size", AutoSize = true }, 0, 1);
        table.Controls.Add(_featureBar, 0, 2);

        var gen = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var terrainButton = new Button { Text = "New terrain", Width = 90 };
        var mazeButton = new Button { Text = "New maze", Width = 90 };
        terrainButton.Click += (_, _) => NewTerrain();
        mazeButton.Click += (_, _) => NewMaze();
        gen.Controls.AddRange(new Control[] { terrainButton, mazeButton });
        table.Controls.Add(gen, 0, 3);

        var clear = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var clearPathButton = new Button { Text = "Clear search", Width = 90 };
        var clearAllButton = new Button { Text = "Blank map", Width = 90 };
        clearPathButton.Click += (_, _) => ClearSearch();
        clearAllButton.Click += (_, _) => BlankMap();
        clear.Controls.AddRange(new Control[] { clearPathButton, clearAllButton });
        table.Controls.Add(clear, 0, 4);

        table.Controls.Add(_gridLinesBox, 0, 5);

        return Finish(group, table);
    }

    private GroupBox BuildToolGroup()
    {
        var group = NewGroup("Draw");
        var table = NewTable();

        var tools = new (Tool tool, string text)[]
        {
            (Tool.Wall, "Wall"),
            (Tool.Erase, "Erase wall"),
            (Tool.Start, "Move start"),
            (Tool.End, "Move end"),
            (Tool.Raise, "Raise ground"),
            (Tool.Lower, "Lower ground"),
        };

        int row = 0;
        foreach (var (tool, text) in tools)
        {
            var rb = new RadioButton { Text = text, AutoSize = true, Checked = tool == Tool.Wall };
            _toolButtons[tool] = rb;
            table.Controls.Add(rb, 0, row++);
        }

        var brush = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        brush.Controls.Add(new Label { Text = "Brush size", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        brush.Controls.Add(_brushBox);
        table.Controls.Add(brush, 0, row++);

        table.Controls.Add(new Label
        {
            Text = "Left-drag applies the tool.\nRight-drag erases walls.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        }, 0, row);

        return Finish(group, table);
    }

    private GroupBox BuildLegend()
    {
        var group = NewGroup("Legend");
        var table = NewTable();

        var entries = new (Color colour, string text)[]
        {
            (GridPanel.HeightColour(0.05f), "Low ground (cost 1)"),
            (GridPanel.HeightColour(0.95f), "High ground (cost 9)"),
            (Color.FromArgb(45, 45, 48), "Wall"),
            (Color.FromArgb(46, 160, 67), "Start"),
            (Color.FromArgb(210, 50, 50), "End"),
            (Color.FromArgb(120, 160, 215), "Visited"),
            (Color.FromArgb(230, 205, 90), "Frontier"),
            (Color.FromArgb(240, 120, 20), "Path"),
        };

        int row = 0;
        foreach (var (colour, text) in entries)
        {
            var line = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
            line.Controls.Add(new Panel { BackColor = colour, Size = new Size(14, 14), Margin = new Padding(3, 4, 6, 0) });
            line.Controls.Add(new Label { Text = text, AutoSize = true, Padding = new Padding(0, 3, 0, 0) });
            table.Controls.Add(line, 0, row++);
        }

        return Finish(group, table);
    }

    private const int SideGroupWidth = 238;

    private static GroupBox NewGroup(string title) => new()
    {
        Text = title,
        Width = SideGroupWidth,
        Margin = new Padding(3, 3, 3, 8),
    };

    private static TableLayoutPanel NewTable() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 1,
        Location = new Point(6, 18),
        Padding = new Padding(2),
    };

    /// <summary>Put the table in the group and size the group to fit it.</summary>
    private static GroupBox Finish(GroupBox group, TableLayoutPanel table)
    {
        group.Controls.Add(table);
        group.Height = table.PreferredSize.Height + table.Top + 8;
        return group;
    }

    // ---------------------------------------------------------------- events

    private void WireEvents()
    {
        _runButton.Click += (_, _) => Run();
        _stepButton.Click += (_, _) => Step();
        _stopButton.Click += (_, _) => Stop();
        _timer.Tick += (_, _) => Advance(StepsPerTick());
        _gridPanel.CellPainted += OnCellPainted;
        _gridLinesBox.CheckedChanged += (_, _) =>
        {
            _gridPanel.ShowGridLines = _gridLinesBox.Checked;
            _gridPanel.Invalidate();
        };
        _algorithmBox.SelectedIndexChanged += (_, _) => ClearSearch();
        _diagonalBox.CheckedChanged += (_, _) => ClearSearch();
        KeyPreview = true;
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Space: if (_timer.Enabled) Stop(); else Run(); e.Handled = true; break;
            case Keys.Right: Step(); e.Handled = true; break;
            case Keys.Escape: Stop(); break;
            case Keys.C: ClearSearch(); break;
            case Keys.T: NewTerrain(); break;
            case Keys.M: NewMaze(); break;
            case Keys.D1: _toolButtons[Tool.Wall].Checked = true; break;
            case Keys.D2: _toolButtons[Tool.Erase].Checked = true; break;
            case Keys.D3: _toolButtons[Tool.Start].Checked = true; break;
            case Keys.D4: _toolButtons[Tool.End].Checked = true; break;
            case Keys.D5: _toolButtons[Tool.Raise].Checked = true; break;
            case Keys.D6: _toolButtons[Tool.Lower].Checked = true; break;
        }
    }

    private Tool CurrentTool => _toolButtons.First(kv => kv.Value.Checked).Key;

    private void OnCellPainted(Point cell, MouseButtons button)
    {
        if (_timer.Enabled)
            return; // no editing while the animation runs

        var tool = button == MouseButtons.Right ? Tool.Erase : CurrentTool;
        int radius = (int)_brushBox.Value - 1;

        switch (tool)
        {
            case Tool.Start:
                if (!_map[cell].IsWall && cell != _map.End) _map.Start = cell;
                break;
            case Tool.End:
                if (!_map[cell].IsWall && cell != _map.Start) _map.End = cell;
                break;
            default:
                foreach (var c in Brush(cell, radius))
                {
                    if (c == _map.Start || c == _map.End) continue;
                    switch (tool)
                    {
                        case Tool.Wall: _map[c].IsWall = true; break;
                        case Tool.Erase: _map[c].IsWall = false; break;
                        case Tool.Raise: _map[c].Height += 0.12f; break;
                        case Tool.Lower: _map[c].Height -= 0.12f; break;
                    }
                }
                break;
        }

        // Any edit invalidates a finished search result.
        if (_search != null && _search.Finished)
            ClearSearch();
        _gridPanel.Invalidate();
    }

    private IEnumerable<Point> Brush(Point centre, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > radius * radius + radius) continue;
                var p = new Point(centre.X + dx, centre.Y + dy);
                if (_map.InBounds(p)) yield return p;
            }
    }

    // ---------------------------------------------------------------- map actions

    private void NewTerrain()
    {
        Stop();
        _map = new Map((int)_columnsBox.Value, (int)_rowsBox.Value);
        TerrainGenerator.Generate(_map, _rng.Next(), _featureBar.Value);
        _gridPanel.Map = _map;
        ClearSearch();
        SetStatus("Generated Perlin-noise terrain.");
    }

    private void NewMaze()
    {
        Stop();
        // Mazes carve on odd coordinates, so round the size up to odd.
        int cols = (int)_columnsBox.Value | 1;
        int rows = (int)_rowsBox.Value | 1;
        _columnsBox.Value = Math.Min(cols, _columnsBox.Maximum);
        _rowsBox.Value = Math.Min(rows, _rowsBox.Maximum);
        _map = new Map((int)_columnsBox.Value, (int)_rowsBox.Value);
        MazeGenerator.Generate(_map, _rng.Next());
        _gridPanel.Map = _map;
        ClearSearch();
        SetStatus("Generated maze (recursive backtracker).");
    }

    private void BlankMap()
    {
        Stop();
        _map = new Map((int)_columnsBox.Value, (int)_rowsBox.Value);
        _gridPanel.Map = _map;
        ClearSearch();
        SetStatus("Blank map. Draw walls or raise ground, then press Run.");
    }

    private void ClearSearch()
    {
        Stop();
        _search = null;
        _steps = null;
        _elapsedMs = 0;
        _gridPanel.Search = null;
        UpdateStats();
    }

    // ---------------------------------------------------------------- running

    private void PrepareSearch()
    {
        if (_search != null && !_search.Finished && _steps != null)
            return;

        _map.EnsureEndpointsValid();
        _search = Algorithms.Create((string)_algorithmBox.SelectedItem!);
        _steps = _search.Run(_map, _diagonalBox.Checked).GetEnumerator();
        _gridPanel.Search = _search;
        _elapsedMs = 0;
        _stopwatch.Restart();
    }

    private void Run()
    {
        PrepareSearch();
        if (_search!.Finished) return;
        _timer.Start();
        _runButton.Enabled = false;
        _stopButton.Enabled = true;
        SetStatus($"Running {_search.Name}...");
    }

    private void Step()
    {
        if (_timer.Enabled) return;
        PrepareSearch();
        Advance(1);
    }

    private void Stop()
    {
        _timer.Stop();
        _runButton.Enabled = true;
        _stopButton.Enabled = false;
        if (_search != null && !_search.Finished)
            SetStatus("Paused. Press Run or Step to continue.");
    }

    private int StepsPerTick()
    {
        // 1..10 -> 1, 2, 4, 7, 11, 16, 25, 40, 70, 150 cells per frame.
        int[] table = { 1, 2, 4, 7, 11, 16, 25, 40, 70, 150 };
        return table[_speedBar.Value - 1];
    }

    private void Advance(int steps)
    {
        if (_search == null || _steps == null) return;

        _stopwatch.Start();
        bool more = true;
        for (int i = 0; i < steps && more; i++)
            more = _steps.MoveNext();
        _stopwatch.Stop();
        _elapsedMs = _stopwatch.ElapsedMilliseconds;

        _gridPanel.Invalidate();
        UpdateStats();

        if (!more)
        {
            _timer.Stop();
            _runButton.Enabled = true;
            _stopButton.Enabled = false;
            SetStatus(_search.PathFound
                ? $"{_search.Name}: path found."
                : $"{_search.Name}: no path exists between start and end.");
        }
    }

    private void UpdateStats()
    {
        if (_search == null)
        {
            _statsLabel.Text = $"{_map.Columns} x {_map.Rows} cells";
            return;
        }

        int visited = _search.Visited.Count + _search.VisitedReverse.Count;
        string path = _search.PathFound
            ? $"  |  Path: {_search.Path.Count - 1} steps, cost {_map.PathCost(_search.Path):0.0}"
            : "";
        _statsLabel.Text = $"Visited: {visited}{path}  |  {_elapsedMs} ms";
    }

    private void SetStatus(string text) => _statusLabel.Text = text;

    /// <summary>Used by the exporter: pick an algorithm and run it to completion on the current map.</summary>
    internal void RunToCompletion(string algorithmName, int seed)
    {
        _map = new Map((int)_columnsBox.Value, (int)_rowsBox.Value);
        TerrainGenerator.Generate(_map, seed, _featureBar.Value);
        _gridPanel.Map = _map;
        _algorithmBox.SelectedItem = algorithmName;
        ClearSearch();
        PrepareSearch();
        Advance(int.MaxValue);
    }

    // ---------------------------------------------------------------- misc

    private void SaveImage()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "PNG image|*.png",
            FileName = "pathfinding.png",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        using var bmp = _gridPanel.RenderToBitmap(_gridPanel.ClientSize);
        bmp.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
        SetStatus($"Saved {dialog.FileName}");
    }

    private void ShowHelp()
    {
        MessageBox.Show(this,
            "Mouse\n" +
            "  Left-drag: apply the selected draw tool\n" +
            "  Right-drag: erase walls\n\n" +
            "Keyboard\n" +
            "  Space: run / pause     Right arrow: single step\n" +
            "  C: clear search        T: new terrain     M: new maze\n" +
            "  1-6: select draw tool\n\n" +
            "Costs\n" +
            "  Entering a cell costs 1 (low ground) to 9 (high ground).\n" +
            "  Diagonal steps cost 1.41x. A* and Dijkstra use these costs;\n" +
            "  BFS, DFS, bidirectional and greedy search ignore them.",
            "Controls", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
