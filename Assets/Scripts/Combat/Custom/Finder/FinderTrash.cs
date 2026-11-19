using System.Collections.Generic;
using UnityEngine;
using K = GenericFrameKit;

/// <summary>
/// 【仮】Finder のステージのゴミ箱（2026-11-19。docs/app-feedback-v2-design.md 4-3）。FinderKit が Finder のステージで付ける。
/// Dock のゴミ箱と「ゴミ箱を空にする」。雑魚の時間の、プレイヤーのごほうびのギミック（危ない物ではない）。
/// ・床の上、右の壁ぎわ（x＝部屋の右の端 − 1.5）。大きさ 1.2×1.4マス。当たり判定はない。奥に置く（床の絵より後ろ。沈むと床にかくれる）
/// ・雑魚が倒れるたびに、倒れた所から小さい書類がゴミ箱へ飛ぶ（0.4秒）。中の紙が増える（空 → 半分 → 山盛り）
/// ・8体たまると、0.8秒ふるえて白く光る（合図。赤にしない）→「ゴミ箱を空にする」：白い輪が画面いっぱいに広がり、
///   輪が届いた雑魚が倒れ、敵の弾が消える。紙のかけらが舞う。ゴミ箱は空に戻る。空にして倒した雑魚は、次の数に入れない
/// ・ボスの登場（PacePhase.Done）で、ふたを閉じて、床の下へ下がって消える
/// </summary>
public class FinderTrash : MonoBehaviour
{
    // ───────── 数値【仮】 ─────────
    const int Needed = 8;              // 【要相談⑧】8体で作って、遊んで決める
    const float FlyTime = 0.4f;
    const float SignalTime = 0.8f;
    const float RingTime = 0.35f;
    const float WallGap = 1.5f;
    const int Order = 0;               // 床（1）より後ろ。沈むと床の絵にかくれる

    StagePacer _pacer;
    Rect _arena;
    Transform _root;
    SpriteRenderer _sr;
    int _count;
    int _pendingFly;                   // 飛んでいる途中の書類（着いたら数える）
    bool _purging;
    float _signalUntil = -1f;
    float _ringStart = -1f;
    float _ringRadius;
    readonly HashSet<Health> _purgeKills = new HashSet<Health>();
    bool _leaving;
    int _shownLevel = -1;

    /// <summary>【仮】2026-11-19：今のステージのゴミ箱（無ければ null。出来事「ドラッグ＆ドロップ」のフォルダのごほうび）</summary>
    public static FinderTrash Current { get; private set; }

    /// <summary>
    /// 【仮】2026-11-19：from から小さい書類を count 枚、ゴミ箱へ飛ばす（雑魚を count 体倒したのと同じ数え方。docs/stage-gimmick-variety-a.md Finder）。
    /// 空にしている途中・去る途中は何もしない
    /// </summary>
    public void Feed(Vector2 from, int count)
    {
        if (_leaving || _purging) return;
        for (int i = 0; i < count; i++) FlyDoc(from + new Vector2(i * 0.3f, 0f));
    }

