using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Phase 2 (reel version): the fishing reel has three rings, each carrying one piece of line.
// Puzzle: tap a ring to turn it one step (45°, costs a move) until the line runs unbroken from the fish to the spool.
// Action: once the line is through, hold the spool to reel in (CÂU fills, CĂNG tightens) and let go to let line
// out (CĂNG eases). While the fish struggles reeling strains the line much more, and stronger fish knock a ring
// out of line.
// Lost fish (both count as a miss): CĂNG full = the line snaps; out of moves with the line still broken = it swims off.
public class ReelPhase : MonoBehaviour
{
    const int Steps = 8;
    const float SpoolR = 110, CalmSeconds = 3.4f, StruggleSeconds = 2.4f;
    // the spool turns this many radians a second while held; letting go eases CĂNG this much a second
    const float ReelSpeed = 2.4f, LetOut = 0.25f;
    static readonly Vector2 ReelCentre = new Vector2(540, 1000);
    static readonly (float r0, float r1)[] Bands = { (330, 440), (220, 325), (115, 215) }; // outside in, as drawn by make_art.py
    static readonly Color[] BandColors = { UI.Hex("#7DB8F0"), UI.Hex("#B99CF0"), UI.Hex("#7FD69A") };
    static readonly Color Green = UI.Hex("#2E8B57"), Gold = UI.Hex("#FFD54F"), Live = Color.white, Dead = new Color(0.62f, 0.7f, 0.74f, 0.75f);

    class Ring
    {
        public int a, b, rot;   // line enters at step a (outer edge), leaves at step b (inner edge); rot = turned steps
        public float shown;     // animated rotation in steps
        public RectTransform rt;
        public Image band;
        public readonly List<Image> core = new List<Image>(), casing = new List<Image>();
        public Image knotA, knotB;
        public bool locked;     // a puffer's spikes hold it still
    }

    FishingGame game;
    FishingGame.Species s;
    CastPhase.PondFish fish;
    readonly Ring[] rings = new Ring[3];
    RectTransform reel, spool, fishShape, floats, hand, entryLine;
    Text handLabel;
    Image fishBody, spoolImg, catchFill, tensionFill;
    Text nameText, phaseText, movesText, catchText, tensionText;
    float catchBar, tension, phaseLeft, spoolAngle, tickAngle, power, strain;
    int moves, floating, struggles;
    bool struggling, over, holding, inked;
    public bool coach;        // first play, first fish: a hand shows which ring to turn, then circles the spool; the fish never struggles
    public bool coachRelease; // first play: the first time a fish struggles, the hand shows spinning back to let line out

    // ---- set-up ----

