using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Word のステージのギミック「スペルチェック」（2026-11-19。docs/app-kits-1-design.md 12章）。数値はすべて【仮】。
/// ・8秒おき（ボス戦は 12秒おき）に、床のどこか（幅 3マス。プレイヤーから 2マス以上離れた所）に赤い波線が出る
///   （予告 1.0秒 → 4秒残る。触るとライフが減る）。同時に 2本まで（ボス戦は 1本）
/// ・波線の横で近接を振ると「修正」：緑の下線に変わって消える。3本直すと、クールタイムを全部回復（右上に 3 corrections）
/// ・絵は Office のスキンの波線（OfficeArt.SpellWave）。判定と修正は自分で持つ（予告 1.0秒・4秒・修正の緑が要るため）
/// </summary>
public class WordRule : StageRule
{
    public const float Interval = 8f;
    public const float BossInterval = 12f;
    public const float Telegraph = 1f;
    public const float Life = 4f;
    public const float Width = 3f;
    public const int FixesForRefill = 3;

    float _timer;
    int _fixed;
    readonly List<WordSpellWave> _waves = new List<WordSpellWave>();

    /// <summary>直した数（3 で 0 に戻る。テスト用）</summary>
    public int Fixed => _fixed;

    public override void Begin(StageRuleContext ctx) => _timer = Interval * 0.5f;

    public override void OnBossAppeared(BossController boss) => _timer = BossInterval * 0.5f;

    public override void Tick(float dt)
    {
        _waves.RemoveAll(w => w == null);
        _timer -= dt;
        if (_timer > 0f) return;
        int max = Ctx.InBossFight ? 1 : 2;
        if (_waves.Count >= max || !Ctx.CanSpawnHazard) { _timer = 0.5f; return; }
        if (Spawn()) _timer = Ctx.InBossFight ? BossInterval : Interval;
        else _timer = 0.5f;
    }

    bool Spawn()
    {
        AttackRunner owner = Ctx.Hazard;
        if (owner == null || Ctx.Player == null) return false;
        Rect view = Ctx.PlayArea;
        Vector2 p = Ctx.Player.position;
        // プレイヤーから 2マス以上離れた所（波線のふちまで）
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float x = Random.Range(view.xMin + Width * 0.5f + 0.3f, view.xMax - Width * 0.5f - 0.3f);
            if (Mathf.Abs(x - p.x) < Width * 0.5f + 2f) continue;
            // 床（プレイヤーの高さから下を探す。足場の下の床）
            float fromY = Mathf.Min(p.y + 0.6f, view.yMin + 1.2f);
            if (!owner.TryFindGround(x, Mathf.Max(fromY, view.yMin + 0.6f), out float ground)) continue;
            if (_waves.Exists(w => w != null && Mathf.Abs(w.transform.position.x - x) < Width + 0.5f)) continue;
            var wave = WordSpellWave.Spawn(owner, new Vector2(x, ground), Width, Telegraph, Life, OnFixed);
            if (wave == null) return false;
            _waves.Add(wave);
            GameAudio.Event("gm_word_wave");
            return true;
        }
        return false;
    }

    /// <summary>【仮】直した数を 1 進める（場面③の出来事「スペルチェックの波」で、波を叩いて直したとき。2026-11-19）</summary>
    public void CountFix() => OnFixed(null);

    void OnFixed(WordSpellWave w)
    {
        _fixed++;
        if (_fixed < FixesForRefill) { GameAudio.Event("gm_word_fix", 1f + 0.12f * (_fixed - 1)); return; }
        _fixed = 0;
        KitPlayer.RefillCooldowns();
        GameAudio.Event("gm_word_refill");
        KitToast.Show($"{FixesForRefill} corrections");
        Transform p = Ctx != null ? Ctx.Player : null;
        if (p != null) FxAnim.Sprite(OfficeArt.Check(OfficeArt.Green), (Vector2)p.position + new Vector2(0f, 1.8f), 60, 0.7f, null, 1.5f).Pop().Rise(0.5f);
    }

    public override void End()
    {
        foreach (var w in _waves) if (w != null) Object.Destroy(w.gameObject);
        _waves.Clear();
        _fixed = 0;
    }

    /// <summary>判定の数値（ライフ -1・ノックバック (6,5)）</summary>
    internal static AttackData HitData => OfficeKitTools.RuntimeData("Attack_Word_SpellCheck",
        "{\"_kind\":1,\"_damage\":1,\"_knockback\":{\"x\":6,\"y\":5},\"_hitStun\":0.25,\"_activeTime\":4,\"_telegraphTime\":1}");
}

