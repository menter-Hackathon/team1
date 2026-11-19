using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スマートフォルダマグネット（2026-11-18。docs/app-boss-skills-design.md 2-2）の、場に出る物。FinderSmartFolderAttack が出す。
/// 出したあとは自分で進む。見た目と値は【仮】。流れは FinderSmartFolderAttack の説明のとおり。
/// ・タグの弾・トゲ付きファイル・スマートフォルダ・プレイヤーの頭の上のタグは、どれもこの GameObject の子
///   （形態変化・正常化で消されると、全部いっしょに消える。頭の上のタグは子のまま、プレイヤーの位置に付いていく）
/// ・プレイヤーのタグ（どの色が付いているか）は、この技の中だけの状態。技が終わると消える。プレイヤーそのものは変えない
/// ・トゲの出る場所は、ここで選ぶ：プレイヤーから3マス以上・たがいに3マス以上離れた空中（高さ 2.5〜6）。
///   ボスの体とフォルダの所・地形には重ねない（条件に合う所が無ければ、いちばん条件に近い所）
/// ・ボスの攻撃が止められたら（形態変化など）、その場で消える
/// </summary>
public class FinderSmartFolderEffect : CustomEffect
{
    // ───────── 見た目【仮】 ─────────
    const int FrameOrder = 18, LineOrder = 19, GlowOrder = 22, FolderOrder = 23, WindupTagOrder = 24, HeadTagOrder = 30;
    const int WindupTagDots = 12;      // 構えのときにボスの上に浮かぶタグの丸
    const int HeadTagDots = 6;         // プレイヤーの頭の上のタグ
    const float HeadTagSnapTime = 0.15f;
    const float HeadTagSnapScale = 3f; // 付いた瞬間は大きい所から縮む
    const float FolderPopTime = 0.15f; // フォルダが開く（下から伸びる）
    const float GlowGrowTime = 0.25f;  // タグの色の光が中から出る
    const float ShakeAmount = 1.5f * FinderArt.Dot;
    static readonly Color FrameColor = new Color(1f, 1f, 1f, 0.55f);

    AttackRunner _owner;
    AttackData _data;
    FinderSmartFolderAttack _attack;
    int _colors = 1;
    int _tag = -1;
    float _tagSetAt;
    Transform _headTag;
    SpriteRenderer _headTagSr;
    readonly List<Transform> _windupTags = new List<Transform>();
    readonly List<Vector2> _windupBase = new List<Vector2>();
    readonly List<Vector2>[] _spots = { new List<Vector2>(), new List<Vector2>() };
    readonly List<GameObject>[] _frames = { new List<GameObject>(), new List<GameObject>() };
    readonly List<FinderSpikeFile>[] _spikes = { new List<FinderSpikeFile>(), new List<FinderSpikeFile>() };
    readonly List<FinderLine>[] _lines = { new List<FinderLine>(), new List<FinderLine>() };
    readonly Collider2D[] _terrainBuffer = new Collider2D[2];
    Transform _folder;
    SpriteRenderer _glow;
    Vector3 _glowBaseScale = Vector3.one;
    int _glowTag = -1;
    float _glowStart;
    float _folderOpenedAt;
    float _closeStart = -1f;
    int _activating;
    // 2026-11-18：スキルの見た目（docs/app-skill-visuals-design.md 11-2）【仮】
    Transform _rays;                    // フォルダの口から出る、タグの色の光の線 3本（ゆっくり回る）
    readonly List<SpriteRenderer> _raySrs = new List<SpriteRenderer>();

    /// <summary>プレイヤーに付いているタグ（-1＝なし、0＝赤、1＝青）。テスト用</summary>
    public int PlayerTag => _tag;

    /// <summary>1回出す</summary>
    public static FinderSmartFolderEffect Spawn(AttackRunner owner, AttackData data, FinderSmartFolderAttack attack)
    {
        if (owner == null || data == null || attack == null) return null;
        GameObject go = owner.CreateSpawnedObject($"Finder_SmartFolder_{data.name}", owner.OriginPoint);
        var e = go.AddComponent<FinderSmartFolderEffect>();
        e._owner = owner;
        e._data = data;
        e._attack = attack;
        e._colors = attack.ColorCount;
        e.StartCoroutine(e.Run());
        return e;
    }

