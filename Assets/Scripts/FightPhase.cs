using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Phase 2: fight the hooked fish on a swap-to-match-3 board (SwapBoard). Two bars:
// GIỮ (hold) starts full and sinks every second; empty = the line snaps and the fish escapes.
// CÂU (catch) starts at 0; full = the fish is landed.
// Every tile kind is a fishing move with its own push on the two bars (FishingGame.TileKind), so the puzzle is
// choosing which moves to line up: strong pulls cost hold, safe moves are slow.
// The fish alternates between struggling (hold sinks fast, pulls ×0.75) and tiring (hold sinks slower, pulls ×1.25);
// strong fish lunge (yank GIỮ) as they start struggling, and a few species also hit the board (FishingGame.Skill).
// One board lasts the whole level: every fish in it is fought on the same board, so special tiles built up in one
// fight carry over to the next. A new level starts a new board (NewBoard).
public class FightPhase : MonoBehaviour
{
    const int ShuffleCost = 50;
    // the two bars stand side by side left of the board, so one glance reads both
    const float BoardW = 780, BoardX = 220, BarX = 22, CatchX = 84, BarY = 440, BarW = 52, BarH = 720;
    static readonly Vector2 FishCenter = new Vector2(540, 160);
    static readonly Color Green = UI.Hex("#2E8B57"), Ink = UI.Hex("#2B2D42"), Gold = UI.Hex("#FFD54F");

    FishingGame game;
    FishingGame.Species s;
    CastPhase.PondFish fish;
    bool busy, over, struggling, skillPending, lowWarned;
    float phaseLeft;
    float catchBar, hold, shownCatch, shownHold, unit, boardOx;
    SwapBoard sb;
    readonly RectTransform[,] tileRt = new RectTransform[SwapBoard.Cols, SwapBoard.Rows]; // the tile drawn in each cell
    Vector2Int picked; // first tile chosen for a swap; x < 0 = none
    // after a few idle seconds the board points out a good swap
    const float HintDelay = 5;
    // set by the game per level: early levels hint sooner; on the first play a hand swipes the hinted pair
    public float hintDelay = HintDelay;
    public bool coach;
    RectTransform hand;
    float idleSince;
    (Vector2Int a, Vector2Int b)? hint;
    readonly List<Vector2Int> hintCells = new List<Vector2Int>();

    // One view per board cell, built once and only updated afterwards, so a render allocates nothing
    // (rebuilding 49 tiles several times per swap made frames hitch, worst in a browser).
    class CellView
    {
        public RectTransform rt;
        public Image bg, icon, stripeH, stripeV;
        public Text label;
        public Outline ring;
    }

    readonly CellView[,] views = new CellView[SwapBoard.Cols, SwapBoard.Rows];

    Text nameText, phaseText, holdText, catchText;
    RectTransform holdFill, catchFill, board, fishShape, floats, boardBg, legend;
    Image fishBody, fishTail, holdFillImg, catchFillImg;
    Color fishTint; // white for full-colour pixel sprites, the species colour for the old grey drawings

