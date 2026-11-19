using UnityEngine;

/// <summary>
/// 【仮】ボスの上の入力の欄（2026-11-19。担当1 のキットで使い回す）。決めた文字（@channel など）を 1文字ずつ打って、送信で光って消える。
/// 飾りだけ（当たらない）。攻撃する人の出した物で、CustomEffect なので、形態変化・正常化で消える
/// </summary>
public class TalkTypingBox : CustomEffect
{
    const int Order = 40;
    const float TextHeight = 0.4f;

    AttackRunner _owner;
    string _text;
    float _start, _typeEnd, _end;
    Color _accent;
    TextMesh _tm;
    SpriteRenderer _bg, _caret;
    Transform _root;

    /// <summary>
    /// 入力の欄を出す。typeTime：打ち終わるまで（この間が予告）、accent：送信で光る色。
    /// 欄は攻撃する人の体の上（画面の中に収める）
    /// </summary>
    public static TalkTypingBox Spawn(AttackRunner owner, string text, float typeTime, Color accent)
    {
        if (owner == null || string.IsNullOrEmpty(text)) return null;
        GameObject go = owner.CreateSpawnedObject("TalkTypingBox", owner.OriginPoint);
        var b = go.AddComponent<TalkTypingBox>();
        b._owner = owner;
        b._text = text;
        b._accent = accent;
        b._start = Time.time;
        b._typeEnd = b._start + Mathf.Max(0.1f, typeTime);
        b._end = b._typeEnd + 0.3f;
        b.Build();
        return b;
    }

    void Build()
    {
        float w = _text.Length * TextHeight + 0.9f;
        float h = 0.9f;
        Bounds body = TalkKitUtil.OwnerBody(_owner);
        Rect view = ScreenUi.ViewRect();
        float x = Mathf.Clamp(body.center.x, view.xMin + w * 0.5f + 0.3f, view.xMax - w * 0.5f - 0.3f);
        float y = Mathf.Min(body.max.y + 0.4f + h * 0.5f, view.yMax - 2.4f);
        _root = TalkArt.Child(transform, "Box", new Vector2(x, y));
        int wp = Mathf.RoundToInt(w * 18f), hp = Mathf.RoundToInt(h * 18f);
        var c = new TalkCanvas(wp, hp);
        c.Round(0, 0, wp, hp, 5, TalkArt.White, TalkArt.Grey);
        _bg = TalkArt.ShowNative(_root, c.ToSprite($"TypeBox_{wp}_{hp}"), Vector2.zero, Order, 1f, "Bg");
        _tm = TalkArt.Text(_root, "", new Vector2(-w * 0.5f + 0.4f, 0f), TextHeight, TalkArt.Ink, Order + 1, true, TextAnchor.MiddleLeft);
        _caret = TalkArt.Show(_root, PlaceholderSprites.Square, new Vector2(-w * 0.5f + 0.4f, 0f), new Vector2(TalkArt.Px * 2f, TextHeight * 1.1f), (Color)TalkArt.Ink, Order + 2, "Caret");
    }

    void Update()
    {
        float now = Time.time;
        if (now >= _end) { Destroy(gameObject); return; }
        if (now < _typeEnd)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt((now - _start) / (_typeEnd - _start) * _text.Length), 0, _text.Length);
            if (_tm.text.Length != n)
            {
                _tm.text = _text.Substring(0, n);
                float w = _text.Length * TextHeight + 0.9f;
                _caret.transform.localPosition = new Vector3(-w * 0.5f + 0.4f + n * TextHeight, 0f, 0f);
            }
            _caret.enabled = Mathf.FloorToInt(now / 0.25f) % 2 == 0;
            return;
        }
        // 送信：欄が色で光って、上へ少し動いて消える
        _caret.enabled = false;
        float t = Mathf.Clamp01((now - _typeEnd) / (_end - _typeEnd));
        _bg.color = Color.Lerp(_accent, new Color(1f, 1f, 1f, 0f), t);
        WorldText.SetColor(_tm, new Color(0.11f, 0.11f, 0.12f, 1f - t));
        _root.position += Vector3.up * Time.deltaTime * 1.5f;
    }
}
