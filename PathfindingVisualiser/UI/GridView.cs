using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PathfindingVisualiser
{
    public enum Tool { Wall, Erase, Raise, Lower, Start, End }

    public static class Theme
    {
        public static readonly Color Back = Color.FromArgb(17, 20, 28);
        public static readonly Color Panel = Color.FromArgb(26, 30, 41);
        public static readonly Color PanelLight = Color.FromArgb(37, 43, 57);
        public static readonly Color Text = Color.FromArgb(225, 230, 240);
        public static readonly Color Muted = Color.FromArgb(135, 145, 165);
        public static readonly Color Accent = Color.FromArgb(255, 159, 67);
        public static readonly Color Wall = Color.FromArgb(44, 48, 60);
        public static readonly Color GridLine = Color.FromArgb(30, 0, 0, 0);
        public static readonly Color Visited = Color.FromArgb(85, 60, 120, 235);
        public static readonly Color Frontier = Color.FromArgb(190, 250, 205, 70);
        public static readonly Color Path = Color.FromArgb(255, 159, 67);
        public static readonly Color Start = Color.FromArgb(46, 204, 113);
        public static readonly Color End = Color.FromArgb(231, 76, 60);

        // flat ground light green -> hills brown
        private static readonly Color[] costColours = BuildCostColours();

        private static Color[] BuildCostColours()
        {
            Color low = Color.FromArgb(206, 231, 190), mid = Color.FromArgb(158, 190, 120), high = Color.FromArgb(132, 96, 66);
            var c = new Color[Map.MaxCost + 1];
            for (int i = 1; i <= Map.MaxCost; i++)
            {
                double t = (i - 1) / (double)(Map.MaxCost - 1);
                c[i] = t < 0.5 ? Mix(low, mid, t * 2) : Mix(mid, high, (t - 0.5) * 2);
            }
            c[0] = c[1];
            return c;
        }

        public static Color ForCost(int cost) { return costColours[Math.Max(1, Math.Min(Map.MaxCost, cost))]; }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    // Draws the map and the search, and handles drawing on the grid with the mouse.
    // (This replaces the 400 PictureBoxes from the NEA version.)
    public class GridView : Control
    {
        public Map Map;
        public Search Search;
        public Tool Tool = Tool.Wall;
        public int BrushSize = 1;
        public event EventHandler MapEdited;

        private bool dragging;
        private MouseButtons dragButton;

        public GridView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Back;
        }

        // size of one square and where the grid starts, so it's centred and fits the window
        private void GetLayout(Size area, out float size, out float ox, out float oy)
        {
            size = Math.Min((area.Width - 16f) / Map.Width, (area.Height - 16f) / Map.Height);
            ox = (area.Width - size * Map.Width) / 2f;
            oy = (area.Height - size * Map.Height) / 2f;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Map == null) return;
            Render(e.Graphics, ClientSize, Map, Search);
        }

        // Static so the --export option can draw exactly the same picture without a window.
        public static void Render(Graphics g, Size area, Map map, Search search)
        {
            g.Clear(Theme.Back);
            float size = Math.Min((area.Width - 16f) / map.Width, (area.Height - 16f) / map.Height);
            float ox = (area.Width - size * map.Width) / 2f;
            float oy = (area.Height - size * map.Height) / 2f;

            using (var brush = new SolidBrush(Color.Black))
            {
                foreach (Cell c in map.AllCells())
                {
                    brush.Color = c.Wall ? Theme.Wall : Theme.ForCost(c.Cost);
                    g.FillRectangle(brush, ox + c.X * size, oy + c.Y * size, size + 0.5f, size + 0.5f);
                }

                if (search != null)
                {
                    brush.Color = Theme.Visited;
                    foreach (Cell c in search.Visited)
                        g.FillRectangle(brush, ox + c.X * size, oy + c.Y * size, size + 0.5f, size + 0.5f);
                    brush.Color = Theme.Frontier;
                    foreach (Cell c in search.Frontier)
                        g.FillRectangle(brush, ox + c.X * size, oy + c.Y * size, size + 0.5f, size + 0.5f);
                }
            }

            if (size >= 7)
            {
                using (var pen = new Pen(Theme.GridLine))
                {
                    for (int x = 0; x <= map.Width; x++)
                        g.DrawLine(pen, ox + x * size, oy, ox + x * size, oy + map.Height * size);
                    for (int y = 0; y <= map.Height; y++)
                        g.DrawLine(pen, ox, oy + y * size, ox + map.Width * size, oy + y * size);
                }
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (search != null && search.Path.Count > 1)
            {
                var pts = new PointF[search.Path.Count];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = new PointF(ox + (search.Path[i].X + 0.5f) * size, oy + (search.Path[i].Y + 0.5f) * size);
                using (var shadow = new Pen(Color.FromArgb(120, 0, 0, 0), Math.Max(3f, size * 0.55f)))
                using (var pen = new Pen(Theme.Path, Math.Max(2f, size * 0.38f)))
                {
                    shadow.LineJoin = pen.LineJoin = LineJoin.Round;
                    shadow.StartCap = shadow.EndCap = pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawLines(shadow, pts);
                    g.DrawLines(pen, pts);
                }
            }
            else if (search != null && search.Current != null && !search.Finished)
            {
                using (var pen = new Pen(Color.White, Math.Max(1.5f, size * 0.15f)))
                    g.DrawRectangle(pen, ox + search.Current.X * size, oy + search.Current.Y * size, size, size);
            }

            DrawEnd(g, map.Start, Theme.Start, size, ox, oy);
            DrawEnd(g, map.End, Theme.End, size, ox, oy);
        }

        private static void DrawEnd(Graphics g, Cell c, Color colour, float size, float ox, float oy)
        {
            float pad = size * 0.08f, d = Math.Max(6f, size * 1.4f);
            float cx = ox + (c.X + 0.5f) * size, cy = oy + (c.Y + 0.5f) * size;
            using (var b = new SolidBrush(colour))
            using (var p = new Pen(Color.White, Math.Max(1.5f, size * 0.18f)))
            {
                g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
                g.DrawEllipse(p, cx - d / 2 + pad, cy - d / 2 + pad, d - 2 * pad, d - 2 * pad);
            }
        }

        // ---- mouse: same idea as the NEA draw/erase, but click-and-drag instead of hover ----

        private Cell CellAt(Point p)
        {
            float size, ox, oy;
            GetLayout(ClientSize, out size, out ox, out oy);
            int x = (int)Math.Floor((p.X - ox) / size), y = (int)Math.Floor((p.Y - oy) / size);
            return Map.InBounds(x, y) ? Map[x, y] : null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Map == null) return;
            dragging = true;
            dragButton = e.Button;
            Apply(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) Apply(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
        }

        private void Apply(Point p)
        {
            Cell c = CellAt(p);
            if (c == null) return;
            Tool tool = dragButton == MouseButtons.Right ? Tool.Erase : Tool;

            if (tool == Tool.Start || tool == Tool.End)
            {
                c.Wall = false;
                if (tool == Tool.Start && c != Map.End) Map.Start = c;
                if (tool == Tool.End && c != Map.Start) Map.End = c;
            }
            else
            {
                int r = BrushSize - 1;
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int x = c.X + dx, y = c.Y + dy;
                        if (!Map.InBounds(x, y)) continue;
                        Cell t = Map[x, y];
                        if (t == Map.Start || t == Map.End) continue;
                        switch (tool)
                        {
                            case Tool.Wall: t.Wall = true; break;
                            case Tool.Erase: t.Wall = false; t.Cost = 1; break;
                            case Tool.Raise: if (!t.Wall) t.Cost = Math.Min(Map.MaxCost, t.Cost + 1); break;
                            case Tool.Lower: if (!t.Wall) t.Cost = Math.Max(1, t.Cost - 1); break;
                        }
                    }
            }
            if (MapEdited != null) MapEdited(this, EventArgs.Empty);
            Invalidate();
        }
    }
}