    public void Init(FishingGame g)
    {
        game = g;
        // backdrops first so everything else draws on top of them
        if (UI.Art("panel_water")) UI.Img(transform, "FishBg", 40, 94, 1000, 132, Color.white).sprite = UI.Art("panel_water");
        if (UI.Art("board_bg"))
        {
            var bg = UI.Img(transform, "BoardBg", BoardX - 22, 424, 824, 784, Color.white);
            bg.sprite = UI.Art("board_bg");
            boardBg = bg.rectTransform;
        }
        nameText = UI.Label(transform, "", 40, 20, 1000, 70, 50, FishingGame.Ink, TextAnchor.MiddleLeft, true);

        fishShape = UI.Rect(transform, "Fish", 0, 0, 260, 100);
        fishShape.pivot = new Vector2(0.5f, 0.5f);
        fishShape.anchoredPosition = new Vector2(540, -160);
        fishTail = UI.Img(fishShape, "Tail", 215, 20, 60, 60, Color.white, true);
        fishBody = UI.Img(fishShape, "Body", 0, 0, 260, 100, Color.white, true);

        phaseText = UI.Label(transform, "", 40, 260, 1000, 90, 60, FishingGame.Ink, TextAnchor.MiddleCenter, true);

        // GIỮ left of the board, CÂU right of it; both fill from the bottom
        (holdFillImg, holdText) = Bar("GIỮ", BarX, Green);
        holdFill = holdFillImg.rectTransform;
        (catchFillImg, catchText) = Bar("CÂU", CatchX, FishingGame.Buoy);
        catchFill = catchFillImg.rectTransform;
        // a mark at 70% on CÂU with the net beside it: past this line the net works
        float nearY = BarY + BarH * (1 - NearAt);
        UI.Img(transform, "NearMark", CatchX - 4, nearY - 3, BarW + 8, 6, Color.white).raycastTarget = false;
        if (UI.Art("tile_vot")) UI.Img(transform, "NearIcon", CatchX + BarW + 6, nearY - 20, 40, 40, Color.white).sprite = UI.Art("tile_vot");

        board = UI.Rect(transform, "Board", BoardX, 440, BoardW, 740);
        board.gameObject.AddComponent<RectMask2D>(); // tiles dropping in from above stay hidden until they enter the board
        legend = UI.Rect(transform, "Legend", 0, 1225, 1080, 150);

        AddIcon(UI.Btn(transform, $"   {ShuffleCost}", 410, 1420, 260, 110, FishingGame.Line, FishingGame.Ink, UseShuffle), "icon_shuffle");

        floats = UI.Rect(transform, "Floats", 0, 0, 1080, 1720);
        var handImg = UI.Img(transform, "Hand", 0, 0, 120, 120, Color.white);
        handImg.sprite = UI.Art("hand");
        handImg.raycastTarget = false;
        hand = handImg.rectTransform;
        hand.pivot = new Vector2(0.5f, 0.97f);
        hand.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    (Image fill, Text value) Bar(string title, float x, Color color)
    {
        UI.Label(transform, title, x - 5, BarY - 46, BarW + 10, 40, 26, color, TextAnchor.MiddleCenter, true);
        // glossy tubes when the art is there, plain rounded bars otherwise
        var back = UI.Img(transform, title + "Bg", x, BarY, BarW, BarH, Color.white);
        if (!UI.Sliced(back, "bar_bg", 24)) { back.color = FishingGame.Line; UI.Round(back); }
        var fill = UI.Img(transform, title + "Fill", x, BarY, BarW, BarH, color);
        if (!UI.Sliced(fill, "bar_fill", 24)) UI.Round(fill);
        var value = UI.Label(transform, "", x - 5, BarY + BarH + 4, BarW + 10, 40, 24, FishingGame.Ink, TextAnchor.MiddleCenter, true);
        return (fill, value);
    }

    public void Begin(CastPhase.PondFish f)
    {
        StopAllCoroutines();
        fish = f;
        s = f.s;
        catchBar = shownCatch = 0;
        hold = shownHold = 1;
        over = busy = lowWarned = false;
        struggling = skillPending = false;
        phaseLeft = 1 + PhaseLength(false);
        foreach (Transform c in floats) Destroy(c.gameObject);
        floating = 0;
        fishShape.localEulerAngles = Vector3.zero;
        fishShape.localScale = Vector3.one;

        nameText.text = s.name;
        // difficulty stars as pictures right after the name
        foreach (Transform c in nameText.transform) Destroy(c.gameObject);
        UI.Stars(nameText.transform, s.stars, s.stars, nameText.preferredWidth + 16, 14, 42);
        // the drawn fish keeps its own proportions; without a drawing, the old oval + tail
        var sprite = UI.Art("fish_" + s.id);
        fishTint = sprite ? Color.white : s.color;
        fishBody.color = fishTail.color = fishTint;
        fishBody.sprite = sprite ? sprite : UI.Circle;
        fishTail.enabled = !sprite;
        var shape = sprite ? s.size * Mathf.Min(120 / s.size.y, 340 / s.size.x) : new Vector2(260, 100);
        fishShape.sizeDelta = fishBody.rectTransform.sizeDelta = shape;

        if (sb == null) sb = new SwapBoard(Kinds.ToList());
        Settle();
        picked = new Vector2Int(-1, -1);
        Idle();
        RenderLegend();
        Render();
    }

    // The five moves, the same on every board (match-3 needs at least five kinds or it chains forever).
    public static readonly string[] Kinds = { "giat", "thu", "noi", "ghim", "vot" };

    // A fresh board for a new level.
    public void NewBoard() => sb = null;

    // A board kept from the last fight: the old fish's ink and spikes go, holes left by a fight that ended
    // mid-cascade fill in, and matches already lined up clear quietly. Weed and special tiles stay.
    void Settle() => Settle(sb);

    public static void Settle(SwapBoard sb)
    {
        sb.Calm();
        sb.Collapse();
        for (int i = 0; i < 20 && sb.FindRuns().Count > 0; i++)
        {
            sb.Resolve(null);
            sb.Collapse();
        }
        if (sb.Moves().Count == 0) sb.Shuffle();
    }

    // ---- fight rhythm, shared with the simulator ----

    public const float NearAt = 0.7f;

    // Seconds the fish stays struggling or tired: a steady rhythm the player can read from the banner.
    public static float PhaseLength(bool struggle) => struggle ? 2.5f : 3.5f;

    // Hold sinks twice as fast while the fish struggles, a bit slower while it is tired.
    public static float DrainMult(bool struggle) => struggle ? 2f : 0.6f;

    public static float GainMult(bool struggle) => struggle ? 0.75f : 1.25f;

    // Each cascade step after the swap's own match pulls harder: ×1, ×1.25, ×1.5 … up to ×2 (CÂU only).
    public static float ComboMult(int step) => Mathf.Min(1 + 0.25f * (step - 1), 2);

    // What a hit of this move does to the two bars. The net (nearOnly) does nothing until the fish is close.
    public static (float reel, float hold) Effect(FishingGame.TileKind k, float amount, FishingGame.Species s, bool struggle, float catchBar, int step = 1)
    {
        if (k.nearOnly && catchBar < NearAt) return (0, 0);
        return (k.reel * amount * 300f / s.hp * GainMult(struggle) * ComboMult(step), k.hold * amount);
    }

    // How good a swap looks right away (not counting the cascades after it): it wants catch, values hold more the lower
    // the hold bar is, and never lets a pull snap the line. Used by the hint and by the simulator's careful bot.
    public static float MoveValue(FishingGame game, FishingGame.Species s, SwapBoard board, (Vector2Int a, Vector2Int b) move,
        bool struggle, float catchBar, float hold)
    {
        var test = board.Clone();
        bool star = test.IsStarSwap(move.a, move.b);
        test.Swap(move.a, move.b);
        var hits = star ? test.StarBlast(move.a, move.b) : test.Resolve(new[] { move.a, move.b });
        float reel = 0, dh = 0;
        foreach (var h in hits)
        {
            var e = Effect(game.GetTile(h.type), h.amount, s, struggle, catchBar);
            reel += e.reel;
            dh += e.hold;
        }
        if (catchBar + reel >= 1) return 1000; // lands the fish
        if (hold + dh <= 0.05f) return -1000;
        float holdWeight = hold < 0.3f ? 3 : hold < 0.6f ? 1.2f : 0.4f;
        return reel + holdWeight * Mathf.Min(dh, 1 - hold) + test.Born * 0.05f + Random.value * 0.01f;
    }

    void Idle()
    {
        idleSince = Time.time;
        hint = null;
    }

    // ---- live fight ----

    void Update()
    {
        if (s == null) return;
        if (!over && !Landed && (phaseLeft -= Time.deltaTime) <= 0)
        {
            struggling = !struggling;
            phaseLeft = PhaseLength(struggling);
            if (struggling && s.lunges)
            {
                hold -= s.yank;
                Float("Cá lao!", FishingGame.Buoy);
                StartCoroutine(Shake());
                Vibrate();
            }
            if (struggling) skillPending = s.skill != FishingGame.Skill.None;
            else
            {
                // a tired fish lets go: spikes fold away and the ink drifts off
                sb.Calm();
                if (!busy) Render();
            }
        }
        // the fish's board move waits for any tiles still moving, so it never lands mid-animation
        if (skillPending && !busy && !over)
        {
            skillPending = false;
            picked = new Vector2Int(-1, -1);
            hint = null; // the board changed: pick a fresh hint, but keep the idle clock
            Float(sb.ApplySkill(s.skill), FishingGame.Buoy);
            StartCoroutine(Shake());
            Vibrate();
            Render();
        }
        if (!busy && !over && hint == null && Time.time - idleSince > (coach ? 0.4f : hintDelay))
        {
            var moves = sb.Moves();
            if (moves.Count > 0)
            {
                hint = moves.OrderByDescending(m => MoveValue(game, s, sb, m, struggling, catchBar, hold)).First();
                Render();
            }
        }
        // the hinted pair pulses around its centres
        if (!busy)
            foreach (var c in hintCells)
            {
                float scale = 1 + 0.08f * Mathf.PingPong(Time.time * 3, 1);
                var rt = views[c.x, c.y].rt;
                rt.localScale = Vector3.one * scale;
                rt.anchoredPosition = TileSpot(c.x, c.y) - new Vector2(1, -1) * rt.sizeDelta.x * (scale - 1) / 2;
            }
        // the coaching hand drags from one hinted tile to the other, over and over
        bool showHand = coach && !busy && !over && hint.HasValue && UI.Art("hand");
        hand.gameObject.SetActive(showHand);
        if (showHand)
        {
            float k = Mathf.Repeat(Time.time * 0.9f, 1), move = Mathf.SmoothStep(0, 1, Mathf.Clamp01((k - 0.2f) / 0.5f));
            hand.anchoredPosition = Vector2.Lerp(CellCentre(hint.Value.a), CellCentre(hint.Value.b), move);
            hand.SetAsLastSibling();
        }
        phaseText.text = struggling ? "CÁ GIẰNG" : "CÁ ĐUỐI";
        phaseText.color = struggling ? FishingGame.Buoy : Green;
        fishShape.localEulerAngles = struggling && !over ? new Vector3(0, 0, Mathf.Sin(Time.time * 30) * 6) : Vector3.zero;
        // once CÂU is full the fight is won: the line stops sinking while the last cascades play out
        if (!over && !Landed)
        {
            hold -= s.drain * DrainMult(struggling) * Time.deltaTime;
            // once CÂU is full the fish is landed, even if the cascade still running pulls GIỮ to zero
            if (hold <= 0 && catchBar < 1)
            {
                over = true;
                StopAllCoroutines();
                game.Lost("Đứt dây!", fish);
            }
        }
        // the bars glide to their values; GIỮ pulses red when the line is about to snap
        shownHold = Mathf.MoveTowards(shownHold, Mathf.Clamp01(hold), 1.2f * Time.deltaTime);
        shownCatch = Mathf.MoveTowards(shownCatch, Mathf.Clamp01(catchBar), 1.2f * Time.deltaTime);
        SetBar(holdFill, BarX, shownHold);
        SetBar(catchFill, CatchX, shownCatch);
        holdFillImg.color = hold < 0.25f ? Color.Lerp(Green, Color.red, Mathf.PingPong(Time.time * 3, 1)) : Green;
        // one warning buzz each time the line drops into the red
        if (!over && hold < 0.25f && !lowWarned)
        {
            lowWarned = true;
            Float("Dây sắp đứt!", Color.red);
            Vibrate();
        }
        if (hold > 0.35f) lowWarned = false;
        holdText.text = Pct(Mathf.Clamp01(hold));
        catchText.text = Pct(Mathf.Clamp01(catchBar));
    }

    // A board cell's centre in this screen's coordinates (the board sits at BoardX, 440).
    Vector2 CellCentre(Vector2Int c) => new Vector2(BoardX, -440) + TileSpot(c.x, c.y) + new Vector2(unit, -unit) * 0.46f;

    // CÂU is full: the fish is as good as caught (the level clock also pauses).
    public bool Landed => catchBar >= 1;

    static void SetBar(RectTransform fill, float x, float v)
    {
        float h = BarH * v;
        fill.anchoredPosition = new Vector2(x, -(BarY + BarH - h));
        fill.sizeDelta = new Vector2(BarW, h);
    }

    static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";

    static string Signed(float v) => (v >= 0 ? "+" : "−") + Pct(Mathf.Abs(v));

    void Render()
    {

        hintCells.Clear();
        unit = Mathf.Min(BoardW / SwapBoard.Cols, 740f / SwapBoard.Rows);
        boardOx = (BoardW - unit * SwapBoard.Cols) / 2;
        if (boardBg)
        {
            boardBg.anchoredPosition = new Vector2(BoardX + boardOx - 24, -(440 - 20));
            boardBg.sizeDelta = new Vector2(unit * SwapBoard.Cols + 48, unit * SwapBoard.Rows + 40);
        }
        float size = unit * 0.92f;
        for (int x = 0; x < SwapBoard.Cols; x++)
            for (int y = 0; y < SwapBoard.Rows; y++)
            {
                var v = views[x, y] ?? (views[x, y] = MakeView(new Vector2Int(x, y), size));
                tileRt[x, y] = v.rt;
                var type = sb.cell[x, y];
                v.rt.gameObject.SetActive(type != null);
                if (type == null) continue;
                var p = new Vector2Int(x, y);
                v.rt.localScale = Vector3.one;
                v.rt.anchoredPosition = TileSpot(x, y);

                bool star = type == SwapBoard.Star, ink = sb.inked[x, y], spike = sb.spiked[x, y];
                var sp = sb.special[x, y];
                var color = ink ? Ink : star ? Gold : game.GetTile(type).color;
                float alpha = type == "vot" && catchBar < NearAt && !ink ? 0.6f : 1; // the net waits for the fish to come close
                if (spike) color *= new Color(0.55f, 0.55f, 0.55f, 1);
                v.bg.color = new Color(color.r, color.g, color.b, alpha);

                var sprite = ink ? null : UI.Art(star ? "star" : "tile_" + type);
                v.icon.enabled = sprite;
                v.icon.sprite = sprite;
                v.icon.color = new Color(1, 1, 1, alpha);
                v.label.text = ink ? "?" : spike ? "GAI" : sprite ? "" : game.GetTile(type)?.name ?? type;
                v.label.color = spike && !ink ? FishingGame.Buoy : ink ? Color.white : FishingGame.Ink;
                v.label.fontSize = Mathf.RoundToInt(size * (ink ? 0.5f : 0.24f));
                v.stripeH.enabled = sp == SwapBoard.Special.LineH;
                v.stripeV.enabled = sp == SwapBoard.Special.LineV;

                bool hinted = hint.HasValue && (p == hint.Value.a || p == hint.Value.b) && !spike && p != picked;
                if (hinted) hintCells.Add(p);
                v.ring.enabled = spike || p == picked || hinted || sp == SwapBoard.Special.Bomb;
                v.ring.effectColor = spike ? Color.red : p == picked ? Color.white : hinted ? Gold : Ink;
                float d = spike ? 6 : hinted ? 8 : 7;
                v.ring.effectDistance = new Vector2(d, -d);

                var input = v.rt.GetComponent<TileInput>();
                input.enabled = sb.Movable(x, y);
                input.threshold = unit * 0.3f;
            }
    }

    // The pieces of one cell: background, picture, a label ("?" under ink, "GAI" on spikes), stripes and a ring.
    CellView MakeView(Vector2Int p, float size)
    {
        var bg = UI.Img(board, "Cell", 0, 0, size, size, Color.white);
        // a glossy candy-like tile, tinted with the move's colour
        if (UI.Art("tile_gloss")) bg.sprite = UI.Art("tile_gloss");
        else UI.Round(bg);
        var v = new CellView { rt = bg.rectTransform, bg = bg };
        v.icon = UI.Img(bg.transform, "Icon", size * 0.1f, size * 0.1f, size * 0.8f, size * 0.8f, Color.white);
        v.icon.raycastTarget = false;
        v.label = UI.Label(bg.transform, "", 0, 0, size, size, 20, FishingGame.Ink, TextAnchor.MiddleCenter, true);
        v.label.raycastTarget = false;
        var stripe = new Color(1, 1, 1, 0.85f);
        v.stripeH = UI.Img(bg.transform, "StripeH", 0, size * 0.44f, size, size * 0.12f, stripe);
        v.stripeV = UI.Img(bg.transform, "StripeV", size * 0.44f, 0, size * 0.12f, size, stripe);
        v.stripeH.raycastTarget = v.stripeV.raycastTarget = false;
        v.ring = bg.gameObject.AddComponent<Outline>();
        var input = bg.gameObject.AddComponent<TileInput>();
        input.onTap = () => SwapTap(p);
        input.onSwipe = dir => Swipe(p, dir);
        return v;
    }

    // A board tile: its colour and picture, a stripe for line specials, a ring for bombs, gold for the star.
    Image TileImage(Transform parent, string type, SwapBoard.Special sp, bool ink, float x, float y, float size)
    {
        bool star = type == SwapBoard.Star;
        var color = ink ? Ink : star ? Gold : game.GetTile(type).color;
        var img = UI.Img(parent, type, x, y, size, size, color);
        UI.Round(img);
        if (ink) UI.Label(img.transform, "?", 0, 0, size, size, Mathf.RoundToInt(size * 0.5f), Color.white, TextAnchor.MiddleCenter, true);
        else if (star && UI.Art("star"))
        {
            var icon = UI.Img(img.transform, "Icon", size * 0.12f, size * 0.12f, size * 0.76f, size * 0.76f, Color.white);
            icon.sprite = UI.Art("star");
            icon.raycastTarget = false;
        }
        else Face(img.transform, type, size);
        var stripe = new Color(1, 1, 1, 0.85f);
        if (sp == SwapBoard.Special.LineH) UI.Img(img.transform, "Stripe", 0, size * 0.44f, size, size * 0.12f, stripe).raycastTarget = false;
        if (sp == SwapBoard.Special.LineV) UI.Img(img.transform, "Stripe", size * 0.44f, 0, size * 0.12f, size, stripe).raycastTarget = false;
        if (sp == SwapBoard.Special.Bomb) Ring(img, Ink, 7);
        return img;
    }

    static void Ring(Image img, Color c, float d)
    {
        var o = img.gameObject.AddComponent<Outline>();
        o.effectColor = c;
        o.effectDistance = new Vector2(d, -d);
    }

    // One small card per move on this board: picture, name and what it does to the bars.
    void RenderLegend()
    {
        foreach (Transform c in legend) Destroy(c.gameObject);
        var kinds = Kinds;
        float w = 1000f / kinds.Length;
        for (int i = 0; i < kinds.Length; i++)
        {
            var k = game.GetTile(kinds[i]);
            float x = 40 + i * w;
            var box = UI.Img(legend, k.id, x + (w - 56) / 2, 0, 56, 56, k.color);
            UI.Round(box);
            var icon = UI.Art("tile_" + k.id);
            if (icon)
            {
                var im = UI.Img(box.transform, "Icon", 4, 4, 48, 48, Color.white);
                im.sprite = icon;
            }
            // coloured like the bars: orange moves CÂU, green moves GIỮ
            if (k.reel != 0) UI.Label(legend, Signed(k.reel), x, 60, w, 34, 28, FishingGame.Buoy, TextAnchor.MiddleCenter, true);
            if (k.hold != 0) UI.Label(legend, Signed(k.hold), x, k.reel != 0 ? 92 : 60, w, 34, 28, Green, TextAnchor.MiddleCenter, true);
            if (k.nearOnly) UI.Label(legend, $"≥{Pct(NearAt)}", x, 124, w, 30, 22, FishingGame.Muted, TextAnchor.MiddleCenter);
        }
    }

    void UseShuffle()
    {
        if (busy || over) return;
        if (!game.Spend(ShuffleCost)) { Float("Không đủ xu", FishingGame.Buoy); return; }
        sb.Shuffle();
        picked = new Vector2Int(-1, -1);
        Idle();
        Render();
    }

    // Drag a tile toward a neighbour to swap them (Candy Crush style). d is in board cells: y grows downward.
    void Swipe(Vector2Int p, Vector2Int d)
    {
        if (busy || over) return;
        Idle();
        var q = p + d;
        if (q.x < 0 || q.x >= SwapBoard.Cols || q.y < 0 || q.y >= SwapBoard.Rows || !sb.CanSwap(p, q)) return;
        picked = new Vector2Int(-1, -1);
        StartCoroutine(SwapRoutine(p, q));
    }

    // Reads a press on a tile: a short drag past the threshold is a swipe in its main direction, anything else is a tap.
    class TileInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public System.Action onTap;
        public System.Action<Vector2Int> onSwipe;
        public float threshold; // in canvas units
        Vector2 start;
        bool swiped;

        Vector2 home;

        public void OnPointerDown(PointerEventData e)
        {
            start = e.position;
            swiped = false;
            var rt = (RectTransform)transform;
            home = rt.anchoredPosition;
            Press(0.9f);
        }

        void Press(float scale)
        {
            var rt = (RectTransform)transform;
            rt.localScale = Vector3.one * scale;
            rt.anchoredPosition = home + new Vector2(1, -1) * rt.sizeDelta.x * (1 - scale) / 2;
        }

        public void OnDrag(PointerEventData e)
        {
            if (swiped) return;
            var delta = e.position - start;
            // screen pixels per canvas unit, so the threshold feels the same on every screen
            if (delta.magnitude < threshold * transform.lossyScale.x) return;
            swiped = true;
            Press(1);
            // screen y points up, board rows count down
            onSwipe(Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? new Vector2Int(delta.x > 0 ? 1 : -1, 0)
                                                           : new Vector2Int(0, delta.y > 0 ? -1 : 1));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (swiped) return;
            Press(1);
            onTap();
        }
    }

