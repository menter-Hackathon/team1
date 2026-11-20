using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】キーフレームの本体（2026-11-19。docs/app-feedback-v2-design.md 5-1）。PremiereKeyframe が出し、あとは自分で進む。
/// ◆ を順に打ち、赤い点線でつなぐ（予告）→ クリップが道すじを ◆ から ◆ へ動く（判定は円）→ 最後の ◆ ではじけて消える。
/// 形態変化・正常化で消されたら、その場で全部消える。場に残る技ではない（技はこれが終わるまで待つ）
/// 2026-11-20（docs/premiere-edit-screen-design.md 6-1）：プログラムモニター（PremiereMonitor）があれば、予告の間は映像が止まり、
/// ◆ が打たれるたびにスクラブバーに白い印。予告の最後の 0.4秒で、映像のコマ（当たらない）がモニターからちぢみながらボスの体（◆1）へ飛び、
/// そこから赤いふちのクリップが走り出す（今の技）。走っている間、モニターは「抜けた絵」。道すじ・◆・判定・速さは今のまま
/// </summary>
public class PremiereKeyframeEffect : CustomEffect
{
    class Clip
    {
        public Transform Root;
        public Hitbox Hitbox;
        public float StartAt;
        public bool Done;
    }

    const int PathOrder = 17, DiamondOrder = 18, ClipOrder = 20;
    const int FlyOrder = 15;          // 2026-11-20：モニターから飛ぶコマ（キャラクター 11〜 の前、道すじ 17 の後ろ）
    const float FlyTime = 0.4f;       // 2026-11-20：コマが飛ぶ秒数（クリップが走り出す瞬間に着く）

    AttackRunner _owner;
    PremiereKeyframe _config;
    readonly List<Vector2> _path = new List<Vector2>();
    readonly List<float> _segEnd = new List<float>(); // 区間ごとの、着くまでの秒数（はじめから）
    readonly List<SpriteRenderer> _diamonds = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _lines = new List<SpriteRenderer>();
    readonly List<Clip> _clips = new List<Clip>();
    float _start, _runAt, _travel;
    int _ticked;        // 2026-11-20：スクラブバーに印を出した ◆ の数
    bool _blanked;      // 2026-11-20：モニターを「抜けた絵」にしたか

    public bool Finished { get; private set; }

    public static PremiereKeyframeEffect Spawn(AttackRunner owner, AttackData data, PremiereKeyframe config, List<Vector2> path)
    {
        GameObject go = owner.CreateSpawnedObject("PremiereKeyframe", path.Count > 0 ? path[0] : (Vector2)owner.transform.position);
        var e = go.AddComponent<PremiereKeyframeEffect>();
        e._owner = owner;
        e._config = config;
        e._path.AddRange(path);
        e._start = Time.time;
        e._runAt = e._start + config.TelegraphTime;
        e.Build(data);
        if (PremiereMonitor.Current != null) PremiereMonitor.Current.Hold(config.TelegraphTime); // 予告の間、映像が止まる
        return e;
    }

    void Build(AttackData data)
    {
        // 区間ごとの時間：距離 ÷ 平均の速さ
        float t = 0f;
        _segEnd.Add(0f);
        for (int i = 1; i < _path.Count; i++)
        {
            t += Vector2.Distance(_path[i - 1], _path[i]) / Mathf.Max(1f, _config.AverageSpeed);
            _segEnd.Add(t);
        }
        _travel = t;

        for (int i = 0; i < _path.Count; i++)
        {
            SpriteRenderer d = RuntimeSprites.Show(transform, PremiereTimelineArt.Diamond, Vector2.zero, DiamondOrder, 1f, "Key");
            d.transform.position = _path[i];
            d.enabled = false;
            _diamonds.Add(d);
            if (i == 0) continue;
            SpriteRenderer l = PremiereTimelineArt.DottedLine(transform, _path[i - 1], _path[i], 2f, PremiereTimelineArt.EnemyRed, PathOrder, "Path");
            l.enabled = false;
            _lines.Add(l);
        }

        for (int c = 0; c < _config.ClipCount; c++)
        {
            var clip = new Clip { StartAt = _runAt + c * _config.ClipGap };
            clip.Root = PremiereArt.Child(transform, "Clip", Vector2.zero);
            clip.Root.position = _path[0];
            SpriteRenderer sr = RuntimeSprites.Show(clip.Root, PremiereTimelineArt.EnemyClip, Vector2.zero, ClipOrder, 1f, "Art");
            PremiereSkinTrail.Attach(clip.Root.gameObject, sr);
            var rb = clip.Root.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            var col = clip.Root.gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = _config.HitDiameter * 0.5f;
            if (_owner != null && data != null)
            {
                clip.Hitbox = _owner.AddHitbox(clip.Root.gameObject, data, col);
                clip.Hitbox.SetErrorKind("Keyframe");
            }
            clip.Root.gameObject.SetActive(false);
            _clips.Add(clip);
            // 2026-11-20：モニターから映像のコマが飛び出し、ちぢみながら ◆1（クリップが出る所）へ。当たらない
            PremiereMonitor monitor = PremiereMonitor.Current;
            if (monitor != null)
            {
                Vector2 start = _path[0];
                Vector2 clipSize = new Vector2(22f, 14f) / 18f;
                PremiereMonitorFlyer.Frame(transform, monitor.VideoCenter, monitor.VideoSize, () => start, clipSize, clip.StartAt - FlyTime, clip.StartAt, FlyOrder);
            }
        }
    }

