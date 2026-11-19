using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Xcode のステージの出来事「Build」（場面③。2026-11-19。docs/stage-gimmick-variety-c.md 6章）。
/// ・はじめ：上のまん中の帯（XcodeRule の XcodeBuildStrip）が Building... になる（XcodeRule.SetEventStrip。その間、決まりの時計は止まる）
/// ・HoldZone（主）：「ここでビルド」。場所は 3回変わる：① 左の低い足場の上 → ② 右の端の床（x ＋7）→ ③ 右の低い足場の上（幅 3）。
///   1か所につき、中に 2.0／2.0／2.5秒いると完了（出ると数え直し＝部品の決まり）。1か所の時間切れは 4.0秒。終わったら 0.3秒で次へ。
///   印：青い帯＋ハンマー。帯のバーは全体の進み、右の丸は完了の数
/// ・HoldZone が出ている間：Pop＝エラーの印（オレンジ寄りの赤い丸に「!」。幅 1.6）。1.8秒おきに 1／1／2か所。
///   場所は今の HoldZone の中（中にいる人の足元）か、すぐ横。予告 1.0秒 → 0.5秒で消える。HoldZone の外の人はねらわない
/// ・結果：2か所以上で Build Succeeded（能力のクールタイムが全部戻る＋雑魚が 1.0秒ひるむ＝移動 ×0.05）。1か所以下は Build Failed（罰なし）
/// ・設計は 16秒だったが、StagePacer の打ち切り（20秒）とダメージの止まりがあるので 14秒に詰めた（1か所の時間切れ 4.5 → 4.0秒・結果は 13.0秒まで）
/// </summary>
public class XcodeBuildEvent : KitTimedEvent
{
    const float ZoneWidth = 3f, ZoneHeight = 2.5f, ZoneLife = 4f, ZoneGap = 0.3f, ZoneWarn = 0.4f;
    const float PopEvery = 1.8f, PopFirst = 0.8f, PopWarn = 1f, PopLife = 0.5f, PopSize = 1.6f;
    const float ResultAt = 13f, ZakoStun = 1f;
    const int Zones = 3, NeedDone = 2;
    static readonly Color ZoneBlue = RuntimeSprites.Hex("6C9BD6");
    static readonly Color ErrorRed = RuntimeSprites.Hex("FF6A3D");

    XcodeRule _rule;
    KitMotionHold _zone;
    int _zoneIndex;
    int _done;
    bool _finished;
    float _nextPop;

