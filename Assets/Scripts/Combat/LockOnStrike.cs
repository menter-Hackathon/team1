using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ロックオン（AttackKind.LockOn。2026-11-18：Zoom の能力「ギャラリービュー」。チームの案は「広範囲ロックオンレーザー・クールタイム長め」）。
/// AttackRunner が AttackData から作る。見た目と値は【仮】。
///
/// 流れ
/// 1. 画面が一瞬、Zoom のギャラリービューのようにタイル（3×3）に分かれる。
///    同時に、画面に映っている相手それぞれに緑の枠（Zoom の「話している人」の枠）が付く（ロックオンの印）
/// 2. 予告の時間のあと、1本ずつ間隔をあけて、持ち主から相手へレーザーを撃つ。当たった相手にダメージ・ゲージ
/// 3. 最後のレーザーが消えたら、自分も消える
///
/// 出したあとは自分で進むので、攻撃した人が攻撃を止めても最後まで残る（AreaStrike と同じ）。
/// 印を付けている間に倒れた・消えた相手には撃たない。
///
/// 2026-11-18（スキルの見た目 7-3。担当2）：動き・秒数・本数は今のまま、Zoom らしさを足す。
/// ・タイルごとに、左下の名札（暗い地に白い棒）と、右上のマイクの印
/// ・印（緑の枠）に、左下の名札と、右上の赤い斜線の入ったマイク（ミュート）
/// ・レーザーが当たった所に、赤い「退出」のボタンが「パッ」と出て消える（同じ相手には1本目だけ。2本目からは白い光だけ）
/// ・雑魚が倒れたら、小さい黒い四角（カメラがオフ）を 0.1秒
/// ・暗くする面は撃ち始めで消え（今のまま）、3×3 の線は撃ち終わりに、まん中へ集まって消える（0.15秒）
/// </summary>
public class LockOnStrike : MonoBehaviour
{
    // ───────── 見た目【仮】 ─────────
    const float Pixel = 1f / 18f;            // 1ドット（ゲーム中の物は1マス＝18ドット）
    const float MarkThickness = 2f * Pixel;  // 枠の太さ
    const float MarkPadding = 0.12f;         // 相手の体と枠のすき間
    const float MarkSnapTime = 0.12f;        // 枠が大きい所から縮んで相手に付くまで
    const float MarkSnapScale = 1.8f;        // 付き始めの枠の大きさ（倍）
    const float MarkFlashTime = 0.08f;       // レーザーを撃った瞬間、枠を白く光らせる時間
    const int GridTiles = 3;                 // タイルの数（3×3）
    const float GridLine = 3f * Pixel;       // タイルの間の線の太さ
    const float GridFadeIn = 0.05f;
    const float GridFadeOut = 0.2f;
    const float LaserWidth = 6f * Pixel;     // レーザーの太さ（外側）
    const float LaserCore = 2f * Pixel;      // レーザーの芯（白）
    const float ImpactTime = 0.15f;          // 当たった所の光
    const float HitWindow = 0.1f;            // 当たり判定を出している時間
    static readonly Color GridColor = new Color(0.05f, 0.05f, 0.06f, 0.85f); // タイルの間（Zoom の黒いすき間）
    static readonly Color DimColor = new Color(0f, 0f, 0f, 0.2f);            // 画面全体を少し暗くする
    const float GridCollapse = 0.15f;        // 撃ち終わりに、線がまん中へ集まって消える秒数（7-3）
    const float LeaveTime = 0.2f;            // 「退出」のボタンが出ている秒数（7-3）
    const float CameraOffTime = 0.1f;        // 雑魚が倒れたときの黒い四角（7-3）

    const int DimOrder = 60, GridOrder = 61, MarkOrder = 63, LaserOrder = 64, ImpactOrder = 65;

    AttackRunner _owner;
    AttackData _data;
    readonly List<Hurtbox> _shots = new List<Hurtbox>();          // 撃つ順の相手（同じ相手が何度も入ることがある）
    readonly List<Mark> _marks = new List<Mark>();
    Transform _grid;
    readonly List<SpriteRenderer> _gridParts = new List<SpriteRenderer>();
    readonly List<Vector3> _gridBase = new List<Vector3>();        // 線の元の位置と長さ（集まって消える動き用。x,y＝位置、z＝長さ）
    readonly List<SpriteRenderer> _tileDeco = new List<SpriteRenderer>(); // タイルの名札・マイク（_gridParts とは別。色を変えない）
    readonly HashSet<Hurtbox> _leaveShown = new HashSet<Hurtbox>(); // 「退出」のボタンを出した相手
    Vector2 _lastOrigin;

