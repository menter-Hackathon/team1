using UnityEngine;

/// <summary>
/// プレイヤーの頭の上のHPバーの絵と置き方（2026-11-20）。PlayerOverheadHPBar が Resources から読む。
/// 素材は Assets/_Project/PlayerHPBar（3倍で書き出し・PPU 54＝プレイヤーと同じ。ピボットは左上）。
/// 長さ・位置は元のドット絵の1ドット単位で書く
/// </summary>
[CreateAssetMenu(menuName = "Game/Player HP Bar Art", fileName = "PlayerHPBarArt")]
public class PlayerHPBarArt : ScriptableObject
{
    public const string ResourcePath = "PlayerHPBarArt";

    [Tooltip("中身が空の枠。上から最大HP 3・4・5 の順")]
    [SerializeField] Sprite[] _frames = new Sprite[3];
    [SerializeField] Sprite _pipFull;
    [SerializeField] Sprite _pipEmpty;
    [SerializeField] Sprite _pipLow;
    [SerializeField] Sprite _pipFlashWhite;
    [SerializeField] Sprite _pipFlashRed;

    [Header("大きさ（元のドット絵のドット）")]
    [Tooltip("素材を何倍で書き出したか")]
    [SerializeField, Min(1)] int _exportScale = 3;
    [Tooltip("足元からアンテナの先までの高さ（Player_Walk / Player_Idle は 23）")]
    [SerializeField] int _headTopDots = 23;
    [Tooltip("アンテナの先とバーの下の端のすき間")]
    [SerializeField] int _gapDots = 2;

    [Header("被弾の点滅")]
    [Tooltip("1コマの秒数（白 → 赤・空 ×3 の7コマ）")]
    [SerializeField, Min(0.01f)] float _flashFrameSeconds = 0.08f;

    public const int MinFrameMax = 3;
    public int MaxFrameMax => MinFrameMax + _frames.Length - 1;

    public Sprite PipFull => _pipFull;
    public Sprite PipEmpty => _pipEmpty;
    public Sprite PipLow => _pipLow;
    public Sprite PipFlashWhite => _pipFlashWhite;
    public Sprite PipFlashRed => _pipFlashRed;
    public int HeadTopDots => _headTopDots;
    public int GapDots => _gapDots;
    public float FlashFrameSeconds => _flashFrameSeconds;

    /// <summary>元のドット絵の1ドットのワールドでの長さ（PPU 54・3倍なら 1/18）</summary>
    public float DotSize => _pipFull != null ? _exportScale / _pipFull.pixelsPerUnit : 1f / 18f;

    /// <summary>最大HPに合う枠。3〜5 の外なら、いちばん近い枠を返す（呼ぶ側で警告を出す）</summary>
    public Sprite FrameFor(int max)
    {
        if (_frames == null || _frames.Length == 0) return null;
        int i = Mathf.Clamp(max - MinFrameMax, 0, _frames.Length - 1);
        return _frames[i];
    }

    public bool HasFrameFor(int max) => max >= MinFrameMax && max <= MaxFrameMax;
}
