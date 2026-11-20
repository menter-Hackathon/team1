using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Premiere Pro のボスの登場「モニターから這い出す」（2026-11-20。docs/premiere-edit-screen-design.md 10章）。
/// BossEntrance.CustomAppear に PremiereTimelineStage が登録する。プログラムモニター（PremiereMonitor）があるときだけ、点滅のかわりに回る。
/// ・本物のボスの体を使う（偽物の絵は作らない）。SpriteMask で、映像の四角の上の端から床までの「映像の幅の柱」の中だけ見せる。
///   映像の中にいる間は、上・左・右が映像のふちで切れる（画面の中）。下のふちを越えた所は切れない（画面から手前に出た）
/// ・流れ（表示された瞬間 s 0 から）：
///   ① 0〜0.3 映像の中・0.8倍で体の下の端を映像の下のふちに付ける（直前の映像のかたまり 2倍と同じ大きさ・場所で入れかわる）。こまかいゆれ・うすさ 0.75
///   ② 0.3〜0.99 3回に分けて這い出す（動く 0.16秒＋止まる 0.07秒。動くたびに 5度かたむく。0.8 → 1.0倍。うすさ 0.85 → 1.0）。動くたびに映像が下へ 1ドット引っぱられる
///   ③ 0.99〜1.24 着く：つぶれて戻る・画面が小さくゆれる。マスクを外す
///   ④ 1.24〜1.65 その場で点滅（当たりが出る合図）→ 戦える（BossEntrance が体当たりを戻す）。最初の技は s 1.95 から（Stun）
/// ・ガラスの幕（10-B1）：映像の中にいる間、映像の四角にうすい青の幕を体より前に重ねる（中はくすみ、出た所はくっきり）
/// ・途中で止まった（ボスが非表示・消された・戦い以外の状態・モニターが消えた）ときは、位置・大きさ・傾き・マスク・うすさをすぐ元に戻す
/// ・切り戻し：Enabled = false で今までの点滅（映像の予告も出さない）
/// </summary>
public class PremiereBossCrawl : MonoBehaviour
{
    /// <summary>【仮】false にすると、今までの点滅で出る</summary>
    public static bool Enabled = true;

    // ───────── 数値【仮】（表示された瞬間からの秒。10-1） ─────────
    const float GripEnd = 0.3f;        // ① 映像の中・ふちに手をかける
    const int Lurches = 3;             // ② 這い出す回数
    const float LurchMove = 0.16f;     //    1回の動く秒
    const float LurchHold = 0.07f;     //    1回の止まる秒
    const float LandTime = 0.25f;      // ③ 着く
    const float BlinkTime = 0.41f;     // ④ その場で点滅
    const float InsideScale = 0.8f;
    const float LurchTilt = 5f;        // 這うときの傾き（度）
    const float FirstMoveAt = 1.95f;   // 最初の技を考え始める（s）
    const float GlassAlpha = 0.4f;     // 10-B1 ガラスの幕
    static readonly Color GlassColor = new Color(0.62f, 0.72f, 0.86f, GlassAlpha);

    static float CrawlEnd => GripEnd + Lurches * (LurchMove + LurchHold);
    /// <summary>表示から戦えるまで（秒）</summary>
    public static float Duration => CrawlEnd + LandTime + BlinkTime;

    /// <summary>終わったか（戦える状態まで来て、元に戻し終えたか）</summary>
    public bool Done { get; private set; }

    BossController _boss;
    PremiereMonitor _monitor;
    float _start;
    Vector3 _target, _scale0;
    Quaternion _rot0;
    SpriteRenderer[] _renderers;
    SpriteMaskInteraction[] _interactions;
    GameObject _mask;
    SpriteRenderer _glass;
    bool _masked, _landed;
    int _lastLurch = -1;
    Vector2 _videoCenter, _videoSize;
    float _bottomOffset = 1.5f;   // 大きさ 1 のとき、体の根元から下の端まで（マス）

    /// <summary>BossEntrance.CustomAppear に渡す入口。モニターが無い・スイッチがオフなら null（今までの点滅）</summary>
    public static IEnumerator Run(BossController boss)
    {
        if (!Enabled || boss == null) return null;
        PremiereMonitor monitor = PremiereMonitor.Current;
        if (monitor == null) return null;
        var c = boss.gameObject.AddComponent<PremiereBossCrawl>();
        c.Begin(boss, monitor);
        return c.WaitDone();
    }