    public void Init(FishingGame g)
    {
        game = g;
        // the whole screen takes the touches; where they land decides ring tap or spool spin
        var catcher = UI.Img(transform, "Input", 0, 0, 1080, 1720, new Color(0, 0, 0, 0));
        catcher.gameObject.AddComponent<ReelInput>().owner = this;

        if (UI.Art("panel_water")) UI.Img(transform, "FishBg", 40, 94, 1000, 132, Color.white).sprite = UI.Art("panel_water");
        nameText = UI.Label(transform, "", 40, 20, 1000, 70, 50, FishingGame.Ink, TextAnchor.MiddleLeft, true);
        // the line from the fish down into the reel (behind the bars)
        entryLine = UI.Img(transform, "EntryLine", 536, 200, 8, ReelCentre.y - Bands[0].r1 - 200, Live).rectTransform;
        entryLine.GetComponent<Image>().raycastTarget = false;

        fishShape = UI.Rect(transform, "Fish", 0, 0, 260, 100);
        fishShape.pivot = new Vector2(0.5f, 0.5f);
        fishShape.anchoredPosition = new Vector2(540, -160);
        fishBody = UI.Img(fishShape, "Body", 0, 0, 260, 100, Color.white);
        fishBody.raycastTarget = false;

        phaseText = UI.Label(transform, "", 40, 236, 1000, 80, 50, FishingGame.Ink, TextAnchor.MiddleCenter, true);
        (catchFill, catchText) = Bar("CÂU", 340, FishingGame.Buoy);
        (tensionFill, tensionText) = Bar("CĂNG", 430, Gold);

        reel = UI.Rect(transform, "Reel", 0, 0, 940, 940);
        reel.pivot = new Vector2(0.5f, 0.5f);
        reel.anchoredPosition = new Vector2(ReelCentre.x, -ReelCentre.y);
        Centered(reel, "Body", 940, Color.white, "reel_body");
        for (int i = 0; i < rings.Length; i++)
        {
            var r = new Ring();
            r.band = Centered(reel, "Ring" + i, Bands[i].r1 * 2, BandColors[i], "reel_ring_" + i);
            r.rt = r.band.rectTransform;
            rings[i] = r;
        }
        spoolImg = Centered(reel, "Spool", SpoolR * 2, Color.white, "reel_spool");
        spool = spoolImg.rectTransform;
        // the crank: an arm and a knob that turn with the spool
        var arm = UI.Img(spool, "Arm", 0, 0, 14, SpoolR - 26, FishingGame.Ink);
        arm.raycastTarget = false;
        var armRt = arm.rectTransform;
        armRt.anchorMin = armRt.anchorMax = new Vector2(0.5f, 0.5f);
        armRt.pivot = new Vector2(0.5f, 0);
        armRt.anchoredPosition = Vector2.zero;
        var knob = UI.Img(spool, "Knob", 0, 0, 44, 44, FishingGame.Ink, true);
        knob.raycastTarget = false;
        var knobRt = knob.rectTransform;
        knobRt.anchorMin = knobRt.anchorMax = knobRt.pivot = new Vector2(0.5f, 0.5f);
        knobRt.anchoredPosition = new Vector2(0, SpoolR - 30);

        UI.Round(UI.Img(transform, "MovesPill", 340, 1475, 400, 100, new Color(0.06f, 0.15f, 0.18f, 0.85f)));
        movesText = UI.Label(transform, "", 340, 1475, 400, 100, 52, Color.white, TextAnchor.MiddleCenter, true);

        floats = UI.Rect(transform, "Floats", 0, 0, 1080, 1720);
        var handImg = UI.Img(reel, "Hand", 0, 0, 120, 120, Color.white);
        handImg.sprite = UI.Art("hand");
        handImg.raycastTarget = false;
        hand = handImg.rectTransform;
        hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
        hand.pivot = new Vector2(0.5f, 0.97f);
        hand.gameObject.SetActive(false);
        handLabel = UI.Label(reel, "", 0, 0, 520, 60, 40, Color.white, TextAnchor.MiddleCenter, true);
        var labelRt = handLabel.rectTransform;
        labelRt.anchorMin = labelRt.anchorMax = labelRt.pivot = new Vector2(0.5f, 0.5f);
        var rim = handLabel.gameObject.AddComponent<Outline>();
        rim.effectColor = new Color(0.06f, 0.15f, 0.18f, 0.9f);
        rim.effectDistance = new Vector2(3, -3);
        gameObject.SetActive(false);
    }

    // An image centred in `parent`, square, with optional art.
    static Image Centered(Transform parent, string name, float size, Color color, string art)
    {
        var img = UI.Img(parent, name, 0, 0, size, size, color, !UI.Art(art));
        if (UI.Art(art)) img.sprite = UI.Art(art);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return img;
    }

    (Image fill, Text value) Bar(string title, float y, Color color)
    {
        UI.Label(transform, title, 60, y, 160, 50, 32, color == Gold ? UI.Hex("#B8860B") : color, TextAnchor.MiddleLeft, true);
        UI.Round(UI.Img(transform, title + "Bg", 220, y, 700, 50, UI.Hex("#22485A")));
        var fill = UI.Img(transform, title + "Fill", 224, y + 4, 0, 42, color);
        UI.Round(fill);
        var value = UI.Label(transform, "", 930, y, 120, 50, 30, FishingGame.Ink, TextAnchor.MiddleLeft, true);
        return (fill, value);
    }

    // Kept so the game can call it the same way as the match-3 fight: the reel needs no level-wide state.
    public void NewBoard() { }