    /// <summary>タグの弾がプレイヤーに当たった（あとから当たった色に上書き。1色だけ持つ）</summary>
    public void SetTag(int tag)
    {
        if (this == null || _closeStart >= 0f) return; // 閉じ始めたら、もう付けない（タグは消えたまま）
        _tag = tag;
        _tagSetAt = Time.time;
        if (_headTag == null)
        {
            _headTag = FinderArt.Child(transform, "HeadTag", HeadTagPosition());
            _headTagSr = AttackRunner.AddVisual(_headTag, FinderArt.TagBall(HeadTagDots, tag), Color.white,
                Vector2.one * (HeadTagDots * FinderArt.Dot), HeadTagOrder, "Tag");
        }
        _headTagSr.sprite = FinderArt.TagBall(HeadTagDots, tag);
        _headTag.position = HeadTagPosition();
        // タグが付いた瞬間：頭の上のタグの丸から、同じ色の小さい輪が広がる（0.15秒。11-2）
        Transform ring = FinderArt.Child(transform, "TagRing", HeadTagPosition());
        SpriteRenderer rs = AttackRunner.AddVisual(ring, FinderArt.Ring(12, 1), FinderArt.TagColor(tag), Vector2.one * (12 * FinderArt.Dot), HeadTagOrder, "Ring");
        GenericFrameTweens.Of(ring.gameObject).Scale(ring, Vector3.one, Vector3.one * 2.6f, 0.15f).Alpha(rs, 1f, 0f, 0.15f).Kill(0.16f);
    }

    // ───────── 進める ─────────

    IEnumerator Run()
    {
        Bounds boss = BossBounds();
        Vector2 target = _owner.TargetPoint ?? _owner.OriginPoint;

        // 1. 構え：ボスの上にタグの丸、トゲの出る場所に薄い枠
        for (int c = 0; c < _colors; c++)
        {
            float x = boss.center.x + (_colors == 1 ? 0f : (c == 0 ? -0.45f : 0.45f));
            var p = new Vector2(x, boss.max.y + 0.6f);
            Transform t = FinderArt.Child(transform, $"WindupTag_{c}", p);
            AttackRunner.AddVisual(t, FinderArt.TagBall(WindupTagDots, c), Color.white, Vector2.one * (WindupTagDots * FinderArt.Dot), WindupTagOrder, "Tag");
            _windupTags.Add(t);
            _windupBase.Add(p);
        }
        Sprite frameSprite = GameArt.Instance != null && GameArt.Instance.TelegraphFrame != null ? GameArt.Instance.TelegraphFrame : PlaceholderSprites.Frame;
        List<Vector2> spots = PickSpots(_colors * _attack.SpikesPerColor, target, boss);
        for (int i = 0; i < spots.Count; i++)
        {
            int c = Mathf.Min(_colors - 1, i / Mathf.Max(1, _attack.SpikesPerColor));
            _spots[c].Add(spots[i]);
            Transform f = FinderArt.Child(transform, $"Frame_{c}", spots[i]);
            AttackRunner.AddVisual(f, frameSprite, FrameColor, _attack.SpikeSize, FrameOrder, "Frame");
            _frames[c].Add(f.gameObject);
        }
        float end = Time.time + _attack.Windup;
        while (Time.time < end)
        {
            if (!Alive()) yield break;
            yield return null;
        }

        // 2. タグ付け：色ごとに、タグの弾を撃ち、トゲ付きファイルを出す
        for (int c = 0; c < _colors; c++)
        {
            if (c > 0)
            {
                end = Time.time + _attack.VolleyGap;
                while (Time.time < end)
                {
                    if (!Alive()) yield break;
                    yield return null;
                }
            }
            FireVolley(c);
            SpawnSpikes(c);
        }
        end = Time.time + _attack.TagPhaseTime;
        while (Time.time < end)
        {
            if (!Alive()) yield break;
            yield return null;
        }

        // 3〜4. スマートフォルダが開く。色ごとに、狙って → 飛ばす（赤 → 少しあとに青。重なって進む）
        foreach (var t in _windupTags)
        {
            if (t != null) Destroy(t.gameObject);
        }
        _windupTags.Clear();
        _windupBase.Clear();
        OpenFolder(boss);
        for (int c = 0; c < _colors; c++)
        {
            if (c > 0)
            {
                end = Time.time + _attack.ColorGap;
                while (Time.time < end)
                {
                    if (!Alive()) yield break;
                    yield return null;
                }
            }
            StartCoroutine(Activate(c));
        }
        yield return null;
        while (_activating > 0 || AnyFlying())
        {
            if (!Alive()) yield break;
            yield return null;
        }

        // 5. 終わり：フォルダが閉じ、タグとトゲが消える
        _closeStart = Time.time;
        if (_headTag != null) Destroy(_headTag.gameObject);
        for (int c = 0; c < _spikes.Length; c++)
        {
            foreach (var s in _spikes[c])
            {
                if (s == null) continue;
                // 残ったトゲ付きファイルは、紙くずの丸になって下へ落ちる（11-2。2026-11-18）
                GenericFrameShard.Spawn(s.transform.position, FinderSkinArt.PaperBall, Color.white, new Vector2(Random.Range(-1f, 1f), 1.5f), 22f, 0.6f, 22, Random.Range(-360f, 360f));
                Destroy(s.gameObject);
            }
            foreach (var line in _lines[c]) line.Visible = false;
        }
        DropPaper();
        end = Time.time + _attack.CloseTime;
        while (Time.time < end) yield return null;
        Destroy(gameObject);
    }