    IEnumerator WaitDone()
    {
        while (this != null && !Done) yield return null;
    }

    void Begin(BossController boss, PremiereMonitor monitor)
    {
        _boss = boss;
        _monitor = monitor;
        _start = Time.time;
        Transform tr = boss.transform;
        _target = tr.position;
        _scale0 = tr.localScale;
        _rot0 = tr.localRotation;
        _videoCenter = monitor.VideoCenter;
        _videoSize = monitor.VideoSize;
        _bottomOffset = MeasureBottom(tr);
        boss.Stun(FirstMoveAt); // ボスの Start のあと（BossEntrance が 1フレーム待ってから呼ぶ）
        monitor.OnBossCrawl(CrawlEnd + LandTime);

        // 映像の上の端から床までの、映像の幅の柱だけ見せる
        _renderers = boss.GetComponentsInChildren<SpriteRenderer>(true);
        _interactions = new SpriteMaskInteraction[_renderers.Length];
        int maxOrder = int.MinValue, maxLayer = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _interactions[i] = _renderers[i].maskInteraction;
            _renderers[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            if (_renderers[i].sortingOrder > maxOrder) { maxOrder = _renderers[i].sortingOrder; maxLayer = _renderers[i].sortingLayerID; }
        }
        if (maxOrder == int.MinValue) maxOrder = 20;
        float top = _videoCenter.y + _videoSize.y * 0.5f;
        float floor = BattleArena.GetBounds().yMin - 1f;
        _mask = new GameObject("PremiereCrawlMask");
        _mask.transform.SetParent(monitor.transform, true);
        var mask = _mask.AddComponent<SpriteMask>();
        mask.sprite = PlaceholderSprites.Square;
        PlaceWorldRect(_mask.transform, mask.sprite, new Vector2(_videoCenter.x, (top + floor) * 0.5f), new Vector2(_videoSize.x, top - floor));
        _masked = true;

        // 10-B1：ガラスの幕（映像の四角だけ。体より前）
        _glass = PremiereArt.Box(monitor.transform, Vector2.zero, Vector2.one, GlassColor, maxOrder + 1, "CrawlGlass");
        _glass.sortingLayerID = maxLayer; // 体と同じ並びの中で、体より前
        PlaceWorldRect(_glass.transform, _glass.sprite, _videoCenter, _videoSize);
        Apply(0f);
    }

    /// <summary>親の大きさを考えて、ワールドの四角（まん中・大きさ）に合わせる</summary>
    static void PlaceWorldRect(Transform t, Sprite sprite, Vector2 center, Vector2 size)
    {
        t.position = new Vector3(center.x, center.y, 0f);
        Vector2 native = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        Vector3 ps = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(size.x / Mathf.Max(0.0001f, native.x * ps.x), size.y / Mathf.Max(0.0001f, native.y * ps.y), 1f);
    }

    void Update()
    {
        float t = Time.time - _start;
        if (Done)
        {
            // 戦えるようになったあと、最初の技（s 1.95）までは、短いひるみで上書きされたらかけ直す（長いひるみは短くしない）
            if (_boss == null || t >= FirstMoveAt) { Destroy(this); return; }
            float need = FirstMoveAt - t;
            if (_boss.State == BossState.Fighting && _boss.StunRemaining < need - 0.02f) _boss.Stun(need);
            return;
        }
        if (_boss == null || _monitor == null || _boss.State != BossState.Fighting)
        {
            Restore(); // 途中で止まった（モニターが消えた・形態変化など）
            return;
        }
        if (t >= Duration)
        {
            Restore();
            return;
        }
        Apply(t);
    }

    /// <summary>体の下の端が、映像の下のふちに来るときの体の根元の y（大きさ s）</summary>
    float GripY(float s) => _videoCenter.y - _videoSize.y * 0.5f + _bottomOffset * s;

