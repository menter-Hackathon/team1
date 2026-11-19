using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】⌘D 複製（Figma の第2形態の技。2026-11-19。docs/app-kits-2-design.md 2-4）。
/// ボスの前に紫のひし形（コンポーネント）。赤い線で向きを 0.8秒 → 撃つ → 0.5秒ごとに ⌘D の印が出て、弾が 2つ、4つに分かれる（扇 20°・40°）。
/// 弾の数値（速さ・大きさ・寿命）は攻撃データの弾の欄。ダメージは共通の欄
/// </summary>
[CreateAssetMenu(fileName = "Figma_Duplicate", menuName = "Team1/Kits/Figma Duplicate")]
public class FigmaDuplicateAttack : CustomAttack
{
    [Tooltip("赤い線の予告（0.8 以上）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("分かれるまでの間（秒）")]
    [SerializeField, Min(0.1f)] float _splitInterval = 0.5f;
    [Tooltip("分かれる回数（2 で 1 → 2 → 4）")]
    [SerializeField, Min(0)] int _splits = 2;
    [Tooltip("1回目・2回目に分かれたときの扇の角度（度）")]
    [SerializeField] float _fan1 = 20f;
    [SerializeField] float _fan2 = 40f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        Vector2 origin = owner.OriginPoint + new Vector2(owner.Facing * 1.2f, 0f);
        Vector2 dir = new Vector2(owner.Facing, 0f);
        if (owner.TargetPoint is Vector2 t && (t - origin).sqrMagnitude > 0.01f) dir = (t - origin).normalized;

        // 予告：ひし形と赤い線
        Transform deco = PathStroke.Decor(owner, origin, _telegraph, "FigmaDiamond");
        var dia = RuntimeSprites.Show(deco, FigmaArt.Diamond, Vector2.zero, 44, 1.6f, "Diamond");
        KitDress.Line(owner, origin, origin + dir * 16f, _telegraph, 2f);
        float end = Time.time + _telegraph;
        while (Time.time < end)
        {
            if (dia != null) dia.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 12f) * 8f);
            yield return null;
        }

        // 撃つ
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        var shots = new List<Projectile> { Fire(owner, data, origin, baseAngle, 0) };
        owner.NotifyReleased(data);
        for (int level = 1; level <= _splits; level++)
        {
            yield return new WaitForSeconds(_splitInterval);
            int n = 1 << level;
            float fan = level == 1 ? _fan1 : level == 2 ? _fan2 : _fan2 * level * 0.5f;
            var next = new List<Projectile>(n);
            for (int i = 0; i < n; i++)
            {
                Projectile parent = shots[Mathf.Min(i / 2, shots.Count - 1)];
                if (parent == null) { next.Add(null); continue; }
                float a = baseAngle - fan * 0.5f + fan * i / (n - 1);
                next.Add(Fire(owner, data, parent.transform.position, a, i));
            }
            foreach (var p in shots)
            {
                if (p == null) continue;
                FxAnim.Sprite(FigmaArt.CmdD, (Vector2)p.transform.position + new Vector2(0f, 0.7f), 47, 0.45f);
                FxAnim.Sprite(PlaceholderSprites.Square, p.transform.position, 46, 0.2f, new Color(FigmaArt.Purple.r, FigmaArt.Purple.g, FigmaArt.Purple.b, 0.8f), 0.5f);
                Destroy(p.gameObject);
            }
            shots = next;
        }
        yield return new WaitForSeconds(0.2f);
    }

    static Projectile Fire(AttackRunner owner, AttackData data, Vector2 pos, float angle, int index)
    {
        Vector2 d = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        Projectile p = Projectile.Spawn(owner, data, pos, d, index);
        if (p != null)
        {
            KitDress.Swap(p.gameObject, FigmaArt.Diamond);
            SpriteRenderer v = KitDress.FindVisual(p.transform);
            if (v != null) v.transform.rotation = Quaternion.identity; // ひし形は回さない
        }
        return p;
    }
}