    /// <summary>1色分：フォルダからその色の光が出て、タグが付いていればトゲが狙い、止まって、飛ぶ</summary>
    IEnumerator Activate(int c)
    {
        _activating++;
        _glowTag = c;
        _glowStart = Time.time;
        float start = Time.time;
        float lockAt = start + _attack.OpenTime - _attack.LockBeforeFly;
        float flyAt = start + _attack.OpenTime;
        bool locked = false, fly = false;
        Vector2 point = _owner != null && _owner.TargetPoint.HasValue ? _owner.TargetPoint.Value : (Vector2)transform.position;

        while (Time.time < flyAt)
        {
            float now = Time.time;
            if (!locked && now >= lockAt)
            {
                // 狙いの固定：この瞬間に同じ色のタグが付いていれば飛ぶ
                locked = true;
                fly = _tag == c;
                foreach (var line in _lines[c]) line.SetStyle(FinderArt.TagColor(c), true, 2);
            }
            bool aiming = locked ? fly : _tag == c;
            if (!locked && _owner != null && _owner.TargetPoint is Vector2 p) point = p;
            for (int i = 0; i < _spikes[c].Count; i++)
            {
                FinderSpikeFile s = _spikes[c][i];
                FinderLine line = i < _lines[c].Count ? _lines[c][i] : null;
                if (s == null)
                {
                    if (line != null) line.Visible = false;
                    continue;
                }
                if (aiming)
                {
                    s.Shake(ShakeAmount);
                    if (line != null)
                    {
                        line.Visible = true;
                        line.Set(s.transform.position, point);
                    }
                }
                else
                {
                    s.Wobble(now - start);
                    if (line != null) line.Visible = false;
                }
            }
            yield return null;
        }
        if (!locked) fly = _tag == c;

        foreach (var line in _lines[c]) line.Visible = false;
        foreach (var s in _spikes[c])
        {
            if (s == null) continue;
            if (fly) s.Launch(point, _attack.FlySpeed, _attack.FlyTime);
            else s.Still();
        }
        if (fly && _owner != null) _owner.NotifyReleased(_data);
        _activating--;
    }

    /// <summary>撃った人がこの技を続けているか。やめていたら（形態変化・正常化・止められた）、消えて false</summary>
    bool Alive()
    {
        if (_owner == null || !_owner.isActiveAndEnabled || _owner.Current != _data)
        {
            Destroy(gameObject);
            return false;
        }
        return true;
    }

    bool AnyFlying()
    {
        for (int c = 0; c < _spikes.Length; c++)
        {
            foreach (var s in _spikes[c])
            {
                if (s != null && s.IsFlying) return true;
            }
        }
        return false;
    }

    void FireVolley(int c)
    {
        Vector2 origin = _owner.OriginPoint;
        Vector2 baseDir = new Vector2(_owner.Facing, 0f);
        if (_owner.TargetPoint is Vector2 p && (p - origin).sqrMagnitude > 0.0001f) baseDir = (p - origin).normalized;
        int n = _attack.BulletsPerVolley;
        float spread = _attack.BulletSpread;
        for (int i = 0; i < n; i++)
        {
            float angle = n > 1 ? -spread * 0.5f + spread * i / (n - 1) : 0f;
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;
            FinderTagBullet.Spawn(this, _owner, c, origin, dir, _attack.BulletSpeed, _attack.BulletDiameter, _attack.BulletLifetime);
        }
        _owner.NotifyReleased(_data);
    }