    public XcodeBuildEvent() : base("Build") { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => StageEvents.Register(XcodeKit.Label, stage => new XcodeBuildEvent());

    float HoldSeconds => Level >= 3 ? 2.5f : 2f;

    protected override void Build()
    {
        _rule = StageRules.Current as XcodeRule;
        if (_rule != null)
        {
            _rule.SetEventStrip(true);
            _rule.ShowEventStrip("Building...", 0f, 0, XcodeBuildStrip.Mode.Busy);
        }
        At(0f, () => GameAudio.Event("gm_xcode_building"));
        At(1f, NextZone);
        At(ResultAt, Finish);
        EndAt(14f);
    }

    protected override void OnEnd(bool byBoss)
    {
        if (_rule != null) _rule.SetEventStrip(false);
        _rule = null;
        base.OnEnd(byBoss);
    }

    void NextZone()
    {
        if (_finished) return;
        if (_zoneIndex >= Zones) { Finish(); return; }
        Rect a = KitMotion.Area;
        float floor = KitMotion.FloorY();
        float x, surface, width = ZoneWidth;
        switch (_zoneIndex)
        {
            case 0:
                if (!FindPlatform(a.center.x - 3.5f, out x, out surface, out float w0)) { x = a.center.x - 3.5f; surface = floor + 3f; }
                else width = Mathf.Min(ZoneWidth, w0);
                break;
            case 1:
                x = Mathf.Min(a.center.x + 7f, a.xMax - ZoneWidth * 0.5f - 0.3f);
                surface = KitMotion.FloorY(x);
                break;
            default:
                if (!FindPlatform(a.center.x + 3.5f, out x, out surface, out float w2)) { x = a.center.x + 3.5f; surface = floor + 3f; }
                else width = Mathf.Min(ZoneWidth, w2);
                break;
        }
        _zoneIndex++;
        _zone = KitMotion.HoldZone(Owner, x, width, HoldSeconds, OnZoneDone, ZoneLife, OnZoneFail, ZoneBlue, ZoneHeight, surface, ZoneWarn);
        if (_zone != null)
            RuntimeSprites.Show(_zone.transform, XcodeArt.HammerIcon(), new Vector2(0f, ZoneHeight * 0.45f), KitMotionItem.OrderZone + 2, 1.4f, "Hammer");
        _nextPop = Clock + ZoneWarn + PopFirst;
    }

    void OnZoneDone()
    {
        _done++;
        GameAudio.Event("gm_xcode_count", 1f + 0.12f * (_done - 1));
        Chain();
    }

    void OnZoneFail() => Chain();

    void Chain()
    {
        _zone = null;
        if (_finished) return;
        At(Clock + ZoneGap, _zoneIndex >= Zones ? (System.Action)Finish : NextZone);
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        if (_zone != null && !_zone.IsDone) _zone.Cancel();
        _zone = null;
        bool ok = _done >= NeedDone;
        if (_rule != null)
            _rule.ShowEventStrip(ok ? "Build Succeeded" : "Build Failed", 1f, _done, ok ? XcodeBuildStrip.Mode.Succeeded : XcodeBuildStrip.Mode.Failed);
        if (ok)
        {
            KitPlayer.RefillCooldowns();
            StatMods.Zako.Speed.SetFor("xcode.event.stun", 0.05f, ZakoStun, XcodeArt.HammerIcon());
            KitToast.Show("Build Succeeded", null, XcodeArt.Green);
            GameAudio.Event("gm_xcode_succeeded");
        }
        else
        {
            KitToast.Show("Build Failed", null, XcodeArt.Red);
            GameAudio.Event("gm_xcode_failed");
        }
        EndAt(Mathf.Min(Length, Clock + 1.2f)); // 結果を少し見せて終わる
    }

    protected override void OnFrame(float clock)
    {
        if (_finished) return;
        if (_rule != null)
        {
            float part = _zone != null && !_zone.IsDone ? _zone.Progress01 : 0f;
            _rule.ShowEventStrip("Building...", Mathf.Clamp01((_done + part) / Zones), _done, XcodeBuildStrip.Mode.Busy);
        }
        if (_zone == null || _zone.IsDone || _zone.Warning || clock < _nextPop) return;
        _nextPop = clock + PopEvery;
        Rect z = _zone.Zone;
        float px = KitMotion.PlayerPos.x;
        float first = _zone.PlayerInside ? Mathf.Clamp(px, z.xMin + 0.3f, z.xMax - 0.3f) : Random.Range(z.xMin + 0.4f, z.xMax - 0.4f);
        ErrorPop(new Vector2(first, z.yMin + PopSize * 0.5f));
        if (Level >= 3)
        {
            // 2つ目：1つ目から遠い方の、帯のすぐ横
            float second = first < z.center.x ? z.xMax + PopSize * 0.45f : z.xMin - PopSize * 0.45f;
            ErrorPop(new Vector2(second, z.yMin + PopSize * 0.5f));
        }
    }

    void ErrorPop(Vector2 at)
    {
        KitMotionPop pop = KitMotion.Pop(Owner, at, PopWarn, PopLife, 0, null, ErrorRed, 1, PopSize);
        if (pop != null && pop.Body != null)
            AttackRunner.AddVisual(pop.Body, KitMotionArt.Exclaim, Color.white, new Vector2(0.27f, 0.8f), KitMotionItem.OrderBody + 1, "Bang");
    }

    /// <summary>nearX に近い、動かない低い足場（床から 1.8〜4.0）の上の面。無ければ false</summary>
    static bool FindPlatform(float nearX, out float x, out float top, out float width)
    {
        Rect a = KitMotion.Area;
        float floor = KitMotion.FloorY();
        x = nearX; top = floor; width = 0f;
        float best = 3f;
        bool found = false;
        foreach (var c in Physics2D.OverlapAreaAll(new Vector2(a.xMin, floor + 1.8f), new Vector2(a.xMax, floor + 4f), LayerMask.GetMask("OneWayPlatform")))
        {
            if (c == null || c.isTrigger || AppStagePlatforms.IsUnsteady(c)) continue;
            Bounds b = c.bounds;
            float d = Mathf.Abs(b.center.x - nearX);
            if (d >= best || b.size.x < 1.5f) continue;
            best = d;
            x = b.center.x;
            top = b.max.y;
            width = b.size.x;
            found = true;
        }
        return found;
    }
}
