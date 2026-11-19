using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 【仮】区画（2026-11-19。docs/app-kits-1-design.md E6）。担当3 の物。Gemini・Zoom・Notion・キット2 が使う。
// ・部屋を区画（Rect）に分け、区画ごとに「赤い予告（PaceZoneMark）→ 判定（AreaStrike）」を出す。
// ・1区画＝1つの ZoneStrikeZone（CustomEffect）。ボスの形態変化（50％）・正常化（100％）で消える。
//   StageRule から出すときは owner に ctx.Hazard を渡す（片づけの表で消える）。
// ・判定の数値（ダメージ・ノックバック・のけぞり・ゲージ）は渡した攻撃データ。大きさ・予告・判定の秒数はここで決める
//   （攻撃データの写しを、区画の大きさで作って使う。アセットは書き換えない）。
// ・当たらない区画（安全な区画）は Safe で色を塗るだけ。赤は使わない（0-2）。
//
// 使い方の例
//   Rect area = ZoneStrike.AreaFor(owner); float floor = ZoneStrike.FloorY(owner);
//   Rect[] z = ZoneStrike.Columns(area, 3, floor, 5f);                 // 床を 3つの区画に（高さ 5）
//   ZoneStrike.Strike(owner, data, z[0], delay: 0f, telegraph: 1f, active: 0.5f);
//   Rect[] tiles = ZoneStrike.Grid(area, 3, floor, 2.8f, 8f);           // 横3×縦2 のタイル（下の段 0〜2.8、上の段 2.8〜8）
//   ZoneStrike.StrikeAll(owner, data, cols, 0f, 0.45f, 0.8f, 0.4f);    // 左から 0.45秒おきに
//   AttackData d = ZoneStrike.MakeData("Attack_Stage_Notion_Cards");    // StageRule で、アセットが無いとき

/// <summary>区画ごとの予告と判定</summary>
public static class ZoneStrike
{
    /// <summary>予告の基本の秒数（短くしない）</summary>
    public const float DefaultTelegraph = 0.8f;
    /// <summary>判定の基本の秒数</summary>
    public const float DefaultActive = 0.4f;

    // ───────── 区画を作る ─────────

    /// <summary>area の横幅を count 等分した区画（左から）。下の端は bottom、高さは height</summary>
    public static Rect[] Columns(Rect area, int count, float bottom, float height)
    {
        count = Mathf.Max(1, count);
        float w = area.width / count;
        var r = new Rect[count];
        for (int i = 0; i < count; i++) r[i] = new Rect(area.xMin + w * i, bottom, w, height);
        return r;
    }

    /// <summary>xMin から右へ、幅 width の区画を gap ずつあけて count 個（左から）</summary>
    public static Rect[] Row(float xMin, float width, float gap, int count, float bottom, float height)
    {
        count = Mathf.Max(1, count);
        var r = new Rect[count];
        for (int i = 0; i < count; i++) r[i] = new Rect(xMin + (width + gap) * i, bottom, width, height);
        return r;
    }

    /// <summary>
    /// 横 cols 列 × 縦（rowTops の数）段のタイル。rowTops は床からの各段の上の端（下の段から。例：2.8, 8）。
    /// 並びは下の段の左から（[0..cols-1] が下の段）
    /// </summary>
    public static Rect[] Grid(Rect area, int cols, float floorY, params float[] rowTops)
    {
        cols = Mathf.Max(1, cols);
        if (rowTops == null || rowTops.Length == 0) rowTops = new[] { area.yMax - floorY };
        var r = new Rect[cols * rowTops.Length];
        float w = area.width / cols;
        float prev = 0f;
        for (int row = 0; row < rowTops.Length; row++)
        {
            float top = rowTops[row];
            for (int c = 0; c < cols; c++) r[row * cols + c] = new Rect(area.xMin + w * c, floorY + prev, w, Mathf.Max(0.1f, top - prev));
            prev = top;
        }
        return r;
    }

