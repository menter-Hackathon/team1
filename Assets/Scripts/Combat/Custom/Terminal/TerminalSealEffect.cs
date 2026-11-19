using UnityEngine;

/// <summary>
/// 【仮】Permission Denied の封印（2026-11-18：chmod 000 player。docs/app-boss-skills-design.md 4-2）。
/// ・かかっている間、プレイヤーのジャンプと近接が使えない（PlayerController.LockJump・PlayerMeleeAttack.Lock）。移動と能力は使える
/// ・頭の上に南京錠（閉まる瞬間に白く光って、画面が少しゆれる）と「zsh: permission denied」の文字（赤・黒いふち。残り1秒で点滅）。足元に赤い鎖の輪
/// ・封じられたボタンを押すと、南京錠がゆれる
/// ・時間が来たら解ける（南京錠が開いて消える）。形態変化・正常化で消されたときも、必ず解く（Restore）
/// 2026-11-19 まさぴ〜の感想で足した（docs/app-feedback-v2-design.md 6-2）：何が使えないかを印で見せる
/// ・権限のカード（頭の上。南京錠の下）：暗い板に印が4つ（移動・ジャンプ・近接・能力）。使える物は緑、使えない物（ジャンプ・近接）は
///   灰色の印に赤い斜めの線と小さい赤い鍵、後ろは暗い赤。かかった瞬間、カードが 150% → 100% で出て、使えない2つが 0.1秒ずつ順に赤くなる
/// ・使えないボタンを押すと、その印が左右に 2ドットふるえて白く光る（0.15秒）。残り1秒でカードも点滅。解けたら2つが緑に戻り、カードが上へ消える
/// ・体の印：足元の赤い鎖の輪（ジャンプ）と、武器を持つ手の所の小さい赤い ×（近接）
/// 解けるまでは場に残る技の効果（Lingers）
/// </summary>
public class TerminalSealEffect : CustomEffect
{
    const float CardHeight = 1.5f;    // 【仮】足元からカードの真ん中まで（体の高さ 1.0 の少し上）
    const float LockHeight = 2.2f;    // 【仮】足元から南京錠まで（カードの上）
    const float TextHeight = 2.75f;   // 【仮】足元から文字まで（南京錠の上）
    const float SnapTime = 0.12f;     // 南京錠が大きい所から縮んで閉まるまで・カードが 150% → 100% になるまで
    const float GachaStep = 0.1f;     // 使えない印が順に赤くなる間
    const float WobbleTime = 0.25f;
    const float ShakeTime = 0.15f;    // 使えない印がふるえて白く光る秒数
    const float OpenTime = 0.35f;     // 解けてから、開いた南京錠とカードが消えるまで
    static readonly Vector2 HandOffset = new Vector2(0.38f, 0.45f); // 【仮】武器を持つ手の所（向きで左右が変わる）
    const int RingOrder = 51;
    const int CardOrder = 52;
    const int IconOrder = 53;
    const int LockOrder = 54;

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
    SpriteRenderer _cross;

    // 権限のカード
    Transform _card;
    SpriteRenderer _plate;
    readonly SpriteRenderer[] _icons = new SpriteRenderer[4];
    readonly Sprite[] _on = new Sprite[4];
    readonly Sprite[] _off = new Sprite[4];
    readonly Sprite[] _offFlash = new Sprite[4];
    readonly float[] _shakeUntil = new float[4];
    readonly bool[] _shownOff = new bool[4];

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

    /// <summary>使えない印か（ジャンプと近接）</summary>
    static bool Locked(int i) => i == (int)TerminalArt.Perm.Jump || i == (int)TerminalArt.Perm.Melee;

