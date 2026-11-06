using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Short sound effects from Resources/Audio (made by ArtSource/make_sfx.py). A few sources take turns
// so quick sounds (cascade pops) overlap instead of cutting each other off.
public static class Sfx
{
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static AudioSource[] sources;
    static int next;

    // remembered between sessions; muting silences everything through the listener
    public static bool Muted
    {
        get => PlayerPrefs.GetInt("mute", 0) == 1;
        set
        {
            PlayerPrefs.SetInt("mute", value ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = value ? 0 : 1;
        }
    }

    // A small speaker button that toggles sound and keeps its icon in step with the setting.
    public static void AddToggle(Transform parent, float x, float y, float size)
    {
        AudioListener.volume = Muted ? 0 : 1;
        var b = UI.Btn(parent, "", x, y, size, size, FishingGame.Line, FishingGame.Ink, () => Muted = !Muted);
        var icon = UI.Img(b.transform, "Icon", size * 0.2f, size * 0.16f, size * 0.6f, size * 0.6f, Color.white);
        icon.raycastTarget = false;
        b.gameObject.AddComponent<SoundIcon>().icon = icon;
    }

    class SoundIcon : MonoBehaviour
    {
        public Image icon;
        void Update() => icon.sprite = UI.Art(Muted ? "icon_sound_off" : "icon_sound_on");
    }

    public static void Play(string name, float pitch = 1, float volume = 0.7f)
    {
        if (sources == null)
        {
            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            sources = new AudioSource[6];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = go.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
        }
        if (!clips.TryGetValue(name, out var clip)) clips[name] = clip = Resources.Load<AudioClip>("Audio/" + name);
        if (!clip) return;
        var s = sources[next];
        next = (next + 1) % sources.Length;
        s.pitch = pitch;
        s.volume = volume;
        s.clip = clip;
        s.Play();
    }
}
