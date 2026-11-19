using System.Collections;
using UnityEngine;

/// <summary>
/// 専用の技の中身（AttackKind.Custom。2026-11-18：Finder・Premiere Pro・Terminal の技、zip圧縮など。docs/app-boss-skills-design.md）。
/// 技ごとにこのクラスを継いだ ScriptableObject を1つ作り、技の数値もそこに持つ。AttackData の「専用の技」の欄から指す。
///
/// 作り方の決まり
/// ・Run の中で出す物は owner.CreateSpawnedObject で作り（ClearSpawned で消えるように）、自分で進む部品（CustomEffect を継ぐ）に任せる。
///   Run 自体は、技の長さだけ待つ（ボスは Run が終わるまで次の技を始めない）
/// ・地形・プレイヤー・ボスの状態を変える技は、その部品の Restore で必ず元に戻す（形態変化・正常化・技の途中で止められたときのため）
/// ・ダメージ・ノックバック・色などの共通の数値は、AttackData の共通の欄を使ってよい
/// </summary>
public abstract class CustomAttack : ScriptableObject
{
    /// <summary>今使えるか（使えなければ false。能力ならクールタイムも始まらない）</summary>
    public virtual bool CanRun(AttackRunner owner, AttackData data) => true;

    /// <summary>技の本体。AttackRunner のコルーチンの中で進む（ボスが止められると、途中で止まる）</summary>
    public abstract IEnumerator Run(AttackRunner owner, AttackData data);

    /// <summary>
    /// 能力用：使ったあと、まだクールタイムを始めないか（true なら始めない）。
    /// 2回押しの能力（zip圧縮）で、1回目（圧縮）のあとに true を返す。PlayerSkillExecutor が Run の直後に聞く
    /// </summary>
    public virtual bool DefersCooldown(AttackRunner owner) => false;

    /// <summary>能力用：HUD の枠に出す絵を変える（null ならいつもの能力のアイコン）。次に投げるエフェクトアイコンなど</summary>
    public virtual Sprite DisplayIcon(AttackRunner owner) => null;

    /// <summary>能力用：もう一度押せる状態か（HUD の枠のふちを光らせる。zip を持っている間など）</summary>
    public virtual bool IsCharged(AttackRunner owner) => false;

    /// <summary>能力用：HUD の枠を光らせる色（IsCharged のとき）</summary>
    public virtual Color ChargedColor => new Color32(0x0a, 0x84, 0xff, 0xff);
}
