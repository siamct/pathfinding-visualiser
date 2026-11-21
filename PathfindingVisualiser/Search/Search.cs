using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PathfindingVisualiser
{
    public enum Algorithm { AStar, Dijkstra, Greedy, BreadthFirst, Bidirectional, DepthFirst }

    // Every algorithm is written as a step iterator: each call to Step() expands
    // one cell, so the UI can animate it, pause it or run it all at once.
    public abstract class Search
    {
        protected readonly Map map;
        public HashSet<Cell> Visited = new HashSet<Cell>();      // closed set (drawn blue)
        public HashSet<Cell> Frontier = new HashSet<Cell>();     // open set (drawn yellow)
        public List<Cell> Path = new List<Cell>();
        public bool Finished { get; protected set; }
        public bool Found { get; protected set; }
        public int Expanded { get; protected set; }
        public Cell Current { get; protected set; }
        public double ElapsedMs { get { return timer.Elapsed.TotalMilliseconds; } }

        protected readonly Stopwatch timer = new Stopwatch();
        private IEnumerator<bool> steps;

        public abstract string Name { get; }
        public abstract bool UsesCost { get; }       // does it care about hills?

        protected Search(Map map) { this.map = map; }

        public bool Step()
        {
            if (Finished) return false;
            if (steps == null) steps = Run().GetEnumerator();
            timer.Start();
            bool more = steps.MoveNext();
            timer.Stop();
            if (!more) Finished = true;
            return !Finished;
        }

        public void RunToEnd()
        {
            while (Step()) { }
        }

        protected abstract IEnumerable<bool> Run();

        public double PathCost()
        {
            double total = 0;
            for (int i = 1; i < Path.Count; i++)
                total += map.MoveCost(Path[i], StepLength(Path[i - 1], Path[i]));
            return total;
        }

        protected static double StepLength(Cell a, Cell b)
        {
            return (a.X != b.X && a.Y != b.Y) ? Map.DiagonalStep : 1.0;
        }

        protected void BuildPath(Dictionary<Cell, Cell> parent, Cell end)
        {
            Path.Clear();
            for (Cell c = end; c != null; c = parent.ContainsKey(c) ? parent[c] : null)
                Path.Add(c);
            Path.Reverse();
            Found = true;
        }

        public static Search Create(Algorithm a, Map map)
        {
            switch (a)
            {
                case Algorithm.AStar: return new AStar(map);
                case Algorithm.Dijkstra: return new Dijkstra(map);
                case Algorithm.Greedy: return new GreedyBestFirst(map);
                case Algorithm.BreadthFirst: return new BreadthFirst(map);
                case Algorithm.Bidirectional: return new BidirectionalBfs(map);
                default: return new DepthFirst(map);
            }
        }
    }

    // Min-heap. Wrote my own so it builds on older .NET too (PriorityQueue is .NET 6+).
    public class MinHeap<T>
    {
        private readonly List<KeyValuePair<double, T>> items = new List<KeyValuePair<double, T>>();
        private long counter;                 // tie-break so equal priorities come out in order
        private readonly List<long> order = new List<long>();

        public int Count { get { return items.Count; } }

        public void Push(T item, double priority)
        {
            items.Add(new KeyValuePair<double, T>(priority, item));
            order.Add(counter++);
            int i = items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Less(i, parent)) break;
                Swap(i, parent);
                i = parent;
            }
        }

        public T Pop()
        {
            T top = items[0].Value;
            int last = items.Count - 1;
            Swap(0, last);
            items.RemoveAt(last);
            order.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, best = i;
                if (l < items.Count && Less(l, best)) best = l;
                if (r < items.Count && Less(r, best)) best = r;
                if (best == i) break;
                Swap(i, best);
                i = best;
            }
            return top;
        }

        private bool Less(int a, int b)
        {
            if (items[a].Key != items[b].Key) return items[a].Key < items[b].Key;
            return order[a] < order[b];
        }

        private void Swap(int a, int b)
        {
            var t = items[a]; items[a] = items[b]; items[b] = t;
            var o = order[a]; order[a] = order[b]; order[b] = o;
        }
    }

    // A*, Dijkstra and greedy are the same loop with a different priority, so they share this.
    public abstract class BestFirst : Search
    {
        protected BestFirst(Map map) : base(map) { }

        protected abstract double Priority(double costSoFar, Cell c);

        // Octile distance: the shortest possible route with 8-way moves, ignoring walls.
        // Multiplied by the cheapest cell cost (1) so it never overestimates.
        protected double Heuristic(Cell c)
        {
            int dx = Math.Abs(c.X - map.End.X), dy = Math.Abs(c.Y - map.End.Y);
            if (!map.AllowDiagonal) return dx + dy;
            return (dx + dy) + (Map.DiagonalStep - 2) * Math.Min(dx, dy);
        }

        protected override IEnumerable<bool> Run()
        {
            var cost = new Dictionary<Cell, double>();
            var parent = new Dictionary<Cell, Cell>();
            var open = new MinHeap<Cell>();

            cost[map.Start] = 0;
            open.Push(map.Start, Priority(0, map.Start));
            Frontier.Add(map.Start);

            while (open.Count > 0)
            {
                Cell cur = open.Pop();
                if (Visited.Contains(cur)) continue;        // old copy left in the heap
                Visited.Add(cur);
                Frontier.Remove(cur);
                Current = cur;
                Expanded++;

                if (cur == map.End)
                {
                    BuildPath(parent, cur);
                    yield break;
                }

                foreach (var n in map.Neighbours(cur))
                {
                    Cell next = n.Key;
                    if (Visited.Contains(next)) continue;
                    double newCost = cost[cur] + (UsesCost ? map.MoveCost(next, n.Value) : n.Value);
                    double old;
                    if (cost.TryGetValue(next, out old) && old <= newCost) continue;
                    cost[next] = newCost;
                    parent[next] = cur;
                    open.Push(next, Priority(newCost, next));
                    Frontier.Add(next);
                }
                yield return true;
            }
        }
    }

    public class AStar : BestFirst
    {
        public AStar(Map map) : base(map) { }
        public override string Name { get { return "A*"; } }
        public override bool UsesCost { get { return true; } }
        protected override double Priority(double g, Cell c) { return g + Heuristic(c); }
    }

    public class Dijkstra : BestFirst
    {
        public Dijkstra(Map map) : base(map) { }
        public override string Name { get { return "Dijkstra"; } }
        public override bool UsesCost { get { return true; } }
        protected override double Priority(double g, Cell c) { return g; }
    }

    public class GreedyBestFirst : BestFirst
    {
        public GreedyBestFirst(Map map) : base(map) { }
        public override string Name { get { return "Greedy best-first"; } }
        public override bool UsesCost { get { return false; } }
        protected override double Priority(double g, Cell c) { return Heuristic(c); }
    }

    public class BreadthFirst : Search
    {
        public BreadthFirst(Map map) : base(map) { }
        public override string Name { get { return "Breadth-first"; } }
        public override bool UsesCost { get { return false; } }

        protected override IEnumerable<bool> Run()
        {
            var parent = new Dictionary<Cell, Cell>();
            var queue = new Queue<Cell>();
            queue.Enqueue(map.Start);
            Frontier.Add(map.Start);

            while (queue.Count > 0)
            {
                Cell cur = queue.Dequeue();
                Frontier.Remove(cur);
                Visited.Add(cur);
                Current = cur;
                Expanded++;
                if (cur == map.End)
                {
                    BuildPath(parent, cur);
                    yield break;
                }
                foreach (var n in map.Neighbours(cur))
                {
                    if (Visited.Contains(n.Key) || Frontier.Contains(n.Key)) continue;
                    parent[n.Key] = cur;
                    Frontier.Add(n.Key);
                    queue.Enqueue(n.Key);
                }
                yield return true;
            }
        }
    }

    // Two breadth-first searches, one from each end, taking turns. Stops when they touch.
    public class BidirectionalBfs : Search
    {
        public BidirectionalBfs(Map map) : base(map) { }
        public override string Name { get { return "Bidirectional BFS"; } }
        public override bool UsesCost { get { return false; } }

        protected override IEnumerable<bool> Run()
        {
            var fromStart = new Dictionary<Cell, Cell> { { map.Start, null } };
            var fromEnd = new Dictionary<Cell, Cell> { { map.End, null } };
            var qa = new Queue<Cell>();
            var qb = new Queue<Cell>();
            qa.Enqueue(map.Start);
            qb.Enqueue(map.End);
            Frontier.Add(map.Start);
            Frontier.Add(map.End);

            if (map.Start == map.End)
            {
                Path.Add(map.Start);
                Found = true;
                yield break;
            }

            while (qa.Count > 0 && qb.Count > 0)
            {
                foreach (var side in new[] { true, false })
                {
                    var q = side ? qa : qb;
                    var mine = side ? fromStart : fromEnd;
                    var other = side ? fromEnd : fromStart;
                    if (q.Count == 0) continue;

                    Cell cur = q.Dequeue();
                    Frontier.Remove(cur);
                    Visited.Add(cur);
                    Current = cur;
                    Expanded++;

                    foreach (var n in map.Neighbours(cur))
                    {
                        Cell next = n.Key;
                        if (mine.ContainsKey(next)) continue;
                        mine[next] = cur;
                        if (other.ContainsKey(next))
                        {
                            Join(next, fromStart, fromEnd);
                            yield break;
                        }
                        Frontier.Add(next);
                        q.Enqueue(next);
                    }
                    yield return true;
                }
            }
        }

        private void Join(Cell meet, Dictionary<Cell, Cell> a, Dictionary<Cell, Cell> b)
        {
            Path.Clear();
            for (Cell c = meet; c != null; c = a[c]) Path.Add(c);
            Path.Reverse();
            for (Cell c = b[meet]; c != null; c = b[c]) Path.Add(c);
            Found = true;
        }
    }

    public class DepthFirst : Search
    {
        public DepthFirst(Map map) : base(map) { }
        public override string Name { get { return "Depth-first"; } }
        public override bool UsesCost { get { return false; } }

        protected override IEnumerable<bool> Run()
        {
            var parent = new Dictionary<Cell, Cell>();
            var stack = new Stack<Cell>();
            stack.Push(map.Start);
            Frontier.Add(map.Start);

            while (stack.Count > 0)
            {
                Cell cur = stack.Pop();
                if (Visited.Contains(cur)) continue;
                Frontier.Remove(cur);
                Visited.Add(cur);
                Current = cur;
                Expanded++;
                if (cur == map.End)
                {
                    BuildPath(parent, cur);
                    yield break;
                }
                foreach (var n in map.Neighbours(cur))
                {
                    if (Visited.Contains(n.Key)) continue;
                    parent[n.Key] = cur;
                    Frontier.Add(n.Key);
                    stack.Push(n.Key);
                }
                yield return true;
            }
        }
    }
}
