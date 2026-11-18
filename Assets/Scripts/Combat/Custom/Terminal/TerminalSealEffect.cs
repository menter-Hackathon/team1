using UnityEngine;

/// <summary>
/// 【仮】Permission Denied の封印（2026-11-18：chmod 000 player。docs/app-boss-skills-design.md 4-2）。
/// ・かかっている間、プレイヤーのジャンプと近接が使えない（PlayerController.LockJump・PlayerMeleeAttack.Lock）。移動と能力は使える
/// ・頭の上に南京錠（閉まる瞬間に白く光って、画面が少しゆれる）と「Permission Denied」の文字（赤・黒いふち。残り1秒で点滅）。足元に赤い鎖の輪
/// ・封じられたボタンを押すと、南京錠がゆれる
/// ・時間が来たら解ける（南京錠が開いて消える）。形態変化・正常化で消されたときも、必ず解く（Restore）
/// 解けるまでは場に残る技の効果（Lingers）
/// </summary>
public class TerminalSealEffect : CustomEffect
{
    const float LockHeight = 1.35f;   // 【仮】足元から南京錠まで（体の高さ 1.0 の少し上）
    const float TextHeight = 1.95f;   // 【仮】足元から文字まで
    const float SnapTime = 0.12f;     // 南京錠が大きい所から縮んで閉まるまで
    const float WobbleTime = 0.25f;
    const float OpenTime = 0.25f;     // 解けてから、開いた南京錠が消えるまで
    const int LockOrder = 52;
    const int RingOrder = 51;

    PlayerController _player;
    PlayerMeleeAttack _melee;
    float _start;
    float _sealTime;
    float _releasedAt = -1f;
    float _wobbleUntil;
    bool _released;
    SpriteRenderer _lock;
    SpriteRenderer _ring;
    SpriteRenderer _flash;

    public override bool Lingers => !_released;
    /// <summary>封印が解けたか</summary>
    public bool Released => _released;

    /// <summary>当たった被弾範囲の持ち主（プレイヤー）に、seconds 秒の封印をかける。プレイヤーでなければ null</summary>
    public static TerminalSealEffect Spawn(AttackRunner owner, Hurtbox hit, float seconds) =>
        hit != null ? SpawnOn(owner, hit.GetComponentInParent<PlayerController>(), seconds) : null;

    /// <summary>プレイヤーを直接指定して封印する（2026-11-18：アクセス禁止エリアの判定から）</summary>
    public static TerminalSealEffect SpawnOn(AttackRunner owner, PlayerController player, float seconds)
    {
        if (owner == null || player == null) return null;
        GameObject go = owner.CreateSpawnedObject("TerminalSeal", player.transform.position);
        var seal = go.AddComponent<TerminalSealEffect>();
        seal.Begin(player, seconds);
        return seal;
    }

    void Begin(PlayerController player, float seconds)
    {
        _player = player;
        _melee = player.GetComponent<PlayerMeleeAttack>();
        _start = Time.time;
        _sealTime = seconds;
        // 本当の秒数で封じる（万一この部品が消えても、時間で解ける）
        _player.LockJump(seconds);
        if (_melee != null) _melee.Lock(seconds);
        _player.JumpBlocked += Wobble;
        if (_melee != null) _melee.SwingBlocked += Wobble;

        _ring = RuntimeSprites.Show(transform, TerminalArt.ChainRing, new Vector2(0f, 0.08f), RingOrder, 1f, "Chain");
        _lock = RuntimeSprites.Show(transform, TerminalArt.LockClosed, new Vector2(0f, LockHeight), LockOrder, 1f, "Lock");
        // 閉まる瞬間の白い光
        _flash = AttackRunner.AddVisual(transform, PlaceholderSprites.Circle, Color.white, Vector2.one * 1.1f, LockOrder + 1, "Flash");
        _flash.transform.localPosition = new Vector3(0f, LockHeight, 0f);
        TerminalCameraShake.Shake(0.15f, 0.07f);
    }

    void Wobble() => _wobbleUntil = Time.time + WobbleTime;

    void Update()
    {
        if (_player == null)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = _player.transform.position;
        float now = Time.time;

        if (!_released && now - _start >= _sealTime)
        {
            RestoreOnce();
            _releasedAt = now;
        }
        if (_released)
        {
            // 解けた：南京錠が開いて、上へ少し浮きながら消える
            float k = _releasedAt >= 0f ? Mathf.Clamp01((now - _releasedAt) / OpenTime) : 1f;
            if (_lock != null)
            {
                _lock.sprite = TerminalArt.LockOpen;
                _lock.color = new Color(1f, 1f, 1f, 1f - k);
                _lock.transform.localPosition = new Vector3(0f, LockHeight + 0.25f * k, 0f);
                _lock.transform.localScale = Vector3.one;
            }
            if (_ring != null) _ring.enabled = false;
            if (_flash != null) _flash.enabled = false;
            if (k >= 1f) Destroy(gameObject);
            return;
        }

        // 閉まる瞬間：大きい所から縮み、白く光る
        float snap = Mathf.Clamp01((now - _start) / SnapTime);
        float scale = Mathf.Lerp(1.8f, 1f, snap);
        float x = now < _wobbleUntil ? Mathf.Sin(now * 60f) * TerminalArt.Pixel * 1.5f : 0f;
        if (_lock != null)
        {
            _lock.transform.localScale = new Vector3(scale, scale, 1f);
            _lock.transform.localPosition = new Vector3(x, LockHeight, 0f);
        }
        if (_flash != null)
        {
            float f = Mathf.Clamp01((now - _start) / (SnapTime * 2f));
            _flash.enabled = f < 1f;
            _flash.color = new Color(1f, 1f, 1f, 0.9f * (1f - f));
        }
        if (_ring != null)
        {
            bool lastSecond = _sealTime - (now - _start) <= 1f;
            _ring.enabled = !lastSecond || Mathf.FloorToInt(now / 0.1f) % 2 == 0;
        }
    }

    void OnGUI()
    {
        if (_released || _player == null || TerminalArt.GuiHidden()) return;
        float now = Time.time;
        bool lastSecond = _sealTime - (now - _start) <= 1f;
        if (lastSecond && Mathf.FloorToInt(now / 0.1f) % 2 == 1) return; // 残り1秒で点滅（切れる予告）

        GUI.depth = 5;
        PixelGui.Begin(false);
        Vector2? p = ScreenUi.WorldToGui(_player.transform.position + Vector3.up * TextHeight);
        if (p is Vector2 gp)
        {
            var r = new Rect(Mathf.Round(gp.x - 100f), Mathf.Round(gp.y - 4f), 200f, 8f);
            TerminalArt.OutlinedTextEn(r, "Permission Denied", 8, TerminalArt.Red, TerminalArt.Black);
        }
        PixelGui.End();
    }

    /// <summary>封印を解く（1回だけ。時間切れ・形態変化・正常化・シーンを閉じるとき）</summary>
    protected override void Restore()
    {
        _released = true;
        if (_player != null)
        {
            _player.UnlockJump();
            _player.JumpBlocked -= Wobble;
        }
        if (_melee != null)
        {
            _melee.Unlock();
            _melee.SwingBlocked -= Wobble;
        }
    }
}