    public void Begin(CastPhase.PondFish f)
    {
        StopAllCoroutines();
        fish = f;
        s = f.s;
        catchBar = 0.05f;
        tension = 0.15f;
        struggling = over = holding = inked = false;
        phaseLeft = CalmSeconds + 1;
        power = 0.8f + 0.1f * s.stars;
        // how much a struggle strains the line: half for a 1-star fish, full from 4 stars
        strain = Mathf.Lerp(0.5f, 1, (s.stars - 1) / 3f);
        struggles = 0;
        moves = 10 + 3 * s.stars; // chép 13 … cá mập 25: enough to fix the start and a few thrashes
        foreach (Transform c in floats) Destroy(c.gameObject);
        floating = 0;

        nameText.text = s.name;
        foreach (Transform c in nameText.transform) Destroy(c.gameObject);
        UI.Stars(nameText.transform, s.stars, s.stars, nameText.preferredWidth + 16, 14, 42);
        var sprite = UI.Art("fish_" + s.id);
        fishBody.sprite = sprite ? sprite : UI.Circle;
        fishBody.color = sprite ? Color.white : s.color;
        var shape = sprite ? s.size * Mathf.Min(120 / s.size.y, 340 / s.size.x) : new Vector2(260, 100);
        fishShape.sizeDelta = fishBody.rectTransform.sizeDelta = shape;
        fishShape.localScale = Vector3.one;
        fishShape.localEulerAngles = Vector3.zero;

        NewPuzzle();
        Render();
    }

    // Each ring gets a fresh piece of line; the solved turn is then undone by a few steps per ring,
    // so it takes that many clockwise taps to put each ring back.
    void NewPuzzle()
    {
        int want = 0;
        for (int i = 0; i < rings.Length; i++)
        {
            var r = rings[i];
            r.a = Random.Range(0, Steps);
            int turn = Random.Range(1, 4) * (Random.value < 0.5f ? 1 : -1);
            r.b = (r.a + turn + Steps) % Steps;
            r.rot = ((want - r.a) % Steps + Steps) % Steps; // solved
            want = (r.b + r.rot) % Steps;
            r.locked = false;
        }
        foreach (var r in rings)
        {
            // how far each ring starts out of line: 1 step in the tutorial, up to 2 for easy fish, up to 3 later
            int maxOff = coach ? 1 : s.stars <= 2 ? 2 : 3;
            r.rot = (r.rot - Random.Range(1, maxOff + 1) + Steps) % Steps;
            r.shown = r.rot;
            BuildPiece(r, System.Array.IndexOf(rings, r));
        }
    }

    // ---- the line pieces, built from short straight segments so they turn with their ring ----

    static Vector2 Polar(float step, float radius)
    {
        float a = step / Steps * Mathf.PI * 2; // clockwise from the top
        return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
    }

    void BuildPiece(Ring r, int i)
    {
        foreach (var img in r.core) Destroy(img.gameObject);
        foreach (var img in r.casing) Destroy(img.gameObject);
        if (r.knotA) Destroy(r.knotA.gameObject);
        if (r.knotB) Destroy(r.knotB.gameObject);
        r.core.Clear();
        r.casing.Clear();
        var (r0, r1) = Bands[i];
        float mid = (r0 + r1) / 2;
        int delta = ((r.b - r.a) % Steps + Steps) % Steps;
        if (delta > Steps / 2) delta -= Steps;
        var pts = new List<Vector2> { Polar(r.a, r1 - 6), Polar(r.a, mid) };
        int n = Mathf.Max(1, Mathf.Abs(delta) * 6);
        for (int k = 1; k <= n; k++) pts.Add(Polar(r.a + delta * (float)k / n, mid));
        pts.Add(Polar(r.b, r0 + 6));
        foreach (var (layer, width, color) in new[] { (r.casing, 26f, new Color(0.06f, 0.15f, 0.18f, 0.7f)), (r.core, 13f, Live) })
            for (int k = 0; k + 1 < pts.Count; k++)
                layer.Add(Segment(r.rt, pts[k], pts[k + 1], width, color));
        r.knotA = Knot(r.rt, Polar(r.a, r1 - 8));
        r.knotB = Knot(r.rt, Polar(r.b, r0 + 8));
    }

