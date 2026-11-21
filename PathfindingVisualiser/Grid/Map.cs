using System;
using System.Collections.Generic;

namespace PathfindingVisualiser
{
    // The first version made every square its own PictureBox (the Block class),
    // which got really slow past about 20x20. Now the grid is just data and
    // GridView draws it all in one go.
    public class Cell
    {
        public int X;
        public int Y;
        public bool Wall;
        public int Cost = 1;          // 1 = flat ground, up to 9 = steep hill

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public class Map
    {
        public const int MaxCost = 9;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public Cell Start { get; set; }
        public Cell End { get; set; }
        public bool AllowDiagonal { get; set; }

        private readonly Cell[,] cells;

        public Map(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new Cell[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    cells[x, y] = new Cell(x, y);

            AllowDiagonal = true;
            ResetEnds();
        }

        public Cell this[int x, int y] { get { return cells[x, y]; } }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public void ResetEnds()
        {
            Start = cells[1, Height / 2];
            End = cells[Width - 2, Height / 2];
            Start.Wall = false;
            End.Wall = false;
        }

        public void Clear()
        {
            foreach (Cell c in cells)
            {
                c.Wall = false;
                c.Cost = 1;
            }
        }

        public IEnumerable<Cell> AllCells()
        {
            foreach (Cell c in cells)
                yield return c;
        }

        // Black border round the edge, so paths can't leave the map.
        public void AddBorder()
        {
            for (int x = 0; x < Width; x++)
            {
                cells[x, 0].Wall = true;
                cells[x, Height - 1].Wall = true;
            }
            for (int y = 0; y < Height; y++)
            {
                cells[0, y].Wall = true;
                cells[Width - 1, y].Wall = true;
            }
        }

        private static readonly int[,] Straight = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        private static readonly int[,] Diagonal = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };

        public const double DiagonalStep = 1.41421356;

        // Neighbours you can actually walk to, with the length of the step (1 or 1.41).
        public List<KeyValuePair<Cell, double>> Neighbours(Cell c)
        {
            var list = new List<KeyValuePair<Cell, double>>(8);
            for (int i = 0; i < 4; i++)
            {
                int nx = c.X + Straight[i, 0], ny = c.Y + Straight[i, 1];
                if (InBounds(nx, ny) && !cells[nx, ny].Wall)
                    list.Add(new KeyValuePair<Cell, double>(cells[nx, ny], 1.0));
            }
            if (!AllowDiagonal)
                return list;

            for (int i = 0; i < 4; i++)
            {
                int dx = Diagonal[i, 0], dy = Diagonal[i, 1];
                int nx = c.X + dx, ny = c.Y + dy;
                if (!InBounds(nx, ny) || cells[nx, ny].Wall)
                    continue;
                // no squeezing between two walls that touch at a corner
                if (cells[c.X + dx, c.Y].Wall || cells[c.X, c.Y + dy].Wall)
                    continue;
                list.Add(new KeyValuePair<Cell, double>(cells[nx, ny], DiagonalStep));
            }
            return list;
        }

        // Cost of moving onto a cell: step length times how steep the ground is.
        public double MoveCost(Cell to, double stepLength)
        {
            return stepLength * to.Cost;
        }
    }
}
