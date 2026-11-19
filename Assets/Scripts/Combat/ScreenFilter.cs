using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 画面のフィルター（2026-11-19。Photoshop のボスの大技。数値は攻撃データ、見た目は【仮】）。AttackRunner が AttackData（種類 Filter）から作る。
/// ・画面の左から右へ、フィルターがかかっていく（明るい帯が横切り、画面の色が変わる）
/// ・狙う相手（プレイヤー）に、ランダムに状態異常をかける（PlayerStatusEffects。速いと遅いは同時にかけない）
/// ・かかった状態異常を、画面の真ん中に少しの間、文字で出す
/// ・決まった秒数（30秒）たつと、画面の色が元に戻る。ボスが正常化して消されたときも、状態異常を解いて元に戻す
/// ・AttackRunner が待つのは、フィルターがかかり終わるまで（判定時間）。そのあとボスは、いつもどおり戦う
/// </summary>
public class ScreenFilter : MonoBehaviour
{
    const float SweepTime = 0.7f;
    const float FadeOutTime = 0.6f;
    const float LabelTime = 2.6f;
    static readonly string[] FilterNames = { "色相・彩度", "セピア", "ポスタリゼーション", "ネオン光彩" };

    AttackData _data;
    PlayerStatusEffects _target;
    readonly List<StatusEffect> _applied = new List<StatusEffect>();
    SpriteRenderer _tint;
    SpriteRenderer _band;
    Color _color;
    float _width; // 画面の幅（マス）
    string _name;
    float _start;
    float _end;

    /// <summary>フィルターがかかり終わったら true（ボスは次の行動へ）</summary>
    public bool IsCastDone => Time.time - _start >= Mathf.Max(SweepTime, _data.ActiveTime);

    public static ScreenFilter Spawn(AttackRunner owner, AttackData data)
    {
        Camera cam = Camera.main;
        GameObject go = owner.CreateSpawnedObject($"Filter_{data.name}", cam != null ? (Vector2)cam.transform.position : Vector2.zero);
        var f = go.AddComponent<ScreenFilter>();
        f._data = data;
        f._start = Time.time;
        f._end = Time.time + data.FilterDuration;
        Color[] tints = data.FilterTints;
        int pick = Random.Range(0, Mathf.Max(1, tints.Length));
        f._color = tints.Length > 0 ? tints[pick] : new Color(0.7f, 0.2f, 1f, 0.22f);
        f._name = FilterNames[pick % FilterNames.Length];
        f.BuildOverlay(cam);
        f.ApplyEffects(owner);
        return f;
    }

    static Sprite Pixel => GameArt.Instance != null && GameArt.Instance.Pixel != null ? GameArt.Instance.Pixel : PlaceholderSprites.Square;

    /// <summary>画面いっぱいの色（カメラに付いていく）と、横切る明るい帯</summary>
    void BuildOverlay(Camera cam)
    {
        if (cam != null) transform.SetParent(cam.transform, true);
        transform.localPosition = new Vector3(0f, 0f, 5f);
        float h = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 12f;
        float w = h * (cam != null ? cam.aspect : 16f / 9f);
        _width = w;
        Color clear = _color;
        clear.a = 0f;
        _tint = AttackRunner.AddVisual(transform, Pixel, clear, new Vector2(w + 1f, h + 1f), 90, "Tint");
        _band = AttackRunner.AddVisual(transform, Pixel, new Color(1f, 1f, 1f, 0.35f), new Vector2(1.2f, h + 1f), 91, "Band");
        _band.transform.localPosition = new Vector3(-w * 0.5f, 0f, 0f);
    }

    /// <summary>ランダムに状態異常をかける（速いと遅いは同時にかけない）</summary>
    void ApplyEffects(AttackRunner owner)
    {
        Transform t = owner.Target;
        if (t == null) return;
        var controller = t.GetComponentInParent<PlayerController>();
        if (controller == null) return;
        _target = controller.GetComponent<PlayerStatusEffects>();
        if (_target == null) _target = controller.gameObject.AddComponent<PlayerStatusEffects>();

        var pool = new List<StatusEffect> { StatusEffect.Fast, StatusEffect.Slow, StatusEffect.LongCooldown, StatusEffect.HighJump };
        int n = Mathf.Clamp(_data.FilterEffectCount, 1, 3);
        for (int i = 0; i < n && pool.Count > 0; i++)
        {
            StatusEffect e = pool[Random.Range(0, pool.Count)];
            pool.Remove(e);
            if (e == StatusEffect.Fast) pool.Remove(StatusEffect.Slow);
            if (e == StatusEffect.Slow) pool.Remove(StatusEffect.Fast);
            _target.Apply(e, _data.FilterDuration);
            _applied.Add(e);
        }
    }

    void Update()
    {
        float age = Time.time - _start;
        float w = _width;
        // 左から右へ、明るい帯が横切りながら色が付いていく
        if (age < SweepTime)
        {
            float k = age / SweepTime;
            _band.enabled = true;
            _band.transform.localPosition = new Vector3(Mathf.Lerp(-w * 0.5f, w * 0.5f, k), 0f, 0f);
            Color c = _color;
            c.a = _color.a * k;
            _tint.color = c;
        }
        else
        {
            _band.enabled = false;
            // かかっている間は、ゆっくり脈打つ。最後は薄くなって消える
            float remain = _end - Time.time;
            float a = _color.a * (0.85f + 0.15f * Mathf.Sin(Time.time * 2f));
            if (remain < FadeOutTime) a *= Mathf.Clamp01(remain / FadeOutTime);
            Color c = _color;
            c.a = a;
            _tint.color = c;
            if (remain <= 0f) Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        // 時間切れ・ボスの正常化：かけた状態異常を解く
        if (_target == null) return;
        foreach (var e in _applied) _target.Remove(e);
    }

    void OnGUI()
    {
        float age = Time.time - _start;
        if (age > LabelTime || PixelGui.FontJa == null) return;
        PixelGui.Begin(false);
        float alpha = age < LabelTime - 0.4f ? 1f : Mathf.Clamp01((LabelTime - age) / 0.4f);
        float w = 260f, lineH = 16f;
        float h = 22f + _applied.Count * lineH + 6f;
        var box = new Rect((PixelGui.Width - w) * 0.5f, PixelGui.Height * 0.3f, w, h);
        PixelGui.Fill(box, new Color(0.05f, 0.06f, 0.1f, 0.85f * alpha));
        Color accent = _color;
        accent.a = alpha;
        PixelGui.Fill(new Rect(box.x, box.y, box.width, 2f), accent);
        PixelGui.Text(new Rect(box.x, box.y + 4f, box.width, 16f), $"フィルター：{_name}", 12, new Color(1f, 1f, 1f, alpha), TextAnchor.MiddleCenter);
        for (int i = 0; i < _applied.Count; i++)
        {
            PixelGui.Text(new Rect(box.x, box.y + 22f + i * lineH, box.width, lineH), PlayerStatusEffects.Label(_applied[i]), 12,
                new Color(1f, 0.85f, 0.4f, alpha), TextAnchor.MiddleCenter);
        }
        PixelGui.End();
    }
}