    /// <summary>根元から、見えている絵の下の端までの距離（大きさを変える前に測る。体当たりの判定はもう止まっていて大きさが 0 なので、絵で測る）</summary>
    static float MeasureBottom(Transform root)
    {
        bool any = false;
        float minY = 0f;
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            float y = sr.bounds.min.y;
            if (!any || y < minY) minY = y;
            any = true;
        }
        float d = any ? root.position.y - minY : 1.5f;
        return Mathf.Clamp(d, 0.3f, 3f);
    }

    void Apply(float t)
    {
        Transform tr = _boss.transform;
        Vector3 pos;
        float s, alpha;
        float tilt = 0f;
        Vector2 squash = Vector2.one;
        if (t < GripEnd)
        {
            // ① 映像の中・下のふちに手をかける。力をためる（左右のこまかいゆれ）
            s = InsideScale;
            alpha = 0.75f;
            pos = new Vector3(_videoCenter.x + Mathf.Sin(t * 60f) * 0.04f, GripY(s), _target.z);
        }
        else if (t < CrawlEnd)
        {
            // ② 3回に分けて這い出す：動く（出だしが速い）→ 止まる。動くたびに大きくなり、左・右・左へ傾く
            float lt = t - GripEnd;
            float step = LurchMove + LurchHold;
            int i = Mathf.Min(Lurches - 1, Mathf.FloorToInt(lt / step));
            float within = lt - i * step;
            float k = within < LurchMove ? Ease(within / LurchMove) : 1f;
            float a = (i + k) / Lurches;
            s = Mathf.Lerp(InsideScale, 1f, a);
            alpha = Mathf.Lerp(0.85f, 1f, a);
            pos = new Vector3(Mathf.Lerp(_videoCenter.x, _target.x, a), Mathf.Lerp(GripY(InsideScale), _target.y, a), _target.z);
            float sign = i % 2 == 0 ? 1f : -1f;
            tilt = within < LurchMove ? sign * LurchTilt * Mathf.Sin(k * Mathf.PI) : 0f;
            if (i != _lastLurch)
            {
                _lastLurch = i;
                if (_monitor != null) _monitor.Tug(); // 映像のふちが引っぱられる（10-A4）
            }
        }
        else if (t < CrawlEnd + LandTime)
        {
            // ③ 着く：つぶれて戻る。画面が小さくゆれる。マスクとガラスの幕を外す
            float k = Mathf.Clamp01((t - CrawlEnd) / LandTime);
            if (!_landed)
            {
                _landed = true;
                RemoveMask();
                StageCamera.Shake(0.12f, 0.25f);
            }
            s = 1f;
            alpha = 1f;
            pos = _target;
            float q = Mathf.Sin(k * Mathf.PI) * 0.12f;
            squash = new Vector2(1f + q, 1f - q);
        }
        else
        {
            // ④ その場で点滅（ここに当たりが出る合図）
            s = 1f;
            pos = _target;
            alpha = Mathf.FloorToInt((t - CrawlEnd - LandTime) / 0.1f) % 2 == 0 ? 1f : 0.5f;
        }
        tr.position = pos;
        tr.localScale = new Vector3(_scale0.x * s * squash.x, _scale0.y * s * squash.y, _scale0.z);
        tr.localRotation = _rot0 * Quaternion.Euler(0f, 0f, tilt);
        _boss.SetVisibility(alpha);
    }

    static float Ease(float k)
    {
        k = Mathf.Clamp01(k);
        return 1f - (1f - k) * (1f - k);
    }

    void RemoveMask()
    {
        if (_masked)
        {
            _masked = false;
            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null) _renderers[i].maskInteraction = _interactions[i];
                }
            }
        }
        if (_mask != null) Destroy(_mask);
        _mask = null;
        if (_glass != null) Destroy(_glass.gameObject);
        _glass = null;
    }

    /// <summary>片づけ（ふつうに終わったときも、止まったときも、これ）。体当たりの有効化は BossEntrance がする</summary>
    void Restore()
    {
        if (Done) return;
        RemoveMask();
        if (_boss != null)
        {
            Transform tr = _boss.transform;
            tr.position = _target;
            tr.localScale = _scale0;
            tr.localRotation = _rot0;
            _boss.SetVisibility(1f);
        }
        if (_monitor != null) _monitor.EndBossCrawl();
        Done = true;
    }

    void OnDisable()
    {
        Restore(); // 途中で非表示・消された：すぐ元に戻す
    }
}