    static Image Segment(RectTransform parent, Vector2 from, Vector2 to, float width, Color color)
    {
        var img = UI.Img(parent, "Seg", 0, 0, 1, 1, color);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var d = to - from;
        rt.anchoredPosition = (from + to) / 2;
        rt.sizeDelta = new Vector2(d.magnitude + width * 0.6f, width); // a little overlap hides the joints
        rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        return img;
    }

    static Image Knot(RectTransform parent, Vector2 at)
    {
        var img = UI.Img(parent, "Knot", 0, 0, 30, 30, Gold, true);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = at;
        return img;
    }

    // How many rings, from the outside in, already carry the line through (3 = the line reaches the spool).
    int Depth()
    {
        int want = 0;
        for (int i = 0; i < rings.Length; i++)
        {
            var r = rings[i];
            if ((r.a + r.rot) % Steps != want) return i;
            want = (r.b + r.rot) % Steps;
        }
        return rings.Length;
    }

    // ---- input (from ReelInput, in reel-local coordinates: y up, centre at 0) ----

    void Press(Vector2 p)
    {
        if (over) return;
        float dist = p.magnitude;
        if (dist < SpoolR + 10)
        {
            holding = true;
            if (Depth() < rings.Length) Float("Dây chưa thông!", FishingGame.Muted);
            return;
        }
        for (int i = 0; i < rings.Length; i++)
            if (dist >= Bands[i].r0 - 5 && dist <= Bands[i].r1 + 10) { Tap(i); return; }
    }

    void Release() => holding = false;

    void Tap(int i)
    {
        var r = rings[i];
        if (r.locked) { Sfx.Play("bad", 1, 0.5f); Float("Gai nóc giữ vòng!", FishingGame.Buoy); return; }
        if (moves <= 0) { Sfx.Play("bad", 1, 0.5f); return; }
        moves--;
        int before = Depth();
        r.rot = (r.rot + 1) % Steps;
        int after = Depth();
        Sfx.Play("swap", 1.1f, 0.6f);
        if (after > before) Sfx.Play("pop", 1 + 0.15f * after);
        if (after == rings.Length && before < rings.Length) { Sfx.Play("special"); Float("Thông dây! Giữ tâm để cuộn", Green); }
        Render();
        CheckOutOfMoves();
    }

    // One frame of holding the spool: it turns and, with the line through, winds the fish in.
    void Reel(float dt)
    {
        float d = ReelSpeed * dt;
        spoolAngle += d;
        if (Depth() < rings.Length) return;
        catchBar += d * 0.045f / power * (struggling ? 0.5f : 1);
        tension += d * (struggling ? 0.11f * strain : 0.035f) * power;
        // a click every so often while the spool turns, rising as the fish comes in
        if (spoolAngle - tickAngle > 0.6f)
        {
            tickAngle = spoolAngle;
            Sfx.Play("tap", 0.8f + catchBar * 0.6f, 0.35f);
        }
        if (catchBar >= 1 && !over) StartCoroutine(Land());
    }

    void CheckOutOfMoves()
    {
        if (!over && moves <= 0 && Depth() < rings.Length) Lose("Hết lượt, cá thoát!");
    }

    void Lose(string why)
    {
        over = true;
        StopAllCoroutines();
        game.Lost(why, fish);
    }

    public bool Landed => catchBar >= 1;

    public void Announce(string text) => Float(text, Green);

    // ---- the fight's rhythm ----

