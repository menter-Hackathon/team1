using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】メールのボスの技「迷惑メール」（2026-11-19。docs/app-kits-1-design.md 15章 P1-2）。Custom 小〜中。
/// 封筒が count 通、床に落ちる（プレイヤーから 2マス以上離れた所。影と赤い「！」の予告 0.8秒。落ちてくる間は痛くない）→ 床に 6秒残る（KitProp）。
/// ・残った封筒は地雷：プレイヤーが近づく（体の真ん中から 1.2マス以内。近接の届く長さ 約1.6 より短くした：22章の【要確認】）と、
///   赤く 0.6秒点滅して、直径 3 の爆発
/// ・近接か能力で叩くと、ゴミ箱へ（爆発せずに消える）。点滅中でも叩けば消える
/// ・封筒（MailMine）は「全員に返信」が弾の出どころに使う
/// 第2形態：4通
/// </summary>
[CreateAssetMenu(fileName = "Mail_Spam", menuName = "Team1/Kits/Mail Spam")]
public class MailSpamAttack : CustomAttack
{
    [Tooltip("落とす封筒の数")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("影の予告（秒）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("床に残る秒数")]
    [SerializeField, Min(0.5f)] float _life = 6f;
    [Tooltip("プレイヤーから離す距離（マス）")]
    [SerializeField, Min(0f)] float _minDistance = 2f;
    [Tooltip("封筒どうしの間（マス）")]
    [SerializeField, Min(0.5f)] float _spacing = 2.5f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        Rect arena = BattleArena.GetBounds();
        float px = (owner.TargetPoint ?? arena.center).x;
        var xs = new List<float>();
        for (int i = 0; i < _count; i++) xs.Add(PickX(arena, px, xs));
        for (int i = 0; i < xs.Count; i++)
        {
            MailEnvelopeDrop.Spawn(owner, data, xs[i], _telegraph, _life);
            yield return new WaitForSeconds(0.12f);
        }
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_telegraph + 0.3f);
    }

    float PickX(Rect arena, float px, List<float> taken)
    {
        float best = arena.center.x;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            float x = Random.Range(arena.xMin + 1f, arena.xMax - 1f);
            if (Mathf.Abs(x - px) < _minDistance) continue;
            best = x;
            bool ok = true;
            foreach (float t in taken) if (Mathf.Abs(t - x) < _spacing) { ok = false; break; }
            if (ok) return x;
        }
        return best;
    }
}

/// <summary>落ちてくる封筒（痛くない）。床の影と赤い「！」。着地したら地雷の封筒（KitProp＋MailMine）になる。形態変化でも消える</summary>
public class MailEnvelopeDrop : CustomEffect
{
    public static readonly Vector2 Size = new Vector2(0.9f, 0.62f);
    const int Order = 24, ShadowOrder = 16;
    AttackRunner _owner;
    AttackData _data;
    float _landAt, _startY, _groundY, _life, _born, _x;
    Transform _env;
    SpriteRenderer _shadow;

    public static MailEnvelopeDrop Spawn(AttackRunner owner, AttackData data, float x, float telegraph, float life)
    {
        Rect arena = BattleArena.GetBounds();
        owner.TryFindGround(x, arena.yMax - 0.2f, out float ground);
        GameObject go = owner.CreateSpawnedObject("Mail_EnvelopeDrop", new Vector2(x, ground));
        var d = go.AddComponent<MailEnvelopeDrop>();
        d._owner = owner;
        d._data = data;
        d._x = x;
        d._groundY = ground;
        d._startY = arena.yMax + 0.5f;
        d._born = Time.time;
        d._landAt = Time.time + telegraph;
        d._life = life;
        d._shadow = MailArt.Put(go.transform, PlaceholderSprites.Circle, new Vector2(0f, 0.1f), new Vector2(1.1f, 0.3f), new Color(0f, 0f, 0f, 0.35f), ShadowOrder, "Shadow");
        RuntimeSprites.Show(go.transform, CallArt.Exclaim, new Vector2(0f, 0.65f), ShadowOrder + 1, 1f, "Exclaim");
        var env = new GameObject("Envelope");
        env.transform.SetParent(go.transform, false);
        MailArt.Put(env.transform, MailArt.SpamEnvelope, Vector2.zero, Size, Color.white, Order, "Art");
        d._env = env.transform;
        d.Place();
        return d;
    }

