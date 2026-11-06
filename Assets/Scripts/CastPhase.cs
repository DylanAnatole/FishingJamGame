using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Phase 1: fish swim in the lake; hold the mouse/finger in the water to steer the hook.
// Any fish the hook touches bites. The chosen bait only helps: the fish it attracts swim toward the hook when it comes near.
public class CastPhase : MonoBehaviour
{
    public class PondFish
    {
        public FishingGame.Species s;
        public float x, y01;
        public int dir;
        public RectTransform rt, tag;
        public Outline ring;
    }

    class Drift
    {
        public FishingGame.Snag d;
        public float x;
        public int dir;
        public RectTransform rt;
    }

    const float W = 1080, H = 1400, RodX = 640, HookSpeed = 325, TurnRate = 400, LineMax = 2600, ReelSpeed = 1500, SnagH = 36;
    // attracted fish within this distance of the hook turn toward it and swim up or down to it
    const float LureRange = 300, LureClimb = 70;
    static readonly Color Wood = UI.Hex("#6B5A45");

    FishingGame game;
    RectTransform water, hook, line, fx;
    Text snagLabel, readyLabel, baitLabel;
    Button baitButton;
    readonly List<PondFish> pond = new List<PondFish>();
    readonly List<Drift> snags = new List<Drift>();
    // the line is drawn through the points the hook has passed, so it bends like a real line
    readonly List<Vector2> trail = new List<Vector2>();
    readonly List<Image> segs = new List<Image>();
    Vector2 hookPos, heading;
    float lineUsed, graceUntil, nextBubble;
    bool running, stuck, reeling, waiting; // waiting: hook hangs under the boat until the player taps the water
    // first play only: a hand taps the water, then rests on the fish to steer to, until a wanted fish is hooked
    public bool coach;
    RectTransform hand;

    public void Init(FishingGame g)
    {
        game = g;
        var sky = UI.Img(transform, "Sky", 0, 0, 1080, 160, UI.Hex("#DBE8EA"));
        if (UI.Art("sky")) { sky.sprite = UI.Art("sky"); sky.color = Color.white; }
        // the pixel boat's fisherman holds his own rod; its tip sits right above RodX
        if (UI.Art("boat")) UI.Img(transform, "Boat", 498, 15, 200, 150, Color.white).sprite = UI.Art("boat");
        else
        {
            UI.Img(transform, "Boat", 400, 118, 220, 42, UI.Hex("#7A5A3A"));
            UI.Img(transform, "Fisher", 470, 62, 50, 50, FishingGame.Ink, true);
        }
        if (!UI.Art("boat")) SetLine(UI.Img(transform, "Rod", 0, 0, 1, 1, FishingGame.Ink).rectTransform, new Vector2(500, 90), new Vector2(RodX, 30), 8);
        UI.Img(transform, "RodLine", RodX - 2, 77, 4, 83, Color.white);
        // switch bait while the hook waits under the boat
        baitButton = UI.Btn(transform, "", 30, 30, 330, 100, FishingGame.Panel, FishingGame.Ink, () => { if (waiting) game.NextBait(); });
        baitLabel = baitButton.GetComponentInChildren<Text>();
        baitLabel.fontSize = 28;

        water = UI.Img(transform, "Water", 0, 160, W, H, UI.Hex("#1D6A80")).rectTransform;
        water.gameObject.AddComponent<RectMask2D>();
        UI.Img(water, "Deep", 0, H / 2, W, H / 2, new Color(0.05f, 0.23f, 0.29f, 0.55f));
        if (UI.Art("seabed")) UI.Img(water, "Seabed", 0, H - 320, W, 320, Color.white).sprite = UI.Art("seabed");
        line = UI.Rect(water, "Line", 0, 0, W, H);
        fx = UI.Rect(water, "Fx", 0, 0, W, H);
        hook = UI.Art("hook")
            ? UI.Img(water, "Hook", 0, 0, 40, 56, Color.white).rectTransform
            : UI.Img(water, "Hook", 0, 0, 30, 30, Color.white, true).rectTransform;
        if (UI.Art("hook")) hook.GetComponent<Image>().sprite = UI.Art("hook");
        hook.pivot = new Vector2(0.5f, 0.5f);
        snagLabel = UI.Label(water, "Vướng cành!", 0, 0, 360, 70, 50, FishingGame.Buoy, TextAnchor.MiddleCenter, true);
        snagLabel.gameObject.SetActive(false);
        readyLabel = UI.Label(water, "Chạm nước để thả câu", 0, 60, W, 60, 42, Color.white, TextAnchor.MiddleCenter, true);
        var handImg = UI.Img(water, "Hand", 0, 0, 110, 110, Color.white);
        handImg.sprite = UI.Art("hand");
        handImg.raycastTarget = false;
        hand = handImg.rectTransform;
        hand.pivot = new Vector2(0.5f, 0.97f);
        hand.gameObject.SetActive(false);
        if (UI.Art("wave")) UI.Img(transform, "Wave", 0, 140, W, 40, Color.white).sprite = UI.Art("wave");
    }

