using System;
using System.Collections.Generic;
using UnityEngine;

// 【仮】アプリの UI の足場（2026-11-19。AppStagePlatforms が作る）。動く足場は別のファイル（AppPlatformMove など）

/// <summary>アプリの足場（static。動く物はこれを継ぐ）。動く足場は、立っている物を Move より前に動かすため、早く動く</summary>
[DefaultExecutionOrder(-200)]
public class AppPlatform : MonoBehaviour
{
    protected const float GhostAlpha = 0.18f;
    static readonly Collider2D[] Riders = new Collider2D[8];
    static readonly HashSet<KinematicBody2D> Carried = new HashSet<KinematicBody2D>();

    public string Id { get; private set; }
    /// <summary>動かない・消えない足場か</summary>
    public virtual bool IsSteady => true;
    /// <summary>今、乗れるか</summary>
    public bool IsSolidNow => _solid && (Col == null || Col.enabled); // ほかの決まり（Notion のトグルなど）が閉じた足場も、乗れないと数える

    protected BoxCollider2D Col;
    protected SpriteRenderer Art;
    protected Rigidbody2D Body;
    protected AppStageParams P;
    protected Vector2 Home;
    protected float StartTime;
    protected float StartFixedTime;
    bool _solid = true;
    float _alpha = 1f;

    internal void Setup(string id, BoxCollider2D col, SpriteRenderer art, Rigidbody2D body, AppStageParams p)
    {
        Id = id;
        Col = col;
        Art = art;
        Body = body;
        P = p ?? new AppStageParams();
        Home = transform.position;
        StartTime = Time.time;
        StartFixedTime = Time.fixedTime;
        AppStagePlatforms.Add(this);
        OnSetup();
    }

    protected virtual void OnSetup() { }

    protected virtual void OnDestroy() => AppStagePlatforms.Remove(this);

    /// <summary>これから seconds 秒、乗れるままか</summary>
    public virtual bool IsSolidFor(float seconds) => IsSolidNow;

    /// <summary>その範囲が、この足場か（足場さがしの Bounds と比べる）</summary>
    internal bool Covers(Bounds b)
    {
        if (Col == null) return false;
        Bounds mine = Col.bounds;
        if (!Col.enabled) mine = new Bounds((Vector3)(Vector2)transform.TransformPoint(Col.offset), Col.size); // 消えている間も、場所で比べる
        return Mathf.Abs(mine.center.x - b.center.x) < 0.3f && Mathf.Abs(mine.max.y - b.max.y) < 0.3f;
    }

    // 乗れる・乗れないを変える（変わったときだけ書く。ほかの技が消した足場を、毎フレームもどさない）
    protected void SetSolid(bool on)
    {
        if (_solid == on) return;
        _solid = on;
        if (Col != null) Col.enabled = on;
    }

    // 絵の濃さ（変わったときだけ書く）
    protected void SetAlpha(float a)
    {
        if (Art == null || Mathf.Abs(_alpha - a) < 0.001f) return;
        _alpha = a;
        Color c = Art.color;
        c.a = a;
        Art.color = c;
    }

    /// <summary>
    /// 動く足場を next へ動かす（FixedUpdate の中で）。carry なら、上に立っている物（プレイヤー・雑魚）もいっしょに動かす。
    /// 体の位置を直接変えるので、このあと動く物の地面さがしには、もう新しい場所が見える
    /// </summary>
    protected void MoveBody(Vector2 next, bool carry)
    {
        if (Body == null) return;
        Vector2 delta = next - Body.position;
        if (delta.sqrMagnitude < 1e-10f) return;
        if (carry && _solid && delta.sqrMagnitude < 1f) CarryRiders(delta); // 大きく飛ぶとき（元の場所へ戻る）は運ばない
        Body.position = next;
    }

    void CarryRiders(Vector2 delta)
    {
        if (Col == null || !Col.enabled) return;
        Bounds b = Col.bounds;
        float top = b.max.y;
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
        filter.SetLayerMask(LayerMask.GetMask("Player", "Enemy"));
        int n = Physics2D.OverlapBox(new Vector2(b.center.x, top + 0.15f), new Vector2(b.size.x + 0.1f, 0.3f), 0f, filter, Riders);
        Carried.Clear();
        for (int i = 0; i < n; i++)
        {
            Collider2D c = Riders[i];
            Rigidbody2D rb = c != null ? c.attachedRigidbody : null;
            if (rb == null || !rb.TryGetComponent(out KinematicBody2D kb) || !rb.TryGetComponent(out BoxCollider2D box) || !Carried.Add(kb)) continue;
            Bounds feet = box.bounds; // 動くときの体（KinematicBody2D の箱）で見る
            if (!kb.IsGrounded || Mathf.Abs(feet.min.y - top) > 0.08f || feet.max.x <= b.min.x || feet.min.x >= b.max.x) continue; // この足場の上に立っている物だけ
            kb.Carry(delta);
        }
        Carried.Clear();
    }

    /// <summary>プレイヤーがこの足場の上に立っているか</summary>
    protected bool PlayerStandsOn()
    {
        if (Col == null || !Col.enabled) return false;
        if (_player == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc == null) return false;
            _player = pc.GetComponent<KinematicBody2D>();
            _playerBox = pc.GetComponent<BoxCollider2D>();
            if (_player == null || _playerBox == null) return false;
        }
        if (!_player.IsGrounded) return false;
        Bounds me = Col.bounds, pb = _playerBox.bounds;
        return pb.max.x > me.min.x && pb.min.x < me.max.x && Mathf.Abs(pb.min.y - me.max.y) < 0.08f;
    }

    KinematicBody2D _player;
    BoxCollider2D _playerBox;
}
