using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】KitMotion の部品1つの土台（2026-11-19。docs/stage-gimmick-variety.md 1章）。担当0 の物。
/// ・予告 → 動く → 消える を Step(age) で進める（age＝出てからの秒数。Time.time なので、止める間・ヒットストップは進まない）
/// ・CustomEffect なので、ボスの物として出すと 50％（形態変化）・100％で消える
/// ・自分で最後まで行ったときだけ Finished・onHit などの知らせを出す。外から消された（片づけ・Cancel）ときは出さない
/// </summary>
public abstract class KitMotionItem : CustomEffect
{
    /// <summary>
    /// 並びの順（床のタイル 1・足場 2 → 場所の印・動く床 8〜9 → キャラクター 10〜12 → 予告 16〜18 → 危ない物 21 → 当たった絵 47〜。
    /// 画面の表示は OnGUI なので、いつも上）
    /// </summary>
    public const int OrderZone = 8, OrderWarn = 17, OrderCrack = 18, OrderBody = 21, OrderMark = 22;

    static readonly List<KitMotionItem> Live = new List<KitMotionItem>();

    /// <summary>自分で最後まで行って終わったとき（外から消されたときは呼ばれない）</summary>
    public event Action<KitMotionItem> Finished;

    /// <summary>終わったか</summary>
    public bool IsDone { get; private set; }
    /// <summary>出てからの秒数</summary>
    public float Age => Time.time - _born;
    /// <summary>予告の秒数</summary>
    public float Warn { get; internal set; }
    /// <summary>予告の間か</summary>
    public bool Warning => !IsDone && Age < Warn;
    /// <summary>動く本体（無ければ null。KitHitTarget では的）</summary>
    public Transform Body { get; protected set; }
    /// <summary>本体の絵（無ければ null）</summary>
    public SpriteRenderer Visual { get; protected set; }
    /// <summary>当たり判定（damage 0・良い物は null）</summary>
    public Hitbox Hitbox { get; protected set; }
    /// <summary>持ち主（片づけ先）</summary>
    public KitOwner Owner { get; internal set; }
    /// <summary>当たると痛い物か</summary>
    public bool Harmful => Hitbox != null;

    float _born;
    Color _baseColor = Color.white;

