using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Fishing prototype: pick a bait (it lures some species), steer the hook to a fish, then fight the fish on a swap-to-match-3 board.
// The fight has two bars: GIỮ (the line's hold, sinks over time; empty = the fish escapes) and CÂU (from 0; full = caught).
// Each tile kind is a fishing move that pushes the two bars its own way.
// Builds the whole UI at runtime; the scene only needs this component. Tiles, baits, fish and levels are edited in the Inspector.
public class FishingGame : MonoBehaviour
{
    [Serializable] public class Count { public string id; public int count; }

    // A fishing move on the board. Values are for a match of 3; 4 in a row ×1.5, 5 or more ×2.
    [Serializable] public class TileKind
    {
        public string id, name;
        public Color color;
        [Tooltip("Thanh CÂU tăng bao nhiêu (0.12 = 12%), nhân 300 / sức cá")] public float reel;
        [Tooltip("Thanh GIỮ tăng (dương) hoặc giảm (âm) bao nhiêu")] public float hold;
        [Tooltip("Chỉ có tác dụng khi thanh CÂU đã đủ cao (vợt cá gần thuyền)")] public bool nearOnly;
    }

    // A bait chosen before casting. Every fish can bite; the species it attracts swim toward the hook, so they are easier to catch.
    [Serializable] public class BaitKind
    {
        public string id, name;
        [Tooltip("Id các loài cá bị mồi này hút")] public string[] attracts;
    }

    // What the fish does to the board each time it starts struggling (only some species have one).
    public enum Skill
    {
        [InspectorName("Không")] None,
        [InspectorName("Phun mực: che mặt 4 ô tới khi cá đuối")] Ink,
        [InspectorName("Phồng gai: khoá 3 ô tới khi cá đuối")] Spikes,
        [InspectorName("Ném rong: 2 rong lên bàn")] Weed,
    }

    [Serializable] public class Species
    {
        public string id, name, shortName;
        [Tooltip("Sức cá = giá cá. Thanh CÂU tăng × 300 / sức cá")] public int hp;
        [Tooltip("Thanh GIỮ tụt bao nhiêu mỗi giây (0.03 = 3%/giây), nhân theo lúc giằng/đuối")] public float drain = 0.03f;
        [Tooltip("Mỗi lần cá lao giật thanh GIỮ xuống bao nhiêu (0.06 = 6%)")] public float yank = 0.06f;
        [Tooltip("Cá khỏe: lao giật GIỮ mỗi lần bắt đầu giằng")] public bool lunges;
        [Tooltip("Độ khó 1–5 sao (hiển thị)")] [Range(1, 5)] public int stars = 1;
        [Tooltip("Chiêu cá dùng lên bàn ô mỗi khi bắt đầu giằng")] public Skill skill;
        public Color color;
        public Vector2 size;
        public float speed;
    }

    [Serializable] public class FishSpot { public string species; [Range(0, 1)] public float depth; }

    [Serializable] public class Snag { [Range(0, 1)] public float depth; public float width = 200, speed = 80; }

    // A level's side quest: done = one extra star (time left gives up to two).
    public enum Quest
    {
        [InspectorName("Không có")] None,
        [InspectorName("Không hụt lần nào")] NoMiss,
        [InspectorName("Tạo đủ số ô đặc biệt (sọc, bom, sao)")] Specials,
        [InspectorName("Ăn dây chuyền đủ số bước trong một lần đổi")] Combo,
    }

    [Serializable] public class Level
    {
        public string name;
        [Tooltip("Mẹo hiện trước khi vào màn (để trống = không hiện)")] [TextArea] public string tip;
        public Quest quest;
        [Tooltip("Số ô đặc biệt / số bước dây chuyền cần đạt")] public int questTarget;
        [Tooltip("Tổng số giây của màn (chạy cả lúc thả câu lẫn lúc kéo cá)")] public float time = 120;
        [Tooltip("Id các mồi được chọn trong màn")] public string[] baits;
        [Tooltip("id = loài cá, count = số con cần bắt")] public Count[] need;
        public FishSpot[] pond;
        [Tooltip("Cành cây trôi ngang; móc chạm vào bị vướng rồi giật về mặt nước")] public Snag[] snags = new Snag[0];
    }

