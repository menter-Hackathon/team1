using System;
using System.Collections.Generic;
using UnityEngine;

// スキン（アプリの見た目の組）の決まった形（2026-11-18。docs/app-skill-visuals-design.md 1章）。
// 「どのアプリか」＝スキン、「どの種類の攻撃か」＝枠。スキンは1つ＝1ファイルで、AppSkin を継いだクラスを足すだけで使われる（下の AppSkins が自動で集める）。
// 絵を付ける・当たった絵を出すのは、各スキンのクラス。いつ呼ぶか（攻撃の部品・当たったときの知らせ）は、共通の部品（SkinDresser・HitFx など）が受け持つ。

/// <summary>スキン（1-1）。値は保存されることがあるので、明示的に書く</summary>
public enum SkinId
{
    None = 0,
    Generic = 1,
    Browser = 2,
    Dev = 3,
    Office = 4,
    Creative = 5,
    Game = 6,
    Chat = 7,
    AI = 8,
    Media = 9,
    Finder = 10,
    Premiere = 11,
    Terminal = 12,
    Zoom = 13,
}

/// <summary>枠（どの種類の攻撃か。1-1 の枠の一覧）。攻撃データの名前から決める（SkinTables.FrameOf）</summary>
public enum AttackFrame
{
    None = 0,
    SmallShot = 1,   // 小弾（タブ手裏剣・拡散ショット）
    LongShot = 2,    // 長弾（エラー弾）
    Thrown = 3,      // 投げ物（ボム・ロブ弾・トラップ弾）
    Pillar = 4,      // 柱（ブレークポイント・ピラー・棒グラフ・連続ピラー）
    Floor = 5,       // 床（赤い波線・トラップ弾の床）
    Paint = 6,       // 塗り（バケツ塗り・ペイント）
    Circle = 7,      // 円（ボムの爆発・ノヴァ）
    Window = 8,      // 窓（ポップアップ出現。足場になる）
    Falling = 9,     // 落下物（エラーウィンドウ落下）
    TopRow = 10,     // 上の列（タブ増殖）
    Warp = 11,       // ワープ（VS Code の瞬間移動・新しいタブ）
}

/// <summary>場面（1-1）：予告 → 本体（判定が始まった）→ 着地（床・壁に当たった）→ 消える</summary>
public enum DressPhase
{
    Telegraph = 0,
    Active = 1,
    Landed = 2,
    Vanish = 3,
}

/// <summary>当たったときの強さ（1-2 の表）</summary>
public enum HitStrength
{
    Small = 0,
    Light = 1,
    Heavy = 2,
    Finisher = 3,
    Hurt = 4,    // 敵の攻撃・体当たりが、プレイヤーに当たった
    Down = 5,    // プレイヤーのライフが 0 になった
}

/// <summary>絵を付けるときの情報（1-1 の「情報」）</summary>
public struct DressInfo
{
    /// <summary>絵を付ける先（攻撃の物の GameObject）。場面が変わっても同じ物が来る</summary>
    public GameObject Target;
    /// <summary>今の絵（攻撃データの絵）。スキンが絵を付けたら、呼んだ側がかくす。無いこともある</summary>
    public SpriteRenderer CurrentVisual;
    /// <summary>攻撃データ（名前で、その能力・技だけの見た目を選んでよい）</summary>
    public AttackData Data;
    /// <summary>攻撃した人</summary>
    public AttackRunner Owner;
    /// <summary>判定の大きさ（マス）。円は x が直径</summary>
    public Vector2 HitSize;
    /// <summary>敵の攻撃か（敵＝赤いふち、プレイヤー＝白いふち。0-3）</summary>
    public bool IsEnemy;
    /// <summary>アプリの色（無ければ null）</summary>
    public AppTheme Theme;
    /// <summary>アプリのアイコン（16ドットくらい。無ければ null）</summary>
    public Sprite AppIcon;
    /// <summary>何個目か（連射・並び・同時に出す物の番号。0 から）</summary>
    public int Index;
    /// <summary>進む向き（弾。無ければ zero）</summary>
    public Vector2 Direction;
    /// <summary>【足した 2026-11-18】同時に出す数・連射の数（予告で「何発来るか」を見せる。0＝分からない）。SkinDresser が入れる</summary>
    public int Count;
    /// <summary>【足した 2026-11-18】この場面が続く秒数（予告なら予告の秒数、本体なら判定の秒数。0＝分からない）。最後の 0.3秒の点滅などに使う</summary>
    public float Duration;
}

