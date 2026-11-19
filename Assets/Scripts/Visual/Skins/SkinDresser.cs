using System;
using System.Collections.Generic;
using UnityEngine;

// 攻撃の物にスキンの絵を付ける入口（2026-11-18。docs/app-skill-visuals-design.md 1-1）。
//
// 【決まり】（スキンを書く人へ）
// ・絵を決める順番：その攻撃のスキン（下の「スキンの決め方」）の Dress → Generic の Dress → 今の絵のまま。
//   「その能力・技だけの見た目」（新しいタブ・棒グラフなど）は、各スキンの Dress の中で info.Data の名前を見て選ぶ。
// ・最初に true を返したスキンが、その物の「持ち主」になる。あとの場面は、持ち主のスキンにだけ聞く（Generic へは回さない）。
//   1つの物の絵が、2つのスキンで混ざらないようにするため。
//   アプリのスキン（クラスがある物）が予告を断ったら、その予告は今の絵のまま（Generic へ回さない。「今の予告はそのまま」の技のため）。
//   持ち主は本体の場面で決まる。ただし予備動作の物（弾の予告だけの物）は、断られたら Generic へ回す
// ・true を返したら、その場面の今の絵（info.CurrentVisual）を Renderer.forceRenderingOff でかくす。enabled はさわらない
//   （元の部品が毎フレーム enabled を書くため。足場の最後の1秒の点滅などをまねたいときは、CurrentVisual.enabled を見る）。
// ・場面と「今の絵」（CurrentVisual）
//   弾（Projectile）     ：予告＝予備動作（攻撃する人の子に出す空の物。Target は消える直前に Vanish が来る。Count＝撃つ数の合計）
//                          本体＝撃った瞬間（弾の絵）、着地＝床・壁に当たった（1回だけ）、消える＝時間切れ・当たった・壁で消えた
//                          弾の Target は、進む向きに回る（右向きが 0°）。字を出すときは、逆さにならないように直す
//   指定位置（AreaStrike）：予告＝床の赤丸（予告の絵）、本体＝判定が始まった（判定の絵）、消える＝判定が終わった。着地は無い
//                          Target は面に沿って回る（壁なら縦）。下の端が床（面）、ローカルの上が面の外
//   落下物（FallingObject）：予告＝影（落とす物）・点線の枠（出現型）・上に並んだ（上の列。落下物の絵）
//                          本体＝落ち始めた・パッと現れた（落下物の絵）、着地＝床に着いた・足場になった（出現型）、消える＝消える直前（クリックで消したときも）
//   瞬間移動（Warp 枠）     ：現れる場所の予告（WarpTelegraph）を出す技は、予告の物が知らせる（Target＝現れる所。担当2 の WarpTelegraph の説明）。
//                          予告を出さない技（プレイヤーの新しいタブ）は AttackRunner が知らせる：予告＝消え始めた（物は元の体の中心、Direction＝行き先への向き）
//                          本体＝移った瞬間（物を行き先の体の中心へ動かす）、消える＝現れきった。新しいタブは3つが同じフレームに来る
//                          付けたら AttackRunner.WarpDressed が true になり、今の小さいウィンドウ（Vfx.TabOpen）は出さない
// ・消える場面のすぐあとに Target は消される。消える演出は、Target の子ではなく別の物として出し、Adopt で持ち主に預ける。
// ・Target の子でない飾り（予告の帯・画面の上の列など）も Adopt で預ける。形態変化（50%）・正常化（100%）で消える。
// ・スキンの中で例外が出ても、ここで止める（攻撃の動きは止めない）。
//
// スキンの決め方（SkinOf）：プレイヤーの能力として覚えた攻撃データ（Remember）→ 攻撃した人の AttackRunner.Skin →
// ボスの設定の表示名（テスト用のシーン：Chrome＝Browser、VS Code＝Dev）→ 無し（雑魚＝ウイルスは、スキンをかけない）
public static class SkinDresser
{
    /// <summary>攻撃の見た目の組（スキン・アプリの色・アプリのアイコン）</summary>
    public struct Look
    {
        public SkinId Skin;
        public AppTheme Theme;
        public Sprite Icon;
    }

    static readonly Dictionary<AttackData, Look> Remembered = new Dictionary<AttackData, Look>();
    static readonly HashSet<string> Logged = new HashSet<string>();

    /// <summary>
    /// プレイヤーの能力の攻撃データに、スキンを覚えさせる（追撃＝ボムの爆発・トラップ弾の床も）。
    /// 能力は同時に使えるので、AttackRunner.Skin（最後に使った能力）ではなく、攻撃データごとに決める
    /// </summary>
    public static void Remember(AttackData data, SkinId skin, AppTheme theme, Sprite icon)
    {
        var look = new Look { Skin = skin, Theme = theme, Icon = icon };
        for (int depth = 0; data != null && depth < 4; depth++)
        {
            Remembered[data] = look;
            data = data.OnTerrainHit;
        }
    }

    /// <summary>攻撃の見た目の組を決める（上の「スキンの決め方」）</summary>
    public static Look LookOf(AttackRunner owner, AttackData data)
    {
        if (data != null && Remembered.TryGetValue(data, out Look remembered)) return remembered;
        if (owner == null) return default;
        if (owner.Skin != SkinId.None) return new Look { Skin = owner.Skin, Theme = owner.SkinTheme, Icon = owner.SkinIcon };
        // テスト用のシーンのボス（StageDirector を通らない）：ボスの設定の表示名から
        if (owner.TryGetComponent(out BossController boss) && boss.Profile != null)
        {
            SkinId s = SkinTables.ForBossName(boss.Profile.DisplayName);
            if (s != SkinId.None) return new Look { Skin = s, Theme = owner.SkinTheme, Icon = owner.SkinIcon != null ? owner.SkinIcon : boss.Profile.Icon };
        }
        return default;
    }