    public static readonly Color Ink = UI.Hex("#10262E"), Muted = UI.Hex("#5B7279"), Buoy = UI.Hex("#FF5A36"),
        Panel = UI.Hex("#F4F8F8"), Line = UI.Hex("#C6D6D9"), Navy = UI.Hex("#16405C");

    // the 7 fishing moves; "rong" is the weed the fish throws onto the board (can't be moved or matched)
    public List<TileKind> tileKinds = new List<TileKind>
    {
        Kind("giat", "Giật cần", "#FF8080", 0.12f, -0.06f),
        Kind("thu", "Thu dây", "#B99CF0", 0.07f, 0),
        Kind("noi", "Nới dây", "#7FD69A", 0, 0.08f),
        Kind("ghim", "Ghìm cần", "#7DB8F0", 0.04f, 0.04f),
        Kind("vot", "Vợt", "#FFB35C", 0.2f, -0.1f, nearOnly: true),
        Kind("rong", "Rong", "#9FB5A0", 0, 0),
    };

    public List<BaitKind> baitKinds = new List<BaitKind>
    {
        Bt("giun", "Giun", "chep,noc,rophi"),
        Bt("ngo", "Ngô", "chep,rophi"),
        Bt("tom", "Tôm", "noc,cathu,muc"),
        Bt("cacon", "Cá con", "cathu,ngu,camap"),
        Bt("moigia", "Mồi giả", "ngu,muc,camap"),
    };

    // stars set the board; hp, drain, yank and style set how the fish fights the two bars
    public List<Species> species = new List<Species>
    {
        NewFish("chep", "Cá chép", "chép", 1, 400, "#E8A33D", 130, 112, 115, 0.012f, 0.05f, false, Skill.None),
        NewFish("noc", "Cá nóc", "nóc", 2, 450, "#CDB64E", 110, 96, 85, 0.015f, 0.06f, false, Skill.Spikes),
        NewFish("rophi", "Cá rô phi", "rô phi", 2, 480, "#8FA67A", 140, 107, 120, 0.016f, 0.05f, false, Skill.None),
        NewFish("cathu", "Cá thu", "thu", 3, 600, "#6FA3B5", 150, 142, 230, 0.022f, 0.07f, true, Skill.None),
        NewFish("ngu", "Cá ngừ", "ngừ", 4, 700, "#3E78C2", 200, 152, 160, 0.024f, 0.07f, true, Skill.None),
        NewFish("muc", "Mực", "mực", 4, 650, "#B07AA1", 140, 94, 100, 0.030f, 0.08f, false, Skill.Ink),
        NewFish("camap", "Cá mập", "cá mập", 5, 900, "#6E7F8A", 260, 191, 90, 0.026f, 0.06f, true, Skill.Weed),
    };

    // harder levels ask for more (and harder) fish from a busier lake; the baits on offer each pull in some unwanted fish
    public List<Level> levels = new List<Level>
    {
        Lv("Làm quen", 100, "giun", "chep:2", "chep:.2,chep:.4,chep:.6,chep:.8", "", Quest.NoMiss, 0,
            "Vuốt ô để ghép 3. CÂU đầy là bắt được cá!"),
        Lv("Chọn mồi", 130, "giun,ngo", "chep:3", "noc:.12,chep:.25,noc:.38,chep:.5,noc:.62,chep:.75,chep:.88", "", Quest.NoMiss, 0,
            "Mỗi mồi hút những loài cá khác nhau."),
        Lv("Rô phi", 150, "giun,ngo,tom", "rophi:2,chep:2", "chep:.1,rophi:.22,noc:.34,chep:.46,rophi:.58,noc:.7,rophi:.82,chep:.92", "", Quest.Specials, 3,
            "Ghép 4 hoặc 5 ô để tạo ô đặc biệt."),
        Lv("Cá ngừ", 120, "ngo,cacon,moigia", "ngu:1,rophi:1,chep:1", "chep:.1,cathu:.2,rophi:.32,chep:.44,ngu:.56,muc:.68,chep:.8,ngu:.9", "", Quest.Combo, 3),
        Lv("Cá thu bơi nhanh", 150, "tom,cacon", "cathu:3", "noc:.1,cathu:.22,ngu:.34,cathu:.46,noc:.58,cathu:.7,ngu:.82,cathu:.92", ".3:200:110,.7:200:110", Quest.Specials, 4),
        Lv("Mực phun mực", 140, "giun,tom,moigia", "muc:2,cathu:1", "chep:.1,noc:.2,muc:.32,cathu:.44,noc:.56,muc:.68,ngu:.8,muc:.9", "", Quest.Combo, 4),
        // 7: puffers are the catch this time, so their spikes finally matter; each bait brings one decoy
        Lv("Cá nóc xù gai", 120, "giun,tom", "noc:2,chep:1", "noc:.15,rophi:.28,chep:.4,noc:.52,cathu:.64,noc:.76,rophi:.88", "", Quest.NoMiss, 0),
        // 8: a crowded lake: the puffer is only on worms, and worms also pull in the many carp
        Lv("Hồ đông cá", 130, "giun,ngo", "rophi:2,noc:1", "chep:.1,rophi:.2,chep:.3,noc:.4,chep:.5,rophi:.6,chep:.7,noc:.8,chep:.9", ".45:220:90", Quest.Specials, 5),
        // 9: lunges and ink in one level, the shark lurking as a decoy on both baits
        Lv("Ngừ và mực", 100, "cacon,moigia", "ngu:1,muc:1", "cathu:.12,ngu:.26,camap:.4,muc:.54,cathu:.68,ngu:.82,muc:.92", ".6:240:100", Quest.Combo, 4),
        Lv("Boss cá mập", 140, "tom,cacon,moigia", "camap:1,muc:1,cathu:1", "noc:.1,cathu:.22,muc:.34,ngu:.46,camap:.6,cathu:.72,noc:.82,ngu:.92", ".4:260:100,.68:260:100", Quest.NoMiss, 0),
    };

