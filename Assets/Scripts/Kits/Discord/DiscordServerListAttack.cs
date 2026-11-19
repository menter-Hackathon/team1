using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【仮】Discord のボスの技「サーバー一覧」（2026-11-19。docs/app-kits-1-design.md 3章 P1-2）。Custom 小：WallShot を呼ぶ。
/// プレイヤーから遠い方の壁に、暗いサーバーの列と丸いアイコンが縦に 3つ並ぶ（高さ 低・中・高）→
/// 光った順に（角の丸い四角に変わり、左に白い棒）1つずつ横へ飛ぶ（毎秒 10マス、間 0.6秒）。順番は毎回変わる。
/// ・予告：光ったアイコンから、赤い細い線（0.8秒。WallShot）
/// ・避け方：低＝ジャンプで越える。中＝ジャンプしない。高＝床にいて、ジャンプしない（足場の上は当たる）
/// ・弾の数値（速さ・大きさ）はこの技の攻撃データの「弾」の欄
/// 第2形態：4つ（3つの高さのどれかが 2回）
/// </summary>
[CreateAssetMenu(fileName = "Discord_ServerList", menuName = "Team1/Kits/Discord Server List")]
public class DiscordServerListAttack : CustomAttack
{
    [Tooltip("飛ばす数（3＝低・中・高を1回ずつ）")]
    [SerializeField, Min(1)] int _count = 3;
    [Tooltip("1つ目が光るまで（アイコンが並ぶ時間。秒）")]
    [SerializeField, Min(0f)] float _lead = 0.4f;
    [Tooltip("光る間隔（秒）")]
    [SerializeField, Min(0.2f)] float _interval = 0.6f;

    static readonly WallShotHeight[] Heights = { WallShotHeight.Low, WallShotHeight.Mid, WallShotHeight.High };

    public override IEnumerator Run(AttackRunner owner, AttackData data)
    {
        if (owner == null || data == null) yield break;
        Rect area = WallShot.AreaFor(owner);
        float floor = WallShot.FloorY(owner, area);
        Vector2 target = owner.TargetPoint ?? area.center;
        // プレイヤーから遠い方の壁から撃つ。dir＝+1：左の壁から右へ
        int dir = target.x >= area.center.x ? 1 : -1;
        float wallX = dir > 0 ? area.xMin + 0.3f : area.xMax - 0.3f;

        // 順番：低・中・高を並べて混ぜる（4つ目からは、どれかをもう一度）
        var order = new List<int>();
        for (int i = 0; i < _count; i++) order.Add(i < 3 ? i : Random.Range(0, 3));
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        // 同じ高さが続かないように（4つのとき）
        for (int i = 1; i < order.Count; i++)
        {
            if (order[i] != order[i - 1]) continue;
            for (int j = i + 1; j < order.Count; j++)
            {
                if (order[j] != order[i] && (j + 1 >= order.Count || order[j + 1] != order[i - 1]))
                {
                    (order[i], order[j]) = (order[j], order[i]);
                    break;
                }
            }
        }

        float life = _lead + (_count - 1) * _interval + WallShot.DefaultTelegraph + 0.4f;
        DiscordServerColumn column = DiscordServerColumn.Spawn(owner, wallX, floor, dir, life);
        yield return new WaitForSeconds(_lead);
        for (int k = 0; k < order.Count; k++)
        {
            if (k > 0) yield return new WaitForSeconds(_interval);
            int h = order[k];
            if (column != null) column.Light(h);
            // 壁に重なって出すと、弾が出た瞬間に地形に当たって消えるので、弾の半分だけ内側から出す（WallShot.Fire は壁から 0.3）
            float startX = wallX + dir * (Mathf.Max(data.HitSize.x, data.HitSize.y) * 0.5f + 0.15f);
            WallShot.FireFrom(owner, data, new Vector2(startX, floor + WallShot.HeightOf(Heights[h])), dir, WallShot.DefaultTelegraph, DiscordArt.ServerIcon(h, true), dir < 0 ? (System.Action<Projectile>)Upright : null);
        }
        yield return new WaitForSeconds(WallShot.DefaultTelegraph + 0.1f);
    }