    // Or tap one tile, then a neighbour, to swap them.
    void SwapTap(Vector2Int p)
    {
        if (busy || over) return;
        Idle();
        if (picked.x >= 0 && sb.CanSwap(picked, p))
        {
            var a = picked;
            picked = new Vector2Int(-1, -1);
            StartCoroutine(SwapRoutine(a, p));
            return;
        }
        picked = p == picked ? new Vector2Int(-1, -1) : p;
        Render();
    }

    Vector2 CellPos(Vector2Int c) => new Vector2(BoardX + boardOx + c.x * unit, 440 + c.y * unit);

    // Where a cell's tile sits on the board (anchored position, y down).
    Vector2 TileSpot(int x, float y) => new Vector2(boardOx + x * unit, -y * unit);

    // The two tiles glide into each other's place.
    IEnumerator Slide(Vector2Int a, Vector2Int b, float duration)
    {
        RectTransform ta = tileRt[a.x, a.y], tb = tileRt[b.x, b.y];
        if (ta) ta.SetAsLastSibling();
        if (tb) tb.SetAsLastSibling();
        for (float k = 0; k < 1; k += Time.deltaTime / duration)
        {
            float e = k * k * (3 - 2 * k);
            if (ta) ta.anchoredPosition = Vector2.Lerp(TileSpot(a.x, a.y), TileSpot(b.x, b.y), e);
            if (tb) tb.anchoredPosition = Vector2.Lerp(TileSpot(b.x, b.y), TileSpot(a.x, a.y), e);
            yield return null;
        }
    }

