using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】オートフィル（Excel のボスの技 P1-1。2026-11-19。docs/app-kits-1-design.md 10章）。数値はすべて【仮】。
/// ボスがセルを1つ選ぶ（緑の枠）→ 右下の ■ を引っぱる：枠が1セルずつ伸びて 1・2・3… が入る（最初から行の全部に赤い細い下線）
/// → 伸びきったら枠が赤に変わり、セルが #REF! になる（0.8秒）→ 行の全部が判定 0.4秒（高さ 1.2）。
/// ・床の行（床から 0〜1.2）：予告は合わせて 2.5秒（伸びる 1.7＋赤 0.8）。足場に乗れば当たらない
/// ・足場の行（床から 3〜4.2）：予告は合わせて 1.2秒（伸びる 0.4＋赤 0.8）。床にいれば当たらない
/// ・第1形態：床の行と足場の行を交互。第2形態：「行 → 列 → 行」（列＝プレイヤーの位置に縦の帯 幅 2.5×天井まで）
/// </summary>
[CreateAssetMenu(fileName = "Excel_Autofill", menuName = "Team1/Kits/Excel Autofill")]
public class ExcelAutofillAttack : CustomAttack
{
    [Tooltip("オン：第2形態の並び（行 → 列 → 行）")]
    [SerializeField] bool _phase2;
    [Header("行（マス・秒）")]
    [SerializeField, Min(0.5f)] float _cellWidth = 2f;
    [SerializeField, Min(0.3f)] float _rowHeight = 1.2f;
    [Tooltip("足場の行の下の端（床から）")]
    [SerializeField] float _platformRowY = 3f;
    [Tooltip("床の行の伸びる時間（赤と合わせて 2.5秒。足場の上だけ安全なので長い）")]
    [SerializeField, Min(0f)] float _floorExtend = 1.7f;
    [Tooltip("足場の行の伸びる時間（赤と合わせて 1.2秒）")]
    [SerializeField, Min(0f)] float _platformExtend = 0.4f;
    [Header("列（第2形態）")]
    [SerializeField, Min(0.5f)] float _columnWidth = 2.5f;
    [SerializeField, Min(0f)] float _columnExtend = 0.6f;
    [Header("共通")]
    [Tooltip("赤に変わってから判定まで（短くしない）")]
    [SerializeField, Min(0.8f)] float _redTime = 0.8f;
    [SerializeField, Min(0.05f)] float _hitTime = 0.4f;
    [Tooltip("並びの中の、1つ終わってから次までの間")]
    [SerializeField, Min(0f)] float _gap = 0.2f;

    [System.NonSerialized] bool _nextPlatform; // 第1形態の交互

    public float RedTime => _redTime;
    public float HitTime => _hitTime;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        var steps = new List<ExcelFillStrip.Kind>();
        bool platform = _nextPlatform;
        _nextPlatform = !_nextPlatform;
        steps.Add(platform ? ExcelFillStrip.Kind.PlatformRow : ExcelFillStrip.Kind.FloorRow);
        if (_phase2)
        {
            steps.Add(ExcelFillStrip.Kind.Column);
            steps.Add(platform ? ExcelFillStrip.Kind.FloorRow : ExcelFillStrip.Kind.PlatformRow);
        }
        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0 && _gap > 0f) yield return new WaitForSeconds(_gap);
            ExcelFillStrip strip = Spawn(owner, data, steps[i]);
            if (strip == null) yield break;
            float timeout = Time.time + 6f;
            while (strip != null && Time.time < timeout) yield return null;
        }
    }

    ExcelFillStrip Spawn(AttackRunner owner, AttackData data, ExcelFillStrip.Kind kind)
    {
        Rect area = OfficeKitTools.Area(owner, out float floor);
        float bossX = owner.OriginPoint.x;
        int dir = bossX <= area.center.x ? 1 : -1; // ボスに近い端から伸ばす
        switch (kind)
        {
            case ExcelFillStrip.Kind.FloorRow:
                return ExcelFillStrip.Spawn(owner, data, new Rect(area.xMin, floor, area.width, _rowHeight), false, dir, _cellWidth, _floorExtend, _redTime, _hitTime);
            case ExcelFillStrip.Kind.PlatformRow:
                return ExcelFillStrip.Spawn(owner, data, new Rect(area.xMin, floor + _platformRowY, area.width, _rowHeight), false, dir, _cellWidth, _platformExtend, _redTime, _hitTime);
            default:
                float px = owner.TargetPoint?.x ?? area.center.x;
                px = Mathf.Clamp(px, area.xMin + _columnWidth * 0.5f, area.xMax - _columnWidth * 0.5f);
                float top = Mathf.Max(floor + 4f, area.yMax);
                return ExcelFillStrip.Spawn(owner, data, new Rect(px - _columnWidth * 0.5f, floor, _columnWidth, top - floor), true, 1, _rowHeight, _columnExtend, _redTime, _hitTime);
        }
    }
}

