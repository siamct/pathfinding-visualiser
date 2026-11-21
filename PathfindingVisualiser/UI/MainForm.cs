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
        private readonly Timer timer = new Timer();
        private readonly Random rng = new Random();

        private ComboBox algorithmBox;
        private NumericUpDown widthBox, heightBox, seedBox, brushBox;
        private CheckBox diagonalBox;
        private TrackBar speedBar;
        private Button runButton;
        private Label statusLabel, statsLabel;
        private readonly List<Button> toolButtons = new List<Button>();
        private ResultsPanel results;
        private string lastMap = "terrain";

        private static readonly string[] AlgorithmNames =
            { "A*", "Dijkstra", "Greedy best-first", "Breadth-first", "Bidirectional BFS", "Depth-first" };

        public MainForm()
        {
            Text = "Pathfinding Visualiser";
            ClientSize = new Size(1300, 860);
            MinimumSize = new Size(900, 600);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;

            BuildSidebar();

            statusLabel = new Label
            {
                Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.Panel, ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0)
            };
            view.Dock = DockStyle.Fill;
            view.MapEdited += (s, e) => ClearSearch();
            Controls.Add(view);
            Controls.Add(statusLabel);
            view.BringToFront();

            timer.Tick += (s, e) => Tick();
            KeyDown += OnKey;

            NewMap("terrain", 11);
        }

        // ------------------------------------------------------------------ sidebar

        private int y;
        private Panel side;

        private void BuildSidebar()
        {
            side = new Panel { Dock = DockStyle.Left, Width = 270, BackColor = Theme.Panel, AutoScroll = true };
            Controls.Add(side);
            y = 18;

            var title = new Label { Text = "Pathfinding Visualiser", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = true, Location = new Point(16, y) };
            side.Controls.Add(title);
            y += 30;
            AddNote("started as my A-level NEA grid");
            y += 8;

            Section("MAP");
            // the two size boxes are what numericUpDown1/2 were for in the NEA version
            side.Controls.Add(new Label { Text = "Size", Location = new Point(16, y + 4), AutoSize = true, ForeColor = Theme.Muted });
            widthBox = SmallNumber(15, 151, 81, 96);
            side.Controls.Add(new Label { Text = "x", Location = new Point(164, y + 4), AutoSize = true, ForeColor = Theme.Muted });
            heightBox = SmallNumber(11, 101, 51, 180);
            y += 30;
            side.Controls.Add(new Label { Text = "Seed", Location = new Point(16, y + 4), AutoSize = true, ForeColor = Theme.Muted });
            seedBox = SmallNumber(0, 99999, 11, 96);
            var dice = MakeButton("Random");
            dice.Size = new Size(70, 24);
            dice.Location = new Point(180, y - 1);
            dice.Click += (s, e) => { seedBox.Value = rng.Next(100000); NewMap(lastMap == "empty" ? "terrain" : lastMap, (int)seedBox.Value); };
            side.Controls.Add(dice);
            y += 32;
            AddButtonRow(new[] { "Terrain", "Maze", "Empty" }, new EventHandler[]
            {
                (s, e) => NewMap("terrain", (int)seedBox.Value),
                (s, e) => NewMap("maze", (int)seedBox.Value),
                (s, e) => NewMap("empty", 0)
            });

            Section("DRAW");
            string[] tools = { "Wall", "Erase", "Raise", "Lower", "Start", "End" };
            for (int i = 0; i < tools.Length; i++)
            {
                int index = i;
                var b = MakeButton(tools[i]);
                b.Size = new Size(74, 28);
                b.Location = new Point(16 + (i % 3) * 80, y + (i / 3) * 34);
                b.Click += (s, e) => SelectTool((Tool)index);
                toolButtons.Add(b);
                side.Controls.Add(b);
            }
            y += 72;
            brushBox = AddNumber("Brush size", 1, 5, 1);
            brushBox.ValueChanged += (s, e) => view.BrushSize = (int)brushBox.Value;
            AddNote("left drag = tool, right drag = erase");
            SelectTool(Tool.Wall);

            Section("SEARCH");
            algorithmBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(16, y), Width = 234, FlatStyle = FlatStyle.Flat, BackColor = Theme.PanelLight, ForeColor = Theme.Text };
            algorithmBox.Items.AddRange(AlgorithmNames);
            algorithmBox.SelectedIndex = 0;
            algorithmBox.SelectedIndexChanged += (s, e) => ClearSearch();
            side.Controls.Add(algorithmBox);
            y += 32;

            diagonalBox = new CheckBox { Text = "Diagonal moves", Checked = true, Location = new Point(16, y), AutoSize = true, ForeColor = Theme.Text };
            diagonalBox.CheckedChanged += (s, e) => { map.AllowDiagonal = diagonalBox.Checked; ClearSearch(); };
            side.Controls.Add(diagonalBox);
            y += 28;

            var speedLabel = new Label { Text = "Speed", Location = new Point(16, y + 4), AutoSize = true, ForeColor = Theme.Muted };
            side.Controls.Add(speedLabel);
            speedBar = new TrackBar { Minimum = 1, Maximum = 10, Value = 6, TickStyle = TickStyle.None, Location = new Point(70, y), Width = 180, BackColor = Theme.Panel };
            side.Controls.Add(speedBar);
            y += 36;

            runButton = MakeButton("Run", true);
            runButton.Size = new Size(114, 34);
            runButton.Location = new Point(16, y);
            runButton.Click += (s, e) => RunPause();
            side.Controls.Add(runButton);
            var step = MakeButton("Step");
            step.Size = new Size(54, 34);
            step.Location = new Point(136, y);
            step.Click += (s, e) => StepOnce();
            side.Controls.Add(step);
            var clear = MakeButton("Clear");
            clear.Size = new Size(54, 34);
            clear.Location = new Point(196, y);
            clear.Click += (s, e) => ClearSearch();
            side.Controls.Add(clear);
            y += 44;

            statsLabel = new Label { Location = new Point(16, y), Size = new Size(234, 62), ForeColor = Theme.Text, Font = new Font("Consolas", 9f) };
            side.Controls.Add(statsLabel);
            y += 64;

            Section("COMPARE");
            AddButtonRow(new[] { "Run all 6", "Save image" }, new EventHandler[] { (s, e) => CompareAll(), (s, e) => SaveImage() });
            results = new ResultsPanel { Location = new Point(16, y), Size = new Size(234, 156) };
            side.Controls.Add(results);
            y += 166;
        }

        private void Section(string name)
        {
            y += 4;
            var l = new Label { Text = name, Location = new Point(16, y), AutoSize = true, ForeColor = Theme.Accent, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            side.Controls.Add(l);
            y += 24;
        }

        private void AddNote(string text)
        {
            var l = new Label { Text = text, Location = new Point(16, y), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8f) };
            side.Controls.Add(l);
            y += 20;
        }

        private NumericUpDown AddNumber(string label, int min, int max, int value)
        {
            var l = new Label { Text = label, Location = new Point(16, y + 4), AutoSize = true, ForeColor = Theme.Muted };
            var n = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Location = new Point(150, y), Width = 100, BackColor = Theme.PanelLight, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            side.Controls.Add(l);
            side.Controls.Add(n);
            y += 30;
            return n;
        }

        private NumericUpDown SmallNumber(int min, int max, int value, int x)
        {
            var n = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Location = new Point(x, y), Width = 64, BackColor = Theme.PanelLight, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            side.Controls.Add(n);
            return n;
        }

        private void AddButtonRow(string[] names, EventHandler[] handlers)
        {
            int w = (234 - (names.Length - 1) * 6) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var b = MakeButton(names[i]);
                b.Size = new Size(w, 30);
                b.Location = new Point(16 + i * (w + 6), y);
                b.Click += handlers[i];
                side.Controls.Add(b);
            }
            y += 38;
        }

        private static Button MakeButton(string text, bool primary = false)
        {
            var b = new Button
            {
                Text = text, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                BackColor = primary ? Theme.Accent : Theme.PanelLight,
                ForeColor = primary ? Color.FromArgb(30, 20, 10) : Theme.Text,
                Font = new Font("Segoe UI", 9f, primary ? FontStyle.Bold : FontStyle.Regular)
            };
            b.FlatAppearance.BorderSize = 0;
            b.TabStop = false;
            return b;
        }

        private void SelectTool(Tool t)
        {
            view.Tool = t;
            for (int i = 0; i < toolButtons.Count; i++)
            {
                bool on = i == (int)t;
                toolButtons[i].BackColor = on ? Theme.Accent : Theme.PanelLight;
                toolButtons[i].ForeColor = on ? Color.FromArgb(30, 20, 10) : Theme.Text;
            }
        }

        // ------------------------------------------------------------------ actions

        private void NewMap(string kind, int seed)
        {
            timer.Stop();
            int w = (int)widthBox.Value, h = (int)heightBox.Value;
            if (kind == "maze") { w |= 1; h |= 1; }      // mazes need odd sizes
            map = new Map(w, h) { AllowDiagonal = diagonalBox == null || diagonalBox.Checked };
            if (kind == "terrain") TerrainGenerator.Generate(map, seed);
            else if (kind == "maze") MazeGenerator.Generate(map, seed);
            else map.AddBorder();
            lastMap = kind;
            view.Map = map;
            results.Clear();
            ClearSearch();
            SetStatus(kind == "empty" ? "Empty grid. Draw some walls." : "New " + kind + " (seed " + seed + "), " + w + " x " + h);
        }

        private void ClearSearch()
        {
            timer.Stop();
            search = null;
            view.Search = null;
            runButton.Text = "Run";
            statsLabel.Text = "";
            view.Invalidate();
        }

        private void EnsureSearch()
        {
            if (search == null || search.Finished && runButton.Text == "Run")
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
            EnsureSearch();
            timer.Interval = 15;
            timer.Start();
            runButton.Text = "Pause";
        }

        private void StepOnce()
        {
            timer.Stop();
            runButton.Text = "Run";
            if (search == null || search.Finished) { search = null; EnsureSearch(); }
            search.Step();
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
                SetStatus(search.Found ? search.Name + " found a path." : search.Name + ": no path, the end is walled off.");
            }
        }

        private void UpdateStats()
        {
            if (search == null) return;
            string s = string.Format("{0}\nvisited   {1,6}\n", search.Name, search.Expanded);
            if (search.Found)
                s += string.Format("steps     {0,6}\ncost      {1,6:0.0}", search.Path.Count - 1, search.PathCost());
            else if (search.Finished)
                s += "no path";
            statsLabel.Text = s;
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
            // leave the selected one on screen
            search = rows[algorithmBox.SelectedIndex];
            view.Search = search;
            UpdateStats();
            view.Invalidate();
            SetStatus("Ran all six on the same map. Bars = cells visited, lower is better.");
        }

        private void SaveImage()
        {
            using (var dlg = new SaveFileDialog { Filter = "PNG image|*.png", FileName = "pathfinding.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                using (var bmp = new Bitmap(view.Width, view.Height))
                {
                    using (var g = Graphics.FromImage(bmp))
                        GridView.Render(g, bmp.Size, map, search);
                    bmp.Save(dlg.FileName, ImageFormat.Png);
                }
                SetStatus("Saved " + dlg.FileName);
            }
        }

        private void SetStatus(string text)
        {
            statusLabel.Text = text + "     |     Space run/pause   → step   T terrain   M maze   C clear   1-6 tools";
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
                default:
                    if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D6) SelectTool((Tool)(e.KeyCode - Keys.D1));
                    else return;
                    break;
            }
            e.Handled = e.SuppressKeyPress = true;
        }
    }

    // Small bar chart of cells visited, with steps and cost underneath.
    public class ResultsPanel : Control
    {
        private List<Search> rows;

        public ResultsPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Panel;
        }

        public void Clear() { rows = null; Invalidate(); }
        public void Show(List<Search> r) { rows = r; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Panel);
            using (var muted = new SolidBrush(Theme.Muted))
            using (var text = new SolidBrush(Theme.Text))
            using (var small = new Font("Segoe UI", 7.5f))
            {
                if (rows == null)
                {
                    g.DrawString("Runs every algorithm on this map\nand compares them here.", small, muted, 0, 4);
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
                    g.DrawString(r.Name, small, text, 0, top);
                    float w = (Width - 2) * r.Expanded / (float)max;
                    bool cheapest = r.Found && Math.Abs(r.PathCost() - best) < 0.01;
                    using (var bar = new SolidBrush(cheapest ? Theme.Accent : Color.FromArgb(90, 130, 220)))
                        g.FillRectangle(bar, 0, top + 14, Math.Max(2, w), 5);
                    string info = r.Found ? string.Format("{0} visited · cost {1:0.0}", r.Expanded, r.PathCost()) : "no path";
                    var sz = g.MeasureString(info, small);
                    g.DrawString(info, small, muted, Width - sz.Width, top);
                }
            }
        }
    }
}
