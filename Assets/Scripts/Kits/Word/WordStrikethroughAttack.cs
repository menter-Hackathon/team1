using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】取り消し線（Word のボスの技 P1-1。2026-11-19。docs/app-kits-1-design.md 12章）。数値はすべて【仮】。
/// 部屋の端から端まで、うすい灰色の文の行が出る → 赤い細い線の予告（0.8秒）→ 太い赤い取り消し線が「シュッ」と引かれる
/// （太さ 0.5。判定は 0.15秒だけ）。これを3回（間 0.9秒）。
/// 行の高さは2つ：低（床から 0.6）と高（床から 3.6）。第1形態はいつも 低 → 高 → 低。第2形態は 5回で、順番が毎回変わる。
/// 避け方：低＝線が引かれる瞬間にジャンプしている。高＝床にいて、ジャンプしない（足場の上は当たる）。ジャンプ・待つ・ジャンプ
/// </summary>
[CreateAssetMenu(fileName = "Word_Strikethrough", menuName = "Team1/Kits/Word Strikethrough")]
public class WordStrikethroughAttack : CustomAttack
{
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("オン：順番を毎回変える（第2形態）。オフ：低 → 高 → 低 …")]
    [SerializeField] bool _random;
    [Tooltip("1本ずつの間（秒）")]
    [SerializeField, Min(0.2f)] float _interval = 0.9f;
    [Tooltip("赤い細い線の予告（秒。短くしない）")]
    [SerializeField, Min(0.8f)] float _telegraph = 0.8f;
    [Tooltip("判定の時間（秒）")]
    [SerializeField, Min(0.05f)] float _hitTime = 0.15f;
    [Tooltip("線の太さ（マス）")]
    [SerializeField, Min(0.1f)] float _thickness = 0.5f;
    [SerializeField] float _lowY = 0.6f;
    [SerializeField] float _highY = 3.6f;

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        var heights = new List<float>();
        for (int i = 0; i < _count; i++)
        {
            bool low = _random ? Random.value < 0.5f : i % 2 == 0;
            heights.Add(low ? _lowY : _highY);
        }
        if (_random && _count >= 2 && heights.TrueForAll(h => Mathf.Approximately(h, heights[0])))
            heights[_count / 2] = Mathf.Approximately(heights[0], _lowY) ? _highY : _lowY; // 全部同じにはしない

        Rect area = OfficeKitTools.Area(owner, out float floor);
        var lines = new List<WordStrikeLine>();
        for (int i = 0; i < heights.Count; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_interval);
            if (OfficeKitTools.OwnerStopped(owner, data)) yield break;
            int dir = Random.value < 0.5f ? 1 : -1;
            var line = WordStrikeLine.Spawn(owner, data, area, floor + heights[i], _thickness, _telegraph, _hitTime, dir);
            if (line != null) lines.Add(line);
        }
        float timeout = Time.time + _telegraph + _hitTime + 1f;
        while (Time.time < timeout && lines.Exists(l => l != null)) yield return null;
    }
}

/// <summary>取り消し線1本（WordStrikethroughAttack が出す）。自分で進む。形態変化・正常化で消える</summary>
public class WordStrikeLine : CustomEffect
{
    AttackRunner _owner;
    AttackData _data;
    Rect _area;
    float _y, _thick, _telegraph, _hit;
    int _dir;
    SpriteRenderer _text, _thin, _bold;
    Hitbox _hitbox;

    public bool IsHitActive => _hitbox != null && _hitbox.IsActive;

    public static WordStrikeLine Spawn(AttackRunner owner, AttackData data, Rect area, float y, float thick, float telegraph, float hit, int dir)
    {
        if (owner == null || data == null) return null;
        GameObject go = owner.CreateSpawnedObject($"Word_Strike_{data.name}", new Vector2(area.center.x, y));
        var l = go.AddComponent<WordStrikeLine>();
        l._owner = owner;
        l._data = data;
        l._area = area;
        l._y = y;
        l._thick = thick;
        l._telegraph = telegraph;
        l._hit = hit;
        l._dir = dir >= 0 ? 1 : -1;
        l.Build(go);
        l.StartCoroutine(l.Run());
        return l;
    }

    void Build(GameObject go)
    {
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(_area.width, _thick);
        _hitbox = _owner.AddHitbox(go, _data, col);
        // うすい灰色の文の行（線の高さに、行の真ん中）
        float rowH = 0.6f;
        _text = AttackRunner.AddVisual(transform, WordArt.TextRow(Mathf.RoundToInt(_area.width * 18f), Mathf.RoundToInt(rowH * 18f)),
            OfficeKitTools.Fade(WordArt.TextGrey, 0.75f), new Vector2(_area.width, rowH), 16, "TextRow");
        _thin = FxKit.Box(transform, Vector2.zero, new Vector2(_area.width, 2f / 18f), KitDress.Red, 17, "Telegraph");
        _bold = FxKit.Box(transform, Vector2.zero, new Vector2(_area.width, _thick), WordArt.Red, 19, "Strike");
        _bold.enabled = false;
    }

    IEnumerator Run()
    {
        // 1. 予告（赤い細い線。最後の 0.3秒は点滅）
        float end = Time.time + _telegraph;
        while (Time.time < end)
        {
            if (OfficeKitTools.OwnerStopped(_owner, _data)) { Destroy(gameObject); yield break; }
            _thin.enabled = end - Time.time > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            yield return null;
        }
        _thin.enabled = false;

        // 2. 「シュッ」：判定は線の全部に 0.15秒。絵は端から端へ 0.06秒で引く
        _owner.NotifyReleased(_data);
        _hitbox.Activate();
        _bold.enabled = true;
        float sweep = 0.06f, start = Time.time, hitEnd = Time.time + _hit;
        while (Time.time < hitEnd)
        {
            float k = Mathf.Clamp01((Time.time - start) / sweep);
            float w = Mathf.Max(0.05f, _area.width * k);
            float from = _dir > 0 ? _area.xMin : _area.xMax;
            ExcelArt.SetBox(_bold, new Vector2(from + _dir * w * 0.5f, _y), new Vector2(w, _thick));
            yield return null;
        }
        _hitbox.Deactivate();
        ExcelArt.SetBox(_bold, new Vector2(_area.center.x, _y), new Vector2(_area.width, _thick * 0.5f));

        // 3. 取り消された行が残って、うすくなる
        float fade = 0.3f, fEnd = Time.time + fade;
        while (Time.time < fEnd)
        {
            float a = (fEnd - Time.time) / fade;
            _bold.color = OfficeKitTools.Fade(WordArt.Red, a);
            _text.color = OfficeKitTools.Fade(WordArt.TextGrey, 0.75f * a);
            yield return null;
        }
        Destroy(gameObject);
    }
}