    void Update()
    {
        if (s == null) return;
        foreach (var r in rings)
        {
            float diff = Mathf.Repeat(r.rot - r.shown + Steps / 2f, Steps) - Steps / 2f;
            r.shown = Mathf.Repeat(r.shown + diff * Mathf.Min(1, Time.deltaTime * 14), Steps);
            r.rt.localEulerAngles = new Vector3(0, 0, -r.shown * 360f / Steps);
        }
        spool.localEulerAngles = new Vector3(0, 0, -spoolAngle * Mathf.Rad2Deg);

        if (!over && !Landed)
        {
            // the first fish of the first play never struggles: it only teaches connect and reel
            if (!coach && (phaseLeft -= Time.deltaTime) <= 0) TogglePhase();
            tension = Mathf.Max(0, tension + (struggling ? 0.06f * power * strain : -0.1f) * Time.deltaTime);
            if (holding) Reel(Time.deltaTime);
            else tension = Mathf.Max(0, tension - LetOut * Time.deltaTime); // let go: line runs out
            if (over) return; // landed this frame
            if (Depth() < rings.Length) catchBar = Mathf.Max(0.02f, catchBar - 0.02f * Time.deltaTime); // slack line: the fish drifts off
            if (tension >= 1) { Lose("Đứt dây!"); return; }
        }

        // the fish sways while it fights and is pulled closer as CÂU fills
        if (!over)
        {
            float sway = struggling ? Mathf.Sin(Time.time * 11) * 60 : 0;
            fishShape.anchoredPosition = new Vector2(540 + sway, -160);
            fishShape.localEulerAngles = new Vector3(0, 0, struggling ? Mathf.Sin(Time.time * 30) * 8 : Mathf.Sin(Time.time * 2) * 3);
        }
        Coach();
        Render();
    }

    void TogglePhase()
    {
        struggling = !struggling;
        phaseLeft = struggling ? StruggleSeconds : CalmSeconds;
        foreach (var r in rings) r.locked = false;
        inked = false;
        if (!struggling) { coachRelease = coachRelease && struggles == 0; return; }
        struggles++;
        // the fish thrashes and knocks rings out of line: never for 1-star fish, every other struggle for 2 stars,
        // one step from 3 stars, two for the strong lungers (4 stars and up); the shark's weed knocks two rings
        int knock = Knock();
        int count = s.skill == FishingGame.Skill.Weed ? 2 : 1;
        for (int k = 0; k < count && knock > 0; k++)
        {
            var r = rings[Random.Range(0, rings.Length)];
            r.rot = (r.rot - knock + Steps) % Steps;
        }
        Sfx.Play("bite", 0.7f);
        StartCoroutine(Shake());
        if (s.skill == FishingGame.Skill.Ink) { inked = true; Float("Mực phun mực!", FishingGame.Buoy); }
        else if (s.skill == FishingGame.Skill.Spikes) { rings[Random.Range(0, rings.Length)].locked = true; Float("Nóc xù gai!", FishingGame.Buoy); }
        else if (s.skill == FishingGame.Skill.Weed) Float("Cá mập quẫy mạnh!", FishingGame.Buoy);
        else Float(knock > 0 ? "Cá giằng!" : "Cá giằng, nhả dây!", FishingGame.Buoy);
        CheckOutOfMoves();
    }

    int Knock() =>
        s.stars <= 1 ? 0 :
        s.stars == 2 ? (struggles % 2 == 0 ? 1 : 0) :
        s.stars == 3 ? 1 :
        s.lunges ? 2 : 1;

