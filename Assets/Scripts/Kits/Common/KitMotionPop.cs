using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】その場に出る物（KitMotion.Pop が作る。2026-11-19）。担当0 の物。
/// ・予告：点線の丸＋うすい丸（危ない物は赤）→ 奥から大きくなって出る（0.15秒）→ life 秒 → 縮んで消える（0.12秒）
/// ・輪の弾：出た時（ringAtEnd なら消える時。消える前の 0.6秒は赤く点滅）に ringCount 個。Burst で好きな時にも出せる（Burst は予告なし）
/// ・弾が全部消えるまで、この物は残る（弾はこの物の子）
/// </summary>
public class KitMotionPop : KitMotionItem
{
    const float GrowTime = 0.15f, ShrinkTime = 0.12f;

    float _life, _ringSpeed, _hitTime, _size;
    int _ringCount;
    bool _ringAtEnd, _appeared, _closing, _gone;
    Color _bulletColor;
    readonly List<KitMotionMover> _bullets = new List<KitMotionMover>();

    /// <summary>出ている（大きくなってから縮むまで）間か</summary>
    public bool IsOut => _appeared && !_closing && !IsDone;

    internal void Setup(Vector2 point, float warn, float life, int ringCount, float ringSpeed, bool ringAtEnd, float hitTime,
        Sprite sprite, Color color, int damage, float size, Color warnColor, Vector2? box)
    {
        Warn = warn;
        _life = life;
        _ringCount = ringCount;
        _ringSpeed = ringSpeed;
        _ringAtEnd = ringAtEnd;
        _hitTime = hitTime;
        _size = size;
        _bulletColor = damage > 0 || ringCount > 0 ? Color.Lerp(color, KitMotion.Red, 0.5f) : color;
        transform.position = point;
        Color faint = new Color(warnColor.r, warnColor.g, warnColor.b, 0.22f);
        if (warn > 0f && box.HasValue)
        {
            // 四角い物（ウィンドウ・板）：点線の四角い枠
            KitMotionArt.DashedFrame(transform, point, box.Value, warnColor, OrderWarn, warn);
            KitMotionMark.Create(transform, PlaceholderSprites.Square, point, box.Value, 0f, faint, OrderWarn - 1, warn, "Fill");
        }
        else if (warn > 0f)
        {
            int d = Mathf.Clamp(Mathf.RoundToInt(size * 18f), 6, 64);
            KitMotionMark.Create(transform, KitMotionArt.Ring(d, true), point, Vector2.one * size, 0f, warnColor, OrderWarn, warn, "Ring");
            KitMotionMark.Create(transform, KitMotionArt.Disc(d), point, Vector2.one * size, 0f, faint, OrderWarn - 1, warn, "Disc");
        }
        if (box.HasValue) MakeBody(sprite, color, box.Value, false, damage, 0.85f);
        else MakeBody(sprite, color, Vector2.one * size, sprite == null, damage, 0.8f);
        Body.localScale = Vector3.one * 0.01f; // 0 にしない（判定の大きさが 0 にならないように）
        Visual.enabled = false;
    }

    /// <summary>
    /// 今すぐ、輪の弾を count 個ばらまく（上から等しい角度。速さ speed・ダメージ damage）。
    /// 予告は付かないので、出す前に見せる物（点滅・ringAtEnd の赤い点滅など）を呼ぶ側で用意する
    /// </summary>
    public void Burst(int count, float speed = 6f, int damage = 1, float bulletSize = 0.4f)
    {
        if (IsDone || count <= 0) return;
        Rect area = KitMotion.Area;
        Vector2 c = Body != null ? (Vector2)Body.position : (Vector2)transform.position;
        for (int i = 0; i < count; i++)
        {
            float a = (90f + 360f * i / count) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            KitPath path = KitPath.Out(c, dir, speed, area, bulletSize + 0.5f);
            var m = KitMotion.Make<KitMotionMover>(Owner, this, "RingShot", c);
            m.BuildBody(null, _bulletColor, Vector2.one * bulletSize, true, damage);
            m.Setup(path, 0f, false);
            _bullets.Add(m);
        }
        FxKit.Ring(c, _size * 1.2f, 0.2f, Color.white);
    }

    /// <summary>早く閉じる（縮んで消える。ringAtEnd なら輪も出る）</summary>
    public void Close()
    {
        if (IsDone || _closing) return;
        if (!_appeared) { Cancel(); return; }
        _life = Mathf.Min(_life, Age - Warn);
    }

    protected override bool Step(float age)
    {
        _bullets.RemoveAll(b => b == null || b.IsDone);
        if (age < Warn) return true;
        float t = age - Warn;
        if (!_appeared)
        {
            _appeared = true;
            if (Visual != null) Visual.enabled = true;
            SetHarm(true);
            GameAudio.Event("popup_open");
            if (_ringCount > 0 && !_ringAtEnd) Burst(_ringCount, _ringSpeed);
        }
        if (_gone) return _bullets.Count > 0;
        if (Body == null) return _bullets.Count > 0;
        if (t < GrowTime)
        {
            float k = t / GrowTime;
            Body.localScale = Vector3.one * Mathf.Max(0.01f, k < 0.7f ? k / 0.7f * 1.15f : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f));
        }
        else if (t < _life)
        {
            Body.localScale = Vector3.one;
            if (_hitTime > 0f && t > _hitTime) SetHarm(false);
            // 消える時の輪の予告：最後の 0.6秒（出ている時間より長くしない）は赤く点滅
            float endBlink = Mathf.Min(KitMotion.MinWarn, _life);
            if (_ringAtEnd && _ringCount > 0 && Visual != null)
                Visual.color = _life - t < endBlink && Mathf.FloorToInt(Time.time / 0.06f) % 2 == 0 ? Color.Lerp(BaseColor, KitMotion.Red, 0.6f) : BaseColor;
        }
        else if (t < _life + ShrinkTime)
        {
            if (!_closing)
            {
                _closing = true;
                SetHarm(false);
                if (Visual != null) Visual.color = BaseColor;
                if (_ringAtEnd && _ringCount > 0) Burst(_ringCount, _ringSpeed);
            }
            Body.localScale = Vector3.one * Mathf.Max(0.01f, 1f - (t - _life) / ShrinkTime);
        }
        else
        {
            if (!_closing)
            {
                _closing = true;
                if (_ringAtEnd && _ringCount > 0) Burst(_ringCount, _ringSpeed);
            }
            _gone = true;
            SetHarm(false);
            Body.gameObject.SetActive(false);
            return _bullets.Count > 0;
        }
        return true;
    }
}