/// <summary>スペルチェックの赤い波線1本（WordRule が出す）。叩くと「修正」：緑の下線になって消える</summary>
public class WordSpellWave : MonoBehaviour
{
    const float H = 0.6f;

    float _born, _telegraph, _life;
    float _width;
    SpriteRenderer _sr;
    Hitbox _hitbox;
    KitProp _prop;
    bool _fixed;
    System.Action<WordSpellWave> _onFixed;
    int _wPx, _hPx;

    public bool IsFixed => _fixed;

    public static WordSpellWave Spawn(AttackRunner owner, Vector2 ground, float width, float telegraph, float life, System.Action<WordSpellWave> onFixed)
    {
        GameObject go = owner.CreateSpawnedObject("Word_SpellWave", ground + new Vector2(0f, H * 0.5f));
        var w = go.AddComponent<WordSpellWave>();
        w._born = Time.time;
        w._telegraph = telegraph;
        w._life = life;
        w._width = width;
        w._onFixed = onFixed;
        w._wPx = Mathf.RoundToInt(width * 18f);
        w._hPx = Mathf.RoundToInt(H * 18f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(width, H * 0.7f);
        col.offset = new Vector2(0f, -H * 0.15f);
        w._hitbox = owner.AddHitbox(go, WordRule.HitData, col, false); // 触っている間（無敵の時間で止まる）
        w._sr = AttackRunner.AddVisual(go.transform, OfficeArt.SpellWave(w._wPx, w._hPx, 0, false), new Color(1f, 1f, 1f, 0.5f), new Vector2(width, H), 17, "Wave");
        // 叩ける置物（波線に重ねる。見えない）。近接・能力が当たると修正
        w._prop = KitProp.Spawn(owner, go.transform.position + new Vector3(0f, 0.25f, 0f), null, new Vector2(width, H + 0.6f), p => w.Fix());
        if (w._prop != null && w._prop.Visual != null) w._prop.Visual.enabled = false;
        return w;
    }

    void Update()
    {
        if (_fixed) return;
        float age = Time.time - _born;
        int phase = Mathf.FloorToInt(Time.time / 0.12f);
        if (age < _telegraph)
        {
            // 予告：うすい赤の波線が点滅
            bool on = Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0 || _telegraph - age > 0.4f;
            _sr.sprite = OfficeArt.SpellWave(_wPx, _hPx, phase, false);
            _sr.color = new Color(1f, 1f, 1f, on ? 0.5f : 0.2f);
            return;
        }
        if (!_hitbox.IsActive)
        {
            _hitbox.Activate();
            GameAudio.Event("gm_word_wave_active");
            _sr.color = Color.white;
        }
        _sr.sprite = OfficeArt.SpellWave(_wPx, _hPx, phase, false);
        float left = _telegraph + _life - age;
        if (left < 0.6f) _sr.enabled = Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
        if (left <= 0f) Destroy(gameObject);
    }

    void Fix()
    {
        if (_fixed) return;
        _fixed = true;
        if (_hitbox != null) _hitbox.Deactivate();
        _sr.enabled = true;
        _sr.sprite = OfficeArt.SpellWave(_wPx, _hPx, 0, false, true);
        _sr.color = Color.white;
        FxKit.Ring(transform.position, 2.2f, 0.2f, OfficeArt.Green);
        try { _onFixed?.Invoke(this); }
        catch (System.Exception e) { Debug.LogException(e); }
        Destroy(gameObject, 0.5f);
    }

    void OnDestroy()
    {
        if (_prop != null) Destroy(_prop.gameObject);
    }
}
