using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// zip圧縮（Finder の能力。2026-11-18。docs/app-boss-skills-design.md 2-3）の状態。FinderZipAttack がプレイヤーに実行時に足す。
/// ・状態：なし → 吸いこみ中（0.15秒）→ 持っている → （発射で）なし
/// ・吸いこめる物：雑魚（Health。陣営が敵・ボスの子でない・ボスの追い出されるウイルスでない）、敵の弾（Projectile。弾の Hitbox の陣営で見分ける）、
///   吸いこめる印（FinderZippable）の付いた物。ボス・ウイルス本体・落下物・柱や床の判定・光の帯・トゲ付きファイルは吸わない（どれも上に当たらない）
/// ・雑魚はダメージで倒す（倒した数・ボスが出るまでのゲージに入る）。弾はその場で消す
/// ・zip はプレイヤーの後ろに浮いて付いてくる（プレイヤーの子。プレイヤーが消えると一緒に消える）。時間切れ・被弾で落とすことはない
/// ・この能力が枠から外れたら（捨てる・入れ替える）、zip ごと自分を消す。ステージが変わると、シーンごと消える
/// 見た目は【仮】：青い選択の四角が正面にパッと出て、中の物の写しが縮みながら zip へ集まる。
/// 2026-11-19（docs/app-feedback-v2-design.md 4-2）：zip は段ごとに変わる（空＝うすい 10ドット、1＝13ドットで書類が1枚のぞく、
/// 2〜3＝16ドットで3枚のぞく、4以上＝18ドットでふるえ、ファスナーが光り、青い点が回る）。数字は出さない。発射も4段（Fire）
/// </summary>
public class FinderZipHolder : MonoBehaviour
{
    public enum ZipState
    {
        /// <summary>持っていない（次に押すと圧縮）</summary>
        None = 0,
        /// <summary>吸いこみ中（押しても何もしない）</summary>
        Compressing = 1,
        /// <summary>持っている（次に押すと発射）</summary>
        Holding = 2,
    }

    // ───────── 見た目【仮】 ─────────
    const float GatherAhead = 0.5f;     // 吸いこんだ物が集まる所：体の前の端から前へ（マス）
    const float GatherHeight = 0.6f;    // 　　〃　　　　　　　　：足元から（マス）
    const float FollowBack = 0.8f;      // 持っている間：体の真ん中から後ろへ（マス）
    const float FollowHeight = 1.35f;   // 　　〃　　　：足元から（マス）
    const float FollowSharpness = 14f;  // 付いてくる速さ
    const float SelectFadeTime = 0.1f;  // 選択の四角が消えるまで
    const float PopTime = 0.12f;        // 大きさが変わったときに、ぽんと膨らむ時間
    const int ZipOrder = 14;            // プレイヤーの絵（15）の後ろ
    const int SelectOrder = 40;

    FinderZipAttack _attack;
    AttackRunner _runner;
    PlayerSkills _skills;
    Collider2D _body;
    float _compressEnd;
    float _selectHideAt = float.NegativeInfinity;
    float _popUntil = float.NegativeInfinity;
    int _shownHeight;
    readonly HashSet<Object> _taken = new HashSet<Object>();
    readonly Collider2D[] _results = new Collider2D[32];
    Transform _zip;
    SpriteRenderer _zipSr;
    Transform _select;
    SpriteRenderer _selectFill;
    SpriteRenderer[] _selectEdges;
    // 2026-11-18：スキルの見た目（docs/app-skill-visuals-design.md 11-3）【仮】
    SpriteRenderer _barBg, _barFill;    // 圧縮の進みのバー（20×4ドット。Mac の進みのバー：両端を 1ドット丸め、地 #D1D1D6、中は青。2026-11-19）
    const int BarW = 20, BarH = 4;

    public ZipState State { get; private set; }
    /// <summary>吸いこんだ数</summary>
    public int Count { get; private set; }
    /// <summary>最後に発射したフレーム（Time.frameCount）。まだなら -1</summary>
    public int LastFireFrame { get; private set; } = -1;

