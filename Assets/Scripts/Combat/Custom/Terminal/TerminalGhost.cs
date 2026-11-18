using UnityEngine;

/// <summary>
/// 【仮】fork() &amp; の分身「ゴーストプロセス」1体（2026-11-18。docs/app-boss-skills-design.md 4-3）。TerminalForkEffect が子として作る。
/// ・ボスのウィンドウを小さく（6割）、緑1色にして5割すかした絵。0.2秒ごとに横に1ドットずれる（ちらつき）。後ろに、もっとうすい残像を2枚
/// ・体力（Health、HP1）と被弾範囲（Hurtbox）を持つ。近接・能力を1回当てれば消える。触ってもダメージはない（攻撃判定を持たない）
/// ・撃つ前は白く光る（Flash）。撃つのは TerminalForkEffect（ボスの AttackRunner で弾を出す）
/// 雑魚の数（EnemyWave）には入れない
/// </summary>
public class TerminalGhost : MonoBehaviour
{
    const int BodyOrder = 22;
    const int TrailOrder = 21;
    const float AppearTime = 0.12f;

    SpriteRenderer _body;
    SpriteRenderer _trail1;
    SpriteRenderer _trail2;
    Health _health;
    float _born;
    float _flashUntil;

    public Health Health => _health;
    public bool IsAlive => this != null && _health != null && !_health.IsDead;
    /// <summary>判定の大きさ（マス）</summary>
    public static Vector2 Size => TerminalArt.GhostWindow.bounds.size;

    /// <summary>白く光らせる（until の時刻まで）</summary>
    public void Flash(float until) => _flashUntil = Mathf.Max(_flashUntil, until);

    /// <summary>分身を1体作る。layer は被弾範囲のレイヤー（プレイヤーの攻撃が当たる Enemy）</summary>
    public static TerminalGhost Create(Transform parent, Vector2 position, int layer)
    {
        Sprite sprite = TerminalArt.GhostWindow;
        Vector2 size = sprite.bounds.size;

        // 体力の最大値を入れてから動かすので、いったん止めて部品を足す
        var go = new GameObject("TerminalGhost");
        go.SetActive(false);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;
        var health = go.AddComponent<Health>();
        // 【仮】HP1（Health には最大値を変える入口が無いので、保存される欄を直接書き換える）
        JsonUtility.FromJsonOverwrite("{\"_max\":1,\"_oneDamagePerHit\":false,\"_invincibleTimeAfterHit\":0}", health);
        health.ResetHealth(); // Awake で入った HP（3）を、書き換えた最大値（1）に入れ直す
        go.AddComponent<Hurtbox>();

        var ghost = go.AddComponent<TerminalGhost>();
        ghost._health = health;
        ghost._trail2 = RuntimeSprites.Show(go.transform, sprite, Vector2.zero, TrailOrder, 1f, "Trail2");
        ghost._trail1 = RuntimeSprites.Show(go.transform, sprite, Vector2.zero, TrailOrder, 1f, "Trail1");
        ghost._body = RuntimeSprites.Show(go.transform, sprite, Vector2.zero, BodyOrder, 1f, "Body");
        ghost.SetAlpha(0f);
        go.SetActive(true);
        return ghost;
    }

    void OnEnable() => _born = Time.time;

    void Update()
    {
        float now = Time.time;
        float appear = Mathf.Clamp01((now - _born) / AppearTime);
        float jitter = (Mathf.FloorToInt(now / 0.2f) % 2 == 0 ? 1f : -1f) * TerminalArt.Pixel;
        bool flash = now < _flashUntil;

        if (_body != null)
        {
            _body.sprite = flash ? TerminalArt.GhostWindowFlash : TerminalArt.GhostWindow;
            _body.color = new Color(1f, 1f, 1f, (flash ? 0.95f : 0.5f) * appear);
            _body.transform.localPosition = new Vector3(jitter, 0f, 0f);
        }
        if (_trail1 != null)
        {
            _trail1.color = new Color(1f, 1f, 1f, 0.22f * appear);
            _trail1.transform.localPosition = new Vector3(jitter - TerminalArt.Pixel * 3f, 0f, 0f);
        }
        if (_trail2 != null)
        {
            _trail2.color = new Color(1f, 1f, 1f, 0.1f * appear);
            _trail2.transform.localPosition = new Vector3(jitter - TerminalArt.Pixel * 6f, 0f, 0f);
        }
    }

    void SetAlpha(float a)
    {
        if (_body != null) _body.color = new Color(1f, 1f, 1f, a);
        if (_trail1 != null) _trail1.color = new Color(1f, 1f, 1f, a);
        if (_trail2 != null) _trail2.color = new Color(1f, 1f, 1f, a);
    }
}