    // Draws the board after a collapse with every moved tile lifted by its drop, then lets them fall into place.
    IEnumerator Fall()
    {
        Render();
        int most = 0;
        foreach (var d in sb.Drop) most = Mathf.Max(most, d);
        if (most == 0) yield break;
        float duration = 0.12f + 0.04f * most;
        for (float k = 0; k < 1; k += Time.deltaTime / duration)
        {
            // speeds up like a real drop, then a small bounce as it lands
            float e = k < 0.8f ? (k / 0.8f) * (k / 0.8f) : 1 + 0.12f * Mathf.Sin((k - 0.8f) / 0.2f * Mathf.PI);
            for (int x = 0; x < SwapBoard.Cols; x++)
                for (int y = 0; y < SwapBoard.Rows; y++)
                    if (tileRt[x, y] && sb.Drop[x, y] > 0)
                        tileRt[x, y].anchoredPosition = TileSpot(x, y - sb.Drop[x, y] * (1 - e));
            yield return null;
        }
        Render();
    }

    // The matched tiles swell for a moment before they fly off to the fish.
    IEnumerator Burst(List<SwapBoard.Hit> hits)
    {
        var cells = hits.SelectMany(h => h.cells).Distinct().Where(c => tileRt[c.x, c.y]).ToList();
        const float duration = 0.1f;
        for (float k = 0; k < 1; k += Time.deltaTime / duration)
        {
            float scale = 1 + 0.2f * Mathf.Sin(k * Mathf.PI / 2);
            foreach (var c in cells)
            {
                var rt = tileRt[c.x, c.y];
                rt.localScale = Vector3.one * scale;
                rt.anchoredPosition = TileSpot(c.x, c.y) - new Vector2(1, -1) * rt.sizeDelta.x * (scale - 1) / 2;
            }
            yield return null;
        }
    }