    public void Restock()
    {
        StopAllCoroutines();
        foreach (var f in pond) Destroy(f.rt.gameObject);
        pond.Clear();
        foreach (var d in snags) Destroy(d.rt.gameObject);
        snags.Clear();
        running = stuck = false;
        snagLabel.gameObject.SetActive(false);
        foreach (Transform c in fx) Destroy(c.gameObject);
        water.anchoredPosition = new Vector2(0, -160);
        ResetLine();
        var spots = game.Current.pond;
        for (int i = 0; i < spots.Length; i++) pond.Add(MakeFish(game.GetFish(spots[i].species), spots[i].depth, i));
        foreach (var f in pond) PlaceFish(f);
        foreach (var d in game.Current.snags ?? new FishingGame.Snag[0])
        {
            var img = UI.Img(water, "Snag", 0, 0, d.width, SnagH, Wood);
            var rt = img.rectTransform;
            if (UI.Art("branch")) { img.sprite = UI.Art("branch"); img.color = Color.white; }
            else UI.Label(rt, "cành cây", 0, 0, d.width, SnagH, 26, Color.white, TextAnchor.MiddleCenter);
            var drift = new Drift { d = d, rt = rt, dir = snags.Count % 2 == 0 ? -1 : 1, x = Random.Range(0, W - d.width) };
            snags.Add(drift);
            PlaceSnag(drift);
        }
        line.SetAsLastSibling();
        fx.SetAsLastSibling();
        hook.SetAsLastSibling();
        snagLabel.transform.SetAsLastSibling();
        readyLabel.transform.SetAsLastSibling();
        hand.SetAsLastSibling();
    }

    PondFish MakeFish(FishingGame.Species s, float y01, int i)
    {
        var rt = UI.Rect(water, "Fish_" + s.id, 0, 0, s.size.x, s.size.y);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var sprite = UI.Art("fish_" + s.id);
        if (!sprite) UI.Img(rt, "Tail", s.size.x * 0.78f, s.size.y * 0.2f, s.size.y * 0.6f, s.size.y * 0.6f, s.color, true);
        // pixel sprites keep their own colours; without one, the Inspector colour tints the plain oval
        var body = UI.Img(rt, "Body", 0, 0, s.size.x, s.size.y, sprite ? Color.white : s.color, !sprite);
        if (sprite) body.sprite = sprite;
        var ring = body.gameObject.AddComponent<Outline>();
        ring.effectColor = FishingGame.Buoy;
        ring.effectDistance = new Vector2(5, 5);
        var tag = UI.Label(rt, s.shortName, 0, 0, s.size.x + 160, 44, 30, Color.white, TextAnchor.MiddleCenter, true).rectTransform;
        tag.pivot = new Vector2(0.5f, 1);
        tag.anchoredPosition = new Vector2(s.size.x / 2, -(s.size.y + 4));
        return new PondFish
        {
            s = s, y01 = y01, dir = i % 2 == 0 ? 1 : -1, rt = rt, tag = tag, ring = ring,
            x = Random.Range(20f, W - s.size.x - 20),
        };
    }

    public void Begin()
    {
        BaitChanged();
        stuck = false;
        // effects frozen when the fight screen took over
        foreach (Transform c in fx) Destroy(c.gameObject);
        running = true;
        ResetLine();
    }

