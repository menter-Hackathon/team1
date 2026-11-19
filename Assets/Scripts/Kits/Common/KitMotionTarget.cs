using System;
using UnityEngine;

/// <summary>
/// 【仮】叩く的（KitMotion.HitTarget が作る。2026-11-19）。担当0 の物。
/// ・中身は KitProp（プレイヤーの攻撃判定が重なると数える）。この物の子にする
/// ・warn があれば、先に点線の丸（赤は使わない）→ 的が大きくなって出る（0.15秒）
/// ・life 秒の間に hits 回叩かれたら onHit（白い輪）。時間切れなら onMiss（最後の 1秒は点滅）。life 0 ならずっと
/// ・path があれば、その道を動く（Orbit で回る・Sweep で流れる など）。道が終わったら、その場に止まる
/// ・外から消された（片づけ・Cancel）ときは、どちらも呼ばない
/// </summary>
public class KitMotionTarget : KitMotionItem
{
    const float GrowTime = 0.15f;

    KitProp _prop;
    float _life;
    Action _onHit, _onMiss;
    KitPath _path;
    bool _shown;

    /// <summary>的の中身（叩ける置物）</summary>
    public KitProp Prop => _prop;
    /// <summary>あと何回で壊れるか</summary>
    public int HitsLeft => _prop != null ? _prop.HitsLeft : 0;

    internal void Setup(Vector2 point, float life, Action onHit, Action onMiss, Sprite sprite, Color color, float size, int hits, KitPath path, float warn)
    {
        Warn = warn;
        _life = life;
        _onHit = onHit;
        _onMiss = onMiss;
        _path = path;
        transform.position = point;
        if (warn > 0f)
        {
            int d = Mathf.Clamp(Mathf.RoundToInt(size * 18f), 6, 64);
            KitMotionMark.Create(transform, KitMotionArt.Ring(d, true), point, Vector2.one * size, 0f, Color.white, OrderWarn, warn, "Ring");
        }
        Sprite look = sprite != null ? sprite : KitMotionArt.Disc(Mathf.Clamp(Mathf.RoundToInt(size * 18f), 6, 48));
        _prop = KitProp.Spawn(point, look, Vector2.one * size, null, hits, 0f, OrderBody);
        _prop.transform.SetParent(transform, true);
        _prop.DestroyOnBreak = false; // 壊れたら、この物ごと消す
        _prop.SetSprite(look, color);
        if (sprite == null)
        {
            // 良い物の印：白いふちと、まん中の白い点
            int d = Mathf.Clamp(Mathf.RoundToInt(size * 18f), 6, 64);
            SpriteRenderer rim = AttackRunner.AddVisual(_prop.transform, KitMotionArt.Ring(d), Color.white, Vector2.one * size, OrderBody + 1, "Rim");
            rim.transform.localPosition = Vector3.zero;
            SpriteRenderer dot = AttackRunner.AddVisual(_prop.transform, KitMotionArt.Disc(6), Color.white, Vector2.one * size * 0.3f, OrderBody + 1, "Dot");
            dot.transform.localPosition = Vector3.zero;
        }
        _prop.Broken += OnBroken;
        Body = _prop.transform;
        Visual = _prop.Visual;
        if (warn > 0f) _prop.gameObject.SetActive(false);
        else Show();
    }

    void Show()
    {
        _shown = true;
        if (_prop == null) return;
        _prop.gameObject.SetActive(true);
        _prop.transform.localScale = Vector3.one * 0.2f;
        GameAudio.Event("popup_open");
    }

    void OnBroken(KitProp p)
    {
        if (IsDone) return;
        Vector2 at = p != null ? (Vector2)p.transform.position : (Vector2)transform.position;
        FxKit.Ring(at, 1.4f, 0.25f, Color.white);
        KitMotion.Safe(_onHit);
        Finish();
    }

    protected override bool Step(float age)
    {
        if (_prop == null) return false;
        if (age < Warn) return true;
        if (!_shown) Show();
        float t = age - Warn;
        Transform pt = _prop.transform;
        pt.localScale = Vector3.one * (t < GrowTime ? Mathf.Lerp(0.2f, 1f, t / GrowTime) : 1f);
        if (_path != null) pt.position = _path.At(t);
        if (_life > 0f)
        {
            if (t >= _life)
            {
                KitMotion.Safe(_onMiss);
                return false;
            }
            if (Visual != null) Visual.enabled = _life - t > 1f || Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0;
        }
        return true;
    }
}
