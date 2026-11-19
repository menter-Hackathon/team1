using UnityEngine;

/// <summary>
/// 【仮】「考え中」の吹き出し（2026-11-19。ChatGPT。docs/app-kits-1-design.md 5章）。担当3 の物。
/// 暗い角の丸い板に、点 3つが順に光る。親に付けて動きについていく。seconds 秒で消える（0 なら消さない）。
/// SetProgress で、下に細い緑のメーター（ギミック「考え中…」の、あとどれだけ叩けば止まるか。文字は出さない）
/// </summary>
public class ChatGptThinkingDots : MonoBehaviour
{
    const int Order = 34;

    readonly SpriteRenderer[] _dots = new SpriteRenderer[3];
    SpriteRenderer _trough;
    SpriteRenderer _fill;
    float _start;
    float _end = float.PositiveInfinity;
    float _period = 0.36f;
    float _scale = 1f;
    float _progress = -1f;
    const float BarW = 17f * ChatGptArt.Px;

    /// <summary>吹き出しを parent の子に付ける（localPos は親からの位置）。seconds：消えるまで（0 ならずっと）</summary>
    public static ChatGptThinkingDots Attach(Transform parent, Vector2 localPos, float seconds, float scale = 1f, float period = 0.36f)
    {
        var go = new GameObject("ChatGPTThinking");
        if (parent != null)
        {
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        go.transform.localPosition = localPos;
        var t = go.AddComponent<ChatGptThinkingDots>();
        t._start = Time.time;
        if (seconds > 0f) t._end = Time.time + seconds;
        t._scale = scale;
        t._period = Mathf.Max(0.06f, period);
        t.Build();
        return t;
    }

    /// <summary>ワールドの位置に置く（親なし）</summary>
    public static ChatGptThinkingDots At(Vector2 position, float seconds, float scale = 1f, float period = 0.36f)
    {
        ChatGptThinkingDots t = Attach(null, position, seconds, scale, period);
        t.transform.position = position;
        return t;
    }

    void Build()
    {
        RuntimeSprites.Show(transform, ChatGptArt.Bubble, Vector2.zero, Order, 1f, "Bubble");
        for (int i = 0; i < 3; i++)
            _dots[i] = RuntimeSprites.Show(transform, ChatGptArt.Dot, new Vector2((i - 1) * 5f * ChatGptArt.Px, 0f), Order + 1, 1f, "Dot" + i);
        Apply();
    }

    /// <summary>下の細いメーター（0〜1）。負なら出さない</summary>
    public void SetProgress(float ratio)
    {
        _progress = ratio;
        if (ratio < 0f)
        {
            if (_trough != null) _trough.enabled = false;
            if (_fill != null) _fill.enabled = false;
            return;
        }
        if (_trough == null)
        {
            _trough = FxKit.Box(transform, new Vector2(0f, -6.5f * ChatGptArt.Px), new Vector2(BarW, 2f * ChatGptArt.Px), ChatGptArt.Panel, Order, "Trough");
            _fill = FxKit.Box(transform, Vector2.zero, new Vector2(BarW, 2f * ChatGptArt.Px), ChatGptArt.Green, Order + 1, "Fill");
        }
        float r = Mathf.Clamp01(ratio);
        _trough.enabled = _fill.enabled = true;
        SetWidth(_fill, Mathf.Max(0.001f, BarW * r), 2f * ChatGptArt.Px);
        // 左の端をそろえる（FxKit.Box の絵は真ん中が原点）
        _fill.transform.localPosition = new Vector3(-BarW * 0.5f * (1f - r), -6.5f * ChatGptArt.Px, 0f);
    }

    /// <summary>四角の幅を変える（並べる絵なら size、伸ばす絵なら大きさ）</summary>
    static void SetWidth(SpriteRenderer sr, float w, float h)
    {
        if (sr == null) return;
        if (sr.drawMode != SpriteDrawMode.Simple) { sr.size = new Vector2(w, h); return; }
        Vector2 native = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        sr.transform.localScale = new Vector3(w / Mathf.Max(0.001f, native.x), h / Mathf.Max(0.001f, native.y), 1f);
    }

    void Update()
    {
        if (Time.time >= _end)
        {
            Destroy(gameObject);
            return;
        }
        Apply();
    }

    void Apply()
    {
        float age = Time.time - _start;
        float pop = age < 0.1f ? Mathf.Lerp(0.3f, 1.1f, age / 0.1f) : age < 0.15f ? Mathf.Lerp(1.1f, 1f, (age - 0.1f) / 0.05f) : 1f;
        float s = _scale * pop;
        transform.localScale = new Vector3(s, s, 1f);
        int lit = Mathf.FloorToInt(age / (_period / 3f)) % 3;
        for (int i = 0; i < 3; i++)
        {
            if (_dots[i] == null) continue;
            bool on = i == lit;
            _dots[i].color = on ? Color.white : new Color(ChatGptArt.Gray.r, ChatGptArt.Gray.g, ChatGptArt.Gray.b, 0.7f);
            _dots[i].transform.localPosition = new Vector3((i - 1) * 5f * ChatGptArt.Px, on ? ChatGptArt.Px : 0f, 0f);
        }
    }
}
