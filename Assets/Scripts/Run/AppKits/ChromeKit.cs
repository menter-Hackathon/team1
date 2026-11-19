using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】本物の Chrome のステージのギミック（2026-11-19。docs/app-feedback-v2-design.md 2-5）。
/// 場面③の出来事を「タブを開きすぎた（群れ）」から「オフライン」に変える（ほかのブラウザ・代用ボスのステージは今のまま）。
/// 中身はボスの技（ChromeOffline）と同じ ChromeOfflineEffect。画面の右の端から左へ流れる
/// </summary>
public static class ChromeKit
{
    public static readonly string[] Names = { "Google Chrome", "Chrome" };
    public static readonly string[] Bundles = { "com.google.Chrome" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        AppStageEvents.Register("Chrome", Names, Bundles, (stage, isFinal) => isFinal ? null : new ChromeOfflineEvent());
    }
}

/// <summary>
/// 【仮】出来事「オフライン」（2026-11-19。docs 2-5）。隅の通知は No internet。
/// ・白っぽい幕は、出来事の間ずっと。終わると色が戻る（「つながった」）
/// ・数：強さ 1／2／3 で、サボテン 5／7／9、翼竜（低）0／2／3。間は 1.2／1.0／0.8秒。高い翼竜は出さない（道中の足場は形がばらばらなので）
/// ・穴の上には出さない。雑魚は、今までどおり StagePacer が出す
/// ・B：途中で1回、恐竜ゲームの「夜」（幕が暗い灰色になって戻る。飾りだけ）
/// ・危ない物は、出来事用の AttackRunner（StagePacer が渡す）から出す。攻撃データは実行時に作る（ダメージ 1・ノックバック 6×5。アセットは要らない）
/// ・2026-11-19（docs/stage-gimmick-variety-a.md Chrome）：ポップアップ広告を足す。出来事の 3秒目と 6.5秒目【仮】に、プレイヤーの前
///   （進む向きに 4〜6マス・床から 2〜3マス）へ 1枚ずつ（強さ 3 は 2枚ずつ）。予告 0.8（赤い点線の枠）→ 出ている 3.5秒（KitMotion.Pop。窓は痛くない）。
///   右上の × を叩くと閉じる（KitMotion.HitTarget。ごほうび：次のサボテンが 1つ消える）。叩かないと、閉じる時に小さい弾を 4つ輪に出す（消える前に赤く点滅）。
///   出る時・閉じる時が、サボテンがプレイヤーの所に着く前後 0.5秒に重なるなら、少し（最大 1.5秒）遅らせる
/// </summary>
public class ChromeOfflineEvent : PaceEvent
{
    // ───────── ポップアップ広告【仮】 ─────────
    const float AdWarn = 0.8f, AdLife = 3.5f, AdRingSpeed = 4f, AdQuiet = 0.5f, AdMaxDelay = 1.5f;
    static readonly float[] AdTimes = { 3f, 6.5f };
    static readonly Vector2 AdSize = new Vector2(2.2f, 1.5f);
    List<ChromeOfflineEffect.Planned> _planned;
    float _clock;
    int _nextAd;
    float _adDelay;

    /// <summary>出来事の長さ（StagePaceTable.GateEventSeconds 12秒から、始まりの 0.8秒と終わりの分を引いた）</summary>
    const float HoldSeconds = 10.8f;

    readonly ChromeOfflineSettings _cfg = new ChromeOfflineSettings();
    AttackData _data;
    ChromeOfflineEffect _fx;
    bool _spawned;

    public ChromeOfflineEvent()
    {
        Toast = "No internet";
    }

    public override float MaxSeconds => 14f;

    protected override void OnBegin()
    {
        _data = ScriptableObject.CreateInstance<AttackData>(); // 初めの値：ダメージ 1・ノックバック (6, 5)・のけぞり 0.25
        _data.name = "Attack_Chrome_Offline_Stage";
        _data.hideFlags = HideFlags.DontSave;
    }