/// <summary>オートフィルの帯1本（行か列）。ExcelAutofillAttack が出す。自分で進む。形態変化・正常化で消える</summary>
public class ExcelFillStrip : CustomEffect
{
    public enum Kind { FloorRow, PlatformRow, Column }

    const int CellOrder = 16, LineOrder = 17, TextOrder = 18;
    const float Thick = 2f / 18f;

    AttackRunner _owner;
    AttackData _data;
    Rect _rect;
    bool _vertical;
    int _dir;
    int _n;
    float _cellLen;
    float _extend, _red, _hit;
    readonly List<SpriteRenderer> _cells = new List<SpriteRenderer>();
    readonly List<TextMesh> _labels = new List<TextMesh>();
    SpriteRenderer[] _frame;
    SpriteRenderer _handle;
    readonly List<SpriteRenderer> _underlines = new List<SpriteRenderer>();
    Hitbox _hitbox;

    /// <summary>判定を出しているか（テスト用）</summary>
    public bool IsHitActive => _hitbox != null && _hitbox.IsActive;

    public static ExcelFillStrip Spawn(AttackRunner owner, AttackData data, Rect rect, bool vertical, int dir, float cellLen, float extend, float red, float hit)
    {
        if (owner == null || data == null) return null;
        GameObject go = owner.CreateSpawnedObject($"Excel_Fill_{data.name}", rect.center);
        var s = go.AddComponent<ExcelFillStrip>();
        s._owner = owner;
        s._data = data;
        s._rect = rect;
        s._vertical = vertical;
        s._dir = dir >= 0 ? 1 : -1;
        float length = vertical ? rect.height : rect.width;
        s._n = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(0.3f, cellLen)));
        s._cellLen = length / s._n;
        s._extend = extend;
        s._red = red;
        s._hit = hit;
        s.Build(go);
        s.StartCoroutine(s.Run());
        return s;
    }

    void Build(GameObject go)
    {
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = _rect.size;
        _hitbox = _owner.AddHitbox(go, _data, col);

        // 最初から、帯の全部に赤い細い下線（列は両わきの線）
        if (_vertical)
        {
            _underlines.Add(FxKit.Box(transform, Vector2.zero, Vector2.one, KitDress.Red, LineOrder, "Underline"));
            _underlines.Add(FxKit.Box(transform, Vector2.zero, Vector2.one, KitDress.Red, LineOrder, "Underline"));
            ExcelArt.SetBox(_underlines[0], new Vector2(_rect.xMin + Thick * 0.5f, _rect.center.y), new Vector2(Thick, _rect.height));
            ExcelArt.SetBox(_underlines[1], new Vector2(_rect.xMax - Thick * 0.5f, _rect.center.y), new Vector2(Thick, _rect.height));
        }
        else
        {
            _underlines.Add(FxKit.Box(transform, Vector2.zero, Vector2.one, KitDress.Red, LineOrder, "Underline"));
            ExcelArt.SetBox(_underlines[0], new Vector2(_rect.center.x, _rect.yMin + Thick * 0.5f), new Vector2(_rect.width, Thick));
        }

        Vector2 cellSize = _vertical ? new Vector2(_rect.width, _cellLen) : new Vector2(_cellLen, _rect.height);
        Sprite cell = ExcelArt.Cell(Mathf.RoundToInt(cellSize.x * 18f), Mathf.RoundToInt(cellSize.y * 18f));
        for (int i = 0; i < _n; i++)
        {
            Vector2 c = CellCenter(i);
            SpriteRenderer sr = AttackRunner.AddVisual(transform, cell, new Color(1f, 1f, 1f, 0.88f), cellSize, CellOrder, "Cell");
            sr.transform.position = c;
            sr.enabled = false;
            _cells.Add(sr);
            TextMesh tm = WorldText.Create(transform, (i + 1).ToString(), 0.36f, ExcelArt.Ink, TextOrder);
            tm.transform.position = new Vector3(c.x, c.y, -0.01f);
            tm.gameObject.SetActive(false);
            _labels.Add(tm);
        }
        _frame = ExcelArt.Frame(transform, ExcelArt.Green, Thick, LineOrder);
        _handle = FxKit.Box(transform, Vector2.zero, new Vector2(5f / 18f, 5f / 18f), ExcelArt.Green, LineOrder + 1, "Handle");
        SetFrameVisible(false);
    }

    Vector2 CellCenter(int i)
    {
        if (_vertical) return new Vector2(_rect.center.x, _rect.yMin + (i + 0.5f) * _cellLen);
        float start = _dir > 0 ? _rect.xMin : _rect.xMax;
        return new Vector2(start + _dir * (i + 0.5f) * _cellLen, _rect.center.y);
    }

    Rect Filled(int k)
    {
        k = Mathf.Clamp(k, 1, _n);
        if (_vertical) return new Rect(_rect.xMin, _rect.yMin, _rect.width, k * _cellLen);
        float start = _dir > 0 ? _rect.xMin : _rect.xMax;
        float end = start + _dir * k * _cellLen;
        return Rect.MinMaxRect(Mathf.Min(start, end), _rect.yMin, Mathf.Max(start, end), _rect.yMax);
    }

    void SetFrameVisible(bool on)
    {
        foreach (var l in _frame) if (l != null) l.enabled = on;
        if (_handle != null) _handle.enabled = on;
    }

    void PlaceFrame(int k)
    {
        Rect r = Filled(k);
        ExcelArt.PlaceFrame(_frame, r, Thick);
        // 右下の ■（伸びる先の、下の角。列は上の角）
        Vector2 corner = _vertical
            ? new Vector2(r.xMax, r.yMax)
            : new Vector2(_dir > 0 ? r.xMax : r.xMin, r.yMin);
        if (_handle != null) _handle.transform.position = corner;
    }

    IEnumerator Run()
    {
        // 1. 選ぶ → 伸びる
        float start = Time.time;
        int shown = 0;
        SetFrameVisible(true);
        while (true)
        {
            if (OfficeKitTools.OwnerStopped(_owner, _data)) { Destroy(gameObject); yield break; }
            float t = _extend > 0f ? (Time.time - start) / _extend : 1f;
            int k = Mathf.Clamp(1 + Mathf.FloorToInt(t * _n), 1, _n);
            while (shown < k)
            {
                _cells[shown].enabled = true;
                _labels[shown].gameObject.SetActive(true);
                shown++;
            }
            PlaceFrame(shown);
            if (t >= 1f) break;
            yield return null;
        }

        // 2. 赤に変わる（セルは #REF!）
        ExcelArt.TintFrame(_frame, KitDress.Red);
        if (_handle != null) _handle.color = KitDress.Red;
        foreach (var tm in _labels)
        {
            tm.text = "#REF!";
            tm.characterSize *= 0.8f;
            WorldText.SetColor(tm, ExcelArt.ErrorRed);
        }
        _owner.NotifyReleased(_data);
        float redEnd = Time.time + _red;
        while (Time.time < redEnd)
        {
            if (OfficeKitTools.OwnerStopped(_owner, _data)) { Destroy(gameObject); yield break; }
            bool on = redEnd - Time.time > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            SetFrameVisible(on);
            yield return null;
        }
        SetFrameVisible(true);

        // 3. 判定
        foreach (var c in _cells) c.color = new Color(1f, 0.55f, 0.5f, 0.95f);
        foreach (var u in _underlines) u.enabled = false;
        _hitbox.Activate();
        float hitEnd = Time.time + _hit;
        while (Time.time < hitEnd) yield return null;
        _hitbox.Deactivate();

        // 4. うすくなって消える
        float fadeEnd = Time.time + 0.15f;
        while (Time.time < fadeEnd)
        {
            float a = (fadeEnd - Time.time) / 0.15f;
            foreach (var c in _cells) c.color = new Color(1f, 0.55f, 0.5f, 0.95f * a);
            yield return null;
        }
        Destroy(gameObject);
    }
}