    protected virtual void Awake() => _born = Time.time;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!Live.Contains(this)) Live.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Live.Remove(this);
    }

    protected override void OnDestroy()
    {
        Live.Remove(this);
        base.OnDestroy();
    }

    protected virtual void Update()
    {
        if (IsDone) return;
        bool more;
        try { more = Step(Age); }
        catch (Exception e)
        {
            Debug.LogException(e);
            more = false;
        }
        if (!more) Finish();
    }

    /// <summary>毎フレーム（age＝出てからの秒数）。false を返したら終わり</summary>
    protected abstract bool Step(float age);

    /// <summary>やめる（知らせは出さない。子も一緒に消える）</summary>
    public void Cancel()
    {
        if (IsDone) return;
        IsDone = true;
        Destroy(gameObject);
    }

    /// <summary>最後まで行って終わる（Finished を出して消える）</summary>
    protected void Finish()
    {
        if (IsDone) return;
        IsDone = true;
        KitMotion.Safe(() => Finished?.Invoke(this));
        Destroy(gameObject);
    }

    // ───────── 本体と判定 ─────────

    /// <summary>
    /// 本体（子の "Body"）を作る：絵（sprite が null なら四角か丸）と、damage が 1 以上なら当たり判定（絵の hitScale 倍）。
    /// 判定は切ってある（SetHarm(true) で入れる）
    /// </summary>
    protected Transform MakeBody(Sprite sprite, Color color, Vector2 size, bool circle, int damage, float hitScale = 0.8f)
    {
        var go = new GameObject("Body");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        Visual = AttackRunner.AddVisual(go.transform, sprite != null ? sprite : circle ? PlaceholderSprites.Circle : PlaceholderSprites.Square, color, size, OrderBody);
        _baseColor = color;
        Hitbox = KitMotion.AddHurt(go, Owner, size * hitScale, circle, damage);
        Body = go.transform;
        return Body;
    }

    internal Transform BuildBody(Sprite sprite, Color color, Vector2 size, bool circle, int damage, float hitScale = 0.8f)
        => MakeBody(sprite, color, size, circle, damage, hitScale);

    /// <summary>当たり判定を入れる・切る（判定が無ければ何もしない）</summary>
    protected void SetHarm(bool on)
    {
        if (Hitbox == null) return;
        if (on && !Hitbox.IsActive) Hitbox.Activate();
        else if (!on && Hitbox.IsActive) Hitbox.Deactivate();
    }

    /// <summary>予告の間の本体：うすく点滅（危ない物は赤みを足す）。最後の 0.3秒は速く点滅。off で元の色</summary>
    protected void Ghost(bool on)
    {
        if (Visual == null) return;
        if (!on)
        {
            Visual.color = _baseColor;
            return;
        }
        bool fast = Warn - Age < 0.3f;
        float a = fast ? (Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0 ? 0.75f : 0.2f) : 0.35f + 0.15f * Mathf.Sin(Time.time * 12f);
        Color c = Harmful ? Color.Lerp(_baseColor, KitMotion.Red, 0.55f) : _baseColor;
        c.a = a * Mathf.Max(0.3f, _baseColor.a);
        Visual.color = c;
    }

    /// <summary>本体の色を変える（元の色として覚える）</summary>
    public void SetColor(Color color)
    {
        _baseColor = color;
        if (Visual != null) Visual.color = color;
    }

    /// <summary>本体の色（元の色）</summary>
    protected Color BaseColor => _baseColor;

    // ───────── まとめて消す（KitMotion.ClearHazards） ─────────

    internal static int ClearHarmful(Vector2? center, float radius)
    {
        var copy = new List<KitMotionItem>(Live);
        float r2 = radius * radius;
        int n = 0;
        foreach (var it in copy)
        {
            if (it == null || it.IsDone || !it.Harmful || !(it is KitMotionMover)) continue;
            Vector2 p = it.Body != null ? (Vector2)it.Body.position : (Vector2)it.transform.position;
            if (center.HasValue && (p - center.Value).sqrMagnitude > r2) continue;
            FxKit.Ring(p, 0.8f, 0.2f, Color.white);
            it.Cancel();
            n++;
        }
        return n;
    }
}

/// <summary>
/// 【仮】KitMotion の部品のまとまり（EdgeSweep の count 個など）。子の部品を時間どおりに出し、子が全部いなくなったら終わる。
/// 子は全部この物の子なので、これを消せば全部消える
/// </summary>
public class KitMotionGroup : KitMotionItem
{
    class Timed
    {
        public float At;
        public Action Act;
        public bool Done;
    }

    readonly List<Timed> _steps = new List<Timed>();
    readonly List<KitMotionItem> _items = new List<KitMotionItem>();

    /// <summary>出した子の部品（消えた物は抜く）</summary>
    public IReadOnlyList<KitMotionItem> Items
    {
        get
        {
            _items.RemoveAll(i => i == null);
            return _items;
        }
    }

    /// <summary>少なくともこの秒数は生きる（予告の印を出している間など）</summary>
    internal float MinLife { get; set; }

    /// <summary>seconds 秒（出てから）に act を呼ぶ</summary>
    internal void At(float seconds, Action act)
    {
        if (act != null) _steps.Add(new Timed { At = seconds, Act = act });
    }

    internal void Add(KitMotionItem item)
    {
        if (item != null && item != this && !_items.Contains(item)) _items.Add(item);
    }

    protected override bool Step(float age)
    {
        bool pending = false;
        for (int i = 0; i < _steps.Count; i++)
        {
            Timed s = _steps[i];
            if (s.Done) continue;
            if (age >= s.At)
            {
                s.Done = true;
                KitMotion.Safe(s.Act);
            }
            else pending = true;
        }
        _items.RemoveAll(i => i == null || i.IsDone);
        return pending || _items.Count > 0 || age < MinLife;
    }
}