    /// <summary>持ち主（プレイヤー）に付いている部品を返す。無ければ足す</summary>
    public static FinderZipHolder GetOrAdd(AttackRunner owner, FinderZipAttack attack)
    {
        var h = owner.GetComponent<FinderZipHolder>();
        if (h == null) h = owner.gameObject.AddComponent<FinderZipHolder>();
        h._attack = attack;
        h._runner = owner;
        return h;
    }

    void Awake()
    {
        _runner = GetComponent<AttackRunner>();
        _skills = GetComponent<PlayerSkills>();
        foreach (var c in GetComponents<Collider2D>())
        {
            if (!c.isTrigger)
            {
                _body = c;
                break;
            }
        }
    }

    void OnDestroy()
    {
        if (_zip != null) Destroy(_zip.gameObject);
        if (_select != null) Destroy(_select.gameObject);
    }

    // ───────── 圧縮・発射 ─────────

    /// <summary>1回目：吸いこみを始める（範囲に何もなくても、空の zip ができる）</summary>
    public void BeginCompress()
    {
        if (_attack == null || _runner == null) return;
        State = ZipState.Compressing;
        Count = 0;
        _taken.Clear();
        _compressEnd = Time.time + _attack.CompressTime;
        EnsureVisuals();
        _zip.position = GatherPoint();
        _zipBase = _zip.position;
        _zip.gameObject.SetActive(true);
        _shownHeight = 0;
        UpdateZipSprite();
        _select.gameObject.SetActive(true);
        PlaceSelection(1f);
        Absorb();
    }

    /// <summary>
    /// 2回目：zip を正面へ撃つ。2026-11-19（docs/app-feedback-v2-design.md 4-2）吸いこんだ数で4段：
    /// 0＝書類1発、1＝散弾、2〜3＝弾幕（半分ずつ2回）、4以上＝解凍レーザー
    /// </summary>
    public void Fire(AttackData data)
    {
        if (_attack == null || _runner == null || data == null) return;
        LastFireFrame = Time.frameCount;
        int count = Mathf.Min(Count, _attack.CountCap);
        int tier = FinderArt.ZipTier(count);
        int f = _runner.Facing;
        Vector2 origin = _runner.OriginPoint + new Vector2(f * data.SpawnOffset.x, data.SpawnOffset.y);
        switch (tier)
        {
            case 0:
                Dress(Projectile.Spawn(_runner, _attack.SingleData(_runner, data) ?? data, origin, new Vector2(f, 0f)), 0);
                break;
            case 1:
                Fan(data, origin, f, _attack.ScatterCount, _attack.ScatterSpread, 0f, false);
                break;
            case 2:
            {
                int n = Mathf.Max(2, _attack.BarrageCount(count));
                int first = (n + 1) / 2;
                Fan(data, origin, f, first, _attack.BarrageSpread, 0f, true);
                StartCoroutine(SecondVolley(data, f, n - first, _attack.BarrageSpread, _attack.BarrageGap));
                break;
            }
            default:
                FinderZipLaser.Spawn(_runner, _attack.LaserData(_runner, data) ?? data, () => MuzzleFor(f), f, _attack.LaserWidth(count), _attack.LaserTime);
                break;
        }
        // zip がはじける。ファスナーが開いた白い光（11-3。2026-11-18）
        if (_zip != null && _zipSr != null && _zip.gameObject.activeSelf)
        {
            FinderGhost.Pop(_zipSr.sprite, origin, 1f, 1.8f, PopTime, ZipOrder + 30);
            _zip.gameObject.SetActive(false);
        }
        Transform flash = GenericFrameKit.Loose("Gen4_ZipFlash", origin);
        SpriteRenderer fs = GenericFrameKit.Spr(flash, FinderArt.Disc(14), Vector2.zero, Color.white, SelectOrder + 5, 1f, "Flash");
        GenericFrameTweens.Of(flash.gameObject).Scale(flash, Vector3.one, Vector3.one * 2.2f, 0.1f).Alpha(fs, 1f, 0f, 0.1f).Kill(0.11f);
        if (_select != null) _select.gameObject.SetActive(false);
        State = ZipState.None;
        Count = 0;
        _taken.Clear();
    }