    protected override bool OnTick(float dt)
    {
        if (!_spawned)
        {
            _spawned = true;
            _planned = Plan(Strength);
            _fx = ChromeOfflineEffect.CreateInView(Runner, _data, _cfg, _planned, HoldSeconds);
            if (_fx != null)
            {
                _fx.SetNight(HoldSeconds * 0.45f); // B：途中で1回「夜」（飾りだけ）
                GameAudio.Event("gm_chrome_offline");
            }
            return _fx != null;
        }
        _clock += dt;
        if (_fx != null) TickAds();
        return _fx != null;
    }

    // ───────── ポップアップ広告 ─────────

    void TickAds()
    {
        if (_nextAd >= AdTimes.Length || Runner == null || Quiet) return;
        if (_clock < AdTimes[_nextAd] + _adDelay) return;
        if (_adDelay < AdMaxDelay && NearCactus(_clock))
        {
            _adDelay += 0.25f;
            return;
        }
        _nextAd++;
        _adDelay = 0f;
        int count = Strength >= 3 ? 2 : 1;
        for (int i = 0; i < count; i++) SpawnAd(i);
    }

    /// <summary>広告が出る時・閉じる時が、サボテンがプレイヤーの所に着く前後 0.5秒に入るか（出来事の時計＝ChromeOfflineEffect の時計）</summary>
    bool NearCactus(float now)
    {
        if (_planned == null) return false;
        Rect view = ScreenUi.ViewRect();
        float px = PlayerPos.x;
        float appear = now + AdWarn, close = appear + AdLife;
        foreach (var p in _planned)
        {
            if (p.Kind == ChromeObstacle.LowBird || p.Kind == ChromeObstacle.HighBird) continue;
            float arrive = p.At + (view.xMax - px) / Mathf.Max(0.1f, _cfg.Speed);
            if (Mathf.Abs(arrive - appear) < AdQuiet || Mathf.Abs(arrive - close) < AdQuiet) return true;
        }
        return false;
    }

    void SpawnAd(int i)
    {
        Rect view = ScreenUi.ViewRect();
        Vector2 feet = PlayerPos;
        float ground = Runner.TryFindGround(feet.x, feet.y + 1f, out float gy) ? gy : feet.y;
        float half = AdSize.x * 0.5f + 0.3f;
        float x = feet.x + Random.Range(4f, 6f) + i * 2.6f; // 進む向き（右）の前
        if (x > view.xMax - half) x = feet.x - Random.Range(4f, 6f) - i * 2.6f;
        x = Mathf.Clamp(x, view.xMin + half, view.xMax - half);
        float y = Mathf.Min(ground + Random.Range(2f, 3f) + i * 0.4f, view.yMax - AdSize.y * 0.5f - 2.3f); // 上の HUD の下
        Vector2 at = new Vector2(x, y);
        KitOwner owner = KitOwner.Of(Track, Runner);
        KitMotionPop pop = KitMotion.Pop(owner, at, AdWarn, AdLife, 4, AdSprite, null, 0, 1.2f, AdRingSpeed, true, 0f, AdSize);
        if (pop == null) return;
        Vector2 closeAt = at + new Vector2(AdSize.x * 0.5f - 0.3f, AdSize.y * 0.5f - 0.27f);
        KitMotion.HitTarget(owner, closeAt, AdLife, () => CloseAd(pop), null, CloseSprite, Color.white, 0.6f, 1, null, AdWarn);
    }

    /// <summary>× を叩いた：輪を出さずに閉じる。次のサボテンが 1つ消える</summary>
    void CloseAd(KitMotionPop pop)
    {
        if (pop != null && !pop.IsDone)
        {
            FxKit.Ring(pop.transform.position, 2.6f, 0.2f, Color.white);
            pop.Cancel();
        }
        if (_fx != null) _fx.RemoveNextCactus(PlayerPos.x);
    }

