using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーの見た目の演出（動きの判定には関わらない）。
/// ・近接：振った瞬間に斬撃のコマを最初から流す。当たった場所に火花
/// ・ダッシュ：残像を残す
/// ・新しいタブ（瞬間移動）：消えた場所と出た場所に、小さなウィンドウが開く（タイトルバーはその能力のアプリの色）
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerVfx : MonoBehaviour
{
    [Tooltip("プレイヤーの絵（残像の元）")]
    [SerializeField] SpriteRenderer _art;
    [Tooltip("斬撃のコマ送り（近接の攻撃判定の子）")]
    [SerializeField] SpriteFrameAnimation _slash;
    [Tooltip("残像を出す間隔（秒）")]
    [SerializeField, Min(0.01f)] float _afterimageInterval = 0.035f;
    [Tooltip("残像が消えるまでの秒数")]
    [SerializeField, Min(0.01f)] float _afterimageLife = 0.18f;
    [SerializeField] Color _afterimageColor = new Color(0.55f, 0.85f, 1f, 0.6f);

    PlayerController _controller;
    PlayerMeleeAttack _melee;
    Hitbox _meleeHitbox;
    AttackRunner _runner;
    PlayerSkills _skills;
    float _nextAfterimage;
    readonly List<(SpriteRenderer sr, float born)> _ghosts = new List<(SpriteRenderer, float)>();

    void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _melee = GetComponent<PlayerMeleeAttack>();
        _runner = GetComponent<AttackRunner>();
        _skills = GetComponent<PlayerSkills>();
        if (_melee != null) _meleeHitbox = GetMeleeHitbox();
    }

    Hitbox GetMeleeHitbox()
    {
        // 近接の Hitbox は PlayerMeleeAttack が持っている。プレイヤーの子にある、陣営がプレイヤーの Hitbox を探す
        foreach (var h in GetComponentsInChildren<Hitbox>(true))
        {
            if (h.GetComponentInParent<PlayerMeleeAttack>() == _melee) return h;
        }
        return null;
    }

    void OnEnable()
    {
        if (_melee != null) _melee.Swung += OnSwung;
        if (_meleeHitbox != null) _meleeHitbox.Hit += OnMeleeHit;
        if (_runner != null) _runner.Warped += OnWarped;
    }

    void OnDisable()
    {
        if (_melee != null) _melee.Swung -= OnSwung;
        if (_meleeHitbox != null) _meleeHitbox.Hit -= OnMeleeHit;
        if (_runner != null) _runner.Warped -= OnWarped;
    }

    void OnDestroy()
    {
        foreach (var g in _ghosts)
        {
            if (g.sr != null) Destroy(g.sr.gameObject);
        }
        _ghosts.Clear();
    }

    void OnSwung()
    {
        if (_slash != null) _slash.Restart();
    }

    void OnMeleeHit(IDamageable target, DamageInfo info) => Vfx.HitSpark(info.HitPoint);

    void OnWarped(AttackData data, Vector2 from, Vector2 to)
    {
        // ウィンドウのタイトルバーは、その能力のアプリの色
        AppTheme theme = ThemeOf(data);
        Vfx.TabOpen(from + new Vector2(0f, 0.5f), theme);
        Vfx.TabOpen(to + new Vector2(0f, 0.5f), theme);
    }

    /// <summary>この攻撃データを使う能力の色（持っている能力から探す）</summary>
    AppTheme ThemeOf(AttackData data)
    {
        if (_skills == null || data == null) return null;
        for (int i = 0; i < _skills.Capacity; i++)
        {
            SkillDefinition s = _skills.GetSkill(i);
            if (s != null && s.Attack == data) return s.Theme;
        }
        return null;
    }

    void Update()
    {
        float now = Time.time;
        if (_art != null && _controller.IsDashing && now >= _nextAfterimage && _art.enabled)
        {
            _nextAfterimage = now + _afterimageInterval;
            SpawnGhost(now);
        }
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            var (sr, born) = _ghosts[i];
            float t = (now - born) / _afterimageLife;
            if (sr == null || t >= 1f)
            {
                if (sr != null) Destroy(sr.gameObject);
                _ghosts.RemoveAt(i);
                continue;
            }
            Color c = _afterimageColor;
            c.a *= 1f - t;
            sr.color = c;
        }
    }

    void SpawnGhost(float now)
    {
        var go = new GameObject("Afterimage");
        go.transform.SetPositionAndRotation(_art.transform.position, _art.transform.rotation);
        go.transform.localScale = _art.transform.lossyScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _art.sprite;
        sr.flipX = _art.flipX;
        sr.sortingOrder = _art.sortingOrder - 1;
        sr.color = _afterimageColor;
        _ghosts.Add((sr, now));
    }
}
