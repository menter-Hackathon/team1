using UnityEngine;

/// <summary>
/// 実行中に作る絵（予告・エフェクト・画面表示）の置き場。Resources/GameArt.asset を読む。
/// 中身は「Tools/Team1/グラフィックを適用」が入れる（Assets/Sprites の絵）。
/// 無いとき（アセットが無い・空の欄）は、それぞれの場所で今までの仮の図形を使う。
/// 絵を差し替えるときは、Assets/Sprites の画像を同じ名前で置き換えるか、このアセットの欄を差し替える。
/// </summary>
[CreateAssetMenu(fileName = "GameArt", menuName = "Team1/Game Art")]
public class GameArt : ScriptableObject
{
    [Header("攻撃の予告")]
    [Tooltip("出現・瞬間移動の予告の点線の枠（9スライス）")]
    [SerializeField] Sprite _telegraphFrame;
    [Tooltip("落下物の影")]
    [SerializeField] Sprite _shadow;
    [Tooltip("ブレークポイントの赤丸（床の上の予告）")]
    [SerializeField] Sprite _breakpointMark;
    [Tooltip("VS Code が現れる場所の文字カーソル（縦に並べる）")]
    [SerializeField] Sprite _warpCursor;

    [Header("エフェクト（コマ）")]
    [Tooltip("ウイルスの煙（大）。ボスの正常化・プレイヤーが倒れたとき")]
    [SerializeField] Sprite[] _smoke;
    [Tooltip("ウイルスの煙（小）。雑魚が倒れたとき・ボスの煙がはがれたとき")]
    [SerializeField] Sprite[] _smokeSmall;
    [Tooltip("攻撃が当たったときの火花")]
    [SerializeField] Sprite[] _hitSpark;
    [Tooltip("新しいタブ（瞬間移動）で開く小さなウィンドウ")]
    [SerializeField] Sprite[] _tabOpen;
    [Tooltip("ボスのウィンドウにひびが入ったときの破片")]
    [SerializeField] Sprite[] _shards;
    [Tooltip("きらめき（きれいになったウィンドウのまわり）")]
    [SerializeField] Sprite[] _sparkle;

    [Header("画面表示")]
    [SerializeField] Sprite _heart;
    [SerializeField] Sprite _heartEmpty;
    [SerializeField] Sprite _slot;
    [SerializeField] Sprite _slotSelected;
    [Tooltip("枠の下に出す 1・2・3")]
    [SerializeField] Sprite[] _digits;
    [Tooltip("白い1ドット（塗りつぶし用）")]
    [SerializeField] Sprite _pixel;
    [SerializeField] Sprite _gaugeFrame;
    [SerializeField] Sprite _gaugeFill;

    [Header("タイトル・リザルト")]
    [SerializeField] Sprite _background;
    [SerializeField] Sprite _windowFrame;
    [SerializeField] Sprite _button;
    [SerializeField] Sprite _buttonHover;
    [SerializeField] Sprite[] _playerWalk;
    [Header("文字（2026-11-18：画面のイメージに合わせたドットのフォント。OFL）")]
    [Tooltip("日本語：DotGothic16（16 か 12 の大きさで使う）")]
    [SerializeField] Font _fontJa;
    [Tooltip("英字の見出し：Press Start 2P（8 の倍数の大きさで使う）")]
    [SerializeField] Font _fontEn;
    [Tooltip("雑魚の絵（ゲームオーバー・タイトルの飾り。シートの1コマ目を使う）")]
    [SerializeField] Sprite[] _zakoWalk;

    public Sprite TelegraphFrame => _telegraphFrame;
    public Sprite Shadow => _shadow;
    public Sprite BreakpointMark => _breakpointMark;
    public Sprite WarpCursor => _warpCursor;
    public Sprite[] Smoke => _smoke;
    public Sprite[] SmokeSmall => _smokeSmall;
    public Sprite[] HitSpark => _hitSpark;
    public Sprite[] TabOpen => _tabOpen;
    public Sprite[] Shards => _shards;
    public Sprite[] Sparkle => _sparkle;
    public Sprite Heart => _heart;
    public Sprite HeartEmpty => _heartEmpty;
    public Sprite Slot => _slot;
    public Sprite SlotSelected => _slotSelected;
    public Sprite[] Digits => _digits;
    public Sprite Pixel => _pixel;
    public Sprite GaugeFrame => _gaugeFrame;
    public Sprite GaugeFill => _gaugeFill;
    public Sprite Background => _background;
    public Sprite WindowFrame => _windowFrame;
    public Sprite Button => _button;
    public Sprite ButtonHover => _buttonHover;
    public Sprite[] PlayerWalk => _playerWalk;
    public Font FontJa => _fontJa;
    public Font FontEn => _fontEn;
    public Sprite[] ZakoWalk => _zakoWalk;

    static GameArt _instance;
    static bool _loaded;

    /// <summary>Resources/GameArt を読む。無ければ null</summary>
    public static GameArt Instance
    {
        get
        {
            if (!_loaded || _instance == null)
            {
                _instance = Resources.Load<GameArt>("GameArt");
                _loaded = true;
            }
            return _instance;
        }
    }

    /// <summary>1マスあたりのドット数（ゲーム中の物の絵）</summary>
    public const float PixelsPerUnit = 18f;
}