    /// <summary>攻撃のスキンだけ</summary>
    public static SkinId SkinOf(AttackRunner owner, AttackData data) => LookOf(owner, data).Skin;

    /// <summary>
    /// 攻撃の物 target に、場面 phase の絵を付ける。付けたら true（current の絵をかくす）。
    /// owner のスキン（AttackRunner.Skin）と、攻撃データの名前から決めた枠で、スキンの Dress を呼ぶ
    /// </summary>
    public static bool Dress(AttackRunner owner, AttackData data, GameObject target, DressPhase phase,
        SpriteRenderer current = null, Vector2 hitSize = default, int index = 0, Vector2 direction = default)
        => Dress(owner, data, target, phase, current, hitSize, index, direction, 0, 0f);

    /// <summary>上と同じ。count：同時に出す数・連射の数、duration：この場面が続く秒数（DressInfo.Count・Duration）</summary>
    public static bool Dress(AttackRunner owner, AttackData data, GameObject target, DressPhase phase,
        SpriteRenderer current, Vector2 hitSize, int index, Vector2 direction, int count, float duration)
    {
        if (target == null || data == null) return false;
        // 敵の赤い予告が出た（音。予備動作の絵は除く）。スキンが付かない予告でも鳴らすので、ここで（2026-11-19）
        if (phase == DressPhase.Telegraph && owner != null && owner.Team == Team.Enemy && !target.TryGetComponent(out SkinWindupMarker _)) GameAudio.Telegraph();
        AttackFrame frame = SkinTables.FrameOf(data);
        if (frame == AttackFrame.None) return false;

        // 場面が変わっても同じスキンで描く（最初に決めた組を、物に覚えさせる）
        if (!target.TryGetComponent(out SkinMark mark))
        {
            mark = target.AddComponent<SkinMark>();
            mark.Look = LookOf(owner, data);
        }
        if (mark.Look.Skin == SkinId.None) return false;

        var info = new DressInfo
        {
            Target = target,
            CurrentVisual = current,
            Data = data,
            Owner = owner,
            HitSize = hitSize,
            IsEnemy = owner != null && owner.Team != Team.Player,
            Theme = mark.Look.Theme,
            AppIcon = mark.Look.Icon,
            Index = index,
            Direction = direction,
            Count = count,
            Duration = duration,
        };

        bool dressed;
        if (mark.Owner != SkinId.None)
        {
            dressed = TryDress(mark.Owner, frame, phase, info);
        }
        else
        {
            dressed = TryDress(mark.Look.Skin, frame, phase, info);
            if (dressed) mark.Owner = mark.Look.Skin;
            else if (mark.Look.Skin != SkinId.Generic)
            {
                // アプリのスキンが予告を断った：今の予告のまま（予備動作の物だけは Generic へ回す）
                bool keepTelegraph = phase == DressPhase.Telegraph && AppSkins.Get(mark.Look.Skin) != null && !target.TryGetComponent(out SkinWindupMarker _);
                if (!keepTelegraph && TryDress(SkinId.Generic, frame, phase, info))
                {
                    dressed = true;
                    mark.Owner = SkinId.Generic;
                }
            }
        }
        if (dressed && current != null) current.forceRenderingOff = true;
        return dressed;
    }

    static bool TryDress(SkinId id, AttackFrame frame, DressPhase phase, DressInfo info)
    {
        AppSkin skin = AppSkins.Get(id);
        if (skin == null) return false;
        try
        {
            return skin.Dress(frame, phase, info);
        }
        catch (Exception e)
        {
            LogOnce($"{id}.{frame}.{phase}", $"スキン {id} の Dress（{frame}・{phase}）で例外：{e}");
            return false;
        }
    }

    /// <summary>
    /// Target の子でない飾り（消える演出・予告の帯など）を、攻撃した人に預ける。
    /// 正常化（ClearSpawned）・形態変化（ClearProjectiles）で消える。自分で消えるように作ること
    /// </summary>
    public static void Adopt(DressInfo info, GameObject fx)
    {
        if (fx == null) return;
        if (!fx.TryGetComponent(out SkinFxMarker _)) fx.AddComponent<SkinFxMarker>();
        if (info.Owner != null) info.Owner.Register(fx);
    }

    /// <summary>スキンの中の例外を、同じ場所では1回だけ出す</summary>
    public static void LogOnce(string key, string message)
    {
        if (Logged.Add(key)) Debug.LogWarning(message);
    }
}

/// <summary>【仮】攻撃の物に付ける目印：決めた見た目の組と、絵を付けたスキン（持ち主）。SkinDresser だけが使う</summary>
[DisallowMultipleComponent]
public class SkinMark : MonoBehaviour
{
    public SkinDresser.Look Look;
    /// <summary>絵を付けたスキン（まだなら None）</summary>
    public SkinId Owner;
}

/// <summary>【仮】スキンが足した飾り（Target の子でない物）の目印。形態変化（AttackRunner.ClearProjectiles）でも消える</summary>
public class SkinFxMarker : MonoBehaviour { }

/// <summary>【仮】予備動作の物（弾の技の予告だけを持つ空の物。AttackRunner が作る）の目印</summary>
public class SkinWindupMarker : MonoBehaviour { }
