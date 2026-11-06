using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The screens around gameplay: Home (the fisherman resting in his boat by the hut), the level select, and the
// loading screen between them, where he rows across the lake while the bar fills.
// Each screen covers the whole phone, so the gameplay HUD underneath needs no hiding.
public class MenuScreens : MonoBehaviour
{
    const float TravelSeconds = 1.8f, WaterY = 760, FrameSeconds = 0.15f;
    // For now the game is just the level select and gameplay: home and the rowing loading screen are skipped
    // until their art is redone. Set false to bring them back.
    public static bool LevelSelectOnly = true;

    FishingGame game;
    RectTransform home, select, loading, list, loadBoat, loadFill, homeBoat, playBtn;
    Image loadBoatImg, homeBoatImg;
    Text homeBank, selectBank;
    readonly Sprite[] row = new Sprite[4], idle = new Sprite[4];
    // a few fish drift across the home lake, just for life
    readonly List<(RectTransform rt, float speed, float baseY)> swimmers = new List<(RectTransform, float, float)>();

    public void Init(FishingGame g)
    {
        game = g;
        for (int i = 0; i < 4; i++)
        {
            row[i] = UI.Art("boat_row_" + i);
            idle[i] = UI.Art("boat_idle_" + i);
        }

        // Home
        home = Backdrop("Home");
        var kinds = new[] { ("chep", 1080f, 90f, 120f), ("ngu", 1700f, 150f, 80f), ("cathu", 880f, 120f, 150f), ("rophi", 1780f, 110f, 100f), ("muc", 980f, 90f, 60f) };
        for (int i = 0; i < kinds.Length; i++)
        {
            var (id, y, w, speed) = kinds[i];
            var sprite = UI.Art("fish_" + id);
            if (!sprite) continue;
            var img = UI.Img(home, "Swimmer", 200 + i * 230, y, w * 1.4f, w * 1.4f * sprite.rect.height / sprite.rect.width, new Color(1, 1, 1, 0.9f));
            img.sprite = sprite;
            img.raycastTarget = false;
            swimmers.Add((img.rectTransform, speed, y));
        }
        var title = UI.Label(home, "FISHING JAM", 40, 120, 1000, 130, 104, FishingGame.Navy, TextAnchor.MiddleCenter, true);
        var rim = title.gameObject.AddComponent<Outline>();
        rim.effectColor = Color.white;
        rim.effectDistance = new Vector2(5, -5);
        UI.Label(home, "Câu cá · Xoay nối dây", 40, 250, 1000, 60, 40, FishingGame.Muted, TextAnchor.MiddleCenter);
        if (UI.Art("hut")) UI.Img(home, "Hut", 30, WaterY - 330, 560, 356, Color.white).sprite = UI.Art("hut");
        homeBoatImg = UI.Img(home, "Boat", 600, WaterY - 200, 320, 240, Color.white);
        homeBoat = homeBoatImg.rectTransform;
        var play = UI.Btn(home, "CHƠI", 240, 1250, 600, 150, FishingGame.Buoy, Color.white, () => Travel(ShowSelect));
        play.GetComponentInChildren<Text>().fontSize = 64;
        playBtn = (RectTransform)play.transform;
        playBtn.pivot = new Vector2(0.5f, 0.5f);
        playBtn.anchoredPosition = new Vector2(540, -1325);
        UI.Btn(home, "Xóa tiến độ", 340, 1440, 400, 90, FishingGame.Line, FishingGame.Ink, () => { game.ResetProgress(); ShowHome(); });
        Sfx.AddToggle(home, 950, 40, 90);
        homeBank = UI.Label(home, "", 40, 1580, 1000, 60, 36, Color.white, TextAnchor.MiddleCenter, true);

        // Level select
        select = Backdrop("Select", false);
        UI.Label(select, "Chọn hồ câu", 40, 60, 1000, 100, 64, FishingGame.Ink, TextAnchor.MiddleCenter, true);
        if (!LevelSelectOnly) UI.Btn(select, "Về nhà", 40, 70, 240, 80, FishingGame.Line, FishingGame.Ink, () => Travel(ShowHome));
        // with no home screen, progress is reset from here (also replays the first-time tutorial)
        else UI.Btn(select, "Chơi lại từ đầu", 340, 1780, 400, 90, FishingGame.Line, FishingGame.Ink, () => { game.ResetProgress(); ShowSelect(); })
            .GetComponentInChildren<Text>().fontSize = 32;
        list = UI.Rect(select, "List", 0, 200, 1080, 1460);
        Sfx.AddToggle(select, 950, 70, 80);
        selectBank = UI.Label(select, "", 40, 1700, 1000, 60, 34, Color.white, TextAnchor.MiddleCenter, true);

        // Loading: a strip of lake the boat rows across
        loading = Backdrop("Loading");
        loadBoatImg = UI.Img(loading, "Boat", 0, WaterY - 200, 320, 240, Color.white);
        loadBoat = loadBoatImg.rectTransform;
        UI.Round(UI.Img(loading, "BarBg", 100, 1300, 880, 50, FishingGame.Line));
        var fill = UI.Img(loading, "BarFill", 100, 1300, 0, 50, FishingGame.Buoy);
        UI.Round(fill);
        loadFill = fill.rectTransform;

        Hide();
    }