    void Begin(PlayerController player, float seconds)
    {
        _player = player;
        _melee = player.GetComponent<PlayerMeleeAttack>();
        _start = Time.time;
        _sealTime = seconds;
        // 本当の秒数で封じる（万一この部品が消えても、時間で解ける）
        _player.LockJump(seconds);
        if (_melee != null) _melee.Lock(seconds);
        _player.JumpBlocked += OnJumpBlocked;
        if (_melee != null) _melee.SwingBlocked += OnSwingBlocked;

        _ring = RuntimeSprites.Show(transform, TerminalArt.ChainRing, new Vector2(0f, 0.08f), RingOrder, 1f, "Chain");
        _lock = RuntimeSprites.Show(transform, TerminalArt.LockClosed, new Vector2(0f, LockHeight), LockOrder, 1f, "Lock");
        // 閉まる瞬間の白い光
        _flash = AttackRunner.AddVisual(transform, PlaceholderSprites.Circle, Color.white, Vector2.one * 1.1f, LockOrder + 1, "Flash");
        _flash.transform.localPosition = new Vector3(0f, LockHeight, 0f);

        // 権限のカード（4つの印。最初は全部緑。使えない2つは、あとから順に赤くなる）
        _card = new GameObject("PermCard").transform;
        _card.gameObject.layer = gameObject.layer;
        _card.SetParent(transform, false);
        _card.localPosition = new Vector3(0f, CardHeight, 0f);
        _plate = RuntimeSprites.Show(_card, TerminalArt.PermCard, Vector2.zero, CardOrder, 1f, "Plate");
        for (int i = 0; i < 4; i++)
        {
            var p = (TerminalArt.Perm)i;
            _on[i] = TerminalArt.PermOn(p);
            _off[i] = TerminalArt.PermOff(p);
            _offFlash[i] = TerminalArt.PermOff(p, true);
            _icons[i] = RuntimeSprites.Show(_card, _on[i], TerminalArt.PermSlot(i), IconOrder, 1f, $"Perm_{p}");
        }

        // 体の印：武器を持つ手の所の小さい赤い ×（近接）
        _cross = RuntimeSprites.Show(transform, TerminalArt.RedCross, HandOffset, IconOrder, 1f, "MeleeCross");
        TerminalCameraShake.Shake(0.15f, 0.07f);
    }

    void OnJumpBlocked() => Blocked((int)TerminalArt.Perm.Jump);
    void OnSwingBlocked() => Blocked((int)TerminalArt.Perm.Melee);

