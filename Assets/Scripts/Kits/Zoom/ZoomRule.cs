using UnityEngine;

/// <summary>
/// 【仮】Zoom のステージのギミック「40分で切れる」（2026-11-19。docs/app-kits-1-design.md 18章。【要相談】2 は「入れる」の扱い）。StageRule。
/// ・右上（ボスのゲージの下）に、小さい時計 40:00。本当は 40秒で 0 になる（1秒で 1分ぶん減る。飾りの数字。OnGUI：ZoomClockGui）
/// ・ボス戦：残り 10 で時計が赤く点滅。0 になると Meeting ended：画面が一瞬暗くなり、ボスのゲージが 8 戻る（E5。0 より下にはならない。
///   第2形態から第1形態には戻らない）。時計は 40:00 に戻る。
///   40秒の間にゲージを 25 以上増やしていれば、戻らない（時計が緑になり、そのまま次の 40秒へ）。攻め続ければ罰は無い
/// ・雑魚の時間：時計は出すが、0 になっても何も戻らない。参加者のタイルが 4枚落ちてくるだけ（影と赤い「！」の予告つき）
/// ・形態変化・追い出しの演出・ボスの登場の間は、時計が止まる（StageRuleHost が Tick を呼ばない）
/// </summary>
public class ZoomRule : StageRule
{
    // 【仮】数値
    const float Window = 40f, BlinkFrom = 10f;
    const int Rewind = 8, Goal = 25, Tiles = 4;

    float _left = Window;
    int _startGauge;
    bool _met;
    bool _warned; // 残り 10 の音を1回だけ鳴らす
    ZoomClockGui _clock;
    AttackData _tileData;

    public override void Begin(StageRuleContext ctx)
    {
        _left = Window;
        var go = ctx.TrackDecor(new GameObject("Zoom_Clock"));
        _clock = go.AddComponent<ZoomClockGui>();
        _clock.Left = _left;
    }

    public override void OnBossAppeared(BossController boss)
    {
        _left = Window;
        _warned = false;
        _met = false;
        _startGauge = boss != null && boss.Gauge != null ? boss.Gauge.Current : 0;
    }

    public override void Tick(float dt)
    {
        _left -= dt;
        BossController boss = Ctx.Boss;
        if (Ctx.InBossFight && boss != null && boss.Gauge != null && !_met && boss.Gauge.Current - _startGauge >= Goal)
        {
            _met = true;
            GameAudio.Event("gm_zoom_goal");
        }
        if (_clock != null)
        {
            _clock.Left = Mathf.Max(0f, _left);
            _clock.Met = _met && Ctx.InBossFight;
            _clock.Warn = !_clock.Met && _left <= BlinkFrom;
        }
        if (!_warned && _left <= BlinkFrom && !(_met && Ctx.InBossFight))
        {
            _warned = true;
            GameAudio.Event("gm_zoom_warning");
        }
        if (_left > 0f) return;

        if (Ctx.InBossFight)
        {
            if (!_met && boss != null)
            {
                KitBoss.RewindGauge(boss, Rewind);
                GameAudio.Event("gm_zoom_cut");
                KitToast.Show("Meeting ended", null, ZoomKitArt.LeaveRed);
                ZoomDimFlash.Show();
            }
            _startGauge = boss != null && boss.Gauge != null ? boss.Gauge.Current : 0;
            _met = false;
        }
        else if (Ctx.CanSpawnHazard)
        {
            DropTiles();
        }
        _left = Window;
        _warned = false;
    }

    public override void End()
    {
        if (_clock != null) Object.Destroy(_clock.gameObject);
        _clock = null;
        if (_tileData != null) Object.Destroy(_tileData);
        _tileData = null;
    }