    /// <summary>広告の窓（40×27）：灰青のタイトルバー、白い中身、水色の帯に AD、灰色の行</summary>
    static Sprite AdSprite
    {
        get
        {
            char[,] g = GimmickA.Grid(40, 27);
            GimmickA.Fill(g, 0, 0, 40, 27, 'O');
            GimmickA.Fill(g, 1, 1, 38, 25, 'W');
            GimmickA.Fill(g, 1, 1, 38, 5, 'T');
            GimmickA.Fill(g, 3, 3, 2, 1, 'O');
            GimmickA.Fill(g, 6, 3, 2, 1, 'O');
            GimmickA.Fill(g, 4, 8, 32, 10, 'C');
            GimmickA.Stamp(g, "AD", 16, 11, 'D');
            GimmickA.Fill(g, 4, 20, 28, 1, 'G');
            GimmickA.Fill(g, 4, 23, 20, 1, 'G');
            var pal = new Dictionary<char, Color32>
            {
                { 'O', RuntimeSprites.Hex("6B7280") }, { 'W', RuntimeSprites.Hex("F4F6F8") }, { 'T', RuntimeSprites.Hex("97A0AD") },
                { 'C', RuntimeSprites.Hex("7FD3E6") }, { 'D', RuntimeSprites.Hex("1F2937") }, { 'G', RuntimeSprites.Hex("C9CED6") },
            };
            return RuntimeSprites.FromAscii("ChromeStageAd", GimmickA.Rows(g), pal);
        }
    }

    /// <summary>広告の × ボタン（9×9。うすい灰色に濃い灰色の ×）</summary>
    static Sprite CloseSprite
    {
        get
        {
            char[,] g = GimmickA.Grid(9, 9);
            GimmickA.Fill(g, 0, 0, 9, 9, 'K');
            GimmickA.Fill(g, 1, 1, 7, 7, 'L');
            for (int k = 2; k <= 6; k++) { g[k, k] = 'K'; g[k, 8 - k] = 'K'; }
            var pal = new Dictionary<char, Color32> { { 'K', RuntimeSprites.Hex("374151") }, { 'L', RuntimeSprites.Hex("E5E7EB") } };
            return RuntimeSprites.FromAscii("ChromeStageAdClose", GimmickA.Rows(g), pal);
        }
    }

    protected override void OnEnd(bool byBoss)
    {
        if (_fx != null) Object.Destroy(_fx.gameObject);
        _fx = null;
        if (_data != null) Object.Destroy(_data);
        _data = null;
    }

    /// <summary>強さごとの並び。翼竜は列の中に散らす（最初には出さない）。サボテンの次の翼竜は、間を少しあける</summary>
    List<ChromeOfflineEffect.Planned> Plan(int strength)
    {
        int cacti = strength <= 1 ? 5 : strength == 2 ? 7 : 9;
        int birds = strength <= 1 ? 0 : strength == 2 ? 2 : 3;
        float gap = strength <= 1 ? 1.2f : strength == 2 ? 1.0f : 0.8f;
        int total = cacti + birds;
        var kinds = new List<ChromeObstacle>();
        for (int i = 0; i < cacti; i++) kinds.Add(Random.value < 0.3f ? ChromeObstacle.BigCactus : ChromeObstacle.SmallCactus);
        // 翼竜を 2番目以降に、なるべく等しい間で入れる
        for (int b = 0; b < birds; b++)
        {
            int at = Mathf.Clamp(Mathf.RoundToInt((b + 1) * total / (float)(birds + 1)), 1, kinds.Count);
            kinds.Insert(at, ChromeObstacle.LowBird);
        }
        var plan = new List<ChromeOfflineEffect.Planned>();
        float t = _cfg.Telegraph;
        for (int i = 0; i < kinds.Count; i++)
        {
            if (i > 0)
            {
                bool cactusThenBird = kinds[i] == ChromeObstacle.LowBird && kinds[i - 1] != ChromeObstacle.LowBird;
                t += cactusThenBird ? Mathf.Max(gap, 1.2f) : gap;
            }
            if (t > HoldSeconds - 1.5f) break; // 終わりの前に通りすぎるように
            plan.Add(new ChromeOfflineEffect.Planned { Kind = kinds[i], At = t });
        }
        return plan;
    }
}