    /// <summary>point を含む区画の番号（含む区画が無ければ一番近い区画。区画が無ければ -1）</summary>
    public static int IndexOf(IList<Rect> zones, Vector2 point)
    {
        if (zones == null || zones.Count == 0) return -1;
        int best = 0;
        float bestD = float.PositiveInfinity;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].Contains(point)) return i;
            Vector2 c = new Vector2(Mathf.Clamp(point.x, zones[i].xMin, zones[i].xMax), Mathf.Clamp(point.y, zones[i].yMin, zones[i].yMax));
            float d = (c - point).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>x だけで区画の番号を探す（床の区画で、ジャンプ中でも同じ区画にしたいとき）</summary>
    public static int IndexOfX(IList<Rect> zones, float x)
    {
        if (zones == null || zones.Count == 0) return -1;
        int best = 0;
        float bestD = float.PositiveInfinity;
        for (int i = 0; i < zones.Count; i++)
        {
            if (x >= zones[i].xMin && x <= zones[i].xMax) return i;
            float d = Mathf.Min(Mathf.Abs(x - zones[i].xMin), Mathf.Abs(x - zones[i].xMax));
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>区画を分ける範囲（ボス部屋にいれば部屋、ほかは画面。WallShot と同じ）</summary>
    public static Rect AreaFor(AttackRunner owner) => WallShot.AreaFor(owner);

    /// <summary>床の高さ（ボス部屋なら部屋の床。ほかは狙う相手の下の床。WallShot と同じ）</summary>
    public static float FloorY(AttackRunner owner) => WallShot.FloorY(owner, AreaFor(owner));

    // ───────── 打つ ─────────

    /// <summary>
    /// 区画1つに、delay 秒後から telegraph 秒の赤い予告 → active 秒の判定。
    /// fill：判定の間の絵（無ければ攻撃データの絵と色。区画の大きさに伸ばす）。fillColor：絵に掛ける色。
    /// telegraph が 0 なら予告なし（プレイヤーの能力など）。出した区画を返す（owner・data が無ければ null）
    /// </summary>
    public static ZoneStrikeZone Strike(AttackRunner owner, AttackData data, Rect zone, float delay = 0f, float telegraph = DefaultTelegraph,
        float active = DefaultActive, Sprite fill = null, Color? fillColor = null)
    {
        if (owner == null || data == null || zone.width <= 0f || zone.height <= 0f) return null;
        GameObject go = owner.CreateSpawnedObject("ZoneStrike", zone.center);
        var z = go.AddComponent<ZoneStrikeZone>();
        z.SetupStrike(owner, Sized(data, zone.size, active), zone, Mathf.Max(0f, delay), Mathf.Max(0f, telegraph), Mathf.Max(0.05f, active), fill, fillColor);
        return z;
    }

    /// <summary>区画を順に打つ（i 番目は delay + i×stagger 秒後に予告を始める）。予告はそれぞれ telegraph 秒</summary>
    public static List<ZoneStrikeZone> StrikeAll(AttackRunner owner, AttackData data, IList<Rect> zones, float delay, float stagger,
        float telegraph = DefaultTelegraph, float active = DefaultActive, Sprite fill = null, Color? fillColor = null)
    {
        var list = new List<ZoneStrikeZone>();
        if (zones == null) return list;
        for (int i = 0; i < zones.Count; i++)
        {
            ZoneStrikeZone z = Strike(owner, data, zones[i], delay + i * stagger, telegraph, active, fill, fillColor);
            if (z != null) list.Add(z);
        }
        return list;
    }

    /// <summary>
    /// 区画を順に打つ。予告は全部いっしょに出し（delay 秒後から）、判定だけ stagger 秒ずつずらす
    /// （i 番目の予告は telegraph + i×stagger 秒。ChatGPT の「長い答え」のように、先に全部見せる型）
    /// </summary>
    public static List<ZoneStrikeZone> StrikeSweep(AttackRunner owner, AttackData data, IList<Rect> zones, float delay, float stagger,
        float telegraph = DefaultTelegraph, float active = DefaultActive, Sprite fill = null, Color? fillColor = null)
    {
        var list = new List<ZoneStrikeZone>();
        if (zones == null) return list;
        for (int i = 0; i < zones.Count; i++)
        {
            ZoneStrikeZone z = Strike(owner, data, zones[i], delay, telegraph + i * stagger, active, fill, fillColor);
            if (z != null) list.Add(z);
        }
        return list;
    }

    /// <summary>
    /// 当たらない区画（安全な区画など）を、delay 秒後から seconds 秒だけ色で塗る。判定は無い。
    /// 色は赤以外（Gemini の青など）。owner があれば、その出した物として登録する（50％・100％で消える）
    /// </summary>
    public static ZoneStrikeZone Safe(AttackRunner owner, Rect zone, float delay, float seconds, Color color)
    {
        if (zone.width <= 0f || zone.height <= 0f) return null;
        GameObject go = owner != null ? owner.CreateSpawnedObject("ZoneSafe", zone.center) : new GameObject("ZoneSafe");
        go.transform.position = zone.center;
        var z = go.AddComponent<ZoneStrikeZone>();
        z.SetupSafe(zone, Mathf.Max(0f, delay), Mathf.Max(0.05f, seconds), color);
        return z;
    }

    // ───────── 攻撃データ ─────────

    static readonly Dictionary<string, AttackData> Copies = new Dictionary<string, AttackData>();

    /// <summary>
    /// 区画の大きさの写し（指定位置の判定・床に付けない・予告なし・判定 active 秒）。同じ大きさは使い回す。
    /// 名前は元のまま（スキンの枠・エラーの絵の種類は元の名前で決まる）
    /// </summary>
    static AttackData Sized(AttackData data, Vector2 size, float active)
    {
        string key = data.GetInstanceID() + "_" + F(size.x) + "_" + F(size.y) + "_" + F(active);
        if (Copies.TryGetValue(key, out AttackData c) && c != null) return c;
        c = Object.Instantiate(data);
        c.name = data.name;
        c.hideFlags = HideFlags.DontSave;
        string json = "{\"_kind\":1,\"_windup\":0,\"_anchor\":0,\"_snapToGround\":false,\"_shape\":0,"
            + "\"_areaSize\":{\"x\":" + F(size.x) + ",\"y\":" + F(size.y) + "},"
            + "\"_areaCount\":1,\"_areaStagger\":0,\"_telegraphTime\":0,\"_persistDuration\":0,"
            + "\"_activeTime\":" + F(active) + "}";
        JsonUtility.FromJsonOverwrite(json, c);
        Copies[key] = c;
        return c;
    }

    /// <summary>
    /// アセットの無いところ（StageRule・試し）で使う攻撃データを作る（実行時だけ。保存しない）。
    /// 種類は指定位置の判定。ダメージ・ゲージ・ノックバック・のけぞり・色だけ入れる。
    /// 名前に枠の語（breakpoint など）を入れると、スキンの絵が付くことがある。ふつうは "Attack_Stage_〇〇"（枠なし）
    /// </summary>
    public static AttackData MakeData(string name, int damage = 1, int gaugeGain = 0, Vector2? knockback = null, float hitStun = 0.25f, Color? color = null)
    {
        var d = ScriptableObject.CreateInstance<AttackData>();
        d.name = string.IsNullOrEmpty(name) ? "Attack_Stage_Zone" : name;
        d.hideFlags = HideFlags.DontSave;
        Vector2 kb = knockback ?? new Vector2(6f, 5f);
        Color col = color ?? Color.white;
        string json = "{\"_kind\":1,\"_damage\":" + Mathf.Max(0, damage) + ",\"_gaugeGain\":" + Mathf.Max(0, gaugeGain)
            + ",\"_knockback\":{\"x\":" + F(kb.x) + ",\"y\":" + F(kb.y) + "},\"_hitStun\":" + F(hitStun)
            + ",\"_color\":{\"r\":" + F(col.r) + ",\"g\":" + F(col.g) + ",\"b\":" + F(col.b) + ",\"a\":" + F(col.a) + "}"
            + ",\"_telegraphTime\":0,\"_snapToGround\":false}";
        JsonUtility.FromJsonOverwrite(json, d);
        return d;
    }

    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