    // Orange ring = still needed.
    public void BaitChanged()
    {
        if (game.Bait == null) return;
        baitLabel.text = $"Mồi: {game.Bait.name}";
        foreach (var f in pond)
        {
            f.ring.enabled = game.StillNeeded(f.s.id);
            // the outline is hard to see on pixel sprites, so the name turns orange too
            f.tag.GetComponent<Text>().color = f.ring.enabled ? FishingGame.Buoy : Color.white;
        }
    }

    // Line wound in: the hook hangs under the boat and waits for a tap.
    void ResetLine()
    {
        hookPos = new Vector2(RodX, 10);
        heading = Vector2.up; // y-down coordinates, so "up" points down into the water
        trail.Clear();
        trail.Add(new Vector2(RodX, 0));
        lineUsed = 0;
        reeling = false;
        waiting = true;
        baitButton.interactable = true;
        // ignore the tap that closed a dialog in this same frame
        graceUntil = Time.time + 0.2f;
        readyLabel.gameObject.SetActive(true);
        PlaceHook();
    }

    // The hook bobs under the boat; a tap in the water drops it, heading toward the tap.
    void WaitToCast()
    {
        hookPos.y = 12 + Mathf.Sin(Time.time * 4) * 4;
        readyLabel.color = new Color(1, 1, 1, 0.55f + 0.45f * Mathf.PingPong(Time.time * 1.5f, 1));
        if (Time.time < graceUntil || !Input.GetMouseButtonDown(0) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(water, Input.mousePosition, null, out var lp)) return;
        var tap = new Vector2(lp.x, -lp.y);
        if (tap.x < 0 || tap.x > W || tap.y < 0 || tap.y > H) return;
        waiting = false;
        baitButton.interactable = false;
        readyLabel.gameObject.SetActive(false);
        var aim = tap - hookPos;
        heading = aim.y > 20 ? aim.normalized : Vector2.up; // a tap right at the surface just drops straight down
        graceUntil = Time.time + 0.3f;
        StartCoroutine(Splash(hookPos));
    }

    // true while the hook is out and can still catch something (used by the level clock, so it rests while the hook waits)
    public bool Running => running && !waiting;

    // stops the hook where it is, e.g. when the level clock runs out
    public void Halt() => running = false;

    public void Remove(PondFish f)
    {
        pond.Remove(f);
        Destroy(f.rt.gameObject);
    }

    void Update()
    {
        if (!running) { hand.gameObject.SetActive(false); return; }

        foreach (var d in snags)
        {
            d.x += d.dir * d.d.speed * Time.deltaTime;
            if (d.x < 0) { d.x = 0; d.dir = 1; }
            if (d.x > W - d.d.width) { d.x = W - d.d.width; d.dir = -1; }
            float cy = PlaceSnag(d);
            if (!stuck && !reeling && !waiting && hookPos.x > d.x - 12 && hookPos.x < d.x + d.d.width + 12 && Mathf.Abs(hookPos.y - cy) < SnagH / 2 + 12)
                StartCoroutine(Snagged());
        }

        if (!stuck)
        {
            if (waiting) WaitToCast();
            else if (reeling) Reel();
            else Swim();
            PlaceHook();
        }

        PondFish hit = null;
        bool hookOut = !stuck && !reeling && !waiting;
        foreach (var f in pond)
        {
            var s = f.s;
            bool lured = hookOut && game.Attracts(s) && Vector2.Distance(Centre(f), hookPos) < LureRange;
            if (lured)
            {
                // smells the bait: heads for the hook
                f.dir = hookPos.x > Centre(f).x ? 1 : -1;
                f.y01 = Mathf.MoveTowards(f.y01, (hookPos.y - 60) / (H - 120), LureClimb / (H - 120) * Time.deltaTime);
            }
            f.x += f.dir * s.speed * Time.deltaTime;
            if (f.x < 4) { f.x = 4; f.dir = 1; }
            if (f.x > W - s.size.x - 4) { f.x = W - s.size.x - 4; f.dir = -1; }
            var (cx, cy) = PlaceFish(f);

            float ex = (hookPos.x - cx) / (s.size.x / 2), ey = (hookPos.y - cy) / (s.size.y / 2);
            if (hit == null && hookOut && Time.time > graceUntil && ex * ex + ey * ey <= 1) hit = f;
        }

        Coach();

        if (hit != null)
        {
            running = false;
            StartCoroutine(Strike(hit));
        }
    }