    /// <summary>参加者のタイルを 4枚落とす（出来事の雨の写し。名前から枠の語を外して、絵を参加者のタイルにする）</summary>
    void DropTiles()
    {
        AttackRunner runner = Ctx.Hazard;
        if (runner == null || runner.IsRunning) return;
        if (_tileData == null)
        {
            PaceAssets assets = PaceAssets.Load();
            if (assets == null || assets.Rain == null) return;
            _tileData = Object.Instantiate(assets.Rain);
            _tileData.name = "Attack_Zoom_Tiles";
            _tileData.hideFlags = HideFlags.DontSave;
            JsonUtility.FromJsonOverwrite("{\"_placement\":0,\"_spawnCount\":" + Tiles + ",\"_objectSize\":{\"x\":2,\"y\":1.5},\"_telegraphTime\":0.8,"
                + "\"_minDistanceFromTarget\":2.5,\"_landRemainTime\":0,\"_becomesPlatform\":false}", _tileData);
        }
        int index = 0;
        System.Action<GameObject> dress = go =>
        {
            if (go == null || !go.TryGetComponent(out FallingObject f) || f.Data != _tileData) return;
            KitDress.Swap(go, ZoomKitArt.ParticipantTile(index++));
            CallKitUtil.AddExclaim(f);
        };
        GameAudio.Event("gm_zoom_tiles");
        runner.Spawned += dress;
        try { runner.Run(_tileData); }
        finally { runner.Spawned -= dress; }
    }
}

/// <summary>右上の小さい時計（OnGUI。文字は飾りの数字だけ）。ふつうは白、残り 10 で赤く点滅、ゲージを 25 増やしたら緑</summary>
public class ZoomClockGui : MonoBehaviour
{
    public float Left = 40f;
    public bool Warn;
    public bool Met;

    const float W = 54f, H = 16f, Top = 60f;

    void OnGUI()
    {
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsTransitioning || session.IsGameOver)) return;
        GUI.depth = 11;
        PixelGui.Begin(false);
        float right = PixelGui.Width - 6f;
        var box = new Rect(right - W, Top, W, H);
        Color edge = Met ? ZoomKitArt.Green : Warn ? ZoomKitArt.LeaveRed : MacUi.Gray3;
        bool blink = Warn && Mathf.FloorToInt(Time.time / 0.25f) % 2 == 0;
        PixelGui.Fill(box, edge);
        PixelGui.Fill(new Rect(box.x + 1f, box.y + 1f, box.width - 2f, box.height - 2f), CallKitUtil.Alpha(ZoomKitArt.Dark, 0.92f));
        // 時計の印（小さい丸と針）
        var dial = new Rect(box.x + 4f, box.y + 4f, 8f, 8f);
        PixelGui.Fill(dial, ZoomKitArt.Blue);
        PixelGui.Fill(new Rect(dial.x + 3f, dial.y + 1f, 2f, 4f), Color.white);
        PixelGui.Fill(new Rect(dial.x + 3f, dial.y + 3f, 4f, 2f), Color.white);
        string text = $"{Mathf.CeilToInt(Left):00}:00";
        Color ink = Met ? ZoomKitArt.Green : Warn ? (blink ? ZoomKitArt.LeaveRed : Color.white) : Color.white;
        PixelGui.TextEn(new Rect(box.x + 14f, box.y + 4f, box.width - 16f, 8f), text, 8, ink, TextAnchor.MiddleCenter);
        PixelGui.End();
    }
}

/// <summary>Meeting ended：画面が一瞬暗くなる（カメラの子の黒い板が 0.5秒でうすくなる）</summary>
public class ZoomDimFlash : MonoBehaviour
{
    SpriteRenderer _sr;
    float _born;
    const float Life = 0.5f;

    public static void Show()
    {
        Transform root = FxKit.CameraChild("Zoom_Dim");
        if (root == null) return;
        var d = root.gameObject.AddComponent<ZoomDimFlash>();
        d._born = Time.time;
        d._sr = FxKit.Box(root, Vector2.zero, FxKit.ViewSize() + Vector2.one * 2f, new Color(0f, 0f, 0f, 0.55f), FxKit.OrderScreen, "Dim");
    }

    void Update()
    {
        float t = (Time.time - _born) / Life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        if (_sr != null) _sr.color = new Color(0f, 0f, 0f, 0.55f * (1f - t * t));
    }
}
