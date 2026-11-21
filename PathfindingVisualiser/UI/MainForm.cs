using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace PathfindingVisualiser
{
    public class MainForm : Form
    {
        private Map map;
        private Search search;
        private readonly GridView view = new GridView();
        private readonly Timer timer = new Timer { Interval = 15 };
        private readonly Random rng = new Random();

        private ComboBox algorithmBox;
        private CheckBox diagonalBox, gridLinesBox;
        private TrackBar speedBar;
        private NumericUpDown columnsBox, rowsBox, seedBox, brushBox;
        private Button runButton, stopButton;
        private ToolStripStatusLabel statusLeft, statusRight;
        private ResultsPanel results;
        private string lastMap = "terrain";

        public MainForm()
        {
            Text = "Pathfinding Visualiser";
            ClientSize = new Size(1180, 760);
            BackColor = Theme.Back;
            MinimumSize = new Size(900, 620);
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;

            // menu
            var menu = new MenuStrip();
            var file = new ToolStripMenuItem("File");
            file.DropDownItems.Add("Save image...", null, (s, e) => SaveImage());
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Exit", null, (s, e) => Close());
            var help = new ToolStripMenuItem("Help");
            help.DropDownItems.Add("Controls", null, (s, e) => MessageBox.Show(this,
                "Left-drag: use the selected draw tool\nRight-drag: erase walls\n\nSpace: run / pause\nRight arrow: one step\nT / M: new terrain / maze\nC: clear search",
                "Controls"));
            menu.Items.Add(file);
            menu.Items.Add(help);

            // status bar
            var status = new StatusStrip();
            statusLeft = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusRight = new ToolStripStatusLabel();
            status.Items.Add(statusLeft);
            status.Items.Add(statusRight);

            var side = BuildSidebar();
            view.Dock = DockStyle.Fill;
            view.MapEdited += (s, e) => ClearSearch();

            Controls.Add(view);
            Controls.Add(side);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;

            timer.Tick += (s, e) => Tick();
            KeyDown += OnKey;
            NewMap("terrain", 11);
        }

        // ------------------------------------------------------------------ sidebar

        // Same layout as the original: plain buttons and number boxes down the left,
        // grid to the right of them on the blue background. Just more of them now.
        private Panel BuildSidebar()
        {
            var side = new Panel { Dock = DockStyle.Left, Width = 170, AutoScroll = true, BackColor = Theme.Back };
            int y = 12;

            // Draw / Erase were the two buttons in the first version; now they're part of a tool list
            Heading(side, "Draw", ref y);
            string[] tools = { "Draw", "Erase", "Start", "End", "Raise", "Lower" };
            Tool[] order = { Tool.Wall, Tool.Erase, Tool.Start, Tool.End, Tool.Raise, Tool.Lower };
            var toolButtons = new List<Button>();
            for (int i = 0; i < tools.Length; i++)
            {
                Tool t = order[i];
                var b = Btn(side, tools[i], 12 + (i % 2) * 78, y + (i / 2) * 29, 75, null);
                toolButtons.Add(b);
                b.Click += (s, e) =>
                {
                    view.Tool = t;
                    foreach (var tb in toolButtons) tb.BackColor = SystemColors.Control;
                    ((Button)s).BackColor = Color.LightSteelBlue;
                };
            }
            toolButtons[0].BackColor = Color.LightSteelBlue;
            y += 3 * 29 + 4;
            brushBox = Number(side, "Brush", 1, 5, 1, ref y);
            brushBox.ValueChanged += (s, e) => view.BrushSize = (int)brushBox.Value;

            Heading(side, "Grid", ref y);
            columnsBox = Number(side, "Width", 15, 151, 41, ref y);
            rowsBox = Number(side, "Height", 11, 101, 27, ref y);
            seedBox = Number(side, "Seed", 0, 99999, 11, ref y);
            Btn(side, "Terrain", 12, y, 75, (s, e) => NewMap("terrain", (int)seedBox.Value));
            Btn(side, "Maze", 90, y, 75, (s, e) => NewMap("maze", (int)seedBox.Value));
            y += 29;
            Btn(side, "Blank", 12, y, 75, (s, e) => NewMap("empty", 0));
            Btn(side, "Random", 90, y, 75, (s, e) =>
            {
                seedBox.Value = rng.Next(100000);
                NewMap(lastMap == "empty" ? "terrain" : lastMap, (int)seedBox.Value);
            });
            y += 29;
            gridLinesBox = new CheckBox { Text = "Grid lines", Checked = true, Location = new Point(12, y), AutoSize = true };
            gridLinesBox.CheckedChanged += (s, e) => { view.GridLines = gridLinesBox.Checked; view.Invalidate(); };
            side.Controls.Add(gridLinesBox);
            y += 26;

            Heading(side, "Search", ref y);
            algorithmBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(12, y), Width = 153 };
            algorithmBox.Items.AddRange(new object[] { "A*", "Dijkstra", "Greedy best-first", "Breadth-first", "Bidirectional BFS", "Depth-first" });
            algorithmBox.SelectedIndex = 0;
            algorithmBox.SelectedIndexChanged += (s, e) => ClearSearch();
            side.Controls.Add(algorithmBox);
            y += 28;
            diagonalBox = new CheckBox { Text = "Diagonal moves", Checked = true, Location = new Point(12, y), AutoSize = true };
            diagonalBox.CheckedChanged += (s, e) => { map.AllowDiagonal = diagonalBox.Checked; ClearSearch(); };
            side.Controls.Add(diagonalBox);
            y += 24;
            side.Controls.Add(new Label { Text = "Speed", Location = new Point(12, y), AutoSize = true });
            y += 16;
            speedBar = new TrackBar { Minimum = 1, Maximum = 10, Value = 6, Location = new Point(6, y), Width = 160, TickStyle = TickStyle.None };
            side.Controls.Add(speedBar);
            y += 46;
            runButton = Btn(side, "Run", 12, y, 49, (s, e) => RunPause());
            Btn(side, "Step", 64, y, 49, (s, e) => StepOnce());
            stopButton = Btn(side, "Stop", 116, y, 49, (s, e) => ClearSearch());
            y += 29;
            Btn(side, "Run all six", 12, y, 153, (s, e) => CompareAll());
            y += 30;
            results = new ResultsPanel { Location = new Point(12, y), Size = new Size(153, 140), BackColor = Theme.Back };
            side.Controls.Add(results);

            return side;
        }

        private static void Heading(Control parent, string text, ref int y)
        {
            parent.Controls.Add(new Label { Text = text, Location = new Point(10, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
            y += 20;
        }

        private static NumericUpDown Number(Control parent, string text, int min, int max, int value, ref int y)
        {
            parent.Controls.Add(new Label { Text = text, Location = new Point(12, y + 3), AutoSize = true });
            var n = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Location = new Point(70, y), Width = 95 };
            parent.Controls.Add(n);
            y += 27;
            return n;
        }

        private static Button Btn(Control parent, string text, int x, int y, int w, EventHandler click)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 23), BackColor = SystemColors.Control, TabStop = false };
            if (click != null) b.Click += click;
            parent.Controls.Add(b);
            return b;
        }

        // ------------------------------------------------------------------ actions

        private void NewMap(string kind, int seed)
        {
            timer.Stop();
            int w = (int)columnsBox.Value, h = (int)rowsBox.Value;
            if (kind == "maze") { w |= 1; h |= 1; }      // mazes need odd sizes
            map = new Map(w, h) { AllowDiagonal = diagonalBox.Checked };
            if (kind == "terrain") TerrainGenerator.Generate(map, seed);
            else if (kind == "maze") MazeGenerator.Generate(map, seed);
            else map.AddBorder();
            lastMap = kind;
            view.Map = map;
            results.Clear();
            ClearSearch();
            statusLeft.Text = kind == "empty" ? "Blank map. Draw some walls." : "New " + kind + " (seed " + seed + "), " + w + " x " + h;
        }

        private void ClearSearch()
        {
            timer.Stop();
            search = null;
            view.Search = null;
            runButton.Text = "Run";
            stopButton.Enabled = false;
            statusRight.Text = "";
            view.Invalidate();
        }

        private void NewSearchIfNeeded()
        {
            if (search == null || search.Finished)
            {
                search = Search.Create((Algorithm)algorithmBox.SelectedIndex, map);
                view.Search = search;
            }
        }

        private void RunPause()
        {
            if (timer.Enabled)
            {
                timer.Stop();
                runButton.Text = "Run";
                return;
            }
            NewSearchIfNeeded();
            timer.Start();
            runButton.Text = "Pause";
            stopButton.Enabled = true;
        }

        private void StepOnce()
        {
            timer.Stop();
            runButton.Text = "Run";
            NewSearchIfNeeded();
            search.Step();
            stopButton.Enabled = true;
            UpdateStats();
            view.Invalidate();
        }

        private void Tick()
        {
            // speed 1 = one cell per tick, 10 = basically instant
            int steps = (int)Math.Pow(2.2, speedBar.Value - 1);
            for (int i = 0; i < steps && search.Step(); i++) { }
            UpdateStats();
            view.Invalidate();
            if (search.Finished)
            {
                timer.Stop();
                runButton.Text = "Run";
                statusLeft.Text = search.Found ? search.Name + ": path found." : search.Name + ": no path, the end is walled off.";
            }
        }

        private void UpdateStats()
        {
            if (search == null) return;
            string s = "Visited: " + search.Expanded;
            if (search.Found)
                s += string.Format("  |  Path: {0} steps, cost {1:0.0}", search.Path.Count - 1, search.PathCost());
            statusRight.Text = s + string.Format("  |  {0:0} ms", search.ElapsedMs);
        }

        private void CompareAll()
        {
            ClearSearch();
            var rows = new List<Search>();
            foreach (Algorithm a in Enum.GetValues(typeof(Algorithm)))
            {
                var s = Search.Create(a, map);
                s.RunToEnd();
                rows.Add(s);
            }
            results.Show(rows);
            search = rows[algorithmBox.SelectedIndex];      // leave the selected one on screen
            view.Search = search;
            UpdateStats();
            view.Invalidate();
            statusLeft.Text = "Ran all six on the same map. Bars = cells visited, orange = cheapest path.";
        }

        private void SaveImage()
        {
            using (var dlg = new SaveFileDialog { Filter = "PNG image|*.png", FileName = "pathfinding.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                using (var bmp = new Bitmap(view.Width, view.Height))
                {
                    using (var g = Graphics.FromImage(bmp))
                        GridView.Render(g, bmp.Size, map, search, view.GridLines);
                    bmp.Save(dlg.FileName, ImageFormat.Png);
                }
                statusLeft.Text = "Saved " + dlg.FileName;
            }
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (ActiveControl is NumericUpDown) return;
            switch (e.KeyCode)
            {
                case Keys.Space: RunPause(); break;
                case Keys.Right: StepOnce(); break;
                case Keys.T: NewMap("terrain", (int)seedBox.Value); break;
                case Keys.M: NewMap("maze", (int)seedBox.Value); break;
                case Keys.C: ClearSearch(); break;
                default: return;
            }
            e.Handled = e.SuppressKeyPress = true;
        }
    }

    // Small bar chart of cells visited for each algorithm, with the cost next to it.
    public class ResultsPanel : Control
    {
        private List<Search> rows;

        public ResultsPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public void Clear() { rows = null; Invalidate(); }
        public void Show(List<Search> r) { rows = r; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            using (var small = new Font("Segoe UI", 7.5f))
            {
                if (rows == null)
                {
                    g.DrawString("Runs every algorithm on\nthis map and compares them.", small, SystemBrushes.ControlText, 0, 2);
                    return;
                }
                int max = 1;
                double best = double.MaxValue;
                foreach (var r in rows)
                {
                    max = Math.Max(max, r.Expanded);
                    if (r.Found) best = Math.Min(best, r.PathCost());
                }
                int rowH = Height / rows.Count;
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    int top = i * rowH;
                    g.DrawString(r.Name, small, SystemBrushes.ControlText, 0, top);
                    string info = r.Found ? string.Format("{0} · {1:0.0}", r.Expanded, r.PathCost()) : "no path";
                    var sz = g.MeasureString(info, small);
                    g.DrawString(info, small, SystemBrushes.ControlText, Width - sz.Width, top);
                    bool cheapest = r.Found && Math.Abs(r.PathCost() - best) < 0.01;
                    using (var bar = new SolidBrush(cheapest ? Theme.Path : Color.FromArgb(110, 140, 200)))
                        g.FillRectangle(bar, 0, top + 14, Math.Max(2, (Width - 2) * r.Expanded / (float)max), 5);
                }
            }
        }
    }
}