    IEnumerator SwapRoutine(Vector2Int a, Vector2Int b)
    {
        busy = true;
        bool star = sb.IsStarSwap(a, b);
        Render();
        yield return Slide(a, b, 0.12f);
        sb.Swap(a, b);
        Render();
        if (!star && sb.FindRuns().Count == 0)
        {
            // no match: the tiles slide back
            yield return Slide(a, b, 0.12f);
            sb.Swap(a, b);
            Render();
            busy = false;
            yield break;
        }

        // 1. the matches (and every cascade after them) work the line
        var hits = star ? sb.StarBlast(a, b) : sb.Resolve(new[] { a, b });
        int step = 0, born = star ? 0 : sb.Born;
        while (hits.Count > 0 && !over)
        {
            yield return Burst(hits);
            ApplyHits(hits, ++step);
            Render();
            yield return new WaitForSeconds(0.12f);
            sb.Collapse();
            yield return Fall();
            // a full CÂU lands the fish only after the cascade has run out, so every chain still plays
            if (hold <= 0 && catchBar < 1) break;
            hits = sb.Resolve(null);
            born += sb.Born;
        }
        game.QuestProgress(step, born);
        coach = false; // one real match is lesson enough
        if (catchBar >= 1 && !over)
        {
            over = true;
            yield return new WaitForSeconds(0.45f);
            game.Caught(fish);
            yield break;
        }
        if (over) yield break; // the line snapped (Update ends the fight)

        if (sb.Moves().Count == 0)
        {
            sb.Shuffle();
            Float("Xáo bàn", FishingGame.Muted);
        }
        Render();
        busy = false;
        Idle();
    }

