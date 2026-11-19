using System.Collections.Generic;
using UnityEngine;

/// <summary>技の試作シーン用。他のアプリボスと同じ体・顔・ウィンドウを使う。</summary>
public sealed class DemoAppBossVisual : MonoBehaviour
{
    readonly List<Sprite> _sprites = new List<Sprite>();
    readonly List<Texture2D> _textures = new List<Texture2D>();
    Sprite[] _bodyFrames, _faceFrames, _windows;
    SpriteRenderer _body, _face, _hands, _window;
    VirusGauge _gauge;
    float _releasedAt = -1f;
    public float Visibility { get; set; } = 1f;

    public void Initialize(VirusGauge gauge, Texture2D body, Texture2D face, Texture2D hands, Sprite icon)
    {
        _gauge = gauge;
        _bodyFrames = Slice(body, 3);
        _faceFrames = Slice(face, 4);
        Sprite[] handFrames = Slice(hands, 1);
        _body = MakeRenderer("Body", 10, 7f / 18f);
        _window = MakeRenderer("Window", 12, -5f / 18f);
        _hands = MakeRenderer("Hands", 13, 7f / 18f);
        _face = MakeRenderer("Face", 13, 25f / 18f);
        _hands.sprite = handFrames.Length > 0 ? handFrames[0] : null;
        _windows = BossWindowArt.Create(icon, null, icon != null && !string.IsNullOrEmpty(icon.name) ? icon.name + " window" : "App window");
        foreach (Sprite sprite in _windows)
        {
            _sprites.Add(sprite);
            _textures.Add(sprite.texture);
        }
    }

    Sprite[] Slice(Texture2D texture, int count)
    {
        if (texture == null) return new Sprite[0];
        var frames = new Sprite[count];
        int width = texture.width / count;
        for (int i = 0; i < count; i++)
        {
            frames[i] = Sprite.Create(texture, new Rect(i * width, 0, width, texture.height),
                Vector2.one * 0.5f, GameArt.PixelsPerUnit);
            _sprites.Add(frames[i]);
        }
        return frames;
    }

    SpriteRenderer MakeRenderer(string name, int order, float y)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0, y, 0);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        return renderer;
    }

    void LateUpdate()
    {
        if (_gauge == null) return;
        bool restored = _gauge.IsFull;
        if (restored && _releasedAt < 0f) _releasedAt = Time.time;
        if (!restored) _releasedAt = -1f;
        Color tint = new Color(1, 1, 1, Visibility);
        _body.enabled = _hands.enabled = _face.enabled = !restored && Visibility > 0.01f;
        _body.color = _hands.color = _face.color = tint;
        if (_bodyFrames.Length > 0) _body.sprite = _bodyFrames[Mathf.FloorToInt(Time.time * 6f) % _bodyFrames.Length];
        if (_faceFrames.Length >= 4)
            _face.sprite = _faceFrames[(_gauge.Ratio >= 0.5f ? 2 : 0) + (Mathf.Repeat(Time.time, 3.4f) < 0.12f ? 1 : 0)];
        int stage = restored ? 0 : _gauge.Ratio >= 2f / 3f ? 2 : _gauge.Ratio >= 1f / 3f ? 1 : 0;
        _window.sprite = _windows[stage];
        float t = restored ? Mathf.Clamp01((Time.time - _releasedAt) / 1.6f) : 0f;
        _window.transform.localPosition = Vector3.up * (-5f / 18f + t * 1.2f);
        _window.color = new Color(1, 1, 1, Visibility * (1f - t));
    }

    void OnDestroy()
    {
        foreach (Sprite sprite in _sprites) if (sprite != null) Destroy(sprite);
        foreach (Texture2D texture in _textures) if (texture != null) Destroy(texture);
    }
}