    void SpawnSpikes(int c)
    {
        foreach (var f in _frames[c])
        {
            if (f != null) Destroy(f);
        }
        _frames[c].Clear();
        foreach (Vector2 spot in _spots[c])
        {
            _spikes[c].Add(FinderSpikeFile.Spawn(transform, _owner, _data, c, spot, _attack.SpikeSize));
            FinderLine line = FinderLine.Create(transform, FinderArt.TagColor(c), true, 1, LineOrder, $"PullLine_{c}");
            line.Visible = false;
            _lines[c].Add(line);
        }
    }

    void OpenFolder(Bounds boss)
    {
        Rect arena = BattleArena.GetBounds();
        Vector2 size = _attack.FolderSize;
        float bottom = Mathf.Min(boss.max.y + 0.25f, arena.yMax - size.y - 0.2f);
        _folder = FinderArt.Child(transform, "SmartFolder", new Vector2(boss.center.x, bottom));
        SpriteRenderer art = AttackRunner.AddVisual(_folder, FinderArt.SmartFolder, Color.white, size, FolderOrder, "Folder");
        art.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        _glow = AttackRunner.AddVisual(_folder, FinderArt.Disc(24), new Color(1f, 1f, 1f, 0f), Vector2.one * (size.x * 1.3f), GlowOrder, "Glow");
        _glow.transform.localPosition = new Vector3(0f, size.y * 0.6f, 0f);
        _glowBaseScale = _glow.transform.localScale;
        _glow.enabled = false;
        _folderOpenedAt = Time.time;
        _folder.localScale = new Vector3(1f, 0.05f, 1f);

        // フォルダの口から、タグの色の光の線が 3本（11-2。2026-11-18）
        _rays = FinderArt.Child(transform, "Rays", (Vector2)_folder.position + new Vector2(0f, size.y * 0.6f));
        for (int i = 0; i < 3; i++)
        {
            Transform arm = FinderArt.Child(_rays, "Ray", _rays.position);
            arm.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
            SpriteRenderer sr = AttackRunner.AddVisual(arm, PlaceholderSprites.Square, Color.white, new Vector2(1.1f, 2f * FinderArt.Dot), GlowOrder, "Beam");
            sr.transform.localPosition = new Vector3(0.75f, 0f, 0f);
            sr.enabled = false;
            _raySrs.Add(sr);
        }
    }