    void Render()
    {
        int depth = Depth();
        for (int i = 0; i < rings.Length; i++)
        {
            var r = rings[i];
            bool live = i < depth;
            r.band.color = r.locked ? Color.Lerp(BandColors[i], Color.red, 0.5f) : Color.Lerp(BandColors[i], Color.white, CoachPulse(i));
            foreach (var img in r.core) img.color = inked ? new Color(0, 0, 0, 0) : live ? Live : Dead;
            foreach (var img in r.casing) img.enabled = !inked;
            r.knotA.color = r.knotB.color = inked ? new Color(0, 0, 0, 0) : live ? Gold : UI.Hex("#5E7C88");
        }
        entryLine.GetComponent<Image>().color = depth > 0 ? Live : Dead;
        spoolImg.color = depth == rings.Length ? UI.Hex("#FFB35C") : UI.Hex("#8FA4AC");
        phaseText.text = struggling ? "CÁ GIẰNG — NHẢ DÂY" : "CÁ ĐUỐI — CUỘN";
        phaseText.color = struggling ? FishingGame.Buoy : Green;
        SetFill(catchFill, catchBar);
        SetFill(tensionFill, tension);
        tensionFill.color = tension > 0.75f ? Color.Lerp(UI.Hex("#FF8A3D"), Color.red, Mathf.PingPong(Time.time * 4, 1)) : Gold;
        catchText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(catchBar) * 100)}%";
        tensionText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(tension) * 100)}%";
        movesText.text = $"Lượt: {moves}";
        movesText.color = moves <= 3 ? FishingGame.Buoy : Color.white;
    }

    static void SetFill(Image fill, float v) => fill.rectTransform.sizeDelta = new Vector2(692 * Mathf.Clamp01(v), 42);

    // First play: the hand taps the ring to turn next (that ring pulses), circles the spool once the line is through,
    // and the first time a fish struggles circles it the other way. A two-word caption sits under it.
    void Coach()
    {
        bool release = coachRelease && struggling, show = (coach || release) && !over && UI.Art("hand");
        hand.gameObject.SetActive(show);
        handLabel.gameObject.SetActive(show);
        if (!show) return;
        int depth = Depth();
        if (depth < rings.Length && !release)
        {
            float mid = (Bands[depth].r0 + Bands[depth].r1) / 2;
            hand.anchoredPosition = new Vector2(0, mid) + Vector2.down * Mathf.Abs(Mathf.Sin(Time.time * 4)) * 20;
            handLabel.text = "Chạm để xoay";
            handLabel.rectTransform.anchoredPosition = new Vector2(0, mid - 160);
        }
        else
        {
            // pressing down on the spool, or lifting off it for the release lesson
            float bob = Mathf.Abs(Mathf.Sin(Time.time * 4));
            hand.anchoredPosition = release ? new Vector2(0, 20 + bob * 90) : new Vector2(0, -bob * 16);
            handLabel.text = release ? "Thả tay để nhả dây" : "Giữ tâm để cuộn";
            handLabel.color = release ? UI.Hex("#FFB35C") : Color.white;
            handLabel.rectTransform.anchoredPosition = new Vector2(0, -SpoolR - 150);
        }
        if (!release) handLabel.color = Color.white;
        hand.SetAsLastSibling();
        handLabel.transform.SetAsLastSibling();
    }

    // The ring the tutorial wants turned next gently pulses.
    float CoachPulse(int i) => coach && !over && i == Depth() ? 0.25f * Mathf.PingPong(Time.time * 3, 1) : 0;

    // ---- effects ----

    IEnumerator Land()
    {
        over = true;
        coach = false;
        game.ReelCaught(moves);
        Float("BẮT ĐƯỢC!", Green);
        var home = fishShape.anchoredPosition;
        for (float t = 0; t < 0.7f; t += Time.deltaTime)
        {
            float k = t / 0.7f;
            fishShape.anchoredPosition = home + new Vector2(0, Mathf.Sin(k * Mathf.PI) * 90);
            fishShape.localScale = Vector3.one * (1 + 0.35f * Mathf.Sin(k * Mathf.PI));
            fishShape.localEulerAngles = new Vector3(0, 0, k * 360);
            yield return null;
        }
        fishShape.localScale = Vector3.one;
        game.Caught(fish);
    }

    IEnumerator Shake()
    {
        var home = reel.anchoredPosition;
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            reel.anchoredPosition = home + new Vector2(Mathf.Sin(t * 90) * 14 * (1 - t / 0.3f), 0);
            yield return null;
        }
        reel.anchoredPosition = home;
    }

    void Float(string text, Color color)
    {
        var t = UI.Label(floats, text, 40, 110 + 64 * (floating % 2), 1000, 64, 44, color, TextAnchor.MiddleCenter, true);
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
        for (float k = 0; k < 1; k += Time.deltaTime / 1.1f)
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

    // Forwards touches on the screen to the reel, converted to reel-local coordinates.
    class ReelInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public ReelPhase owner;

        Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.reel, e.position, e.pressEventCamera, out var p);
            return p;
        }

        public void OnPointerDown(PointerEventData e) => owner.Press(Local(e));
        public void OnPointerUp(PointerEventData e) => owner.Release();
    }
}
