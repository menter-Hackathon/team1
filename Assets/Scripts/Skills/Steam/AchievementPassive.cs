using System;
using UnityEngine;

/// <summary>バフをかける相手</summary>
public enum BuffTargets
{
    /// <summary>自分だけ（プレイヤー版）</summary>
    Self = 0,
    /// <summary>自分と、同じ陣営のステージ内の敵（ボス版：ボスと雑魚ウイルス）</summary>
    SelfAndAllies = 1,
}

/// <summary>
/// Steam スキル：装備している間、戦闘中の実績を数え、解除したらバフをかける（SkillPassive）。
/// プレイヤー版とボス版は、このアセットを2つ作って中身（実績・バフ・絵）を変える。ロジックは同じ（AchievementRunner）。
/// 作り方：Project ウィンドウで右クリック ＞ Create ＞ Team1 ＞ Achievement Passive (Steam)
/// </summary>
[CreateAssetMenu(fileName = "Passive_Steam", menuName = "Team1/Achievement Passive (Steam)")]
public class AchievementPassive : SkillPassive
{
    [Header("実績")]
    [SerializeField] AchievementDefinition[] _achievements;

    [Header("バフ")]
    [SerializeField] BuffSpec _buff = BuffSpec.Default;
    [SerializeField] BuffTargets _targets = BuffTargets.Self;
    [Tooltip("バフの名前（同じ名前のバフは「重なったときの扱い」に従う）")]
    [SerializeField] string _buffId = "steam_achievement";

    [Header("通知（画面の隅）")]
    [Tooltip("通知の背景（Steam_ToastBG）")]
    [SerializeField] Sprite _toastBackground;
    [Tooltip("通知のアイコン（Steam_Trophy、6コマ）")]
    [SerializeField] Sprite[] _trophyFrames;
    [SerializeField, Min(0.1f)] float _trophyFrameRate = 10f;
    [SerializeField] string _toastTitle = "実績解除";
    [SerializeField] Color _titleColor = new Color(0.75f, 0.85f, 1f);
    [SerializeField] Color _nameColor = Color.white;
    [Tooltip("通知を出しておく秒数（スライドイン・アウトを含む）")]
    [SerializeField, Min(0.5f)] float _toastDuration = 3f;
    [Tooltip("通知の位置：オンで右下、オフで左下")]
    [SerializeField] bool _toastOnRight = true;

    [Header("キャラクターの演出")]
    [Tooltip("解除した瞬間に、体の中心に出す（Steam_BuffBurst、8コマ）")]
    [SerializeField] Sprite[] _burstFrames;
    [SerializeField, Min(0.1f)] float _burstFrameRate = 14f;
    [Tooltip("バフが続く間、足元に重ねる（Steam_BuffAura、6コマ、繰り返し）")]
    [SerializeField] Sprite[] _auraFrames;
    [SerializeField, Min(0.1f)] float _auraFrameRate = 10f;

    public AchievementDefinition[] Achievements => _achievements ?? Array.Empty<AchievementDefinition>();
    public BuffSpec Buff => _buff;
    public BuffTargets Targets => _targets;
    public string BuffId => _buffId;
    public Sprite ToastBackground => _toastBackground;
    public Sprite[] TrophyFrames => _trophyFrames;
    public float TrophyFrameRate => _trophyFrameRate;
    public string ToastTitle => _toastTitle;
    public Color TitleColor => _titleColor;
    public Color NameColor => _nameColor;
    public float ToastDuration => _toastDuration;
    public bool ToastOnRight => _toastOnRight;
    public Sprite[] BurstFrames => _burstFrames;
    public float BurstFrameRate => _burstFrameRate;
    public Sprite[] AuraFrames => _auraFrames;
    public float AuraFrameRate => _auraFrameRate;

    public override IDisposable Attach(GameObject owner)
    {
        var runner = owner.AddComponent<AchievementRunner>();
        runner.Setup(this);
        return runner;
    }
}
