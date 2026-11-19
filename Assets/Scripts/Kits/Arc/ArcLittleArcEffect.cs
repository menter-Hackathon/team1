using System.Collections;
using UnityEngine;

/// <summary>
/// 【仮】Little Arc の小窓1つ（ArcLittleArcAttack が出す）。自分で進む。
/// 赤い枠（点滅）→ 窓が開く → プレイヤーへ赤い線（固定）→ 撃つ → 閉じる（左上の赤い丸がへこんで、まん中へ縮む）
/// </summary>
public class ArcLittleArcEffect : CustomEffect
{
    AttackRunner _owner;
    AttackData _data;
    Vector2 _size;
    float _delay, _frame, _aim, _gap;
    int _shots;

    public float TotalTime => _delay + _frame + 0.15f + _aim + (_shots - 1) * _gap + 0.45f;

    internal void Setup(AttackRunner owner, AttackData data, Vector2 size, float delay, float frame, float aim, int shots, float gap)
    {
        _owner = owner;
        _data = data;
        _size = size;
        _delay = delay;
        _frame = frame;
        _aim = aim;
        _shots = Mathf.Max(1, shots);
        _gap = gap;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (_delay > 0f) yield return new WaitForSeconds(_delay);
        Vector2 c = transform.position;

        // 赤い枠
        var frame = new GameObject("Frame").transform;
        frame.SetParent(transform, false);
        Vector2 h = _size * 0.5f;
        var pts = new System.Collections.Generic.List<Vector2> { c + new Vector2(-h.x, -h.y), c + new Vector2(h.x, -h.y), c + new Vector2(h.x, h.y), c + new Vector2(-h.x, h.y) };
        PathStroke.Dashes(frame, pts, 0.3f, 0.15f, 2f * KitDress.Px, KitDress.Red, 40, true);
        float end = Time.time + _frame;
        while (Time.time < end)
        {
            frame.gameObject.SetActive(end - Time.time > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0);
            yield return null;
        }
        Destroy(frame.gameObject);

        // 窓が開く（Mac のウィンドウ。ふちは赤＝敵の物。中にグラデーションの帯）
        int wpx = Mathf.RoundToInt(_size.x * 18f), hpx = Mathf.RoundToInt(_size.y * 18f);
        Transform win = MacUi.PlaceWindow(transform, Vector2.zero, new Vector2(wpx, hpx), MacLook.Light, MacUi.Red, 39);
        var grad = RuntimeSprites.Show(win, ArcArt.Gradient(wpx - 6, hpx - 14, 4, ArcArt.Pink, ArcArt.Purple, ArcArt.Cyan, false), new Vector2(0f, -0.2f), 41, 1f, "Page");
        var lens = RuntimeSprites.Show(win, ArcArt.Orb(12), new Vector2(0f, -0.2f), 42, 1f, "Lens");
        float open = 0.15f, t0 = Time.time;
        while (Time.time - t0 < open)
        {
            float k = (Time.time - t0) / open;
            win.localScale = Vector3.one * (k < 0.7f ? Mathf.Lerp(0.2f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f));
            yield return null;
        }
        win.localScale = Vector3.one;

        // 赤い線（固定）
        Vector2 from = c + new Vector2(0f, -0.2f);
        Vector2 dir = _owner != null && _owner.TargetPoint is Vector2 tp && (tp - from).sqrMagnitude > 0.01f ? (tp - from).normalized : Vector2.down;
        KitDress.Line(_owner, from, from + dir * 24f, _aim, 2f);
        end = Time.time + _aim;
        while (Time.time < end)
        {
            if (lens != null) lens.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(Time.time * 18f));
            yield return null;
        }

        // 撃つ
        for (int i = 0; i < _shots; i++)
        {
            if (i > 0) yield return new WaitForSeconds(_gap);
            if (_owner == null) break;
            Projectile p = Projectile.Spawn(_owner, _data, from, dir, i);
            if (p != null) KitDress.Swap(p.gameObject, ArcArt.Orb(10));
            _owner.NotifyReleased(_data);
        }

        // 閉じる
        yield return new WaitForSeconds(0.15f);
        bool closed = false;
        MacUi.CloseAnim(win, () => closed = true);
        float limit = Time.time + 0.5f;
        while (!closed && Time.time < limit) yield return null;
        Destroy(gameObject);
    }
}
