using System.Drawing.Drawing2D;
using PathfindingVisualiser.Grid;
using PathfindingVisualiser.Search;

namespace PathfindingVisualiser.UI;

/// <summary>
/// Draws the map and the current state of a search, and reports which cell the
/// mouse is over so the form can apply the selected editing tool.
/// </summary>
public sealed class GridPanel : Control
{
    private static readonly Color WallColour = Color.FromArgb(45, 45, 48);
    private static readonly Color StartColour = Color.FromArgb(46, 160, 67);
    private static readonly Color EndColour = Color.FromArgb(210, 50, 50);
    private static readonly Color VisitedColour = Color.FromArgb(80, 60, 110, 200);
    private static readonly Color VisitedReverseColour = Color.FromArgb(80, 150, 80, 190);
    private static readonly Color FrontierColour = Color.FromArgb(170, 250, 205, 50);
    private static readonly Color PathColour = Color.FromArgb(240, 120, 20);
    private static readonly Color GridLineColour = Color.FromArgb(60, 0, 0, 0);

    private Map? _map;
    private SearchAlgorithm? _search;
    private Point? _lastPaintedCell;

    public GridPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(230, 230, 230);
    }

    public Map? Map
    {
        get => _map;
        set { _map = value; Invalidate(); }
    }

    public SearchAlgorithm? Search
    {
        get => _search;
        set { _search = value; Invalidate(); }
    }

    public bool ShowGridLines { get; set; } = true;

    /// <summary>Raised when the mouse presses or drags over a cell.</summary>
    public event Action<Point, MouseButtons>? CellPainted;

    /// <summary>Size of one cell in pixels, chosen so the whole map fits.</summary>
    public int CellSize
    {
        get
        {
            if (_map == null) return 1;
            int byWidth = Math.Max(1, (ClientSize.Width - 2) / _map.Columns);
            int byHeight = Math.Max(1, (ClientSize.Height - 2) / _map.Rows);
            return Math.Min(byWidth, byHeight);
        }
    }

    private Point Origin
    {
        get
        {
            if (_map == null) return Point.Empty;
            int cs = CellSize;
            return new Point((ClientSize.Width - cs * _map.Columns) / 2,
                             (ClientSize.Height - cs * _map.Rows) / 2);
        }
    }

    public Point? CellAt(Point pixel)
    {
        if (_map == null) return null;
        var o = Origin;
        int cs = CellSize;
        int x = (pixel.X - o.X) / cs;
        int y = (pixel.Y - o.Y) / cs;
        if (pixel.X < o.X || pixel.Y < o.Y || !_map.InBounds(x, y))
            return null;
        return new Point(x, y);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _lastPaintedCell = null;
        RaisePaint(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button != MouseButtons.None)
            RaisePaint(e);
    }

    private void RaisePaint(MouseEventArgs e)
    {
        var cell = CellAt(e.Location);
        if (cell == null || cell == _lastPaintedCell)
            return;
        _lastPaintedCell = cell;
        CellPainted?.Invoke(cell.Value, e.Button);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_map == null)
            return;
        Render(e.Graphics, ClientSize);
    }

    /// <summary>Render the current state into a new bitmap (used by "Save image" and the exporter).</summary>
    public Bitmap RenderToBitmap(Size size)
    {
        var bmp = new Bitmap(size.Width, size.Height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(BackColor);
        Render(g, size, overrideSize: true);
        return bmp;
    }

    private void Render(Graphics g, Size area, bool overrideSize = false)
    {
        if (_map == null) return;

        int cs = overrideSize
            ? Math.Max(1, Math.Min((area.Width - 2) / _map.Columns, (area.Height - 2) / _map.Rows))
            : CellSize;
        var o = overrideSize
            ? new Point((area.Width - cs * _map.Columns) / 2, (area.Height - cs * _map.Rows) / 2)
            : Origin;

        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.None;

        using var brush = new SolidBrush(Color.Black);

        // Terrain and walls.
        foreach (var cell in _map.AllCells())
        {
            brush.Color = cell.IsWall ? WallColour : HeightColour(cell.Height);
            g.FillRectangle(brush, o.X + cell.X * cs, o.Y + cell.Y * cs, cs, cs);
        }

        // Search overlays.
        if (_search != null)
        {
            brush.Color = VisitedColour;
            foreach (var p in _search.Visited)
                g.FillRectangle(brush, o.X + p.X * cs, o.Y + p.Y * cs, cs, cs);

            brush.Color = VisitedReverseColour;
            foreach (var p in _search.VisitedReverse)
                g.FillRectangle(brush, o.X + p.X * cs, o.Y + p.Y * cs, cs, cs);

            brush.Color = FrontierColour;
            foreach (var p in _search.Frontier)
                g.FillRectangle(brush, o.X + p.X * cs, o.Y + p.Y * cs, cs, cs);
        }

        // Grid lines (only when cells are large enough for them to be useful).
        if (ShowGridLines && cs >= 7)
        {
            using var pen = new Pen(GridLineColour);
            for (int x = 0; x <= _map.Columns; x++)
                g.DrawLine(pen, o.X + x * cs, o.Y, o.X + x * cs, o.Y + _map.Rows * cs);
            for (int y = 0; y <= _map.Rows; y++)
                g.DrawLine(pen, o.X, o.Y + y * cs, o.X + _map.Columns * cs, o.Y + y * cs);
        }

        // Path, drawn as a line through cell centres.
        if (_search != null && _search.Path.Count > 1)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(PathColour, Math.Max(2f, cs * 0.35f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };
            var pts = _search.Path
                .Select(p => new PointF(o.X + p.X * cs + cs / 2f, o.Y + p.Y * cs + cs / 2f))
                .ToArray();
            g.DrawLines(pen, pts);
            g.SmoothingMode = SmoothingMode.None;
        }

        // Start and end markers.
        DrawMarker(g, brush, StartColour, _map.Start, o, cs);
        DrawMarker(g, brush, EndColour, _map.End, o, cs);
    }

    private static void DrawMarker(Graphics g, SolidBrush brush, Color colour, Point p, Point o, int cs)
    {
        brush.Color = colour;
        int inset = cs >= 6 ? 1 : 0;
        g.FillRectangle(brush, o.X + p.X * cs + inset, o.Y + p.Y * cs + inset, cs - 2 * inset, cs - 2 * inset);
    }

    /// <summary>
    /// Three-stop colour ramp for terrain: low ground is pale green, mid ground
    /// is sandy brown, high ground is dark grey rock.
    /// </summary>
    public static Color HeightColour(float h)
    {
        (int r, int g, int b) low = (176, 214, 150);
        (int r, int g, int b) mid = (205, 176, 118);
        (int r, int g, int b) high = (112, 104, 98);

        if (h < 0.5f)
            return Mix(low, mid, h / 0.5f);
        return Mix(mid, high, (h - 0.5f) / 0.5f);
    }

    private static Color Mix((int r, int g, int b) a, (int r, int g, int b) b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.r + (b.r - a.r) * t),
            (int)(a.g + (b.g - a.g) * t),
            (int)(a.b + (b.b - a.b) * t));
    }
}