    // Adds up one resolve step's moves into the two bars and shows it as one line.
    void ApplyHits(List<SwapBoard.Hit> hits, int step)
    {
        float reel = 0, dh = 0;
        bool netTooFar = false;
        foreach (var h in hits)
        {
            var k = game.GetTile(h.type);
            var e = Effect(k, h.amount, s, struggling, catchBar, step);
            reel += e.reel;
            dh += e.hold;
            netTooFar |= k.nearOnly && catchBar < NearAt;
            foreach (var c in h.cells) StartCoroutine(Fly(h.type, CellPos(c), unit * 0.92f, FishCenter - Vector2.one * 25, 50, 0.22f));
        }
        catchBar += reel;
        hold = Mathf.Min(1, hold + dh);

        if (step >= 2)
        {
            Float($"COMBO ×{ComboMult(step):0.##}", Gold);
            StartCoroutine(Bump(0.2f + 0.05f * step));
        }
        // just the numbers, in the bars' colours
        if (reel > 0) Float(Signed(reel), FishingGame.Buoy);
        else if (dh > 0) Float(Signed(dh), Green);
        if (netTooFar) Float($"Vợt: CÂU ≥{Pct(NearAt)}", FishingGame.Muted);
        StartCoroutine(Flash());
        StartCoroutine(Bump(Mathf.Clamp(reel * 2, 0.05f, 0.3f)));
        if (reel >= 0.15f) Vibrate();
    }

