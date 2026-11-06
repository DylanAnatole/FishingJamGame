using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The swap-to-match-3 board (Candy Crush style): swap two neighbours to line up 3+ of a kind; matched tiles vanish,
// the rest fall and new ones drop in, so the board never runs out. Shared by FightPhase and the editor simulator.
// Special tiles: 4 in a row leaves a striped tile (clears its row or column when matched), an L or T leaves a bomb
// (clears 3x3), 5 in a row leaves a star (swap it with any tile to clear every tile of that kind).
// Weed ("rong") can't move or match; a match next to it, or a special tile's blast, breaks it.
public class SwapBoard
{
    public const int Cols = 7, Rows = 7;
    public const string Weed = "rong", Star = "sao";
    public enum Special { None, LineH, LineV, Bomb, Star }

    public readonly string[,] cell = new string[Cols, Rows];
    public readonly Special[,] special = new Special[Cols, Rows];
    public readonly bool[,] inked = new bool[Cols, Rows], spiked = new bool[Cols, Rows];
    readonly List<string> kinds;

    public struct Run { public string type; public List<Vector2Int> cells; }

    // how many special tiles the last Resolve left behind (for the level's side quest)
    public int Born { get; private set; }

    // What one resolve step did: amount is in triples (a run of 3 = 1, 4 = 1.5, 5+ = 2, a tile blown up by a special = 1/3).
    public class Hit { public string type; public float amount; public List<Vector2Int> cells = new List<Vector2Int>(); }

    public SwapBoard(List<string> kinds)
    {
        this.kinds = kinds;
        // fill without ready-made matches
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                do cell[x, y] = RandomKind(); while (MakesRun(x, y));
    }

    SwapBoard(SwapBoard o)
    {
        kinds = o.kinds;
        System.Array.Copy(o.cell, cell, cell.Length);
        System.Array.Copy(o.special, special, special.Length);
        System.Array.Copy(o.inked, inked, inked.Length);
        System.Array.Copy(o.spiked, spiked, spiked.Length);
    }

    public SwapBoard Clone() => new SwapBoard(this);

    public IEnumerable<string> Kinds => kinds;

    string RandomKind() => kinds[Random.Range(0, kinds.Count)];

    bool MakesRun(int x, int y)
    {
        var t = cell[x, y];
        return x >= 2 && cell[x - 1, y] == t && cell[x - 2, y] == t || y >= 2 && cell[x, y - 1] == t && cell[x, y - 2] == t;
    }

    static bool In(Vector2Int p) => p.x >= 0 && p.x < Cols && p.y >= 0 && p.y < Rows;

    public static bool Matchable(string t) => t != null && t != Weed && t != Star;

    public bool Movable(int x, int y) => cell[x, y] != null && cell[x, y] != Weed && !spiked[x, y];