    // A full-phone panel with sky above the water line and the lake below.
    RectTransform Backdrop(string name, bool lake = true)
    {
        var bg = UI.Img(transform, name, 0, 0, 1080, 1920, FishingGame.Panel).rectTransform;
        // plain sky, with the drawn strip (clouds, sun, hills) at its own 1080x160 size just above the water
        UI.Img(bg, "Sky", 0, 0, 1080, WaterY, UI.Hex("#A9D6EA"));
        if (UI.Art("sky")) UI.Img(bg, "SkyArt", 0, WaterY - 160, 1080, 160, Color.white).sprite = UI.Art("sky");
        UI.Img(bg, "Water", 0, WaterY, 1080, 1920 - WaterY, UI.Hex("#1D6A80"));
        if (UI.Art("wave")) UI.Img(bg, "Wave", 0, WaterY - 20, 1080, 40, Color.white).sprite = UI.Art("wave");
        // the level list sits on a pale card so the text stays readable over the water
        if (!lake) UI.Round(UI.Img(bg, "Card", 40, 180, 1000, 1500, new Color(1, 1, 1, 0.9f)));
        return bg;
    }

    void Hide()
    {
        home.gameObject.SetActive(false);
        select.gameObject.SetActive(false);
        loading.gameObject.SetActive(false);
    }

    public void ShowHome()
    {
        if (LevelSelectOnly) { ShowSelect(); return; }
        Hide();
        home.gameObject.SetActive(true);
        homeBank.text = $"{TotalStars()}/{game.levels.Count * 3} sao   ·   {FishingGame.Bank} xu";
    }