    /// <summary>出してから消えるまでの秒数</summary>
    public float Duration { get; private set; }
    /// <summary>撃つレーザーの本数（テスト用）</summary>
    public int ShotCount => _shots.Count;
    /// <summary>印を付けた相手の数（テスト用）</summary>
    public int MarkCount => _marks.Count;

    class Mark
    {
        public Hurtbox Target;
        public Transform Root;
        public SpriteRenderer[] Sides; // 上・下・左・右
        public SpriteRenderer Tag;     // 左下の名札（7-3）
        public SpriteRenderer Mic;     // 右上のミュートのマイク（7-3）
        public float FlashUntil;
    }

    // ───────── 相手の探し方 ─────────

    /// <summary>
    /// 画面に映っている、狙える相手（被弾範囲）を、持ち主から近い順に返す。
    /// 相手ごとに1つ（体に被弾範囲が2つあっても1つ）。倒れた相手・ゲージが満タンのボスは入れない
    /// </summary>
    public static List<Hurtbox> FindTargets(AttackRunner owner)
    {
        var list = new List<Hurtbox>();
        if (owner == null) return list;
        Rect view = ViewRect();
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
        filter.SetLayerMask(owner.TargetLayers);
        var hits = new List<Collider2D>();
        Physics2D.OverlapArea(view.min, view.max, filter, hits);

        var seen = new HashSet<IDamageable>();
        foreach (Collider2D col in hits)
        {
            // 被弾範囲（Hurtbox）だけを見る。相手の弾や移動用の当たり判定は無視する
            if (col == null || !col.enabled || !col.TryGetComponent(out Hurtbox hurtbox)) continue;
            if (!IsAlive(hurtbox) || hurtbox.Owner.Team == owner.Team) continue;
            if (!seen.Add(hurtbox.Owner)) continue;
            list.Add(hurtbox);
        }
        Vector2 origin = owner.OriginPoint;
        list.Sort((a, b) =>
            ((Vector2)a.Collider.bounds.center - origin).sqrMagnitude.CompareTo(((Vector2)b.Collider.bounds.center - origin).sqrMagnitude));
        return list;
    }

    /// <summary>まだ狙える相手か（消えていない・倒れていない・ゲージが満タンでない）</summary>
    static bool IsAlive(Hurtbox hurtbox)
    {
        if (hurtbox == null || !hurtbox.isActiveAndEnabled || hurtbox.Collider == null || !hurtbox.Collider.enabled) return false;
        IDamageable owner = hurtbox.Owner;
        if (owner == null || (owner is Object o && o == null)) return false;
        if (owner is Health h && h.IsDead) return false;
        if (owner is VirusGauge g && g.IsFull) return false;
        return true;
    }