    static void AddIcon(Button b, string art)
    {
        if (!UI.Art(art)) return;
        var icon = UI.Img(b.transform, "Icon", 16, 26, 56, 56, Color.white);
        icon.sprite = UI.Art(art);
        icon.raycastTarget = false;
    }

    // A tile's picture; its name only if there is no picture for it.
    void Face(Transform parent, string type, float size)
    {
        var title = game.GetTile(type)?.name ?? type;
        var icon = UI.Art("tile_" + type);
        if (!icon)
        {
            UI.Label(parent, title, 0, 0, size, size, Mathf.RoundToInt(size * 0.22f), FishingGame.Ink, TextAnchor.MiddleCenter, true);
            return;
        }
        var img = UI.Img(parent, "Icon", size * 0.1f, size * 0.1f, size * 0.8f, size * 0.8f, Color.white);
        img.sprite = icon;
        img.raycastTarget = false;
    }

    // a line from the level (e.g. the side quest is done)
    public void Announce(string text) => Float(text, Green);

    int floating; // lines currently rising; each new one starts lower so they never overlap

    void Float(string text, Color color)
    {
        var t = UI.Label(floats, text, 40, 90 + 64 * floating, 1000, 64, 44, color, TextAnchor.MiddleCenter, true);
        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(1, 1, 1, 0.8f);
        shadow.effectDistance = new Vector2(2, -2);
        floating++;
        StartCoroutine(Rise(t));
    }