/// <summary>当たったときの情報（1-2 の「入れる物」）</summary>
public struct HitInfo
{
    /// <summary>当たった位置</summary>
    public Vector2 Point;
    /// <summary>攻撃から相手への向き（かけらの飛ぶ向き）</summary>
    public Vector2 Direction;
    public HitStrength Strength;
    /// <summary>当たった相手（ボス・雑魚・逃げるウイルス本体・プレイヤー）</summary>
    public GameObject Target;
    /// <summary>相手がプレイヤーか（エラーの絵を出す）</summary>
    public bool TargetIsPlayer;
    /// <summary>攻撃のスキン</summary>
    public SkinId Skin;
    /// <summary>エラーの絵の種類（短い名前。空でよい。Terminal の技ごとの字など）</summary>
    public string ErrorKind;
    /// <summary>攻撃データ（無いこともある）</summary>
    public AttackData Data;
    /// <summary>アプリの色（無ければ null）</summary>
    public AppTheme Theme;
    /// <summary>アプリのアイコン（無ければ null）</summary>
    public Sprite AppIcon;
}

/// <summary>
/// スキン1つ分。継いだクラスを1つのファイルに書けば、AppSkins が自動で集める（引数の無いコンストラクタが要る）。
/// ・Dress：攻撃の物に絵を付ける。付けたら true（呼んだ側が今の絵をかくす）。この枠・場面で付けないなら false（ほかのスキン・今の絵に任せる）。
///   同じ物に、場面ごとに何回か呼ばれる。物ごとの状態は、Target に自分の部品（MonoBehaviour）を足して持つ。
///   消える場面（Vanish）のすぐあとに、Target は消される。消える演出は、Target の子ではなく、別の物として出す（自分で消えるように）
/// ・Hit：プレイヤーの攻撃が敵に当たったときの絵（1-4 の「当たった絵」と「かけら」）。出したら true
/// ・Error：敵の攻撃がプレイヤーに当たったときの絵（1-4 の「エラーの絵」）。出したら true
/// ・NotificationTitle：とどめのときの隅の通知の題名（1-5。短い決まった文字。無ければ null）
/// ヒットストップ・画面のゆれ・相手の光り方・白い輪は、共通の部品（HitFx）が出す。スキンは絵だけを出す
/// </summary>
public abstract class AppSkin
{
    public abstract SkinId Id { get; }
    public virtual bool Dress(AttackFrame frame, DressPhase phase, DressInfo info) => false;
    public virtual bool Hit(HitInfo info) => false;
    public virtual bool Error(HitInfo info) => false;
    public virtual string NotificationTitle => null;
}

/// <summary>スキンを集めて引く。スキンのクラスを足すだけで使われる</summary>
public static class AppSkins
{
    static Dictionary<SkinId, AppSkin> _skins;

    /// <summary>そのスキン（無ければ null）</summary>
    public static AppSkin Get(SkinId id)
    {
        Ensure();
        return _skins.TryGetValue(id, out AppSkin s) ? s : null;
    }

    static void Ensure()
    {
        if (_skins != null) return;
        _skins = new Dictionary<SkinId, AppSkin>();
        foreach (Type t in typeof(AppSkin).Assembly.GetTypes())
        {
            if (t.IsAbstract || !typeof(AppSkin).IsAssignableFrom(t) || t.GetConstructor(Type.EmptyTypes) == null) continue;
            try
            {
                var skin = (AppSkin)Activator.CreateInstance(t);
                if (skin.Id != SkinId.None) _skins[skin.Id] = skin;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"スキン {t.Name} を作れません：{e.Message}");
            }
        }
    }
}