    public bool CanSwap(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1 && Movable(a.x, a.y) && Movable(b.x, b.y);

    public bool IsStarSwap(Vector2Int a, Vector2Int b) => cell[a.x, a.y] == Star || cell[b.x, b.y] == Star;

    public void Swap(Vector2Int a, Vector2Int b)
    {
        (cell[a.x, a.y], cell[b.x, b.y]) = (cell[b.x, b.y], cell[a.x, a.y]);
        (special[a.x, a.y], special[b.x, b.y]) = (special[b.x, b.y], special[a.x, a.y]);
        (inked[a.x, a.y], inked[b.x, b.y]) = (inked[b.x, b.y], inked[a.x, a.y]);
    }

    // Every straight line of 3+ of a kind, across and down.
    public List<Run> FindRuns()
    {
        var runs = new List<Run>();
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Cols;)
            {
                int e = x;
                while (e + 1 < Cols && Matchable(cell[x, y]) && cell[e + 1, y] == cell[x, y]) e++;
                if (Matchable(cell[x, y]) && e - x >= 2)
                    runs.Add(new Run { type = cell[x, y], cells = Enumerable.Range(x, e - x + 1).Select(i => new Vector2Int(i, y)).ToList() });
                x = e + 1;
            }
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows;)
            {
                int e = y;
                while (e + 1 < Rows && Matchable(cell[x, y]) && cell[x, e + 1] == cell[x, y]) e++;
                if (Matchable(cell[x, y]) && e - y >= 2)
                    runs.Add(new Run { type = cell[x, y], cells = Enumerable.Range(y, e - y + 1).Select(i => new Vector2Int(x, i)).ToList() });
                y = e + 1;
            }
        return runs;
    }

    public static float RunMult(int length) => length >= 5 ? 2 : length == 4 ? 1.5f : 1;

    // Clears the current matches (plus whatever their specials blow up), leaves new specials behind, and returns what was hit.
    // moved: the two swapped cells, so a new special appears where the player made the match; null during cascades.
    public List<Hit> Resolve(Vector2Int[] moved)
    {
        var runs = FindRuns();
        var hits = new List<Hit>();
        Born = 0;
        if (runs.Count == 0) return hits;

        // runs that share a cell (L and T shapes) form one group
        var groups = new List<List<Run>>();
        foreach (var r in runs)
        {
            var g = groups.FirstOrDefault(gr => gr.Any(o => o.type == r.type && o.cells.Intersect(r.cells).Any()));
            if (g == null) groups.Add(new List<Run> { r });
            else g.Add(r);
        }

        var gone = new HashSet<Vector2Int>();
        var born = new Dictionary<Vector2Int, (string type, Special sp)>();
        foreach (var g in groups)
        {
            foreach (var r in g)
            {
                hits.Add(new Hit { type = r.type, amount = RunMult(r.cells.Count), cells = r.cells });
                gone.UnionWith(r.cells);
            }
            var longest = g.OrderByDescending(r => r.cells.Count).First();
            var sp = longest.cells.Count >= 5 ? Special.Star : g.Count >= 2 ? Special.Bomb
                   : longest.cells.Count == 4 ? (longest.cells[0].y == longest.cells[1].y ? Special.LineH : Special.LineV) : Special.None;
            if (sp == Special.None) continue;
            var all = g.SelectMany(r => r.cells).Distinct().ToList();
            var at = longest.cells[longest.cells.Count / 2];
            if (moved != null)
                foreach (var m in moved)
                    if (all.Contains(m)) { at = m; break; }
            born[at] = (sp == Special.Star ? Star : longest.type, sp);
        }

        // weed next to a match breaks
        foreach (var p in gone.ToList())
            foreach (var n in Neighbours(p))
                if (cell[n.x, n.y] == Weed) gone.Add(n);

        Blast(gone, hits, born.Keys);
        foreach (var p in gone) Empty(p);
        foreach (var kv in born)
        {
            cell[kv.Key.x, kv.Key.y] = kv.Value.type;
            special[kv.Key.x, kv.Key.y] = kv.Value.sp;
        }
        Born = born.Count;
        return hits;
    }

    // A star swapped with a tile clears every tile of that kind (two stars clear the whole board).
    public List<Hit> StarBlast(Vector2Int a, Vector2Int b)
    {
        var star = cell[a.x, a.y] == Star ? a : b;
        var other = star == a ? b : a;
        string type = cell[other.x, other.y];
        var gone = new HashSet<Vector2Int> { star };
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                if (type == Star ? cell[x, y] != Weed : cell[x, y] == type) gone.Add(new Vector2Int(x, y));
        special[star.x, star.y] = Special.None;
        var hits = new List<Hit>();
        Blast(gone, hits, new Vector2Int[0]);
        foreach (var p in gone) Empty(p);
        return hits;
    }

    // Sets off every special among the cleared cells (each can set off more), then scores the cells that no run covered.
    void Blast(HashSet<Vector2Int> gone, List<Hit> hits, IEnumerable<Vector2Int> keep)
    {
        var inRuns = new HashSet<Vector2Int>(hits.SelectMany(h => h.cells));
        var keepSet = new HashSet<Vector2Int>(keep);
        var todo = new Queue<Vector2Int>(gone.Where(p => special[p.x, p.y] != Special.None && !keepSet.Contains(p)));
        var fired = new HashSet<Vector2Int>();
        while (todo.Count > 0)
        {
            var p = todo.Dequeue();
            if (!fired.Add(p)) continue;
            foreach (var q in Area(p, special[p.x, p.y]))
            {
                if (keepSet.Contains(q) || cell[q.x, q.y] == null) continue;
                if (gone.Add(q) && special[q.x, q.y] != Special.None) todo.Enqueue(q);
            }
        }
        // blown-up tiles count a third of a triple each, grouped by kind
        foreach (var grp in gone.Where(p => !inRuns.Contains(p) && !keepSet.Contains(p) && Matchable(cell[p.x, p.y])).GroupBy(p => cell[p.x, p.y]))
            hits.Add(new Hit { type = grp.Key, amount = grp.Count() / 3f, cells = grp.ToList() });
    }

    static IEnumerable<Vector2Int> Area(Vector2Int p, Special sp)
    {
        switch (sp)
        {
            case Special.LineH: for (int x = 0; x < Cols; x++) yield return new Vector2Int(x, p.y); break;
            case Special.LineV: for (int y = 0; y < Rows; y++) yield return new Vector2Int(p.x, y); break;
            case Special.Bomb:
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var q = new Vector2Int(p.x + dx, p.y + dy);
                        if (In(q)) yield return q;
                    }
                break;
        }
    }

    static IEnumerable<Vector2Int> Neighbours(Vector2Int p)
    {
        foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            if (In(p + d)) yield return p + d;
    }

    void Empty(Vector2Int p)
    {
        cell[p.x, p.y] = null;
        special[p.x, p.y] = Special.None;
        inked[p.x, p.y] = spiked[p.x, p.y] = false;
    }

    // How many rows each cell's tile fell in the last Collapse (0 = it stayed put), for the fall animation.
    public readonly int[,] Drop = new int[Cols, Rows];

    // Tiles fall into the gaps (row 0 is the top) and new ones drop in from above.
    public void Collapse()
    {
        System.Array.Clear(Drop, 0, Drop.Length);
        for (int x = 0; x < Cols; x++)
        {
            int w = Rows - 1;
            for (int y = Rows - 1; y >= 0; y--)
            {
                if (cell[x, y] == null) continue;
                if (w != y)
                {
                    cell[x, w] = cell[x, y];
                    special[x, w] = special[x, y];
                    inked[x, w] = inked[x, y];
                    spiked[x, w] = spiked[x, y];
                    Empty(new Vector2Int(x, y));
                    Drop[x, w] = w - y;
                }
                w--;
            }
            // new tiles start just above the board, stacked in the order they land
            for (int y = w; y >= 0; y--)
            {
                Empty(new Vector2Int(x, y));
                cell[x, y] = RandomKind();
                Drop[x, y] = w + 1;
            }
        }
    }

    // Every swap that would make a match right away, or sets off a star.
    public List<(Vector2Int a, Vector2Int b)> Moves()
    {
        var moves = new List<(Vector2Int, Vector2Int)>();
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                foreach (var d in new[] { Vector2Int.right, Vector2Int.up })
                {
                    Vector2Int a = new Vector2Int(x, y), b = a + d;
                    if (!In(b) || !CanSwap(a, b)) continue;
                    if (IsStarSwap(a, b)) { moves.Add((a, b)); continue; }
                    Swap(a, b);
                    if (FindRuns().Count > 0) moves.Add((a, b));
                    Swap(a, b);
                }
        return moves;
    }

    // Deals the movable tiles into new places, retrying until at least one match is possible.
    public void Shuffle()
    {
        var spots = new List<Vector2Int>();
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                if (Movable(x, y)) spots.Add(new Vector2Int(x, y));
        for (int tries = 0; tries < 20; tries++)
        {
            var deal = spots.Select(p => (cell[p.x, p.y], special[p.x, p.y])).OrderBy(_ => Random.value).ToList();
            for (int i = 0; i < spots.Count; i++) (cell[spots[i].x, spots[i].y], special[spots[i].x, spots[i].y]) = deal[i];
            if (FindRuns().Count == 0 && Moves().Count > 0) return;
        }
    }

    // Turns a few random plain tiles into weed (the fish throwing weed onto the board).
    public void AddWeed(int n)
    {
        for (int i = 0; i < n; i++)
        {
            var p = RandomPlain();
            if (p.x >= 0) { Empty(p); cell[p.x, p.y] = Weed; }
        }
    }

    Vector2Int RandomPlain()
    {
        var spots = new List<Vector2Int>();
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                if (Movable(x, y) && special[x, y] == Special.None) spots.Add(new Vector2Int(x, y));
        return spots.Count > 0 ? spots[Random.Range(0, spots.Count)] : new Vector2Int(-1, -1);
    }

    // The fish's board move when it starts struggling. Returns the line to show.
    public string ApplySkill(FishingGame.Skill skill)
    {
        switch (skill)
        {
            case FishingGame.Skill.Ink:
                for (int i = 0; i < 4; i++) { var p = RandomPlain(); if (p.x >= 0) inked[p.x, p.y] = true; }
                return "Mực phun mực!";
            case FishingGame.Skill.Spikes:
                for (int i = 0; i < 3; i++) { var p = RandomPlain(); if (p.x >= 0) spiked[p.x, p.y] = true; }
                return "Nóc xù gai!";
            case FishingGame.Skill.Weed:
                AddWeed(2);
                return "Cá mập ném rong!";
        }
        return "";
    }

    // A tired fish lets go: spikes fold away and the ink drifts off.
    public void Calm()
    {
        System.Array.Clear(inked, 0, inked.Length);
        System.Array.Clear(spiked, 0, spiked.Length);
    }
}