    public static FinderTrash Attach(StagePacer pacer)
    {
        if (pacer == null) return null;
        var go = new GameObject("FinderTrash");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, pacer.gameObject.scene);
        var t = go.AddComponent<FinderTrash>();
        t.Setup(pacer);
        return t;
    }

    void Setup(StagePacer pacer)
    {
        _pacer = pacer;
        Current = this;
        _arena = BattleArena.GetBounds();
        _root = transform;
        _root.position = new Vector3(_arena.xMax - WallGap, _arena.yMin, 0f);
        _sr = RuntimeSprites.Show(_root, TrashSprite(0), new Vector2(0f, 0.72f), Order, 1f, "Can");
        pacer.PhaseChanged += OnPhase;
        Health.AnyDied += OnAnyDied;
        // 出てくる：床から上がる
        GenericFrameTweens.Of(gameObject).Move(_sr.transform, new Vector3(0f, -0.8f, 0f), new Vector3(0f, 0.72f, 0f), 0.3f);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
        Health.AnyDied -= OnAnyDied;
        if (_pacer != null) _pacer.PhaseChanged -= OnPhase;
    }

    void OnPhase(PacePhase phase)
    {
        if (phase == PacePhase.Done) Leave();
    }

    bool IsZako(Health h)
    {
        if (h == null || h.Team != Team.Enemy) return false;
        if (h.GetComponentInParent<BossController>() != null || h.GetComponent<PlayerController>() != null) return false;
        foreach (var b in FindObjectsOfType<BossController>()) if (b != null && b.EjectedVirus == h) return false;
        return true;
    }

    void OnAnyDied(Health h)
    {
        if (_leaving || h == null) return;
        if (_purgeKills.Remove(h)) return; // 空にして倒した雑魚は数えない
        if (_purging) return;
        if (!IsZako(h)) return;
        FlyDoc(h.transform.position + Vector3.up * 0.5f);
    }

    /// <summary>倒れた所から、小さい書類がゴミ箱へ飛ぶ（0.4秒。山なり）。着いたら数える</summary>
    void FlyDoc(Vector2 from)
    {
        _pendingFly++;
        Vector2 to = (Vector2)_root.position + new Vector2(0f, 1.3f);
        Transform doc = K.Loose("Gen4_FinderTrashDoc", from);
        SpriteRenderer sr = K.Spr(doc, FinderArt.Pellet, Vector2.zero, Color.white, 30, 1.3f, "Doc");
        float h = Mathf.Max(1.2f, Mathf.Abs(to.x - from.x) * 0.25f);
        var tw = GenericFrameTweens.Of(doc.gameObject).Kill(FlyTime + 0.02f);
        tw.Each(FlyTime, k =>
        {
            if (doc == null) return;
            Vector2 p = Vector2.Lerp(from, to, k);
            p.y += Mathf.Sin(k * Mathf.PI) * h;
            doc.position = p;
            doc.rotation = Quaternion.Euler(0f, 0f, k * 360f);
            if (sr != null) sr.transform.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.8f, k);
        });
        tw.Call(FlyTime, Arrive);
    }

    void Arrive()
    {
        _pendingFly = Mathf.Max(0, _pendingFly - 1);
        if (this == null || _leaving || _purging) return;
        _count++;
        GameAudio.Event("gm_finder_trash_add", 1f + 0.06f * (_count - 1)); // たまるほど高く
        // ぽんと膨らむ
        if (_sr != null) GenericFrameTweens.Of(_sr.gameObject).Scale(_sr.transform, Vector3.one * 1.12f, Vector3.one, 0.12f);
        if (_count >= Needed) BeginSignal();
    }

    void BeginSignal()
    {
        _purging = true;
        _signalUntil = Time.time + SignalTime;
        GameAudio.Event("gm_finder_trash_full");
        if (_sr != null && FlashOverlay.Available) FlashOverlay.PlayOn(_sr, Color.white, new Color(1f, 1f, 1f, 0.4f), SignalTime);
    }

    void Update()
    {
        UpdateLevel();
        if (_leaving || _sr == null) return;
        SuckLandedFiles();
        // 合図：ふるえる
        if (_signalUntil > 0f)
        {
            if (Time.time < _signalUntil)
            {
                int step = Mathf.FloorToInt(Time.time / 0.04f);
                _sr.transform.localPosition = new Vector3(((step % 3) - 1) * FinderArt.Dot, 0.72f, 0f);
                return;
            }
            _signalUntil = -1f;
            _sr.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            BeginRing();
        }
        if (_ringStart > 0f) UpdateRing();
    }

    // ───────── B：③で降ってきたファイルが、床に着いたらゴミ箱へ吸いこまれる（飾り。数には入れない） ─────────

    readonly HashSet<FallingObject> _sucked = new HashSet<FallingObject>();
    float _nextSuck;

    void SuckLandedFiles()
    {
        if (_pacer == null || _pacer.Phase != PacePhase.Event || Time.time < _nextSuck) return;
        _nextSuck = Time.time + 0.1f;
        foreach (var f in FindObjectsOfType<FallingObject>())
        {
            if (f == null || !f.HasLanded || f.Visual == null || f.Visual.sprite == null || !_sucked.Add(f)) continue;
            if (Time.time - f.LandedTime > 0.5f) continue;
            // 写しが縮みながらゴミ箱へ飛ぶ（元の物はそのまま。足場になる物を消さないように）
            SpriteRenderer src = f.Visual;
            Vector2 from = src.bounds.center;
            Vector2 to = (Vector2)_root.position + new Vector2(0f, 1.2f);
            Transform t = K.Loose("Gen4_FinderTrashSuck", from);
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite;
            sr.sortingOrder = 30;
            sr.color = new Color(1f, 1f, 1f, 0.8f);
            Vector3 s0 = src.transform.lossyScale;
            var tw = GenericFrameTweens.Of(t.gameObject).Kill(0.47f);
            tw.Each(0.45f, k =>
            {
                if (t == null) return;
                float e = k * k;
                Vector2 p = Vector2.Lerp(from, to, e);
                p.y += Mathf.Sin(k * Mathf.PI) * 0.8f;
                t.position = p;
                t.localScale = s0 * Mathf.Lerp(1f, 0.15f, e);
            });
        }
        _sucked.RemoveWhere(x => x == null);
    }

    // ───────── ゴミ箱を空にする ─────────

    void BeginRing()
    {
        _ringStart = Time.time;
        GameAudio.Event("gm_finder_empty");
        Rect view = ScreenUi.ViewRect();
        Vector2 c = (Vector2)_root.position + new Vector2(0f, 0.7f);
        float far = 0f;
        foreach (Vector2 corner in new[] { new Vector2(view.xMin, view.yMin), new Vector2(view.xMin, view.yMax), new Vector2(view.xMax, view.yMin), new Vector2(view.xMax, view.yMax) })
            far = Mathf.Max(far, Vector2.Distance(c, corner));
        _ringRadius = Mathf.Max(6f, far + 0.5f);

        Transform ring = K.Loose("Gen4_FinderTrashRing", c);
        SpriteRenderer rs = K.Spr(ring, FinderArt.Ring(64, 3), Vector2.zero, Color.white, 45, 1f, "Ring");
        float baseD = 64f * FinderArt.Dot;
        var tw = GenericFrameTweens.Of(ring.gameObject).Kill(RingTime + 0.15f);
        tw.Scale(ring, Vector3.one * 0.2f, Vector3.one * (_ringRadius * 2f / baseD), RingTime).Alpha(rs, 1f, 0f, 0.15f, RingTime);
        ScreenFx.WhiteFlash(0.25f, 0.08f);
        // くしゃくしゃの紙のかけら
        K.Shards(c + Vector2.up * 0.6f, Vector2.up, 10, i => FinderArt.Pellet, i => i % 2 == 0 ? Color.white : FinderArt.Paper, 70f, 1.4f);
        ClearBullets();
        _count = 0;
    }

    void UpdateRing()
    {
        float k = Mathf.Clamp01((Time.time - _ringStart) / RingTime);
        float r = _ringRadius * k;
        Vector2 c = (Vector2)_root.position + new Vector2(0f, 0.7f);
        KillWithin(c, r);
        if (k >= 1f)
        {
            _ringStart = -1f;
            _purging = false;
            _count = 0;
        }
    }

    void KillWithin(Vector2 c, float r)
    {
        // 出し方を問わず、画面の雑魚を全部見る（輪が広がる 0.35秒の間だけ）
        foreach (Health h in FindObjectsOfType<Health>())
        {
            if (h == null || h.IsDead || !IsZako(h)) continue;
            Vector2 p = (Vector2)h.transform.position + Vector2.up * 0.5f;
            if (Vector2.Distance(p, c) > r) continue;
            _purgeKills.Add(h);
            var info = new DamageInfo
            {
                Amount = Mathf.Max(1, h.Current),
                SourceTeam = Team.Player,
                HitPoint = p,
                Source = gameObject,
                Skin = SkinId.Finder,
                ErrorKind = "Trash",
            };
            if (!h.TryTakeDamage(info)) _purgeKills.Remove(h);
        }
    }

    static void ClearBullets()
    {
        foreach (var p in FindObjectsOfType<Projectile>())
        {
            if (p == null) continue;
            Hitbox hb = p.GetComponent<Hitbox>();
            bool enemy = hb != null ? hb.Team != Team.Player : p.Owner != null && p.Owner.Team != Team.Player;
            if (!enemy) continue;
            K.Shards(p.transform.position, Vector2.up, 2, i => FinderArt.Pellet, i => Color.white, 60f, 0.8f);
            Destroy(p.gameObject);
        }
    }

    // ───────── 中の紙・去る ─────────

    void UpdateLevel()
    {
        if (_sr == null) return;
        int level = _count <= 0 ? 0 : _count < 5 ? 1 : 2;
        if (level == _shownLevel) return;
        _shownLevel = level;
        _sr.sprite = TrashSprite(level);
    }

    /// <summary>ボスの登場：ふたを閉じて、床の下へ下がって消える</summary>
    void Leave()
    {
        if (_leaving) return;
        _leaving = true;
        _signalUntil = -1f;
        _ringStart = -1f;
        if (_sr == null) { Destroy(gameObject); return; }
        _sr.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        GameAudio.Event("gm_finder_trash_leave");
        SpriteRenderer lid = K.Spr(_sr.transform, LidSprite, new Vector2(0f, 0.95f), Color.white, Order, 1f, "Lid");
        var tw = GenericFrameTweens.Of(gameObject).Kill(0.9f);
        tw.Move(lid.transform, new Vector3(0f, 1.4f, 0f), new Vector3(0f, 0.83f, 0f), 0.2f);
        tw.Move(_sr.transform, new Vector3(0f, 0.72f, 0f), new Vector3(0f, -1f, 0f), 0.5f, 0.3f, GenericFrameTweens.Ease.Linear);
    }

    // ───────── 絵（22×26 ドット。Dock のゴミ箱：網のかご、中に紙）【仮】 ─────────

    static readonly Sprite[] Cache = new Sprite[3];
    static Sprite _lid;

    static Sprite LidSprite => _lid != null ? _lid : (_lid = RuntimeSprites.FromAscii("Finder_TrashLid", new[]
    {
        ".........EEEE.........",
        "..LLLLLLLLLLLLLLLLLL..",
        ".LLLLLLLLLLLLLLLLLLLL.",
        ".DDDDDDDDDDDDDDDDDDDD.",
    }, Palette));

    static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        { 'E', RuntimeSprites.Hex("6E6E73") },
        { 'L', RuntimeSprites.Hex("D1D1D6") },
        { 'D', RuntimeSprites.Hex("8E8E93") },
        { 'R', RuntimeSprites.Hex("AEAEB2") },
        { 'G', RuntimeSprites.Hex("E5E5EAB0") },
        { 'P', RuntimeSprites.Hex("FFFFFF") },
        { 'p', RuntimeSprites.Hex("D0D0D6") },
    };

    /// <summary>level：0＝空、1＝半分、2＝山盛り</summary>
    static Sprite TrashSprite(int level)
    {
        level = Mathf.Clamp(level, 0, 2);
        if (Cache[level] != null) return Cache[level];
        const int w = 22, h = 26, rim = 4;
        var rows = new string[h];
        var chars = new char[w];
        int fillTop = level == 0 ? h : level == 1 ? 17 : 8;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                char c = '.';
                if (y < rim)
                {
                    // 山盛りの紙（ふちの上にはみ出す）
                    if (level == 2)
                    {
                        bool on = (y == 3 && x >= 4 && x <= 17) || (y == 2 && ((x >= 5 && x <= 9) || (x >= 11 && x <= 16))) || (y == 1 && ((x >= 6 && x <= 8) || (x >= 12 && x <= 15))) || (y == 0 && x >= 13 && x <= 14);
                        if (on) c = (x + y) % 3 == 0 ? 'p' : 'P';
                    }
                }
                else if (y == rim || y == rim + 1)
                {
                    if (x >= 1 && x <= w - 2) c = y == rim ? 'L' : 'D';
                }
                else
                {
                    int inset = (y - (rim + 2)) * 2 / (h - rim - 3);
                    int left = 1 + inset, right = w - 2 - inset;
                    if (x == left || x == right || (y == h - 1 && x >= left && x <= right)) c = 'E';
                    else if (x > left && x < right)
                    {
                        if (y >= fillTop) c = ((x * 3 + y * 5) % 7 == 0) ? 'p' : 'P';
                        else c = 'G';
                        if ((x - left) % 4 == 0) c = 'R'; // 網の線
                    }
                }
                chars[x] = c;
            }
            rows[y] = new string(chars);
        }
        return Cache[level] = RuntimeSprites.FromAscii($"Finder_Trash_{level}", rows, Palette);
    }
}