    public void ShowSelect()
    {
        Hide();
        select.gameObject.SetActive(true);
        selectBank.text = $"{TotalStars()}/{game.levels.Count * 3} sao   ·   {FishingGame.Bank} xu";
        foreach (Transform c in list) Destroy(c.gameObject);
        for (int i = 0; i < game.levels.Count; i++)
        {
            int index = i;
            var level = game.levels[i];
            bool open = game.IsOpen(i), next = i == FishingGame.Unlocked;
            // cards shrink to fit however many levels there are
            float step = Mathf.Min(200, 1440f / game.levels.Count), ch = step - 20, y = 20 + i * step;
            var card = UI.Btn(list, "", 90, y, 900, ch, open ? (next ? FishingGame.Buoy : FishingGame.Line) : UI.Hex("#9AA9AD"),
                FishingGame.Ink, () => { if (open) Travel(() => game.PlayLevel(index)); });
            var fg = next ? Color.white : FishingGame.Ink;
            // the level number in a round badge, then the name
            float bs = ch - 36;
            var badge = UI.Img(card.transform, "Badge", 24, 18, bs, bs, next ? Color.white : FishingGame.Navy, true);
            UI.Label(badge.transform, $"{i + 1}", 0, 0, bs, bs, Mathf.RoundToInt(bs * 0.5f), next ? FishingGame.Buoy : Color.white, TextAnchor.MiddleCenter, true);
            var nameLabel = UI.Label(card.transform, level.name, 44 + bs, 0, 456 - 70 - bs, ch, 36, fg, TextAnchor.MiddleLeft, true);
            nameLabel.resizeTextForBestFit = true; // long names shrink instead of running under the fish
            nameLabel.resizeTextMinSize = 24;
            nameLabel.resizeTextMaxSize = 38;
            nameLabel.verticalOverflow = VerticalWrapMode.Truncate;
            for (int k = 0; k < level.need.Length && k < 3; k++)
            {
                var sprite = UI.Art("fish_" + level.need[k].id);
                if (!sprite) continue;
                float fw = 64, fh = fw * sprite.rect.height / sprite.rect.width;
                var icon = UI.Img(card.transform, "Need", 470 + k * 64, (ch - fh) / 2, fw, fh, Color.white);
                icon.sprite = sprite;
                icon.raycastTarget = false;
            }
            int best = FishingGame.BestStars(i);
            float sz = Mathf.Min(52, ch * 0.5f);
            if (open) UI.Stars(card.transform, best, 3, 870 - sz * 3.15f, (ch - sz) / 2, sz);
            else UI.Label(card.transform, "KHÓA", 640, 0, 230, ch, 36, FishingGame.Muted, TextAnchor.MiddleRight, true);
        }
    }

    int TotalStars()
    {
        int n = 0;
        for (int i = 0; i < game.levels.Count; i++) n += FishingGame.BestStars(i);
        return n;
    }

    // The loading screen: the boat rows from left to right as the bar fills, then the next screen opens.
    public void Travel(Action then)
    {
        if (LevelSelectOnly) { Hide(); then(); return; }
        StopAllCoroutines();
        StartCoroutine(TravelRoutine(then));
    }

    IEnumerator TravelRoutine(Action then)
    {
        Hide();
        loading.gameObject.SetActive(true);
        loading.SetAsLastSibling();
        for (float t = 0; t < TravelSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / TravelSeconds;
            loadBoat.anchoredPosition = new Vector2(Mathf.Lerp(-200, 860, k), -(WaterY - 200) + Mathf.Sin(t * 6) * 6);
            loadFill.sizeDelta = new Vector2(880 * k, 50);
            yield return null;
        }
        Hide();
        then();
    }

    void Update()
    {
        // both boats loop their 4 frames; the resting one also bobs on the water
        int frame = (int)(Time.unscaledTime / FrameSeconds) % 4;
        if (row[frame]) loadBoatImg.sprite = row[frame];
        if (idle[frame]) homeBoatImg.sprite = idle[frame];
        homeBoat.anchoredPosition = new Vector2(600, -(WaterY - 200) + Mathf.Sin(Time.unscaledTime * 2) * 6);
        if (home.gameObject.activeSelf)
            foreach (var (rt, speed, baseY) in swimmers)
            {
                // the sprites face left, so they swim right to left and come back in from the right edge
                float x = rt.anchoredPosition.x - speed * Time.unscaledDeltaTime;
                if (x < -rt.sizeDelta.x) x = 1080;
                rt.anchoredPosition = new Vector2(x, -baseY + Mathf.Sin(Time.unscaledTime * 1.5f + baseY) * 8);
            }
        // the play button breathes to invite the first tap
        playBtn.localScale = Vector3.one * (1 + 0.04f * Mathf.Sin(Time.unscaledTime * 4));
    }
}