    IEnumerator Rise(Text t)
    {
        var rt = t.rectTransform;
        var start = rt.anchoredPosition;
        for (float k = 0; k < 1; k += Time.deltaTime / 0.9f)
        {
            rt.anchoredPosition = start + new Vector2(0, 90 * k);
            var c = t.color;
            c.a = 1 - k;
            t.color = c;
            yield return null;
        }
        Destroy(t.gameObject);
        floating--;
    }

    IEnumerator Shake()
    {
        for (float k = 0; k < 0.45f; k += Time.deltaTime)
        {
            fishShape.localEulerAngles = new Vector3(0, 0, Mathf.Sin(k * 40) * 10);
            yield return null;
        }
        fishShape.localEulerAngles = Vector3.zero;
    }

    IEnumerator Bump(float amount)
    {
        for (float k = 0; k < 0.4f; k += Time.deltaTime)
        {
            fishShape.localScale = Vector3.one * (1 + amount * Mathf.Sin(k / 0.4f * Mathf.PI));
            yield return null;
        }
        fishShape.localScale = Vector3.one;
    }

    IEnumerator Flash()
    {
        fishBody.color = fishTail.color = fishTail.enabled ? Color.white : new Color(1, 1, 1, 0.4f);
        yield return new WaitForSeconds(0.08f);
        fishBody.color = fishTail.color = fishTint;
    }

    // A copy of a tile that eases from one spot to another (top-left, y-down coordinates), then disappears.
    IEnumerator Fly(string type, Vector2 from, float fromSize, Vector2 to, float toSize, float duration)
    {
        var ghost = TileImage(floats, type, SwapBoard.Special.None, false, from.x, from.y, fromSize);
        var rt = ghost.rectTransform;
        for (float k = 0; k < 1; k += Time.deltaTime / duration)
        {
            float e = 1 - (1 - k) * (1 - k);
            var p = Vector2.Lerp(from, to, e);
            rt.anchoredPosition = new Vector2(p.x, -p.y);
            rt.localScale = Vector3.one * Mathf.Lerp(1, toSize / fromSize, e);
            yield return null;
        }
        Destroy(ghost.gameObject);
    }

    static void Vibrate()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