    static TileKind Kind(string id, string name, string color, float reel, float hold, bool nearOnly = false) => new TileKind
    {
        id = id, name = name, color = UI.Hex(color), reel = reel, hold = hold, nearOnly = nearOnly,
    };

    static BaitKind Bt(string id, string name, string attracts) => new BaitKind { id = id, name = name, attracts = attracts.Split(',') };

    static Species NewFish(string id, string name, string shortName, int stars, int hp, string color,
        float w, float h, float speed, float drain, float yank, bool lunges, Skill skill) => new Species
    {
        id = id, name = name, shortName = shortName, stars = stars, hp = hp,
        drain = drain, yank = yank, lunges = lunges, skill = skill,
        color = UI.Hex(color), size = new Vector2(w, h), speed = speed,
    };

    static Level Lv(string name, float time, string baits, string need, string pond, string snags = "",
        Quest quest = Quest.None, int questTarget = 0, string tip = "") => new Level
    {
        name = name, quest = quest, questTarget = questTarget, tip = tip, time = time, baits = baits.Split(','), need = Counts(need),
        pond = pond.Split(',').Select(p => p.Split(':'))
            .Select(a => new FishSpot { species = a[0], depth = float.Parse(a[1], CultureInfo.InvariantCulture) }).ToArray(),
        // "depth:width:speed", comma separated
        snags = snags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split(':').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray())
            .Select(a => new Snag { depth = a[0], width = a[1], speed = a[2] }).ToArray(),
    };

    static Count[] Counts(string spec) =>
        spec.Split(',').Select(p => p.Split(':')).Select(a => new Count { id = a[0], count = int.Parse(a[1]) }).ToArray();

    public Species GetFish(string id) => species.Find(s => s.id == id);
    public TileKind GetTile(string id) => tileKinds.Find(t => t.id == id);
    public BaitKind GetBait(string id) => baitKinds.Find(b => b.id == id);
    // the bait on the hook this level; the cast phase can switch it between casts
    public BaitKind Bait { get; private set; }
    public bool Attracts(Species s) => Bait != null && Bait.attracts.Contains(s.id);
    public Level Current => levels[levelIndex];

    [Tooltip("Bỏ khóa mọi màn để test, không cần thắng màn trước")] public bool unlockAll;

    // Progress lives in PlayerPrefs: highest unlocked level, best stars per level, coin bank.
    const int StartCoins = 200;
    public static int Unlocked => PlayerPrefs.GetInt("unlocked", 0);
    public static int Bank => PlayerPrefs.GetInt("bank", StartCoins);
    public static int BestStars(int i) => PlayerPrefs.GetInt("stars" + i, 0);
    public bool IsOpen(int i) => unlockAll || i <= Unlocked;

    // Boosters are paid from the saved bank, not from this level's coins.
    public bool Spend(int cost)
    {
        if (Bank < cost) return false;
        PlayerPrefs.SetInt("bank", Bank - cost);
        PlayerPrefs.Save();
        return true;
    }

    // out of time: once per level the player may buy a little more, paid from the bank
    // the only way to lose is running out of time: a miss (wrong fish, snapped line) costs MissPenalty seconds
    const float ExtraTime = 20, HurryAt = 15, MissPenalty = 10;
    const int ExtraCost = 100;

    int levelIndex, coins, misses, specialsMade, bestCombo;
    bool extended;
    float timeLeft;
    Text toast;
    Image toastBg;
    Dictionary<string, int> got;
    Text titleText, needsText, timeText;
    RectTransform modal, phone;
    Rect fittedTo;
    CastPhase cast;
    FightPhase fight;
    MenuScreens menus;

    void Awake()
    {
        // mobile defaults are 30 FPS and auto-rotate; the game is portrait only and the hook needs smooth motion
#if !UNITY_WEBGL
        // in a browser the page's own frame clock paces the game better, and the page itself keeps it portrait
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
#endif
        Input.multiTouchEnabled = false;
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        if (Camera.main)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = UI.Hex("#0D3A49");
        }

        var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var phoneImg = UI.Img(canvas.transform, "Phone", 0, 0, 1080, 1920, Panel);
        if (UI.Art("bg_soft")) { phoneImg.sprite = UI.Art("bg_soft"); phoneImg.color = Color.white; }
        phone = phoneImg.rectTransform;
        phone.anchorMin = phone.anchorMax = phone.pivot = new Vector2(0.5f, 0.5f);
        FitSafeArea();

        // the HUD keeps just three things: the level, what is still to catch, and the clock
        // a dark bar so the HUD reads apart from the lake and the board; the clock sits in its own pill
        UI.Img(phone, "HudBar", 0, 0, 1080, 200, Navy);
        titleText = UI.Label(phone, "", 40, 24, 760, 80, 42, Color.white, TextAnchor.MiddleLeft, true);
        UI.Btn(phone, "Chọn màn", 820, 32, 220, 64, Line, Ink, ShowLevelSelect);
        needsText = UI.Label(phone, "", 40, 110, 640, 70, 32, UI.Hex("#CFE6EE"), TextAnchor.MiddleLeft, true);
        UI.Round(UI.Img(phone, "ClockPill", 800, 108, 240, 76, new Color(0, 0, 0, 0.3f)));
        timeText = UI.Label(phone, "", 800, 104, 240, 80, 52, Color.white, TextAnchor.MiddleCenter, true);

        cast = UI.Rect(phone, "Cast", 0, 200, 1080, 1720).gameObject.AddComponent<CastPhase>();
        cast.Init(this);
        fight = UI.Rect(phone, "Fight", 0, 200, 1080, 1720).gameObject.AddComponent<FightPhase>();
        fight.Init(this);
        // home, level select and the rowing loading screen sit above gameplay, below notices and dialogs
        menus = UI.Rect(phone, "Menus", 0, 0, 1080, 1920).gameObject.AddComponent<MenuScreens>();
        menus.Init(this);
        // short notices (fish caught, fish lost) that fade on their own instead of waiting for a tap
        toastBg = UI.Img(phone, "Toast", 90, 860, 900, 110, new Color(0.06f, 0.15f, 0.18f, 0.85f));
        UI.Round(toastBg);
        toastBg.raycastTarget = false;
        toast = UI.Label(toastBg.transform, "", 20, 0, 860, 110, 38, Color.white, TextAnchor.MiddleCenter, true);
        toastBg.gameObject.SetActive(false);
        modal = UI.Img(phone, "Modal", 0, 0, 1080, 1920, new Color(0.05f, 0.23f, 0.29f, 0.6f)).rectTransform;
        CloseModal();

        menus.ShowHome();
    }

    void Update()
    {
        if (Screen.safeArea != fittedTo) FitSafeArea();
        // the last seconds pulse red, on the cast screen and in fights alike
        if (got != null && timeLeft < HurryAt)
            timeText.color = Color.Lerp(Buoy, Color.red, Mathf.PingPong(Time.time * 4, 1));

        // the level clock runs while the hook is out and during fights, but not while the hook waits or a dialog is open
        if (got == null || modal.gameObject.activeSelf) return;
        if (!(cast.gameObject.activeSelf && cast.Running) && !fight.gameObject.activeSelf) return;
        if (fight.gameObject.activeSelf && fight.Landed) return; // the fish is caught; the last cascades play for free
        timeLeft -= Time.deltaTime;
        RenderClock();
        if (timeLeft <= 0) TimeUp();
    }

    // Shrinks and centres the 1080x1920 layout so nothing sits under a notch, punch-hole or home bar.
    void FitSafeArea()
    {
        var safe = fittedTo = Screen.safeArea;
        float f = Mathf.Min(Screen.width / 1080f, Screen.height / 1920f); // the canvas scale in Expand mode
        phone.localScale = Vector3.one * Mathf.Min(1, safe.width / (1080 * f), safe.height / (1920 * f));
        phone.anchoredPosition = (safe.center - new Vector2(Screen.width, Screen.height) / 2) / f;
    }

    // Leaves the level: the boat rows back to the level select.
    void ShowLevelSelect()
    {
        CloseModal();
        cast.Halt();
        cast.gameObject.SetActive(false);
        fight.gameObject.SetActive(false);
        got = null; // stops the level clock
        menus.Travel(menus.ShowSelect);
    }

    // From the level select (after the boat trip) or "next level".
    public void PlayLevel(int index) => StartLevel(index);

    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey("unlocked");
        PlayerPrefs.DeleteKey("bank");
        for (int i = 0; i < levels.Count; i++) PlayerPrefs.DeleteKey("stars" + i);
        PlayerPrefs.Save();
    }

    // Saves the result. Returns true if the stars beat the saved best.
    bool SaveWin(int stars)
    {
        bool best = stars > BestStars(levelIndex);
        if (best) PlayerPrefs.SetInt("stars" + levelIndex, stars);
        PlayerPrefs.SetInt("unlocked", Mathf.Max(Unlocked, levelIndex + 1));
        PlayerPrefs.SetInt("bank", Bank + coins);
        PlayerPrefs.Save();
        return best;
    }

    void StartLevel(int index)
    {
        levelIndex = index;
        got = Current.need.ToDictionary(n => n.id, n => 0);
        timeLeft = Current.time;
        coins = misses = specialsMade = bestCombo = 0;
        extended = false;
        Bait = null;
        fight.gameObject.SetActive(false);
        cast.gameObject.SetActive(true);
        cast.Restock();
        fight.NewBoard();
        // the very first play is coached by a hand; the first two levels also hint sooner
        cast.coach = fight.coach = index == 0 && BestStars(0) == 0;
        fight.hintDelay = index < 2 ? 3 : 5;
        RenderHud();
        if (string.IsNullOrEmpty(Current.tip)) ChooseBait();
        else ShowModal("Mẹo", Current.tip, ("OK", true, ChooseBait));
    }

    // The bait is picked up front: each one pulls in its own species, wanted or not.
    void ChooseBait()
    {
        // only one bait on offer: nothing to choose, straight to the water
        if (Offer().Count == 1)
        {
            SetBait(Offer()[0]);
            ToCast();
            return;
        }
        var choices = Offer().Select((b, i) =>
            ($"{b.name}: {AttractText(b)}", i == 0, (Action)(() => { SetBait(b); ToCast(); }))).ToArray();
        ShowModal("Chọn mồi",
            $"Cần: {NeedText()} · {Current.time:0}s" + (Current.quest != Quest.None ? $"\n+1 sao: {QuestName()}" : ""),
            choices);
    }

    string QuestName()
    {
        switch (Current.quest)
        {
            case Quest.NoMiss: return "không hụt lần nào";
            case Quest.Specials: return $"tạo {Current.questTarget} ô đặc biệt";
            case Quest.Combo: return $"ăn dây chuyền {Current.questTarget} bước";
            default: return "";
        }
    }

    bool QuestDone() =>
        Current.quest == Quest.NoMiss ? misses == 0 :
        Current.quest == Quest.Specials ? specialsMade >= Current.questTarget :
        Current.quest == Quest.Combo && bestCombo >= Current.questTarget;

    // Called by the fight after each swap: how many cascade steps it chained and how many special tiles it made.
    public void QuestProgress(int comboSteps, int specials)
    {
        bool was = QuestDone();
        bestCombo = Mathf.Max(bestCombo, comboSteps);
        specialsMade += specials;
        if (!was && QuestDone()) fight.Announce("+1 sao!");
    }

    List<BaitKind> Offer() => Current.baits.Select(GetBait).Where(b => b != null).ToList();

    public string AttractText(BaitKind b) => string.Join(", ", b.attracts.Select(GetFish).Where(f => f != null).Select(f => f.shortName));

    void SetBait(BaitKind b)
    {
        Bait = b;
        cast.BaitChanged();
    }

    // The next bait on offer this level (the cast phase's bait button).
    public void NextBait()
    {
        var offer = Offer();
        SetBait(offer[(offer.IndexOf(Bait) + 1) % offer.Count]);
    }

    int NeedOf(string sp) => Current.need.FirstOrDefault(n => n.id == sp)?.count ?? 0;

    public bool StillNeeded(string sp) => got.ContainsKey(sp) && got[sp] < NeedOf(sp);

    string NeedText() => string.Join("  ·  ", Current.need.Select(n =>
        $"{GetFish(n.id).shortName} {got[n.id]}/{n.count}"));

    void RenderHud()
    {
        titleText.text = $"{levelIndex + 1}. {Current.name}";
        needsText.text = "Cần: " + NeedText();
        RenderClock();
    }

    void RenderClock()
    {
        timeText.text = $"{Mathf.CeilToInt(Mathf.Max(0, timeLeft))}s";
        timeText.color = timeLeft < HurryAt ? Buoy : Color.white;
    }

    void TimeUp()
    {
        timeLeft = 0;
        cast.Halt();
        fight.gameObject.SetActive(false);
        RenderClock();
        var buttons = new List<(string, bool, Action)>();
        if (!extended && Bank >= ExtraCost)
            buttons.Add(($"+{ExtraTime:0}s ({ExtraCost} xu)", true, () =>
            {
                Spend(ExtraCost);
                extended = true;
                timeLeft += ExtraTime;
                RenderClock();
                ToCast();
            }));
        buttons.Add(("Chơi lại màn", buttons.Count == 0, () => StartLevel(levelIndex)));
        buttons.Add(("Chọn màn", false, () => ShowLevelSelect()));
        ShowModal("Hết giờ!", Summary(false), buttons.ToArray());
    }

    // The end-of-level card: what was caught, time, misses and the side quest.
    string Summary(bool won)
    {
        string quest = Current.quest == Quest.None ? "" : $"\nPhụ ({QuestName()}): {(QuestDone() ? "xong" : "chưa")}";
        return (won ? $"Còn {Mathf.CeilToInt(timeLeft)}s · +{coins} xu" : $"Cần: {NeedText()}") + quest;
    }

    void Toast(string text) => StartCoroutine(ShowToast(text));

    IEnumerator ShowToast(string text)
    {
        toast.text = text;
        toastBg.transform.SetAsLastSibling();
        toastBg.gameObject.SetActive(true);
        for (float k = 0; k < 1; k += Time.unscaledDeltaTime / 2.2f)
        {
            if (toast.text != text) yield break; // a newer notice took over
            float a = k < 0.8f ? 1 : (1 - k) / 0.2f;
            toastBg.color = new Color(0.06f, 0.15f, 0.18f, 0.85f * a);
            toast.color = new Color(1, 1, 1, a);
            yield return null;
        }
        toastBg.gameObject.SetActive(false);
    }

    void ToCast()
    {
        CloseModal();
        fight.gameObject.SetActive(false);
        cast.gameObject.SetActive(true);
        cast.Begin();
    }

    public void Hooked(CastPhase.PondFish f)
    {
        var s = f.s;
        // a fish the level doesn't need is let go at once: it only costs time
        if (!StillNeeded(s.id))
        {
            AddMiss($"Nhầm {s.shortName}!");
            return;
        }
        CloseModal();
        cast.coach = false;
        cast.gameObject.SetActive(false);
        fight.gameObject.SetActive(true);
        fight.Begin(f);
    }

    public void Caught(CastPhase.PondFish f)
    {
        var s = f.s;
        coins += s.hp;
        cast.Remove(f);
        got[s.id]++;
        RenderHud();

        if (Current.need.All(n => got[n.id] >= n.count))
        {
            // up to two stars for the time left, one more for the side quest
            float left = timeLeft / Current.time;
            bool quest = Current.quest != Quest.None && QuestDone();
            int stars = (left >= 0.4f ? 2 : 1) + (quest ? 1 : 0);
            bool best = SaveWin(stars);
            var buttons = new List<(string, bool, Action)>();
            int next = levelIndex + 1;
            if (next < levels.Count)
                buttons.Add(("Màn tiếp theo", true, () =>
                {
                    cast.gameObject.SetActive(false);
                    menus.Travel(() => StartLevel(next));
                }));
            buttons.Add(("Chơi lại", false, () => StartLevel(levelIndex)));
            buttons.Add(("Chọn màn", buttons.Count == 1 && levelIndex + 1 >= levels.Count, () => ShowLevelSelect()));
            cardStars = stars;
            ShowModal("Qua màn!", (best ? "Kỷ lục mới!\n" : "") + Summary(true), buttons.ToArray());
            return;
        }
        // not done yet: a short notice and straight back to casting
        Toast($"+1 {s.shortName}!");
        ToCast();
    }

    public void Lost(string why, CastPhase.PondFish f) => AddMiss(why);

    // A wrong fish or a fish that got away: a few seconds off the clock, then back to casting.
    void AddMiss(string text)
    {
        misses++;
        timeLeft -= MissPenalty;
        RenderClock();
        Toast($"{text} −{MissPenalty:0}s");
        if (timeLeft <= 0) TimeUp();
        else ToCast();
    }

    int cardStars = -1; // set just before ShowModal to put that many stars (of 3) on the card

    void ShowModal(string title, string text, params (string label, bool primary, Action onClick)[] buttons)
    {
        foreach (Transform c in modal) Destroy(c.gameObject);
        int stars = cardStars;
        cardStars = -1;
        float starH = stars >= 0 ? 130 : 0;
        // the text box grows with the text (about 38 characters per line at this size)
        float textH = Mathf.Max(100, text.Split('\n').Sum(l => Mathf.Max(1, Mathf.CeilToInt(l.Length / 38f))) * 46);
        float h = 180 + starH + textH + buttons.Length * 120;
        var cardImg = UI.Img(modal, "Card", 100, (1920 - h) / 2, 880, h, Panel);
        UI.Round(cardImg);
        var card = cardImg.transform;
        // a navy title band across the top of the card
        UI.Round(UI.Img(card, "Header", 0, 0, 880, 130, Navy));
        UI.Label(card, title, 40, 20, 800, 90, 54, Color.white, TextAnchor.MiddleCenter, true);
        if (stars >= 0) UI.Stars(card, stars, 3, 440 - 160, 150, 104);
        UI.Label(card, text, 40, 140 + starH, 800, textH, 36, Muted, TextAnchor.UpperCenter);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            UI.Btn(card, b.label, 40, 160 + starH + textH + i * 120, 800, 100, b.primary ? Buoy : Line, b.primary ? Color.white : Ink,
                () => { CloseModal(); b.onClick(); });
        }
        modal.gameObject.SetActive(true);
        StartCoroutine(Pop(cardImg.rectTransform));
        if (stars >= 0) StartCoroutine(PopStars(card));
    }

    // The won stars bounce in one after another.
    static IEnumerator PopStars(Transform card)
    {
        var list = new List<RectTransform>();
        foreach (Transform c in card) if (c.name == "Star") list.Add((RectTransform)c);
        foreach (var rt in list)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition += new Vector2(rt.sizeDelta.x / 2, -rt.sizeDelta.y / 2);
            rt.localScale = Vector3.zero;
        }
        yield return new WaitForSecondsRealtime(0.25f);
        foreach (var rt in list)
        {
            for (float t = 0; t < 0.25f && rt; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.25f;
                rt.localScale = Vector3.one * (k + 0.35f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            if (rt) rt.localScale = Vector3.one;
        }
    }

    // The card springs in from a little smaller instead of just appearing.
    static IEnumerator Pop(RectTransform card)
    {
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition += new Vector2(card.sizeDelta.x / 2, -card.sizeDelta.y / 2);
        for (float t = 0; t < 0.22f && card; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.22f;
            card.localScale = Vector3.one * (0.85f + 0.15f * k + 0.08f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        if (card) card.localScale = Vector3.one;
    }

    void CloseModal() => modal.gameObject.SetActive(false);
}
