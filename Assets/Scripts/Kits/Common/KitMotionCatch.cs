using System;
using UnityEngine;

/// <summary>
/// 【仮】取る良い物（KitMotion.Catch が作る。2026-11-19）。担当0 の物。
/// ・道すじ（KitPath）に沿って動き、少し上下にゆれる。道の最後の 1秒は点滅して消える
/// ・プレイヤーの体（移動用の箱）が触ったら onCatch（1回。白い輪と音）。当たり判定（ダメージ）は無い
/// ・warn があれば、その間は最初の場所でうすく点滅（赤は使わない）
/// ・外から消された（片づけ・Cancel）ときは onCatch を呼ばない
/// </summary>
public class KitMotionCatch : KitMotionItem
{
    KitPath _path;
    Action _onCatch;
    float _half;
    bool _live;

    /// <summary>取られたか</summary>
    public bool Caught { get; private set; }

    internal void Setup(KitPath path, Action onCatch, Sprite sprite, Color color, float size, float warn)
    {
        _path = path;
        _onCatch = onCatch;
        _half = size * 0.5f;
        Warn = warn;
        transform.position = path.At(0f);
        MakeBody(sprite != null ? sprite : KitMotionArt.Disc(Mathf.Clamp(Mathf.RoundToInt(size * 18f), 6, 48)), color, Vector2.one * size, true, 0);
        if (sprite == null)
        {
            int d = Mathf.Clamp(Mathf.RoundToInt(size * 18f), 6, 64);
            AttackRunner.AddVisual(Body, KitMotionArt.Ring(d), Color.white, Vector2.one * size, OrderBody + 1, "Rim");
        }
        if (warn > 0f) Ghost(true);
        else GameAudio.Event("popup_open");
    }

    protected override bool Step(float age)
    {
        if (_path == null || Body == null) return false;
        if (age < Warn)
        {
            transform.position = _path.At(0f);
            Ghost(true);
            return true;
        }
        float t = age - Warn;
        if (!_live)
        {
            _live = true;
            Ghost(false);
        }
        transform.position = _path.At(t);
        Body.localPosition = new Vector3(0f, 0.08f * Mathf.Sin(t * 6f), 0f);
        float left = _path.Duration - t;
        if (Visual != null && !float.IsInfinity(left)) Visual.enabled = left > 1f || Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;

        if (KitMotion.PlayerBounds(out Bounds b))
        {
            Vector2 c = Body.position;
            if (b.max.x >= c.x - _half && b.min.x <= c.x + _half && b.max.y >= c.y - _half && b.min.y <= c.y + _half)
            {
                Caught = true;
                FxKit.Ring(c, _half * 3f, 0.25f, Color.white);
                GameAudio.Event("skill_pickup");
                KitMotion.Safe(_onCatch);
                return false;
            }
        }
        return t < _path.Duration;
    }
}