    /// <summary>扇に撃つ。offsetHalf：角度を半分ずらす（弾幕の2回目を、1回目のすき間に入れる）。mixed：書類・フォルダ・画像を混ぜる</summary>
    void Fan(AttackData data, Vector2 origin, int f, int n, float spread, float offsetHalf, bool mixed)
    {
        Vector2 baseDir = new Vector2(f, 0f);
        float step = n > 1 ? spread / (n - 1) : 0f;
        for (int i = 0; i < n; i++)
        {
            float angle = n > 1 ? -spread * 0.5f + step * i : 0f;
            angle += offsetHalf * step * 0.5f;
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;
            Dress(Projectile.Spawn(_runner, data, origin, dir), mixed ? i % 3 : 0);
        }
    }

    System.Collections.IEnumerator SecondVolley(AttackData data, int f, int n, float spread, float gap)
    {
        if (n <= 0) yield break;
        yield return new WaitForSeconds(gap);
        if (_runner == null) yield break;
        Vector2 origin = _runner.OriginPoint + new Vector2(f * data.SpawnOffset.x, data.SpawnOffset.y);
        Fan(data, origin, f, n, spread * 0.9f, 1f, true);
    }

    /// <summary>レーザーの根元：撃った向き（f）の体の前の端、体の高さのまん中（向きは撃った瞬間に固定）</summary>
    Vector2 MuzzleFor(int f)
    {
        Bounds b = BodyBounds();
        return new Vector2((f > 0 ? b.max.x : b.min.x) + f * 0.1f, b.center.y);
    }

    /// <summary>散弾の見た目を小さい書類にする（判定・速さは攻撃データのまま）。kind：0＝書類、1＝フォルダ、2＝画像（弾幕）</summary>
    static void Dress(Projectile p, int kind)
    {
        if (p == null) return;
        SpriteRenderer sr = p.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sprite = FinderArt.Shot(kind);
        sr.transform.localScale = Vector3.one;
        sr.color = Color.white;
        // 書類の弾の後ろに、うすい青の線（弾は進む向きに回るので、後ろ＝ローカルの左。11-3。2026-11-18）
        SpriteRenderer trail = AttackRunner.AddVisual(p.transform, PlaceholderSprites.Square, new Color(0.35f, 0.78f, 0.98f, 0.55f), new Vector2(0.55f, FinderArt.Dot), sr.sortingOrder - 1, "Trail");
        trail.transform.localPosition = new Vector3(-0.32f, 0f, 0f);
    }

    // ───────── 毎フレーム ─────────

    void Update()
    {
        if (!StillEquipped())
        {
            Destroy(this); // zip も OnDestroy で消える
            return;
        }
        if (State != ZipState.Compressing) return;
        Absorb();
        if (Time.time >= _compressEnd)
        {
            State = ZipState.Holding;
            _selectHideAt = Time.time;
        }
    }

