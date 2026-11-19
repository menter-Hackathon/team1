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
/// </summary>
public class ChromeOfflineEvent : PaceEvent
{
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
            _fx = ChromeOfflineEffect.CreateInView(Runner, _data, _cfg, Plan(Strength), HoldSeconds);
            if (_fx != null)
            {
                _fx.SetNight(HoldSeconds * 0.45f); // B：途中で1回「夜」（飾りだけ）
                GameAudio.Event("gm_chrome_offline");
            }
            return _fx != null;
        }
        return _fx != null;
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