    /// <summary>メインカメラに映っている範囲（無ければ戦う部屋の範囲）</summary>
    static Rect ViewRect()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return BattleArena.GetBounds();
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        return new Rect(c.x - w, c.y - h, w * 2f, h * 2f);
    }

    /// <summary>
    /// レーザーを相手に割り当てる。1巡目は近い順に1本ずつ。余った分は、まだ倒れない相手（ボス・HPが残る相手）に順にもう一度。
    /// 1回で倒れる雑魚に2本目は撃たない
    /// </summary>
    static List<Hurtbox> AssignShots(List<Hurtbox> targets, AttackData data)
    {
        var shots = new List<Hurtbox>();
        int n = Mathf.Max(1, data.LockOnCount);
        var left = new Dictionary<Hurtbox, int>();
        foreach (var t in targets) left[t] = HitsToDefeat(t, data);

        foreach (var t in targets)
        {
            if (shots.Count >= n) break;
            shots.Add(t);
            left[t]--;
        }
        bool added = true;
        while (shots.Count < n && added)
        {
            added = false;
            foreach (var t in targets)
            {
                if (shots.Count >= n) break;
                if (left[t] <= 0) continue;
                shots.Add(t);
                left[t]--;
                added = true;
            }
        }
        return shots;
    }

    /// <summary>何本当てれば倒れるか（ゲージのボスは倒れないものとして扱う）</summary>
    static int HitsToDefeat(Hurtbox target, AttackData data)
    {
        if (target.Owner is VirusGauge) return int.MaxValue;
        if (target.Owner is Health h) return data.Damage > 0 ? Mathf.Max(1, Mathf.CeilToInt((float)h.Current / data.Damage)) : 1;
        return 1;
    }

    // ───────── 出す ─────────

    /// <summary>ロックオンを1回出す。targets は FindTargets の結果（空なら何もしないで null）</summary>
    public static LockOnStrike Spawn(AttackRunner owner, AttackData data, List<Hurtbox> targets)
    {
        if (owner == null || data == null || targets == null || targets.Count == 0) return null;
        GameObject go = owner.CreateSpawnedObject($"LockOn_{data.name}", owner.OriginPoint);
        var strike = go.AddComponent<LockOnStrike>();
        strike._owner = owner;
        strike._data = data;
        strike._lastOrigin = strike.LaserOrigin();
        strike._shots.AddRange(AssignShots(targets, data));
        strike.Duration = data.TelegraphTime + (strike._shots.Count - 1) * data.RepeatInterval + Mathf.Max(data.ActiveTime, ImpactTime) + 0.05f;

        strike.BuildGrid();
        foreach (var t in targets)
        {
            if (strike._shots.Contains(t)) strike._marks.Add(strike.BuildMark(t));
        }
        strike.StartCoroutine(strike.Run());
        return strike;
    }

    Vector2 LaserOrigin()
    {
        if (_owner == null) return _lastOrigin;
        Vector2 offset = new Vector2(_owner.Facing * _data.SpawnOffset.x, _data.SpawnOffset.y);
        return _owner.OriginPoint + offset;
    }

    void BuildGrid()
    {
        _grid = new GameObject("Gallery").transform;
        _grid.gameObject.layer = gameObject.layer;
        _grid.SetParent(transform, false);
        Rect view = ViewRect();
        _grid.position = view.center;
        Sprite sq = PlaceholderSprites.Square;
        _gridParts.Add(AttackRunner.AddVisual(_grid, sq, DimColor, view.size, DimOrder, "Dim"));
        _gridBase.Add(Vector3.zero);
        // タイルの間の線（外周と、間）
        for (int i = 0; i <= GridTiles; i++)
        {
            float x = -view.width * 0.5f + view.width * i / GridTiles;
            float y = -view.height * 0.5f + view.height * i / GridTiles;
            var v = AttackRunner.AddVisual(_grid, sq, GridColor, new Vector2(GridLine, view.height), GridOrder, "LineV");
            v.transform.localPosition = new Vector3(x, 0f, 0f);
            var hl = AttackRunner.AddVisual(_grid, sq, GridColor, new Vector2(view.width, GridLine), GridOrder, "LineH");
            hl.transform.localPosition = new Vector3(0f, y, 0f);
            _gridParts.Add(v);
            _gridParts.Add(hl);
            _gridBase.Add(new Vector3(x, 0f, view.height));
            _gridBase.Add(new Vector3(0f, y, view.width));
        }
        BuildTileDeco(view);
        SetGridAlpha(0f, 0f, 0f);
    }

    /// <summary>タイルごとに、左下の名札（暗い地に白い棒）と、右上のマイクの印（7-3）</summary>
    void BuildTileDeco(Rect view)
    {
        Sprite tag = ZoomArt.NameTag();
        Sprite mic = ZoomArt.Mic();
        float tw = view.width / GridTiles, th = view.height / GridTiles;
        for (int i = 0; i < GridTiles; i++)
        {
            for (int j = 0; j < GridTiles; j++)
            {
                float x0 = -view.width * 0.5f + tw * i, y0 = -view.height * 0.5f + th * j;
                var t = RuntimeSprites.Show(_grid, tag, new Vector2(x0 + GridLine + 9f * Pixel, y0 + GridLine + 5f * Pixel), GridOrder, 1f, "NameTag");
                var m = RuntimeSprites.Show(_grid, mic, new Vector2(x0 + tw - GridLine - 5f * Pixel, y0 + th - GridLine - 5f * Pixel), GridOrder, 1f, "Mic");
                _tileDeco.Add(t);
                _tileDeco.Add(m);
            }
        }
    }

    Mark BuildMark(Hurtbox target)
    {
        var root = new GameObject("Mark").transform;
        root.gameObject.layer = gameObject.layer;
        root.SetParent(transform, false);
        var sides = new SpriteRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            sides[i] = AttackRunner.AddVisual(root, PlaceholderSprites.Square, _data.LockOnMarkColor, Vector2.one, MarkOrder, "Side");
        }
        // 左下の名札と、右上のミュートのマイク（7-3）
        var tag = RuntimeSprites.Show(root, ZoomArt.NameTag(), Vector2.zero, MarkOrder + 1, 1f, "NameTag");
        var mic = RuntimeSprites.Show(root, ZoomArt.MutedMic(), Vector2.zero, MarkOrder + 1, 1f, "Mic");
        var mark = new Mark { Target = target, Root = root, Sides = sides, Tag = tag, Mic = mic };
        PlaceMark(mark, MarkSnapScale);
        return mark;
    }

    /// <summary>枠を相手の体に合わせて置く。scale は枠の大きさの倍率（付き始めは大きい）</summary>
    void PlaceMark(Mark mark, float scale)
    {
        Bounds b = mark.Target.Collider.bounds;
        Vector2 size = ((Vector2)b.size + Vector2.one * (MarkPadding * 2f)) * scale;
        mark.Root.position = b.center;
        float hx = size.x * 0.5f, hy = size.y * 0.5f, t = MarkThickness;
        SetSide(mark.Sides[0], new Vector2(0f, hy - t * 0.5f), new Vector2(size.x, t));
        SetSide(mark.Sides[1], new Vector2(0f, -hy + t * 0.5f), new Vector2(size.x, t));
        SetSide(mark.Sides[2], new Vector2(-hx + t * 0.5f, 0f), new Vector2(t, size.y));
        SetSide(mark.Sides[3], new Vector2(hx - t * 0.5f, 0f), new Vector2(t, size.y));
        // 名札は枠の左下（枠の外へ少しはみ出す）、マイクは右上の内側
        if (mark.Tag != null) mark.Tag.transform.localPosition = new Vector3(-hx + 7f * Pixel, -hy + 1f * Pixel, 0f);
        if (mark.Mic != null) mark.Mic.transform.localPosition = new Vector3(hx - 5f * Pixel, hy - 5f * Pixel, 0f);
    }

    static void SetSide(SpriteRenderer sr, Vector2 localPos, Vector2 size)
    {
        sr.transform.localPosition = localPos;
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(size.x / Mathf.Max(native.x, 0.001f), size.y / Mathf.Max(native.y, 0.001f), 1f);
    }

    /// <summary>
    /// タイルの濃さ。dim＝暗くする面、lines＝線と名札・マイク、collapse＝線がまん中へ集まる割合（0＝元の所、1＝まん中）
    /// </summary>
    void SetGridAlpha(float dim, float lines, float collapse)
    {
        float keep = 1f - collapse;
        for (int i = 0; i < _gridParts.Count; i++)
        {
            Color c = i == 0 ? DimColor : GridColor;
            c.a *= i == 0 ? dim : lines;
            _gridParts[i].color = c;
            if (i == 0) continue;
            // 線：位置と長さを、まん中へ縮める
            Vector3 b = _gridBase[i];
            Transform tr = _gridParts[i].transform;
            tr.localPosition = new Vector3(b.x * keep, b.y * keep, 0f);
            Vector2 native = _gridParts[i].sprite.bounds.size;
            bool vertical = i % 2 == 1;
            float len = Mathf.Max(0.001f, b.z * keep);
            tr.localScale = vertical
                ? new Vector3(GridLine / Mathf.Max(native.x, 0.001f), len / Mathf.Max(native.y, 0.001f), 1f)
                : new Vector3(len / Mathf.Max(native.x, 0.001f), GridLine / Mathf.Max(native.y, 0.001f), 1f);
        }
        foreach (var d in _tileDeco)
        {
            if (d == null) continue;
            d.color = new Color(1f, 1f, 1f, lines * (1f - collapse));
        }
    }

    // ───────── 進める ─────────

    IEnumerator Run()
    {
        float start = Time.time;
        float fireAt = start + _data.TelegraphTime;
        float gridOutStart = fireAt;
        float lastShot = fireAt + Mathf.Max(0, _shots.Count - 1) * _data.RepeatInterval; // 撃ち終わり（7-3：線がまん中へ集まる）
        int next = 0;
        float nextShot = fireAt;
        float end = start + Duration;

        while (Time.time < end)
        {
            float now = Time.time;
            if (_owner != null) _lastOrigin = LaserOrigin();

            // 撃つ（1フレームに間隔が何本分も過ぎていたら、まとめて撃つ）
            while (next < _shots.Count && now >= nextShot)
            {
                Fire(_shots[next]);
                next++;
                nextShot += _data.RepeatInterval;
            }

            // タイル：すぐ出る。暗くする面は撃ち始めたら消えていく。線と名札は撃ち終わりに、まん中へ集まって消える（7-3）
            float fadeIn = Mathf.Clamp01((now - start) / GridFadeIn);
            float dim = now < gridOutStart ? fadeIn : 1f - Mathf.Clamp01((now - gridOutStart) / GridFadeOut);
            float collapse = Mathf.Clamp01((now - lastShot) / GridCollapse);
            float eased = collapse * collapse;
            SetGridAlpha(dim, fadeIn * (1f - collapse), eased);
            Rect view = ViewRect(); // カメラが動いても画面に付いていく
            _grid.position = view.center;

            // 枠：相手に付いていく。撃ち終わった相手の枠は消す
            float snap = Mathf.Clamp01((now - start) / MarkSnapTime);
            float scale = Mathf.Lerp(MarkSnapScale, 1f, 1f - (1f - snap) * (1f - snap));
            foreach (var m in _marks)
            {
                bool alive = IsAlive(m.Target);
                bool pending = alive && _shots.IndexOf(m.Target, next) >= 0;
                bool show = alive && (pending || now < m.FlashUntil);
                m.Root.gameObject.SetActive(show);
                if (!show) continue;
                PlaceMark(m, scale);
                Color c = now < m.FlashUntil ? Color.white : _data.LockOnMarkColor;
                foreach (var s in m.Sides) s.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>1本撃つ。相手が倒れていたら撃たない</summary>
    void Fire(Hurtbox target)
    {
        if (!IsAlive(target)) return;
        Vector2 from = _lastOrigin;
        Bounds b = target.Collider.bounds;
        Vector2 to = b.center;

        foreach (var m in _marks)
        {
            if (m.Target == target) m.FlashUntil = Time.time + MarkFlashTime;
        }
        StartCoroutine(Laser(from, to));
        // 当たった所に「退出」のボタン（同じ相手には1本目だけ）。雑魚が倒れたら「カメラがオフ」（7-3）
        bool first = _leaveShown.Add(target);
        if (first) StartCoroutine(Leave(to + LeaveOffset(b, from)));
        if (target.Owner is Health) StartCoroutine(CameraOff(target, b));

        // 当たり判定：相手の体を少し大きく覆う箱を、短い時間だけ出す（ダメージ・ゲージ・のけぞりは Hitbox に任せる）
        if (_owner == null) return;
        var hit = new GameObject("LaserHit");
        hit.layer = gameObject.layer;
        hit.transform.SetParent(transform, false);
        hit.transform.position = to;
        var rb = hit.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var box = hit.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = (Vector2)b.size + Vector2.one * 0.1f;
        Hitbox hitbox = _owner.AddHitbox(hit, _data, box);
        hitbox.Activate();
        Destroy(hit, HitWindow);
    }

    IEnumerator Laser(Vector2 from, Vector2 to)
    {
        var root = new GameObject("Laser").transform;
        root.gameObject.layer = gameObject.layer;
        root.SetParent(transform, false);
        Vector2 d = to - from;
        float length = Mathf.Max(d.magnitude, 0.01f);
        root.position = (from + to) * 0.5f;
        root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        Sprite sprite = _data.Sprite != null ? _data.Sprite : PlaceholderSprites.Square;
        var outer = AttackRunner.AddVisual(root, sprite, _data.Color, new Vector2(length, LaserWidth), LaserOrder, "Outer");
        var core = AttackRunner.AddVisual(root, PlaceholderSprites.Square, Color.white, new Vector2(length, LaserCore), LaserOrder, "Core");
        core.sortingOrder = LaserOrder + 1;

        var impactGo = new GameObject("Impact").transform;
        impactGo.gameObject.layer = gameObject.layer;
        impactGo.SetParent(transform, false);
        impactGo.position = to;
        var impact = AttackRunner.AddVisual(impactGo, PlaceholderSprites.Circle, Color.white, Vector2.one, ImpactOrder, "Flash");

        // 細くなりながら消える（太さだけを縮める）
        Vector3 outerScale = outer.transform.localScale, coreScale = core.transform.localScale;
        float start = Time.time;
        float life = Mathf.Max(_data.ActiveTime, 0.01f);
        while (true)
        {
            float t = (Time.time - start) / life;
            float ti = (Time.time - start) / ImpactTime;
            if (t >= 1f && ti >= 1f) break;
            float k = 1f - Mathf.Clamp01(t);
            outer.transform.localScale = new Vector3(outerScale.x, outerScale.y * k, 1f);
            core.transform.localScale = new Vector3(coreScale.x, coreScale.y * k, 1f);
            float ik = Mathf.Clamp01(ti);
            float size = Mathf.Lerp(0.3f, 1.0f, ik);
            impactGo.localScale = new Vector3(size, size, 1f);
            impact.color = new Color(1f, 1f, 1f, 1f - ik);
            yield return null;
        }
        Destroy(root.gameObject);
        Destroy(impactGo.gameObject);
    }

    /// <summary>「退出」のボタンを出す位置のずれ：相手の中心から、レーザーが来た側の上へ（顔をかくさない）</summary>
    static Vector2 LeaveOffset(Bounds b, Vector2 from)
    {
        Vector2 side = from - (Vector2)b.center;
        float sx = Mathf.Abs(side.x) > 0.01f ? Mathf.Sign(side.x) : 1f;
        return new Vector2(sx * Mathf.Min(0.35f, b.extents.x * 0.6f), Mathf.Min(0.45f, b.extents.y * 0.7f));
    }

    /// <summary>赤い「退出」のボタンが「パッ」と出て消える（0.2秒。7-3）</summary>
    IEnumerator Leave(Vector2 at)
    {
        var go = new GameObject("Leave");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        var sr = RuntimeSprites.Show(go.transform, ZoomArt.LeaveButton(), Vector2.zero, ImpactOrder + 1, 1f, "Button");
        float start = Time.time;
        while (Time.time - start < LeaveTime)
        {
            float e = Time.time - start;
            float t = e / 0.08f;
            float s = t >= 1f ? 1f : t < 0.6f ? Mathf.Lerp(0f, 1.15f, t / 0.6f) : Mathf.Lerp(1.15f, 1f, (t - 0.6f) / 0.4f);
            go.transform.localScale = new Vector3(s, s, 1f);
            float k = e / LeaveTime;
            sr.color = new Color(1f, 1f, 1f, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>雑魚が倒れたら、タイルが閉じる煙の前に、小さい黒い四角（カメラがオフ）を 0.1秒（7-3）</summary>
    IEnumerator CameraOff(Hurtbox target, Bounds b)
    {
        float start = Time.time;
        while (Time.time - start < HitWindow + 0.1f)
        {
            if (!IsAlive(target))
            {
                var go = new GameObject("CameraOff");
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                go.transform.position = b.center;
                RuntimeSprites.Show(go.transform, ZoomArt.CameraOff(), Vector2.zero, ImpactOrder + 1, 1f, "Off");
                Destroy(go, CameraOffTime);
                yield break;
            }
            yield return null;
        }
    }
}

/// <summary>【仮】Zoom のギャラリービューの小さい絵（7-3。コードの中のドット絵、1マス＝18ドット）</summary>
public static class ZoomArt
{
    static readonly Color32 White = new Color32(255, 255, 255, 255);
    static readonly Color32 Plate = new Color32(0x1C, 0x1C, 0x1E, 0xD0);
    static readonly Color32 Red = new Color32(0xE0, 0x2F, 0x2F, 0xFF);
    static readonly Color32 Green = new Color32(0x24, 0xD9, 0x59, 0xFF);

    /// <summary>名札（暗い地、8×3ドットの白い棒。12×5）</summary>
    public static Sprite NameTag() => RuntimeSprites.FromAscii("BDZoom_NameTag", new[]
    {
        "PPPPPPPPPPPP",
        "PWWWWWWWWPPP",
        "PWWWWWWWWPPP",
        "PWWWWWWWWPPP",
        "PPPPPPPPPPPP",
    }, new Dictionary<char, Color32> { { 'P', Plate }, { 'W', White } });

    /// <summary>マイクの印（4ドット）</summary>
    public static Sprite Mic() => RuntimeSprites.FromAscii("BDZoom_Mic", new[]
    {
        ".W.",
        ".W.",
        "W.W",
        ".W.",
    }, new Dictionary<char, Color32> { { 'W', new Color32(255, 255, 255, 200) } });

    /// <summary>赤い斜線の入ったマイク（ミュート。6ドット）</summary>
    public static Sprite MutedMic() => RuntimeSprites.FromAscii("BDZoom_MicMuted", new[]
    {
        "..WW.R",
        "..WWR.",
        ".W.RW.",
        "..RW..",
        ".R.W..",
        "R.WWW.",
    }, new Dictionary<char, Color32> { { 'W', White }, { 'R', Red } });

    /// <summary>赤い「退出」のボタン（角の丸い赤い四角、14×8。白い棒）</summary>
    public static Sprite LeaveButton() => RuntimeSprites.FromAscii("BDZoom_Leave", new[]
    {
        ".RRRRRRRRRRRR.",
        "RRRRRRRRRRRRRR",
        "RRRRRRRRRRRRRR",
        "RRRWWWWWWWWRRR",
        "RRRWWWWWWWWRRR",
        "RRRRRRRRRRRRRR",
        "RRRRRRRRRRRRRR",
        ".RRRRRRRRRRRR.",
    }, new Dictionary<char, Color32> { { 'R', Red }, { 'W', White } });

    /// <summary>カメラがオフ（小さい黒い四角に、灰色のふち。12×9）</summary>
    public static Sprite CameraOff() => RuntimeSprites.FromAscii("BDZoom_CameraOff", new[]
    {
        "GGGGGGGGGGGG",
        "GKKKKKKKKKKG",
        "GKKKKKKKKKKG",
        "GKKKKKKKKKKG",
        "GKKKKKKKKKKG",
        "GKKKKKKKKKKG",
        "GKKKKKKKKKKG",
        "GKKKKKKKKKKG",
        "GGGGGGGGGGGG",
    }, new Dictionary<char, Color32> { { 'G', new Color32(0x3A, 0x3A, 0x3C, 0xFF) }, { 'K', new Color32(0x08, 0x08, 0x0A, 0xFF) } });

    /// <summary>緑の枠のかけら（L の形、4ドット）</summary>
    public static Sprite FrameBit() => RuntimeSprites.FromAscii("BDZoom_FrameBit", new[]
    {
        "GGGG",
        "GG..",
        "G...",
        "G...",
    }, new Dictionary<char, Color32> { { 'G', Green } });
}

/// <summary>
/// 【仮】Zoom の能力（ギャラリービュー）のスキン（7-3）。当たった絵は LockOnStrike が出す「退出」のボタンなので、
/// ここはかけら（緑の枠のかけら）だけを出して true を返す（Generic の当たった絵が重ならないように）
/// </summary>
public class ZoomGallerySkin : AppSkin
{
    public override SkinId Id => SkinId.Zoom;

    public override bool Hit(HitInfo info)
    {
        try
        {
            int n = info.Strength == HitStrength.Small ? 1 : info.Strength == HitStrength.Light ? 3 : info.Strength == HitStrength.Finisher ? 12 : 6;
            Sprite bit = ZoomArt.FrameBit();
            Vector2 dir = info.Direction.sqrMagnitude > 0.0001f ? info.Direction : Vector2.up;
            BrowserDevFx.Burst(info.Point, dir, n, i => bit, i => Color.white, unscaled: true);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ZoomGallerySkin.Hit：{e}");
            return false;
        }
    }
}