    void LateUpdate()
    {
        // 選択の四角：吸いこみ中は正面に付いてくる。終わったらすぐ消える
        if (_select != null && _select.gameObject.activeSelf)
        {
            if (State == ZipState.Compressing) PlaceSelection(1f);
            else
            {
                float k = 1f - (Time.time - _selectHideAt) / SelectFadeTime;
                if (k <= 0f) _select.gameObject.SetActive(false);
                else PlaceSelection(k);
            }
        }

        if (_zip == null) return;
        if (State == ZipState.None)
        {
            if (_zip.gameObject.activeSelf) _zip.gameObject.SetActive(false);
            return;
        }
        UpdateZipSprite();
        Vector2 target = State == ZipState.Compressing ? GatherPoint() : FollowPoint();
        _zipBase = Vector2.Lerp(_zipBase, target, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));
        _zip.position = (Vector3)_zipBase + TierShake();
        float pop = Time.time < _popUntil ? 1f + 0.35f * (_popUntil - Time.time) / PopTime : 1f;
        // 持っている間：0.6秒ごとに 1ドットふくらんで戻る（「ぱんぱん」）。数が多いほど大きく（11-3。2026-11-18）
        if (State == ZipState.Holding && _shownHeight > 0)
        {
            float bump = Mathf.Max(0f, 1f - Mathf.Repeat(Time.time, 0.6f) / 0.15f);
            pop += bump * (1f + 0.5f * Mathf.Min(Count, 5)) / _shownHeight;
        }
        _zip.localScale = new Vector3(pop, pop, 1f);
        UpdateTierLook();
    }

    // ───────── 段ごとの見た目（2026-11-19。docs/app-feedback-v2-design.md 4-2）【仮】 ─────────
    // 空＝うすい。4個以上＝小さくふるえ、まわりに青い点が回る（ファスナーの光は絵の中）
    readonly SpriteRenderer[] _orbit = new SpriteRenderer[3];
    Vector2 _zipBase; // ふるえを足す前の位置

    /// <summary>4個以上：0.05秒ごとに 1ドットふるえる</summary>
    Vector3 TierShake()
    {
        if (State != ZipState.Holding || FinderArt.ZipTier(Count) < 3) return Vector3.zero;
        int step = Mathf.FloorToInt(Time.time / 0.05f);
        return new Vector3(((step * 7) % 3 - 1) * FinderArt.Dot, ((step * 5) % 3 - 1) * FinderArt.Dot, 0f);
    }

    void UpdateTierLook()
    {
        if (_zipSr == null) return;
        int tier = FinderArt.ZipTier(Count);
        _zipSr.color = new Color(1f, 1f, 1f, tier == 0 && State == ZipState.Holding ? 0.6f : 1f);
        bool hot = tier >= 3 && State == ZipState.Holding;
        for (int i = 0; i < _orbit.Length; i++)
        {
            if (_orbit[i] == null)
            {
                if (!hot) continue;
                _orbit[i] = RuntimeSprites.Show(_zip, FinderArt.Disc(3), Vector2.zero, ZipOrder + 1, 1f, "Orbit");
                _orbit[i].color = FinderArt.Blue;
            }
            _orbit[i].enabled = hot;
            if (!hot) continue;
            float a = Time.time * 5f + i * Mathf.PI * 2f / _orbit.Length;
            _orbit[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 0.62f, Mathf.Sin(a) * 0.45f, 0f);
        }
    }

    /// <summary>この能力（zip圧縮）が、まだどれかの枠に入っているか</summary>
    bool StillEquipped()
    {
        if (_skills == null) return true;
        for (int i = 0; i < _skills.Capacity; i++)
        {
            SkillDefinition s = _skills.GetSkill(i);
            if (s != null && s.Attack != null && s.Attack.Custom is FinderZipAttack) return true;
        }
        return false;
    }

    // ───────── 吸いこむ ─────────

    void Absorb()
    {
        if (_attack == null || _runner == null) return;
        GetRange(out Vector2 center, out Vector2 size);
        var filter = new ContactFilter2D { useTriggers = true }; // レイヤーは問わない（弾は撃った人のレイヤー）
        int n = Physics2D.OverlapBox(center, size, 0f, filter, _results);
        BossController[] bosses = null;
        for (int i = 0; i < n; i++)
        {
            Collider2D col = _results[i];
            if (col == null || !col.enabled) continue;
            // 雑魚（被弾範囲の持ち主が Health）
            if (col.TryGetComponent(out Hurtbox hurtbox))
            {
                if (hurtbox.Owner is Health h) TryTakeZako(h, hurtbox, ref bosses);
                continue;
            }
            // 敵の弾
            Projectile p = col.GetComponentInParent<Projectile>();
            if (p != null)
            {
                if (IsEnemyProjectile(p)) Take(p.gameObject);
                continue;
            }
            // 吸いこめる印の付いた物（タグの弾など）
            FinderZippable z = col.GetComponentInParent<FinderZippable>();
            if (z != null && z.isActiveAndEnabled) Take(z.gameObject);
        }
    }

    void TryTakeZako(Health h, Hurtbox hurtbox, ref BossController[] bosses)
    {
        if (h == null || !h.isActiveAndEnabled || h.IsDead || h.IsInvincible) return;
        if (h.Team == _runner.Team || _taken.Contains(h)) return;
        if (h.GetComponentInParent<BossController>() != null) return; // ボスの体の一部
        if (bosses == null) bosses = FindObjectsOfType<BossController>();
        foreach (var b in bosses)
        {
            if (b != null && b.EjectedVirus == h) return; // 追い出されたウイルス本体（親から外れているので、ボスの子かでは分からない）
        }
        _taken.Add(h);
        GhostOf(h.gameObject);
        var info = new DamageInfo
        {
            Amount = Mathf.Max(1, h.Current), // その場で倒す
            GaugeGain = 0,
            SourceTeam = _runner.Team,
            Knockback = Vector2.zero,
            HitStun = 0f,
            HitPoint = hurtbox.Collider != null ? (Vector2)hurtbox.Collider.bounds.center : (Vector2)h.transform.position,
            Source = gameObject,
            Skin = SkinId.Finder,
            ErrorKind = "Zip",
        };
        if (h.TryTakeDamage(info))
        {
            Count++;
            // 吸い込みは攻撃の判定（Hitbox）を通らないので、当たったエフェクトを自分で出す（1-2。2026-11-18）
            Vector2 toZip = (_zip != null ? (Vector2)_zip.position : GatherPoint()) - info.HitPoint;
            HitFx.Play(new HitInfo
            {
                Point = info.HitPoint,
                Direction = toZip.sqrMagnitude > 0.0001f ? toZip.normalized : Vector2.up,
                Strength = HitStrength.Light,
                Target = h.gameObject,
                TargetIsPlayer = false,
                Skin = SkinId.Finder,
                ErrorKind = "Zip",
            });
        }
    }

    /// <summary>敵の弾か（自分の散弾は吸わない）。弾の Hitbox の陣営で見る（撃った人が消えていても分かる）</summary>
    bool IsEnemyProjectile(Projectile p)
    {
        if (p == null || !p.isActiveAndEnabled) return false;
        Hitbox hb = p.GetComponent<Hitbox>();
        if (hb != null) return hb.Team != _runner.Team;
        return p.Owner != null && p.Owner.Team != _runner.Team;
    }

    void Take(GameObject go)
    {
        if (go == null || !_taken.Add(go)) return;
        GhostOf(go);
        go.SetActive(false); // このフレームのうちに当たらないように、すぐ止める
        Destroy(go);
        Count++;
    }

    /// <summary>吸いこんだ物の写しを、縮めながら zip へ集める（いちばん大きい絵を写す）</summary>
    void GhostOf(GameObject go)
    {
        SpriteRenderer best = null;
        float bestArea = 0f;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            Vector3 s = sr.bounds.size;
            float area = s.x * s.y;
            if (best == null || area > bestArea)
            {
                best = sr;
                bestArea = area;
            }
        }
        if (best == null) return;
        Vector2 to = _zip != null ? (Vector2)_zip.position : GatherPoint();
        FinderGhost.CopyOf(best, _zip, to, _attack.CompressTime + 0.05f);
    }

    // ───────── 位置 ─────────

    Bounds BodyBounds() => _body != null ? _body.bounds : new Bounds(transform.position + new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1f, 0f));

    int Facing => _runner != null ? _runner.Facing : 1;

    /// <summary>吸いこむ範囲（体の前の端から横、足元から縦）</summary>
    void GetRange(out Vector2 center, out Vector2 size)
    {
        Bounds b = BodyBounds();
        int f = Facing;
        size = _attack != null ? _attack.RangeSize : new Vector2(3f, 2f);
        float front = f > 0 ? b.max.x : b.min.x;
        center = new Vector2(front + f * size.x * 0.5f, b.min.y + size.y * 0.5f);
    }

    Vector2 GatherPoint()
    {
        Bounds b = BodyBounds();
        int f = Facing;
        float front = f > 0 ? b.max.x : b.min.x;
        return new Vector2(front + f * GatherAhead, b.min.y + GatherHeight);
    }

    Vector2 FollowPoint()
    {
        Bounds b = BodyBounds();
        return new Vector2(b.center.x - Facing * FollowBack, b.min.y + FollowHeight + Mathf.Sin(Time.time * 3f) * 0.06f);
    }

    // ───────── 見た目 ─────────

    void EnsureVisuals()
    {
        if (_zip == null)
        {
            var go = new GameObject("FinderZip");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _zipSr = go.AddComponent<SpriteRenderer>();
            _zipSr.sortingOrder = ZipOrder;
            _zip = go.transform;
            go.SetActive(false);
        }
        if (_select == null)
        {
            var go = new GameObject("FinderZipSelect");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _select = go.transform;
            _selectFill = AttackRunner.AddVisual(_select, PlaceholderSprites.Square, FinderArt.Blue, Vector2.one, SelectOrder, "Fill");
            _selectEdges = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                _selectEdges[i] = AttackRunner.AddVisual(_select, PlaceholderSprites.Square, FinderArt.Blue, Vector2.one, SelectOrder + 1, "Edge");
            }
            // 圧縮の進みのバー（四角の上）。Mac の進みのバー（docs/mac-look-design.md 1-6）：両端を 1ドット丸めた地に、同じ形の青が左から伸びる
            Sprite bar = GenericFrameKit.RoundRect(BarW, BarH, 1);
            _barBg = AttackRunner.AddVisual(_select, bar, GenericMac.Light3, new Vector2(BarW, BarH) * FinderArt.Dot, SelectOrder + 2, "BarBg");
            _barFill = AttackRunner.AddVisual(_select, bar, FinderArt.Blue, new Vector2(BarW, BarH) * FinderArt.Dot, SelectOrder + 3, "BarFill");
            go.SetActive(false);
        }
    }

    void UpdateZipSprite()
    {
        if (_zipSr == null) return;
        int h = FinderArt.ZipHeight(Count);
        if (h == _shownHeight) return;
        if (_shownHeight != 0) _popUntil = Time.time + PopTime; // 大きくなったら、ぽんと膨らむ
        _shownHeight = h;
        _zipSr.sprite = FinderArt.ZipTierSprite(FinderArt.ZipTier(Count)); // 2026-11-19：段ごとの絵（書類がのぞく）
    }

    /// <summary>選択の四角を範囲に合わせる（中は青を薄く、ふちは青1ドット）。alpha は消えていく途中の濃さ</summary>
    void PlaceSelection(float alpha)
    {
        if (_select == null) return;
        GetRange(out Vector2 center, out Vector2 size);
        _select.position = center;
        _select.rotation = Quaternion.identity;
        float t = FinderArt.Dot;
        SetBox(_selectFill, Vector2.zero, size, 0.22f * alpha);
        SetBox(_selectEdges[0], new Vector2(0f, size.y * 0.5f - t * 0.5f), new Vector2(size.x, t), alpha);
        SetBox(_selectEdges[1], new Vector2(0f, -size.y * 0.5f + t * 0.5f), new Vector2(size.x, t), alpha);
        SetBox(_selectEdges[2], new Vector2(-size.x * 0.5f + t * 0.5f, 0f), new Vector2(t, size.y), alpha);
        SetBox(_selectEdges[3], new Vector2(size.x * 0.5f - t * 0.5f, 0f), new Vector2(t, size.y), alpha);
        // 進みのバー：吸いこみの間に 0 → 100%
        if (_barBg != null && _barFill != null)
        {
            float k = _attack != null && State == ZipState.Compressing ? 1f - Mathf.Clamp01((_compressEnd - Time.time) / Mathf.Max(0.01f, _attack.CompressTime)) : 1f;
            float y = size.y * 0.5f + (BarH * 0.5f + 3f) * t;
            float full = BarW * t;
            SetBox(_barBg, new Vector2(0f, y), new Vector2(BarW, BarH) * t, alpha);
            _barBg.color = GenericFrameKit.WithAlpha(GenericMac.Light3, alpha);
            SetBox(_barFill, new Vector2(-full * 0.5f + full * k * 0.5f, y), new Vector2(Mathf.Max(0.001f, full * k), BarH * t), alpha);
        }
    }

    static void SetBox(SpriteRenderer sr, Vector2 localPos, Vector2 size, float alpha)
    {
        if (sr == null) return;
        sr.transform.localPosition = localPos;
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
        Color c = FinderArt.Blue;
        c.a = alpha;
        sr.color = c;
    }
}