    // Snake-style steering: the hook always moves forward and, while the finger is held, turns toward it at a capped rate.
    void Swim()
    {
        if (Input.GetMouseButton(0) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(water, Input.mousePosition, null, out var lp))
        {
            var want = new Vector2(lp.x, -lp.y) - hookPos;
            // close to the finger, keep going straight instead of circling around it
            if (want.sqrMagnitude > 60 * 60)
                heading = Vector3.RotateTowards(heading, want.normalized, TurnRate * Mathf.Deg2Rad * Time.deltaTime, 0);
        }
        hookPos += heading * HookSpeed * Time.deltaTime;
        // bounce off the edges of the lake
        if (hookPos.x < 10 || hookPos.x > W - 10) { hookPos.x = Mathf.Clamp(hookPos.x, 10, W - 10); heading.x = -heading.x; }
        if (hookPos.y < 10 || hookPos.y > H - 10) { hookPos.y = Mathf.Clamp(hookPos.y, 10, H - 10); heading.y = -heading.y; }
        lineUsed += HookSpeed * Time.deltaTime;
        if (Vector2.Distance(trail[trail.Count - 1], hookPos) > 24) trail.Add(hookPos);
        if (Time.time > nextBubble)
        {
            nextBubble = Time.time + 0.12f;
            StartCoroutine(Bubble(hookPos - heading * 18 + Random.insideUnitCircle * 6));
        }
        if (lineUsed >= LineMax) reeling = true;
    }

    // Out of line or snagged: the hook is wound back along the path it came, then hangs under the boat again.
    void Reel()
    {
        float step = ReelSpeed * Time.deltaTime;
        while (trail.Count > 1)
        {
            var p = trail[trail.Count - 1];
            float d = Vector2.Distance(hookPos, p);
            if (d > step)
            {
                hookPos = Vector2.MoveTowards(hookPos, p, step);
                return;
            }
            hookPos = p;
            step -= d;
            trail.RemoveAt(trail.Count - 1);
        }
        ResetLine();
    }

    // A short beat on the hook-up: the line snaps taut, the lake shakes, the fish jerks and is yanked toward the boat.
    IEnumerator Strike(PondFish f)
    {
        var c = f.rt.anchoredPosition;
        var label = UI.Label(water, "Dính!", c.x - 150, -c.y - f.s.size.y - 70, 300, 80, 60, FishingGame.Buoy, TextAnchor.MiddleCenter, true);
        trail.RemoveRange(1, trail.Count - 1);
        StartCoroutine(Splash(hookPos));
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
        var home = water.anchoredPosition;
        var rod = new Vector2(RodX, 0);
        for (float k = 0; k < 0.5f; k += Time.deltaTime)
        {
            f.rt.localEulerAngles = new Vector3(0, 0, Mathf.Sin(k * 50) * 15);
            // the fish is dragged a little up the taut line
            f.rt.anchoredPosition = c + new Vector2(rod.x - c.x, -c.y) * (0.15f * k / 0.5f);
            hook.anchoredPosition = f.rt.anchoredPosition;
            hookPos = new Vector2(hook.anchoredPosition.x, -hook.anchoredPosition.y);
            water.anchoredPosition = home + Random.insideUnitCircle * 10 * (1 - k / 0.5f);
            PlaceHook();
            yield return null;
        }
        water.anchoredPosition = home;
        f.rt.localEulerAngles = Vector3.zero;
        Destroy(label.gameObject);
        game.Hooked(f);
    }

