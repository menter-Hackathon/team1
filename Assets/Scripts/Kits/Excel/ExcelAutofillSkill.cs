using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】能力「オートフィル」（Excel。2026-11-19。docs/app-kits-1-design.md 10章）。数値はすべて【仮】。
/// 正面へ、セルが1マスずつ 8マスまで伸びる（1マス 0.05秒。高さ 1.2）。セルに 1〜8。
/// 伸びた行の敵すべてに当たる（雑魚は倒れる）。ボスのゲージは、当たったセルの数字 × 2（目の前 +2、8マス先 +16）。
/// 遠くから当てるほど強い、ただ1つの能力。相手ごとに、最初に触れたセルの数字で1回だけ当たる。
/// ゲージの「×2」は攻撃データのゲージ（2）× 数字。ダメージ・ノックバックは攻撃データ
/// </summary>
[CreateAssetMenu(fileName = "Excel_AutofillSkill", menuName = "Team1/Kits/Excel Autofill Skill")]
public class ExcelAutofillSkill : CustomAttack
{
    [SerializeField, Min(1)] int _cells = 8;
    [Tooltip("1セル伸びる秒数")]
    [SerializeField, Min(0.01f)] float _cellTime = 0.05f;
    [Tooltip("セルの幅（マス）")]
    [SerializeField, Min(0.3f)] float _cellWidth = 1f;
    [Tooltip("行の高さ（マス）")]
    [SerializeField, Min(0.3f)] float _height = 1.2f;

    public int Cells => _cells;
    public float CellTime => _cellTime;
    public float CellWidth => _cellWidth;
    public float Height => _height;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        ExcelAutofillSkillEffect.Spawn(owner, data, this);
        owner.NotifyReleased(data);
        yield return new WaitForSeconds(_cells * _cellTime);
    }
}

/// <summary>能力のオートフィルの行（ExcelAutofillSkill が出す）。自分で進んで消える</summary>
public class ExcelAutofillSkillEffect : MonoBehaviour
{
    const int CellOrder = 24, LineOrder = 25, TextOrder = 26;
    const float Thick = 2f / 18f;

    AttackRunner _owner;
    AttackData _data;
    ExcelAutofillSkill _skill;
    int _dir;
    float _startX, _bottom;
    readonly List<SpriteRenderer> _cells = new List<SpriteRenderer>();
    readonly List<TextMesh> _labels = new List<TextMesh>();
    readonly HashSet<IDamageable> _hit = new HashSet<IDamageable>();
    readonly List<Hurtbox> _buffer = new List<Hurtbox>();
    SpriteRenderer[] _frame;
    SpriteRenderer _handle;

    public static ExcelAutofillSkillEffect Spawn(AttackRunner owner, AttackData data, ExcelAutofillSkill skill)
    {
        var go = new GameObject("Excel_AutofillSkill");
        go.layer = owner.gameObject.layer;
        go.transform.position = owner.transform.position;
        var e = go.AddComponent<ExcelAutofillSkillEffect>();
        e._owner = owner;
        e._data = data;
        e._skill = skill;
        e._dir = owner.Facing >= 0 ? 1 : -1;
        Vector2 feet = owner.transform.position;
        e._startX = feet.x + e._dir * 0.35f;
        e._bottom = feet.y - 0.05f;
        e._frame = ExcelArt.Frame(go.transform, ExcelArt.Green, Thick, LineOrder);
        e._handle = FxKit.Box(go.transform, Vector2.zero, new Vector2(5f / 18f, 5f / 18f), ExcelArt.Green, LineOrder + 1, "Handle");
        e.StartCoroutine(e.Run());
        return e;
    }

    Rect CellRect(int i)
    {
        float w = _skill.CellWidth;
        float a = _startX + _dir * i * w, b = a + _dir * w;
        return Rect.MinMaxRect(Mathf.Min(a, b), _bottom, Mathf.Max(a, b), _bottom + _skill.Height);
    }

    IEnumerator Run()
    {
        Rect area = OfficeKitTools.PlayerArea(_owner);
        Vector2 size = new Vector2(_skill.CellWidth, _skill.Height);
        Sprite cell = ExcelArt.Cell(Mathf.RoundToInt(size.x * 18f), Mathf.RoundToInt(size.y * 18f));
        int made = 0;
        for (int i = 0; i < _skill.Cells; i++)
        {
            Rect r = CellRect(i);
            // 部屋の端・壁で止まる
            if (r.xMin < area.xMin - 0.05f || r.xMax > area.xMax + 0.05f) break;
            if (Physics2D.OverlapBox(r.center, r.size * 0.6f, 0f, _owner.SolidMask) != null) break;

            SpriteRenderer sr = AttackRunner.AddVisual(transform, cell, new Color(1f, 1f, 1f, 0.92f), size, CellOrder, "Cell");
            sr.transform.position = r.center;
            _cells.Add(sr);
            TextMesh tm = WorldText.Create(transform, (i + 1).ToString(), 0.4f, ExcelArt.Ink, TextOrder);
            tm.transform.position = new Vector3(r.center.x, r.center.y, -0.01f);
            _labels.Add(tm);
            made = i + 1;
            Rect filled = Rect.MinMaxRect(Mathf.Min(CellRect(0).xMin, r.xMin), r.yMin, Mathf.Max(CellRect(0).xMax, r.xMax), r.yMax);
            ExcelArt.PlaceFrame(_frame, filled, Thick);
            if (_handle != null) _handle.transform.position = new Vector2(_dir > 0 ? filled.xMax : filled.xMin, filled.yMin);

            // このセルに入った、まだ当てていない相手に、セルの数字で1回だけ当てる
            OfficeKitTools.FindTargets(_owner, r, _buffer);
            foreach (var hb in _buffer)
            {
                if (!_hit.Add(hb.Owner)) continue;
                int gauge = Mathf.Max(1, _data.GaugeGain) * (i + 1);
                OfficeKitTools.HitOnce(_owner, OfficeKitTools.WithGauge(_data, gauge), hb);
                // 当たったセルは緑に光る
                FxKit.Text($"+{gauge}", r.center + Vector2.up * 0.9f, 0.34f, ExcelArt.GreenLight, 27, 0.5f).Rise(0.6f).Pop();
            }
            yield return new WaitForSeconds(_skill.CellTime);
        }
        if (made == 0) { Destroy(gameObject); yield break; }

        // 少し見せてから、うすくなって消える
        yield return new WaitForSeconds(0.25f);
        float end = Time.time + 0.25f;
        while (Time.time < end)
        {
            float a = (end - Time.time) / 0.25f;
            foreach (var c in _cells) if (c != null) c.color = new Color(1f, 1f, 1f, 0.92f * a);
            foreach (var l in _frame) if (l != null) l.color = OfficeKitTools.Fade(ExcelArt.Green, a);
            if (_handle != null) _handle.color = OfficeKitTools.Fade(ExcelArt.Green, a);
            foreach (var t in _labels) if (t != null) WorldText.SetColor(t, OfficeKitTools.Fade(ExcelArt.Ink, a));
            yield return null;
        }
        Destroy(gameObject);
    }
}