    void Place()
    {
        float k = Mathf.Clamp01((Time.time - _born) / Mathf.Max(0.01f, _landAt - _born));
        float y = Mathf.Lerp(_startY, _groundY + Size.y * 0.5f, k * k); // 速くなりながら落ちる
        if (_env != null)
        {
            _env.position = new Vector3(_x, y, 0f);
            _env.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 9f) * 12f * (1f - k));
        }
    }

    void Update()
    {
        Place();
        if (Time.time < _landAt) return;
        if (_owner != null)
        {
            MailMine.Spawn(_owner, _data, new Vector2(_x, _groundY + Size.y * 0.5f), _life);
            FxKit.Fragments(new Vector2(_x, _groundY + 0.1f), Vector2.up, 3, null, new[] { MailArt.Paper }, 0.8f);
        }
        Destroy(gameObject);
    }
}

/// <summary>
/// 床の地雷の封筒（KitProp に付ける）。近づくと赤く 0.6秒点滅して直径 3 の爆発。叩かれたらゴミ箱へ。
/// KitProp.Spawn(owner, …) で置くので、50％・100％で消える
/// </summary>
public class MailMine : MonoBehaviour
{
    const float Trigger = 1.2f, Fuse = 0.6f, Blast = 3f, BlastActive = 0.15f;
    static readonly List<MailMine> Alive = new List<MailMine>();

    AttackRunner _owner;
    AttackData _data;
    KitProp _prop;
    float _armedAt = -1f;
    bool _done;

    /// <summary>場に残っている封筒（爆発していない物）</summary>
    public static List<MailMine> All()
    {
        Alive.RemoveAll(m => m == null || m._done);
        return new List<MailMine>(Alive);
    }

    public static MailMine Spawn(AttackRunner owner, AttackData data, Vector2 center, float life)
    {
        KitProp prop = KitProp.Spawn(owner, center, MailArt.SpamEnvelope, MailEnvelopeDrop.Size, null, 1, life, 22);
        var m = prop.gameObject.AddComponent<MailMine>();
        m._owner = owner;
        m._data = data;
        m._prop = prop;
        prop.Broken += m.OnBroken;
        Alive.Add(m);
        return m;
    }

    void OnDestroy() => Alive.Remove(this);

    void OnBroken(KitProp p)
    {
        if (_done) return;
        _done = true;
        // ゴミ箱へ：ゴミ箱の絵がぽんと出て、封筒が吸いこまれる（文字は出さない）
        Vector2 at = transform.position;
        FxAnim.Sprite(MailArt.Trash, at + Vector2.up * 0.35f, 40, 0.5f, Color.white, 1.4f, "Mail_Trash").Pop().FadeFrom(0.6f);
        FxAnim.Sprite(MailArt.SpamEnvelope, at, 41, 0.25f, Color.white, 1f, "Mail_ToTrash").Velocity(new Vector2(0f, 2f)).Grow(0.2f);
    }

    /// <summary>撃ったら消える（全員に返信）</summary>
    public void Consume()
    {
        if (_done) return;
        _done = true;
        Destroy(gameObject);
    }

    void Update()
    {
        if (_done || _owner == null) return;
        Vector2 c = transform.position;
        if (_armedAt < 0f)
        {
            if (CallKitUtil.TryPlayerBounds(out Bounds p) && Mathf.Abs(p.center.x - c.x) <= Trigger && Mathf.Abs(p.center.y - c.y) <= Trigger + 0.3f)
                _armedAt = Time.time;
            return;
        }
        float t = Time.time - _armedAt;
        if (_prop != null && _prop.Visual != null)
            _prop.Visual.color = Mathf.FloorToInt(t / 0.08f) % 2 == 0 ? Color.Lerp(Color.white, KitDress.Red, 0.8f) : Color.white;
        if (t < Fuse) return;
        _done = true;
        CallKitUtil.TimedHit(_owner, _data, c, Vector2.one * Blast, true, 0f, BlastActive, "Spam");
        MailBlastFx.Show(c, Blast);
        Destroy(gameObject);
    }
}

/// <summary>爆発の見た目（赤い円がひろがって消える。紫のかけら）</summary>
public static class MailBlastFx
{
    public static void Show(Vector2 c, float diameter)
    {
        FxAnim a = FxAnim.Sprite(CallArt.Disc(Mathf.Clamp(Mathf.RoundToInt(diameter * CallArt.Ppu), 8, 48)), c, 45, 0.25f, CallKitUtil.Alpha(KitDress.Red, 0.55f), diameter / (Mathf.Clamp(Mathf.RoundToInt(diameter * CallArt.Ppu), 8, 48) * CallArt.Px), "Mail_Blast");
        a.Grow(1.15f).FadeFrom(0.3f);
        FxKit.Ring(c, diameter, 0.25f, Color.white);
        FxKit.Fragments(c, Vector2.up, 6, null, new[] { MailArt.Purple, MailArt.Paper, KitDress.Red }, 1f);
    }
}
