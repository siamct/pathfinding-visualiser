using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PathfindingVisualiser
{
    public enum Tool { Wall, Erase, Raise, Lower, Start, End }

    public static class Theme
    {
        public static readonly Color Back = Color.FromArgb(153, 180, 209);   // ActiveCaption on Windows, same blue as the original form
        public static readonly Color Wall = Color.Black;
        public static readonly Color GridLine = Color.FromArgb(90, 0, 0, 0);
        public static readonly Color Visited = Color.FromArgb(110, 70, 130, 220);
        public static readonly Color Frontier = Color.FromArgb(150, 250, 215, 80);
        public static readonly Color Path = Color.FromArgb(240, 120, 20);
        public static readonly Color Start = Color.FromArgb(40, 160, 60);
        public static readonly Color End = Color.FromArgb(210, 45, 45);

        // flat ground is white like the original squares, hills get darker brown
        private static readonly Color[] costColours = BuildCostColours();

        private static Color[] BuildCostColours()
        {
            Color low = Color.White, mid = Color.FromArgb(222, 196, 150), high = Color.FromArgb(130, 96, 66);
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
    // (The first version had a PictureBox per square, which was far too slow for big grids.)
    public class GridView : Control
    {
        public Map Map;
        public Search Search;
        public Tool Tool = Tool.Wall;
        public int BrushSize = 1;
        public bool GridLines = true;
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
            Render(e.Graphics, ClientSize, Map, Search, GridLines);
        }

        // Static so the --export option can draw exactly the same picture without a window.
        public static void Render(Graphics g, Size area, Map map, Search search, bool gridLines = true)
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

            if (gridLines && size >= 6)
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
                using (var pen = new Pen(Theme.Path, Math.Max(2f, size * 0.35f)))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = pen.EndCap = LineCap.Round;
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
            float pad = size * 0.1f;
            using (var b = new SolidBrush(colour))
                g.FillRectangle(b, ox + c.X * size + pad, oy + c.Y * size + pad, size - 2 * pad, size - 2 * pad);
        }

        // ---- mouse: click and drag to draw ----

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
