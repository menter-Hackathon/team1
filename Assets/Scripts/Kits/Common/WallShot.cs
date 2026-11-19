using System;
using UnityEngine;

// 【仮】高さの決まった横の弾（2026-11-19。docs/app-kits-1-design.md E6）。担当0 の物。
// ・決まった高さを、壁（またはボスの横・決めた位置）から横へ飛ぶ弾。通り道に赤い細い線の予告 0.8秒（短くしない）
// ・高さは床から：低＝0.6（ジャンプで越える）・中＝2.2・高＝3.6（床にいれば当たらない。足場の上は当たる）
// ・弾の数値（速さ・大きさ・寿命・ダメージ）は、渡した攻撃データ（種類は何でもよい。弾として出す）。絵は sprite で差し替えられる
// ・ボスの技からは owner＝ボスの AttackRunner、ギミックからは ctx.Hazard を渡す（出した物として登録され、片づけで消える）
// ・プレイヤーの能力（PowerPoint のフライインなど）からも使える：owner がプレイヤーの陣営なら、予告は 0.8秒に縛らず、線は赤にしない（白）

/// <summary>横の弾の高さ</summary>
public enum WallShotHeight { Low, Mid, High }

/// <summary>高さの決まった横の弾</summary>
public static class WallShot
{
    public const float LowY = 0.6f, MidY = 2.2f, HighY = 3.6f;
    /// <summary>予告の基本の秒数</summary>
    public const float DefaultTelegraph = 0.8f;

    /// <summary>床からの高さ（マス）</summary>
    public static float HeightOf(WallShotHeight h) => h == WallShotHeight.Low ? LowY : h == WallShotHeight.Mid ? MidY : HighY;

    /// <summary>
    /// 壁から撃つ。dir＝+1：左の壁から右へ、-1：右の壁から左へ。床は「戦う部屋の床」（ボス部屋の外では、画面の下の端から探す）。
    /// 出した予告（WallShotTelegraph）を返す。onFired：弾が出たとき
    /// </summary>
    public static WallShotTelegraph Fire(AttackRunner owner, AttackData data, WallShotHeight height, int dir, float telegraph = DefaultTelegraph, Sprite sprite = null, Action<Projectile> onFired = null)
    {
        if (owner == null || data == null) return null;
        Rect area = AreaFor(owner);
        float floor = FloorY(owner, area);
        int d = dir >= 0 ? 1 : -1;
        // 壁から少し内側（弾の大きさの半分＋0.1）。出た瞬間に壁に当たって消えないように（2026-11-19：担当2 の指摘）
        float inset = Mathf.Max(0.3f, Mathf.Max(data.HitSize.x, data.HitSize.y) * 0.5f + 0.1f);
        float x = d > 0 ? area.xMin + inset : area.xMax - inset;
        return FireFrom(owner, data, new Vector2(x, floor + HeightOf(height)), d, telegraph, sprite, onFired);
    }

    /// <summary>決めた位置から横へ撃つ（ボスの横など）。dir＝+1 で右へ</summary>
    public static WallShotTelegraph FireFrom(AttackRunner owner, AttackData data, Vector2 from, int dir, float telegraph = DefaultTelegraph, Sprite sprite = null, Action<Projectile> onFired = null)
    {
        if (owner == null || data == null) return null;
        Rect area = AreaFor(owner);
        int d = dir >= 0 ? 1 : -1;
        float endX = d > 0 ? area.xMax : area.xMin;
        GameObject go = owner.CreateSpawnedObject($"WallShot_{data.name}", from);
        var t = go.AddComponent<WallShotTelegraph>();
        bool mine = owner.Team == Team.Player; // プレイヤーの能力：赤は危ない物だけに使う
        float wait = mine ? Mathf.Max(0f, telegraph) : Mathf.Max(DefaultTelegraph, telegraph);
        t.Setup(owner, data, from, d, wait, Mathf.Abs(endX - from.x), sprite, onFired, mine ? PlayerLine : KitDress.Red);
        return t;
    }

    /// <summary>プレイヤーの能力のときの線の色（白。危なくない）</summary>
    public static readonly Color PlayerLine = new Color(1f, 1f, 1f, 0.7f);

    /// <summary>撃つ範囲（ボス部屋にいれば部屋、ほかは画面）</summary>
    public static Rect AreaFor(AttackRunner owner)
    {
        Rect arena = BattleArena.GetBounds();
        Transform target = owner != null ? owner.Target : null;
        if (target == null || arena.Contains(target.position)) return arena;
        return ScreenUi.ViewRect();
    }

    /// <summary>床の高さ（ボス部屋なら部屋の床。ほかは狙う相手の下の床）</summary>
    public static float FloorY(AttackRunner owner, Rect area)
    {
        Rect arena = BattleArena.GetBounds();
        if (area == arena) return arena.yMin;
        Transform target = owner != null ? owner.Target : null;
        if (owner != null && target != null && owner.TryFindGround(target.position.x, target.position.y + 0.5f, out float g)) return g;
        return area.yMin;
    }
}

/// <summary>横の弾の予告（赤い細い線）。時間が来たら弾を出して消える。ボスの形態変化でも消える（CustomEffect）</summary>
public class WallShotTelegraph : CustomEffect
{
    AttackRunner _owner;
    AttackData _data;
    Vector2 _from;
    int _dir;
    float _fireAt;
    Sprite _sprite;
    Action<Projectile> _onFired;
    SpriteRenderer _line;

    /// <summary>弾が出る時刻</summary>
    public float FireAt => _fireAt;

    internal void Setup(AttackRunner owner, AttackData data, Vector2 from, int dir, float telegraph, float length, Sprite sprite, Action<Projectile> onFired, Color lineColor)
    {
        _owner = owner;
        _data = data;
        _from = from;
        _dir = dir;
        _sprite = sprite;
        _onFired = onFired;
        _fireAt = Time.time + telegraph;
        if (telegraph <= 0f) return; // 予告なし（プレイヤーの能力ですぐ撃つとき）
        var lineGo = new GameObject("Line");
        lineGo.transform.SetParent(transform, false);
        lineGo.transform.position = new Vector3(from.x + dir * length * 0.5f, from.y, 0f);
        _line = AttackRunner.AddVisual(lineGo.transform, PlaceholderSprites.Square, lineColor, new Vector2(Mathf.Max(0.5f, length), 2f * KitDress.Px), 17, "Line");
    }

    void Update()
    {
        float left = _fireAt - Time.time;
        if (left > 0f)
        {
            if (_line != null) _line.enabled = left > 0.3f || Mathf.FloorToInt(Time.time / 0.08f) % 2 == 0;
            return;
        }
        if (_owner != null && _data != null)
        {
            Projectile p = Projectile.Spawn(_owner, _data, _from, new Vector2(_dir, 0f));
            if (p != null)
            {
                if (_sprite != null) KitDress.Swap(p.gameObject, _sprite);
                try { _onFired?.Invoke(p); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _owner.NotifyReleased(_data);
        }
        Destroy(gameObject);
    }
}