    /// <summary>
    /// 左へ飛ぶ弾は、進む向きに 180° 回るので絵が逆さになる。絵を上下に返して、左右の反転だけにする
    /// </summary>
    static void Upright(Projectile p)
    {
        if (p == null) return;
        SpriteRenderer sr = KitDress.FindVisual(p.transform);
        if (sr != null) sr.flipY = true;
    }
}

/// <summary>壁の、暗いサーバーの列と 3つのアイコン（飾り）。光ったアイコンは角の丸い四角になり、左に白い棒。形態変化でも消える</summary>
public class DiscordServerColumn : CustomEffect
{
    const int BackOrder = 14, IconOrder = 16, PillOrder = 17;
    const float IconSize = 1.2f;

    readonly SpriteRenderer[] _icons = new SpriteRenderer[3];
    readonly SpriteRenderer[] _pills = new SpriteRenderer[3];
    readonly float[] _litAt = { -1f, -1f, -1f };
    SpriteRenderer _back;
    float _born, _endAt;

    public static DiscordServerColumn Spawn(AttackRunner owner, float wallX, float floor, int dir, float seconds)
    {
        if (owner == null) return null;
        GameObject go = owner.CreateSpawnedObject("Discord_ServerColumn", new Vector2(wallX, floor));
        var c = go.AddComponent<DiscordServerColumn>();
        c._born = Time.time;
        c._endAt = Time.time + seconds;
        c.Build(dir);
        return c;
    }

    void Build(int dir)
    {
        float top = WallShot.HighY + IconSize * 0.5f + 0.4f;
        // 暗い列（Discord の左のサーバーの列）。壁に付ける
        _back = DiscordArt.Put(transform, CallArt.Round(30, Mathf.RoundToInt(top * CallArt.Ppu), 4), new Vector2(dir * 0.5f, top * 0.5f),
            new Vector2(30f * CallArt.Px, top), CallKitUtil.Alpha(DiscordArt.Darker, 0.9f), BackOrder, "Column");
        for (int i = 0; i < 3; i++)
        {
            float y = WallShot.HeightOf((WallShotHeight)i);
            _icons[i] = DiscordArt.Put(transform, DiscordArt.ServerIcon(i, false), new Vector2(dir * 0.5f, y), Vector2.one * IconSize, Color.white, IconOrder, "Icon" + i);
            // 選んでいる印は、壁の側（アイコンの外側）
            _pills[i] = DiscordArt.Put(transform, DiscordArt.Pill, new Vector2(dir * 0.5f - dir * 0.78f, y), new Vector2(4f, 12f) * CallArt.Px, Color.white, PillOrder, "Pill" + i);
            _pills[i].enabled = false;
        }
        Animate();
    }

    /// <summary>そのアイコン（0＝低・1＝中・2＝高）を光らせる</summary>
    public void Light(int i)
    {
        if (i < 0 || i >= 3) return;
        _litAt[i] = Time.time;
        if (_icons[i] != null) _icons[i].sprite = DiscordArt.ServerIcon(i, true);
        if (_pills[i] != null) _pills[i].enabled = true;
    }

    void Update()
    {
        if (Time.time >= _endAt)
        {
            Destroy(gameObject);
            return;
        }
        Animate();
    }

    void Animate()
    {
        float t = Time.time - _born;
        float appear = Mathf.Clamp01(t / 0.2f);
        float fade = Mathf.Clamp01((_endAt - Time.time) / 0.3f);
        float a = appear * fade;
        if (_back != null) _back.color = CallKitUtil.Alpha(DiscordArt.Darker, 0.9f * a);
        for (int i = 0; i < 3; i++)
        {
            if (_icons[i] == null) continue;
            bool lit = _litAt[i] >= 0f;
            // 光ってから 0.8秒（予告の間）は、赤く点滅
            bool warn = lit && Time.time - _litAt[i] < WallShot.DefaultTelegraph;
            Color c = warn && Mathf.FloorToInt(Time.time / 0.1f) % 2 == 0 ? Color.Lerp(Color.white, DiscordArt.Red, 0.6f) : Color.white;
            c.a = a;
            _icons[i].color = c;
            float s = lit ? 1.1f : 1f;
            _icons[i].transform.localScale = new Vector3(s * IconSize / 22f * CallArt.Ppu, s * IconSize / 22f * CallArt.Ppu, 1f);
            if (_pills[i] != null) _pills[i].color = CallKitUtil.Alpha(Color.white, a);
        }
    }
}
