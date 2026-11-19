using System;
using UnityEngine;

/// <summary>
/// 【仮】強い雑魚（2026-11-19。docs/stage-pacing-design.md 4-3。担当3）。歩く雑魚の 1.5倍・HP 3・速さ 0.7倍（ZakoVariants が作る）。
/// ・そのステージのアプリのアイコンを小さく抱えている（ボスの子分に見える）。頭の上に HP の赤い点（当たるたびに1つはじけて消える）
/// ・倒すと Heavy の当たった絵（ヒットストップつき）と、拾える物を1つ落とす（ライフが満タンならシールド、ほかは回復）
/// ・ボスの登場が始まったあとは、拾える物を落とさない
/// 歩き方は ZakoWalker のまま
/// </summary>
[RequireComponent(typeof(Health))]
public class ZakoElite : MonoBehaviour
{
    [SerializeField] SpriteRenderer[] _pips;
    [SerializeField] Transform _icon;

    Health _health;
    Vector3 _iconBase;
    float _shakeUntil;
    bool _dropped;

    /// <summary>ひな形を作るときに入れる（ZakoVariants）</summary>
    public void Configure(SpriteRenderer[] pips, Transform icon)
    {
        _pips = pips;
        _icon = icon;
    }

    void Awake()
    {
        _health = GetComponent<Health>();
        if (_icon != null) _iconBase = _icon.localPosition;
    }

    void OnEnable()
    {
        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
        RefreshPips();
    }

    void OnDisable()
    {
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
    }

    void OnDamaged(DamageInfo info)
    {
        try
        {
            // 消える点の位置で、赤いかけらがはじける
            if (_pips != null && !_health.IsDead)
            {
                int idx = Mathf.Clamp(_health.Current, 0, _pips.Length - 1);
                SpriteRenderer pip = _pips[idx];
                if (pip != null && pip.enabled)
                {
                    FxKit.Fragments(pip.transform.position, Vector2.up, 3, new[] { FxArt.Square(2) }, new[] { FxKit.EnemyRed, Color.white });
                }
            }
            RefreshPips();
            _shakeUntil = Time.time + 0.2f;
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoElite.OnDamaged", $"強い雑魚で例外：{e}");
        }
    }

    void OnDied()
    {
        try
        {
            RefreshPips();
            if (_icon != null) _icon.gameObject.SetActive(false);
            StagePacer pacer = StagePacer.Current;
            Vector2 c = HitFx.CenterOf(gameObject, (Vector2)transform.position + Vector2.up * 0.6f);
            Transform player = RewardKit.PlayerOf(pacer);
            Vector2 dir = player != null ? c - RewardKit.BodyCenter(player) : Vector2.up;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            HitFx.Play(new HitInfo
            {
                Point = c,
                Direction = dir.normalized,
                Strength = HitStrength.Heavy,
                Target = gameObject,
                TargetIsPlayer = false,
                Skin = RewardKit.SkinOf(pacer),
                ErrorKind = "",
                Theme = RewardKit.ThemeOf(pacer),
                AppIcon = RewardKit.IconOf(pacer),
            });

            if (_dropped || !KillChain.RewardsOpen) return;
            _dropped = true; // 強い雑魚1体につき1つだけ
            Health ph = player != null ? player.GetComponent<Health>() : null;
            PickupKind kind = ph != null && ph.Current >= ph.Max ? PickupKind.Shield : PickupKind.Heal;
            Pickup.Drop(c, kind, player != null ? player.position.x : c.x);
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("ZakoElite.OnDied", $"強い雑魚で例外：{e}");
        }
    }

    void Update()
    {
        if (_icon == null) return;
        // 当たった直後は、抱えたアイコンが小さくふるえる
        float shake = Time.time < _shakeUntil ? (Mathf.FloorToInt(Time.time / 0.04f) % 2 == 0 ? 1f : -1f) * (1f / 18f) : 0f;
        _icon.localPosition = _iconBase + new Vector3(shake, 0f, 0f);
    }

    void RefreshPips()
    {
        if (_pips == null || _health == null) return;
        for (int i = 0; i < _pips.Length; i++)
        {
            if (_pips[i] != null) _pips[i].enabled = i < _health.Current;
        }
    }
}