    /// <summary>フォルダが閉じるとき、書類が1枚ひらひら落ちる（11-2。2026-11-18）</summary>
    void DropPaper()
    {
        if (_folder == null) return;
        Vector2 from = (Vector2)_folder.position + new Vector2(0f, _attack.FolderSize.y * 0.5f);
        Transform paper = GenericFrameKit.Loose("Gen4_FinderPaper", from);
        SpriteRenderer sr = GenericFrameKit.Spr(paper, FinderSkinArt.Doc6, Vector2.zero, Color.white, FolderOrder + 1, 1.5f, "Doc");
        float phase = Random.Range(0f, 6f);
        GenericFrameTweens.Of(paper.gameObject).Each(0.8f, k =>
        {
            if (paper == null) return;
            paper.position = from + new Vector2(Mathf.Sin(k * 9f + phase) * 0.35f, -k * 2.4f);
            paper.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * 9f + phase + 1f) * 30f);
            GenericFrameKit.SetAlpha(sr, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
        }).Kill(0.8f);
    }

    // ───────── 見た目の動き ─────────

    void Update()
    {
        float now = Time.time;
        // 構えのタグの丸：ふわふわ浮く
        for (int i = 0; i < _windupTags.Count && i < _windupBase.Count; i++)
        {
            if (_windupTags[i] != null) _windupTags[i].position = _windupBase[i] + new Vector2(0f, Mathf.Sin(now * 4f + i) * 0.08f);
        }
        // 頭の上のタグ：プレイヤーに付いていく。付いた瞬間は大きい所から縮む
        if (_headTag != null)
        {
            _headTag.position = HeadTagPosition();
            float k = Mathf.Clamp01((now - _tagSetAt) / HeadTagSnapTime);
            float s = Mathf.Lerp(HeadTagSnapScale, 1f, 1f - (1f - k) * (1f - k));
            _headTag.localScale = new Vector3(s, s, 1f);
        }
        // フォルダ：下から伸びて開く・縮んで閉じる
        if (_folder != null)
        {
            float k = _closeStart >= 0f
                ? 1f - Mathf.Clamp01((now - _closeStart) / Mathf.Max(0.01f, _attack.CloseTime))
                : Mathf.Clamp01((now - _folderOpenedAt) / FolderPopTime);
            _folder.localScale = new Vector3(1f, Mathf.Max(0.05f, k), 1f);
        }
        // フォルダの口の光の線：色が動いている間、ゆっくり回る
        if (_rays != null)
        {
            bool on = _glowTag >= 0 && _closeStart < 0f;
            _rays.localRotation = Quaternion.Euler(0f, 0f, now * 40f);
            Color rc = _glowTag >= 0 ? FinderArt.TagColor(_glowTag) : Color.white;
            rc.a = 0.45f + 0.15f * Mathf.Sin(now * 10f);
            foreach (var sr in _raySrs)
            {
                sr.enabled = on;
                sr.color = rc;
            }
        }
        // タグの色の光：色が動き出すと中から広がり、ゆっくり脈打つ
        if (_glow != null)
        {
            if (_glowTag < 0 || _closeStart >= 0f) _glow.enabled = false;
            else
            {
                _glow.enabled = true;
                float t = now - _glowStart;
                float grow = Mathf.Clamp01(t / GlowGrowTime);
                float pulse = 1f + 0.08f * Mathf.Sin(t * 14f);
                _glow.transform.localScale = _glowBaseScale * (Mathf.Lerp(0.3f, 1f, grow) * pulse);
                Color c = FinderArt.TagColor(_glowTag);
                c.a = Mathf.Lerp(0.75f, 0.4f, grow);
                _glow.color = c;
            }
        }
    }

    // ───────── 位置 ─────────

    Vector2 HeadTagPosition()
    {
        if (FinderArt.TryGetTargetBounds(_owner, out Bounds b)) return new Vector2(b.center.x, b.max.y + 0.3f);
        return transform.position;
    }

    Bounds BossBounds()
    {
        var w = _owner != null ? _owner.GetComponent<IWarpable>() : null;
        if (w != null) return w.BodyBounds;
        Vector3 c = _owner != null ? _owner.transform.position : transform.position;
        return new Bounds(c, new Vector3(2f, 2f, 0f));
    }

    /// <summary>トゲの出る場所を選ぶ（ポップアップ出現の「離れた空中のランダムな位置」と同じ考え方）</summary>
    List<Vector2> PickSpots(int count, Vector2 target, Bounds boss)
    {
        var result = new List<Vector2>();
        Rect arena = BattleArena.GetBounds();
        Vector2 size = _attack.SpikeSize;
        Vector2 half = size * 0.5f;
        Vector2 heights = _attack.SpikeHeightRange;
        // よける所：ボスの体と、その上に開くフォルダ
        float avoidMinX = boss.min.x - 0.6f, avoidMaxX = boss.max.x + 0.6f;
        float avoidMinY = boss.min.y - 0.6f, avoidMaxY = boss.max.y + _attack.FolderSize.y + 0.9f;

        for (int i = 0; i < count; i++)
        {
            Vector2 best = default;
            float bestScore = float.NegativeInfinity;
            bool found = false;
            for (int attempt = 0; attempt < 150; attempt++)
            {
                float x = UnityEngine.Random.Range(arena.xMin + half.x + 0.3f, arena.xMax - half.x - 0.3f);
                float y = arena.yMin + UnityEngine.Random.Range(heights.x, heights.y);
                var c = new Vector2(x, y);
                if (c.x + half.x > avoidMinX && c.x - half.x < avoidMaxX && c.y + half.y > avoidMinY && c.y - half.y < avoidMaxY) continue;
                if (OverlapsTerrain(c, size)) continue;
                float nearest = float.PositiveInfinity;
                foreach (Vector2 o in result) nearest = Mathf.Min(nearest, Vector2.Distance(c, o));
                float score = Mathf.Min(Vector2.Distance(c, target) - _attack.SpikeMinDistanceFromTarget, nearest - _attack.SpikeMinSpacing);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                    found = true;
                }
                if (score >= 0f) break; // 条件に合った
            }
            if (!found)
            {
                // ここに来ることは、ふつうは無い（部屋がとても狭いとき）：プレイヤーから離れた高い所
                float side = target.x <= arena.center.x ? 1f : -1f;
                best = new Vector2(Mathf.Clamp(target.x + side * (_attack.SpikeMinDistanceFromTarget + i), arena.xMin + half.x, arena.xMax - half.x),
                    arena.yMin + heights.y);
            }
            result.Add(best);
        }
        return result;
    }

    bool OverlapsTerrain(Vector2 center, Vector2 size)
    {
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(_owner.GroundMask | _owner.SolidMask);
        return Physics2D.OverlapBox(center, size + Vector2.one * 0.2f, 0f, filter, _terrainBuffer) > 0;
    }
}