    /// <summary>使えないボタンを押した：その印がふるえて白く光る。南京錠もゆれる</summary>
    void Blocked(int icon)
    {
        _wobbleUntil = Time.time + WobbleTime;
        if (icon >= 0 && icon < 4) _shakeUntil[icon] = Time.time + ShakeTime;
    }

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
            // 解けた：南京錠が開いて、上へ少し浮きながら消える。カードの赤い2つが緑に戻り、上へ消える
            float k = _releasedAt >= 0f ? Mathf.Clamp01((now - _releasedAt) / OpenTime) : 1f;
            if (_lock != null)
            {
                _lock.sprite = TerminalArt.LockOpen;
                _lock.color = new Color(1f, 1f, 1f, 1f - k);
                _lock.transform.localPosition = new Vector3(0f, LockHeight + 0.25f * k, 0f);
                _lock.transform.localScale = Vector3.one;
            }
            if (_card != null)
            {
                _card.gameObject.SetActive(true);
                _card.localScale = Vector3.one;
                _card.localPosition = new Vector3(0f, CardHeight + 0.35f * k, 0f);
                SetCardAlpha(1f - k);
                for (int i = 0; i < 4; i++)
                {
                    if (_icons[i] == null) continue;
                    _icons[i].sprite = _on[i];
                    _icons[i].transform.localPosition = TerminalArt.PermSlot(i);
                    _icons[i].transform.localScale = Vector3.one;
                }
            }
            if (_ring != null) _ring.enabled = false;
            if (_flash != null) _flash.enabled = false;
            if (_cross != null) _cross.enabled = false;
            if (k >= 1f) Destroy(gameObject);
            return;
        }

        float age = now - _start;
        bool lastSecond = _sealTime - age <= 1f;
        bool blinkOff = lastSecond && Mathf.FloorToInt(now / 0.1f) % 2 == 1; // 残り1秒で点滅（切れる予告）

        // 閉まる瞬間：大きい所から縮み、白く光る
        float snap = Mathf.Clamp01(age / SnapTime);
        float scale = Mathf.Lerp(1.8f, 1f, snap);
        float x = now < _wobbleUntil ? Mathf.Sin(now * 60f) * TerminalArt.Pixel * 1.5f : 0f;
        if (_lock != null)
        {
            _lock.transform.localScale = new Vector3(scale, scale, 1f);
            _lock.transform.localPosition = new Vector3(x, LockHeight, 0f);
        }
        if (_flash != null)
        {
            float f = Mathf.Clamp01(age / (SnapTime * 2f));
            _flash.enabled = f < 1f;
            _flash.color = new Color(1f, 1f, 1f, 0.9f * (1f - f));
        }
        if (_ring != null) _ring.enabled = !blinkOff;
        UpdateCard(now, age, snap, blinkOff);
        if (_cross != null)
        {
            int facing = _player.FacingDirection >= 0 ? 1 : -1;
            _cross.transform.localPosition = new Vector3(HandOffset.x * facing, HandOffset.y, 0f);
            _cross.enabled = !blinkOff;
        }
    }

    /// <summary>権限のカード：出るときの 150% → 100%、使えない2つが順に赤くなる（ガチャ・ガチャ）、押したときのふるえと白い光、残り1秒の点滅</summary>
    void UpdateCard(float now, float age, float snap, bool blinkOff)
    {
        if (_card == null) return;
        _card.gameObject.SetActive(!blinkOff);
        float s = Mathf.Lerp(1.5f, 1f, snap);
        _card.localScale = new Vector3(s, s, 1f);
        int order = 0;
        for (int i = 0; i < 4; i++)
        {
            SpriteRenderer sr = _icons[i];
            if (sr == null) continue;
            if (!Locked(i))
            {
                sr.sprite = _on[i];
                continue;
            }
            order++;
            bool off = age >= SnapTime + GachaStep * (order - 1); // カードが出たあと、0.1秒ずつ
            if (off && !_shownOff[i])
            {
                _shownOff[i] = true;
                sr.transform.localScale = Vector3.one * 1.35f; // 赤くなった瞬間、少しはねる
                if (order == 1) TerminalCameraShake.Shake(0.06f, 0.03f);
            }
            bool shaking = off && now < _shakeUntil[i];
            sr.sprite = !off ? _on[i] : shaking ? _offFlash[i] : _off[i];
            float dx = shaking ? (Mathf.FloorToInt(now / 0.03f) % 2 == 0 ? 2f : -2f) * TerminalArt.Pixel : 0f;
            sr.transform.localPosition = TerminalArt.PermSlot(i) + new Vector2(dx, 0f);
            float sc = sr.transform.localScale.x;
            if (sc > 1f) sr.transform.localScale = Vector3.one * Mathf.MoveTowards(sc, 1f, Time.deltaTime * 4f);
        }
    }

    void SetCardAlpha(float a)
    {
        if (_plate != null) _plate.color = new Color(1f, 1f, 1f, a);
        foreach (var sr in _icons) if (sr != null) sr.color = new Color(1f, 1f, 1f, a);
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
            TerminalArt.OutlinedTextEn(r, "zsh: permission denied", 8, TerminalArt.Red, TerminalArt.Black); // 2026-11-19：zsh の言い方（2章 9）。カードの上に小さく残す（飾り）
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
            _player.JumpBlocked -= OnJumpBlocked;
        }
        if (_melee != null)
        {
            _melee.Unlock();
            _melee.SwingBlocked -= OnSwingBlocked;
        }
    }
}
