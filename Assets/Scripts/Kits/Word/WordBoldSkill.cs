using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】能力「太字」（Word。2026-11-19。docs/app-kits-1-design.md 12章）。数値はすべて【仮】。
/// 押した瞬間、自分のまわりに B の字の衝撃（直径 3。ゲージ・ダメージは攻撃データ。ゲージ +5）。
/// そこから 4秒、プレイヤーのふちが太くなり（青いふち）、近接のゲージ ×1.5（StatMods.Player.MeleeGauge。HUD に印）、
/// 近接の届く長さ ×1.3（StatMods.Player.MeleeReach。担当0 の KitMeleeReach が判定を前へ伸ばし、外すと戻す）。
/// クールタイム 8秒（効果 4秒）。自分を強くする、ただ1つの能力。倍率は 100％・シーンの切り替えで外れる
/// </summary>
[CreateAssetMenu(fileName = "Word_BoldSkill", menuName = "Team1/Kits/Word Bold Skill")]
public class WordBoldSkill : CustomAttack
{
    [Tooltip("衝撃の直径（マス）")]
    [SerializeField, Min(0.5f)] float _diameter = 3f;
    [Tooltip("強化の秒数")]
    [SerializeField, Min(0.5f)] float _duration = 4f;
    [Tooltip("与えるゲージの倍率")]
    [SerializeField, Min(1f)] float _gaugeRate = 1.5f;
    [Tooltip("近接の届く長さの倍率")]
    [SerializeField, Min(1f)] float _reach = 1.3f;

    /// <summary>倍率の鍵</summary>
    public const string Key = "word.bold";

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 center = (Vector2)owner.transform.position + new Vector2(0f, 0.5f);
        // B の衝撃（円の判定を一瞬）
        GameObject go = owner.CreateSpawnedObject($"Word_Bold_{data.name}", center);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = _diameter * 0.5f;
        Hitbox hb = owner.AddHitbox(go, data, col);
        hb.Activate();
        Destroy(go, 0.12f);
        owner.NotifyReleased(data);

        FxAnim.Sprite(WordArt.BoldB, center + new Vector2(0f, 1.2f), 40, 0.6f, null, 1.6f, "Word_B").Pop().Rise(0.5f);
        FxKit.Ring(center, _diameter, 0.22f, WordArt.BlueLight);

        // 4秒の強化
        StatMods.Player.MeleeGauge.SetFor(Key, _gaugeRate, _duration, owner.SkinIcon);
        StatMods.Player.MeleeReach.SetFor(Key, _reach, _duration);
        WordBoldAura.Apply(owner.gameObject, _duration);
        yield return new WaitForSeconds(0.1f);
    }
}

/// <summary>太字の間の、プレイヤーの青いふち（WordBoldSkill が付ける）。時間・100％・消えたときに消える。判定の大きさは変えない</summary>
public class WordBoldAura : MonoBehaviour
{
    float _endAt;
    SpriteRenderer _main;
    readonly SpriteRenderer[] _outline = new SpriteRenderer[4];
    bool _applied;

    /// <summary>付ける（もう付いていれば、残りの時間を戻すだけ）</summary>
    public static WordBoldAura Apply(GameObject player, float seconds)
    {
        if (player == null) return null;
        var a = player.GetComponent<WordBoldAura>();
        if (a == null) a = player.AddComponent<WordBoldAura>();
        a._endAt = Time.time + seconds;
        a.Begin();
        return a;
    }

    void Begin()
    {
        if (_applied) return;
        _applied = true;
        VirusGauge.AnyFilled += OnAnyFilled;
        // 青いふち：見た目の絵の後ろに、上下左右へ 1.5ドットずらした写しを4枚
        _main = OfficeKitTools.PlayerSprite(transform);
        if (_main == null) return;
        Vector2[] offs = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
        Vector3 ls = _main.transform.lossyScale;
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("BoldOutline");
            go.layer = _main.gameObject.layer;
            go.transform.SetParent(_main.transform, false);
            Vector2 o = offs[i] * (1.5f / 18f);
            go.transform.localPosition = new Vector3(o.x / Mathf.Max(0.01f, Mathf.Abs(ls.x)), o.y / Mathf.Max(0.01f, Mathf.Abs(ls.y)), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = _main.sortingLayerID;
            sr.sortingOrder = _main.sortingOrder - 1;
            sr.color = WordArt.Blue;
            _outline[i] = sr;
        }
    }

    void LateUpdate()
    {
        if (Time.time >= _endAt) { Destroy(this); return; }
        if (_main == null) return;
        bool blink = _endAt - Time.time < 0.6f && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        foreach (var sr in _outline)
        {
            if (sr == null) continue;
            sr.sprite = _main.sprite;
            sr.flipX = _main.flipX;
            sr.flipY = _main.flipY;
            sr.enabled = _main.enabled && !blink;
        }
    }

    void OnAnyFilled(VirusGauge g) => Destroy(this);

    void OnDestroy()
    {
        VirusGauge.AnyFilled -= OnAnyFilled;
        foreach (var sr in _outline) if (sr != null) Destroy(sr.gameObject);
    }
}