    // A ring that widens and fades where the hook breaks the water or bites.
    IEnumerator Splash(Vector2 at)
    {
        var ring = UI.Img(fx, "Splash", at.x, at.y, 120, 120, Color.white, true).rectTransform;
        ring.pivot = new Vector2(0.5f, 0.5f);
        ring.anchoredPosition = new Vector2(at.x, -at.y);
        var img = ring.GetComponent<Image>();
        for (float k = 0; k < 1; k += Time.deltaTime / 0.4f)
        {
            ring.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.4f, k);
            img.color = new Color(1, 1, 1, 0.6f * (1 - k));
            yield return null;
        }
        Destroy(ring.gameObject);
    }

    // Small bubbles left behind the moving hook, drifting up and fading.
    IEnumerator Bubble(Vector2 at)
    {
        float size = Random.Range(8f, 16f);
        var b = UI.Img(fx, "Bubble", at.x, at.y, size, size, Color.white, true);
        var rt = b.rectTransform;
        for (float k = 0; k < 1; k += Time.deltaTime / 0.7f)
        {
            rt.anchoredPosition = new Vector2(at.x + Mathf.Sin(k * 9) * 4, -(at.y - 50 * k));
            b.color = new Color(1, 1, 1, 0.5f * (1 - k));
            yield return null;
        }
        Destroy(b.gameObject);
    }

    // The hook catches on a branch: it rattles for a moment, then the line is wound back in.
    IEnumerator Snagged()
    {
        stuck = true;
        var at = hookPos;
        snagLabel.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(at.x - 180, 0, W - 360), -Mathf.Max(0, at.y - 90));
        snagLabel.gameObject.SetActive(true);
        for (float k = 0; k < 0.6f; k += Time.deltaTime)
        {
            hookPos = at + Random.insideUnitCircle * 8;
            PlaceHook();
            yield return null;
        }
        snagLabel.gameObject.SetActive(false);
        hookPos = at;
        reeling = true;
        stuck = false;
    }

    // Positions a branch; returns its centre y in water coordinates.
    float PlaceSnag(Drift d)
    {
        float cy = d.d.depth * (H - 120) + 60;
        d.rt.anchoredPosition = new Vector2(d.x, -(cy - SnagH / 2));
        return cy;
    }

    // The hand points at the nearest fish still needed: tapping it while the hook waits, holding on it once the hook is out.
    void Coach()
    {
        PondFish target = null;
        if (coach && !stuck && !reeling && UI.Art("hand"))
            foreach (var f in pond)
                if (game.StillNeeded(f.s.id) && (target == null || Vector2.Distance(Centre(f), hookPos) < Vector2.Distance(Centre(target), hookPos)))
                    target = f;
        hand.gameObject.SetActive(target != null);
        if (target == null) return;
        var c = Centre(target);
        float press = waiting ? Mathf.Abs(Mathf.Sin(Time.time * 4)) * 24 : Mathf.Sin(Time.time * 3) * 8;
        hand.anchoredPosition = new Vector2(c.x, -c.y - press);
    }

    static Vector2 Centre(PondFish f) => new Vector2(f.x + f.s.size.x / 2, f.y01 * (H - 120) + 60);

    // Positions and faces a fish; returns its centre in water coordinates.
    static (float cx, float cy) PlaceFish(PondFish f)
    {
        var s = f.s;
        float cx = f.x + s.size.x / 2, cy = f.y01 * (H - 120) + 60;
        f.rt.anchoredPosition = new Vector2(cx, -cy);
        var flip = new Vector3(f.dir < 0 ? 1 : -1, 1, 1);
        f.rt.localScale = flip;
        f.tag.localScale = flip;
        return (cx, cy);
    }

    void PlaceHook()
    {
        hook.anchoredPosition = new Vector2(hookPos.x, -hookPos.y);
        // one segment per trail point, the last one ending at the hook; the line turns orange when it is nearly out
        var color = !reeling && lineUsed > LineMax * 0.8f ? FishingGame.Buoy : Color.white;
        int n = trail.Count;
        for (int i = 0; i < n; i++)
        {
            if (i == segs.Count) segs.Add(UI.Img(line, "Seg", 0, 0, 1, 1, Color.white));
            SetLine(segs[i].rectTransform, trail[i], i + 1 < n ? trail[i + 1] : hookPos, 4);
            segs[i].color = color;
            segs[i].enabled = true;
        }
        for (int i = n; i < segs.Count; i++) segs[i].enabled = false;
    }

    // Stretches an image between two points given in top-left, y-down coordinates.
    static void SetLine(RectTransform rt, Vector2 a, Vector2 b, float thickness)
    {
        rt.pivot = new Vector2(0.5f, 0.5f);
        var mid = (a + b) / 2;
        rt.anchoredPosition = new Vector2(mid.x, -mid.y);
        var d = b - a;
        rt.sizeDelta = new Vector2(d.magnitude, thickness);
        rt.localEulerAngles = new Vector3(0, 0, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }
}
