using UnityEngine;
using UnityEngine.UI;

// Small helpers to build the prototype UI from code. Coordinates are top-left based, y grows downward.
public static class UI
{
    static Font font;
    static Sprite circle;

    // Liberation Sans ships with the game (SIL OFL, see Resources/Fonts), so Vietnamese reads the same in a browser
    // as in the editor; the built-in font is only a fallback.
    public static Font Font => font ? font : (font = Resources.Load<Font>("Fonts/LiberationSans") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    // A row of star pictures: the first `earned` gold, the rest faded.
    public static void Stars(Transform parent, int earned, int total, float x, float y, float size)
    {
        for (int i = 0; i < total; i++)
        {
            var img = Img(parent, "Star", x + i * size * 1.05f, y, size, size, i < earned ? Color.white : new Color(0, 0, 0, 0.25f));
            img.sprite = Art("star");
            img.raycastTarget = false;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle) return circle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return circle;
        }
    }

    static readonly System.Collections.Generic.Dictionary<string, Sprite> art = new System.Collections.Generic.Dictionary<string, Sprite>();

    // Sprites drawn by ArtSource/make_art.py into Assets/Resources/Art; null if the file is missing.
    public static Sprite Art(string name)
    {
        if (art.TryGetValue(name, out var s)) return s;
        var tex = Resources.Load<Texture2D>("Art/" + name);
        s = tex ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
        art[name] = s;
        return s;
    }

    static Sprite rounded;

    // Gives an image the rounded "button" art, 9-sliced so the corners keep their size at any width/height.
    public static void Round(Image img)
    {
        if (!rounded)
        {
            var tex = Resources.Load<Texture2D>("Art/button");
            if (!tex) return;
            rounded = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
        }
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
    }

    static readonly System.Collections.Generic.Dictionary<string, Sprite> slicedArt = new System.Collections.Generic.Dictionary<string, Sprite>();

    // Gives an image a vertically 9-sliced sprite from Resources/Art (caps of `cap` px top and bottom); false if missing.
    public static bool Sliced(Image img, string name, float cap)
    {
        if (!slicedArt.TryGetValue(name, out var s))
        {
            var tex = Resources.Load<Texture2D>("Art/" + name);
            s = tex ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(0, cap, 0, cap)) : null;
            slicedArt[name] = s;
        }
        if (!s) return false;
        img.sprite = s;
        img.type = Image.Type.Sliced;
        return true;
    }

    // Plain C# parsing so it is safe in field initializers (Unity APIs are not allowed there).
    public static Color Hex(string hex)
    {
        int v = System.Convert.ToInt32(hex.TrimStart('#'), 16);
        return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
    }

    public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    public static Image Img(Transform parent, string name, float x, float y, float w, float h, Color color, bool round = false)
    {
        var img = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
        img.color = color;
        if (round) img.sprite = Circle;
        return img;
    }

    public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color,
        TextAnchor align = TextAnchor.MiddleLeft, bool bold = false)
    {
        var t = Rect(parent, "Text", x, y, w, h).gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button Btn(Transform parent, string text, float x, float y, float w, float h, Color bg, Color fg, System.Action onClick)
    {
        var img = Img(parent, "Button", x, y, w, h, bg);
        Round(img);
        var b = img.gameObject.AddComponent<Button>();
        b.onClick.AddListener(() => { Sfx.Play("tap", 1, 0.5f); onClick(); });
        var label = Label(img.transform, text, 0, 0, w, h, 38, fg, TextAnchor.MiddleCenter, true);
        // white text on a coloured button gets a soft drop shadow so it pops
        if (fg == Color.white) label.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.3f);
        return b;
    }
}