    void Update()
    {
        if (Finished) return;
        float now = Time.time;

        // ── 予告：◆ を順に打つ（150% → 100%）。点線は ◆ が打たれたらつなぐ。最後の 0.3秒は点滅 ──
        bool blink = now < _runAt && _runAt - now < 0.3f && Mathf.FloorToInt(now / 0.06f) % 2 == 1;
        for (int i = 0; i < _diamonds.Count; i++)
        {
            SpriteRenderer d = _diamonds[i];
            if (d == null) continue;
            float at = _start + i * _config.PlaceInterval;
            if (now < at) continue;
            if (!d.gameObject.activeSelf) continue;
            d.enabled = !blink;
            float k = Mathf.Clamp01((now - at) / 0.08f);
            float s = Mathf.Lerp(1.5f, 1f, k);
            d.transform.localScale = new Vector3(s, s, 1f);
            if (i > 0 && i - 1 < _lines.Count && _lines[i - 1] != null) _lines[i - 1].enabled = !blink;
            if (i >= _ticked && PremiereMonitor.Current != null)
            {
                // 2026-11-20：スクラブバーに白い印（◆ の x の所。予告が終わるまで）
                _ticked = i + 1;
                PremiereMonitor.Current.AddTick(PremiereMonitor.Current.KAt(_path[i].x), _runAt - now + 0.1f, Color.white);
            }
        }
        if (now < _runAt) return;
        if (!_blanked)
        {
            // 2026-11-20：コマを取られたモニターは「抜けた絵」（最後のクリップがはじけるころまで）
            _blanked = true;
            if (PremiereMonitor.Current != null) PremiereMonitor.Current.Blank(_travel + (_clips.Count - 1) * _config.ClipGap + 0.3f);
        }

        // ── 本体：クリップが道すじを動く ──
        bool allDone = true;
        float lastProgress = float.PositiveInfinity;
        for (int c = 0; c < _clips.Count; c++)
        {
            Clip clip = _clips[c];
            if (clip.Done) continue;
            float t = now - clip.StartAt;
            if (t < 0f) { allDone = false; lastProgress = Mathf.Min(lastProgress, 0f); continue; }
            if (!clip.Root.gameObject.activeSelf)
            {
                clip.Root.gameObject.SetActive(true);
                if (clip.Hitbox != null) clip.Hitbox.Activate();
            }
            if (t >= _travel)
            {
                Pop(clip);
                continue;
            }
            allDone = false;
            clip.Root.position = PositionAt(t);
            lastProgress = Mathf.Min(lastProgress, t);
        }

        // ◆ と点線は、いちばん後ろのクリップが通ったら消える
        if (!float.IsPositiveInfinity(lastProgress) || allDone)
        {
            float passed = allDone ? float.PositiveInfinity : lastProgress;
            for (int i = 0; i < _diamonds.Count; i++)
            {
                if (_diamonds[i] == null || !_diamonds[i].gameObject.activeSelf) continue;
                if (passed >= _segEnd[i] - 0.001f && (i > 0 || passed > 0.02f))
                {
                    _diamonds[i].gameObject.SetActive(false);
                    if (i > 0 && i - 1 < _lines.Count && _lines[i - 1] != null) _lines[i - 1].gameObject.SetActive(false);
                }
            }
        }

        if (allDone)
        {
            Finished = true;
            Destroy(gameObject, 0.02f);
        }
    }

    /// <summary>はじめから t 秒の位置。区間ごとに、◆ の手前でゆっくり、間で速く（なめらかな出入り）</summary>
    Vector2 PositionAt(float t)
    {
        for (int i = 1; i < _path.Count; i++)
        {
            if (t > _segEnd[i]) continue;
            float len = Mathf.Max(0.0001f, _segEnd[i] - _segEnd[i - 1]);
            float k = Mathf.Clamp01((t - _segEnd[i - 1]) / len);
            float e = k * k * (3f - 2f * k);
            return Vector2.Lerp(_path[i - 1], _path[i], e);
        }
        return _path[_path.Count - 1];
    }

    /// <summary>最後の ◆ でクリップがはじけて消える</summary>
    void Pop(Clip clip)
    {
        clip.Done = true;
        if (clip.Hitbox != null) clip.Hitbox.Deactivate();
        Vector2 at = clip.Root.position;
        clip.Root.gameObject.SetActive(false);
        Transform root = GenericFrameKit.Loose("Gen4_KeyframePop", at);
        SpriteRenderer sr = GenericFrameKit.Spr(root, PremiereTimelineArt.EnemyClip, Vector2.zero, Color.white, ClipOrder, 1f, "Pop");
        GenericFrameTweens.Of(root.gameObject).Scale(root, Vector3.one, Vector3.one * 1.6f, 0.15f).Alpha(sr, 1f, 0f, 0.15f).Kill(0.3f);
        GenericFrameKit.Shards(at, Vector2.up, 6, i => PremiereSkinArt.Film8, i => Color.white, 80f, 1f);
    }
}
