using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】GitHub Desktop のステージの出来事「マージの衝突」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 5章）。
/// ・はじめ：背景のブランチの線（GitHubBranchLines）の2本が近づく（飾り）
/// ・FloorWave（主）：左から &lt;&lt;&lt;&lt;（青）、右から &gt;&gt;&gt;&gt;（紫）の低い壁（高さ 0.9。ジャンプで越える）。3／4／5回・2.8秒おき。
///   1回目は左だけ、2回目は右だけ、3回目からは両方から同時（まん中でぶつかって消え、そこに ======= の字が 0.4秒）。速さ 7・予告 1.0秒
/// ・HitTarget（6.0秒）：Resolve のボタン（青い角丸にチェック）。まん中の足場のすぐ上（床から 5.7）。6秒。
///   叩く：残りの波が全部消え、出来事が 2秒早く終わる（緑のチェックと Merged）。叩かない：最後に両方から波がもう1回（罰は軽く）
/// ・合流の柱（GitHubDesktopRule）は、場面③の間は出ない（ctx.CanSpawnHazard が false。決まりの側は直さない）
/// ・Resolve の高さは設計の 6.2 から 5.7 に下げた（近接の判定は足から 0.2〜0.8 の高さ。まん中の足場 5.1 の上に立って届き、
///   低い足場 3.0 からのジャンプの上りでも届く。床からは届かない。数えただけで、ゲームの中ではまだ確かめていない）
/// ・設計の「3.0秒おき」は、強さ 3 の 5回目が終わりに入るよう 2.8秒おきにした
/// </summary>
public class GitHubMergeConflictEvent : KitTimedEvent
{
    const float WaveEvery = 2.8f, WaveSpeed = 7f, WaveHeight = 0.9f, WaveWidth = 1.4f, WaveWarn = 1f;
    const float ResolveAt = 6f, ResolveLife = 6f, ResolveHeight = 5.7f, ResolveSize = 1.1f;
    static readonly Color ResolveBlue = RuntimeSprites.Hex("5B8FD0");

    readonly List<KitMotionGroup> _waves = new List<KitMotionGroup>();
    readonly List<KitMotionGroup> _meets = new List<KitMotionGroup>();
    readonly HashSet<KitMotionItem> _dressed = new HashSet<KitMotionItem>();
    bool _resolved;

    public GitHubMergeConflictEvent() : base("Merge conflict") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(GitHubDesktopKit.Label, stage => new GitHubMergeConflictEvent());

    protected override void Build()
    {
        GitHubBranchLines lines = Object.FindObjectOfType<GitHubBranchLines>();
        if (lines != null) lines.Merge();
        int rounds = Level >= 3 ? 5 : Level == 2 ? 4 : 3;
        Every(1f, WaveEvery, rounds, i =>
        {
            if (_resolved) return;
            Wave(i == 0 ? KitSide.Left : i == 1 ? KitSide.Right : KitSide.Both);
        });
        At(ResolveAt, ShowResolve);
        EndAt(15f);
    }

    void Wave(KitSide side)
    {
        bool both = side == KitSide.Both;
        KitMotionGroup g = KitMotion.FloorWave(Owner, side, WaveSpeed, WaveHeight, WaveWarn, null, GitHubDesktopArt.Blue, 1, WaveWidth, both);
        if (g == null) return;
        _waves.Add(g);
        if (both) _meets.Add(g);
        GameAudio.Event("gm_github_merge_warn");
    }

    void ShowResolve()
    {
        Rect a = KitMotion.Area;
        var at = new Vector2(a.center.x, KitMotion.FloorY() + ResolveHeight);
        KitMotionTarget t = KitMotion.HitTarget(Owner, at, ResolveLife, OnResolved, OnMissed, MacUi.RoundRectSprite(20, 20, 4), ResolveBlue, ResolveSize, 1, null, 0.5f);
        if (t == null || t.Body == null) return;
        AttackRunner.AddVisual(t.Body, FxArt.Check, Color.white, new Vector2(0.55f, 0.43f), KitMotionItem.OrderBody + 1, "Check");
        GitHubDesktopArt.Text(t.Body, "Resolve", new Vector2(0f, -ResolveSize * 0.5f - 0.22f), 0.24f, Color.white, KitMotionItem.OrderBody + 1);
    }

    void OnResolved()
    {
        _resolved = true;
        foreach (var g in _waves) if (g != null && !g.IsDone) g.Cancel();
        _waves.Clear();
        _meets.Clear();
        Rect a = KitMotion.Area;
        var at = new Vector2(a.center.x, KitMotion.FloorY() + ResolveHeight);
        FxAnim.Sprite(FxArt.Check, at + Vector2.up * 0.3f, FxKit.OrderToast - 1, 0.6f, GitHubDesktopArt.Green, 2f, "GitHubMerged");
        FxKit.Text("Merged", at + Vector2.up * 1.1f, 0.4f, GitHubDesktopArt.Green, FxKit.OrderToast - 1, 0.6f, Color.black);
        GameAudio.Event("gm_github_commit", 1.2f);
        EndAt(Mathf.Max(Clock + 1.5f, Length - 2f)); // 2秒早く終わる
    }

    void OnMissed()
    {
        if (!_resolved) Wave(KitSide.Both);
    }

    protected override void OnFrame(float clock)
    {
        float cx = KitMotion.Area.center.x;
        _waves.RemoveAll(g => g == null || g.IsDone);
        foreach (var g in _waves)
            foreach (var item in g.Items)
            {
                if (!(item is KitMotionMover m) || m.Body == null || !_dressed.Add(m)) continue;
                bool fromLeft = m.transform.position.x < cx;
                m.SetColor(fromLeft ? GitHubDesktopArt.Blue : GitHubDesktopArt.Purple);
                GitHubDesktopArt.Text(m.Body, fromLeft ? "<<<<" : ">>>>", Vector2.zero, 0.3f, Color.white, KitMotionItem.OrderBody + 1);
            }
        _dressed.RemoveWhere(x => x == null);
        // 両方からの波がまん中でぶつかって消えた：======= の字
        for (int i = _meets.Count - 1; i >= 0; i--)
        {
            KitMotionGroup g = _meets[i];
            if (g != null && !g.IsDone) continue;
            _meets.RemoveAt(i);
            if (_resolved) continue;
            float y = KitMotion.FloorY() + WaveHeight * 0.5f;
            FxKit.Text("=======", new Vector2(cx, y + 0.2f), 0.4f, Color.white, FxKit.OrderToast - 2, 0.4f, GitHubDesktopArt.Navy);
        }
    }
}
