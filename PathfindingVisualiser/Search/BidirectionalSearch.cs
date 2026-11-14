using PathfindingVisualiser.Grid;

namespace PathfindingVisualiser.Search;

/// <summary>
/// Bidirectional breadth-first search. Two searches grow at the same time, one
/// from the start and one from the end, and stop when they touch. Each side
/// only has to cover about half the distance, so far fewer cells are expanded
/// than with a single BFS. Like BFS, it ignores terrain cost.
/// </summary>
public sealed class BidirectionalSearch : SearchAlgorithm
{
    public override string Name => "Bidirectional BFS";
    public override bool UsesWeights => false;
    public override bool IsOptimal => false;

    protected override IEnumerable<Point> Search(Map map, bool allowDiagonal)
    {
        if (map.Start == map.End)
        {
            Visited.Add(map.Start);
            Path.Add(map.Start);
            yield return map.Start;
            yield break;
        }

        var parentF = new Dictionary<Point, Point>();
        var parentR = new Dictionary<Point, Point>();
        var seenF = new HashSet<Point> { map.Start };
        var seenR = new HashSet<Point> { map.End };
        var queueF = new Queue<Point>();
        var queueR = new Queue<Point>();
        queueF.Enqueue(map.Start);
        queueR.Enqueue(map.End);
        Frontier.Add(map.Start);
        Frontier.Add(map.End);

        while (queueF.Count > 0 && queueR.Count > 0)
        {
            // Expand one cell from the forward side.
            var f = queueF.Dequeue();
            Visited.Add(f);
            Frontier.Remove(f);
            yield return f;

            if (seenR.Contains(f))
            {
                Join(map, parentF, parentR, f);
                yield break;
            }

            foreach (var (next, _) in map.Neighbours(f, allowDiagonal))
            {
                if (!seenF.Add(next))
                    continue;
                parentF[next] = f;
                queueF.Enqueue(next);
                Frontier.Add(next);
            }

            // Then one from the reverse side.
            var r = queueR.Dequeue();
            VisitedReverse.Add(r);
            Frontier.Remove(r);
            yield return r;

            if (seenF.Contains(r))
            {
                Join(map, parentF, parentR, r);
                yield break;
            }

            foreach (var (next, _) in map.Neighbours(r, allowDiagonal))
            {
                if (!seenR.Add(next))
                    continue;
                parentR[next] = r;
                queueR.Enqueue(next);
                Frontier.Add(next);
            }
        }
    }

    /// <summary>Stitch the two half-paths together at the meeting cell.</summary>
    private void Join(Map map, Dictionary<Point, Point> parentF, Dictionary<Point, Point> parentR, Point meet)
    {
        Path.Clear();

        var forward = new List<Point>();
        var p = meet;
        forward.Add(p);
        while (p != map.Start && parentF.TryGetValue(p, out p))
            forward.Add(p);
        forward.Reverse();

        var q = meet;
        var backward = new List<Point>();
        while (q != map.End && parentR.TryGetValue(q, out q))
            backward.Add(q);

        Path.AddRange(forward);
        Path.AddRange(backward);
    }
}
