using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // Scene

/// <summary>
/// 【仮】アプリの画面（UI）の部品を、乗れる足場とギミックにする（2026-11-19。Tools/art/appbg/FORMAT.md「足場とギミック」）。
/// ・表：Resources/AppBackgrounds/Stage_&lt;ラベル&gt;.json（render.py が書く。ワールド座標）。絵：Resources/AppBackgrounds/Parts/&lt;ラベル&gt;__&lt;id&gt;.png
/// ・1画面のステージだけ（シーンにフォルダの足場 Folder_Left・Folder_Right があるとき）。横スクロール（Chrome）・ウィンドウ（VS Code）は今のまま
/// ・StageDirector が、ステージの始め（Awake）に Build を呼ぶ。ほかの部品の Start（キットの決まりの足場さがし・タイムラインの飾り）より先に足場がそろう
/// ・足場：絵（フォルダと同じ並び 2。キャラクターより後ろ）＋上の端だけの薄い当たり判定（すり抜け床。フォルダと同じ 0.3 マス）
///   static：動かない／move：往復／blink：出る→点滅→消える／scroll：流れて、消えて戻る／fall：乗ると揺れて落ちる→戻る
///   button：乗れない。プレイヤーの攻撃が当たると押せる（敵の弾を消す・ボスをひるませる・能力のクールタイムを戻す）
/// ・動く足場は、上に立っている物（KinematicBody2D：プレイヤー・雑魚）をいっしょに動かす（KinematicBody2D.Carry）
/// ・止めるときは AppBackgrounds.StagePlatforms = false（表が無いアプリも、今までのフォルダのまま）
/// </summary>
public static class AppStagePlatforms
{
    const float ColliderThick = 0.3f;   // フォルダの足場と同じ厚さ
    internal const int ArtOrder = 2;    // フォルダの足場の絵と同じ（キャラクター 11〜・弾より後ろ。背の高い絵も後ろに出る）
    const int ButtonOrder = 4;
    static readonly Color PlateColor = new Color32(0xdf, 0xe4, 0xea, 0xff);

    /// <summary>今のステージで、アプリの足場を出しているか</summary>
    public static bool Active { get; private set; }
    /// <summary>今のステージのラベル（出していなければ null）</summary>
    public static string Label { get; private set; }

    static readonly List<AppPlatform> All = new List<AppPlatform>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        All.Clear();
        Active = false;
        Label = null;
    }

    internal static void Add(AppPlatform p) { if (p != null && !All.Contains(p)) All.Add(p); }
    internal static void Remove(AppPlatform p) => All.Remove(p);

    /// <summary>
    /// ステージの始めに呼ぶ（StageDirector.Awake）。表があれば足場を出し、フォルダの足場をかくす。出した数を返す（0＝今までどおり）。
    /// 失敗してもステージは止めない（ログを出して 0）
    /// </summary>
    public static int Build(StagePlan plan, StageDirector director)
    {
        Active = false;
        Label = null;
        All.Clear();
        if (!AppBackgrounds.StagePlatforms || plan == null || director == null) return 0;
        try
        {
            Scene scene = director.gameObject.scene;
            GameObject left = FindInScene(scene, "Folder_Left"), right = FindInScene(scene, "Folder_Right");
            if (left == null || right == null)
            {
                if (Debug.isDebugBuild) Debug.Log("[AppStagePlatforms] フォルダの足場が無いステージ（横スクロール・ウィンドウ）なので、出さない");
                return 0; // 1画面のステージではない
            }
            // ウィンドウのステージ（2026-11-20。docs/window-stage-and-pickup-design.md 1章）：フォルダの代わりに、Mac のウィンドウ 4枚を足場にする。
            // 作れなければ（0）今までどおり、アプリの UI の足場
            if (WindowStage.Wants(plan))
            {
                int made = WindowStage.Build(plan, left.transform.parent);
                if (made > 0)
                {
                    left.SetActive(false); right.SetActive(false);
                    Physics2D.SyncTransforms();
                    try { ReseatWave(director.Wave); } catch (Exception e) { Debug.LogException(e); }
                    Active = true; Label = AppBackgrounds.LabelFor(plan) ?? "Window";
                    return made;
                }
            }
            string label = AppBackgrounds.LabelFor(plan);
            if (string.IsNullOrEmpty(label)) return 0;
            var asset = Resources.Load<TextAsset>($"{AppBackgrounds.Folder}/Stage_{label}");
            if (asset == null)
            {
                if (Debug.isDebugBuild) Debug.Log($"[AppStagePlatforms] {label}：表が無いので、今までのフォルダの足場");
                return 0;
            }
            AppStageFile file = JsonUtility.FromJson<AppStageFile>(asset.text);
            if (file == null || file.platforms == null || file.platforms.Length == 0) return 0;

            var root = new GameObject($"AppStagePlatforms_{label}");
            root.transform.SetParent(left.transform.parent, false); // フォルダと同じ入れ物（Terrain）の中＝ステージのシーン。シーンといっしょに消える
            int count = 0;
            foreach (AppStagePlatformRow row in file.platforms)
            {
                try { if (row != null && Spawn(row, root.transform)) count++; }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (count == 0)
            {
                UnityEngine.Object.Destroy(root);
                return 0;
            }
            if (file.hideFolders)
            {
                left.SetActive(false);
                right.SetActive(false);
            }
            Physics2D.SyncTransforms();
            if (file.hideFolders)
            {
                try { ReseatWave(director.Wave); }
                catch (Exception e) { Debug.LogException(e); }
            }
            Active = true;
            Label = label;
            if (Debug.isDebugBuild) Debug.Log($"[AppStagePlatforms] {label}：足場 {count} 個（ボタンも数える）。フォルダ{(file.hideFolders ? "をかくす" : "は残す")}");
            return count;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return 0;
        }
    }

    /// <summary>
    /// その足場（Bounds）が、これから seconds 秒、乗れるままか（2026-11-19：会議室などの「安全な所」を選ぶとき用）。
    /// アプリの足場でなければ true。消えている・その間に消える（blink）・揺れている・落ちた（fall）・動く（move・scroll）足場は false
    /// </summary>
    public static bool IsSolidFor(Bounds bounds, float seconds)
    {
        foreach (AppPlatform p in All)
        {
            if (p == null || !p.Covers(bounds)) continue;
            return p.IsSolidFor(seconds);
        }
        return true;
    }

    /// <summary>動く・消えるアプリの足場の当たり判定か（雑魚をそこに出さないため）</summary>
    public static bool IsUnsteady(Collider2D col)
    {
        if (col == null) return false;
        AppPlatform p = col.GetComponentInParent<AppPlatform>();
        return p != null && !p.IsSteady;
    }

    /// <summary>
    /// Physics2D.Raycast と同じだが、動く・消えるアプリの足場は飛ばして、その先のいちばん近い物を返す（無ければ当たりなし）。
    /// 拾う物・置く物の床さがし用（宙に残らないように）
    /// </summary>
    public static RaycastHit2D RaycastSteady(Vector2 origin, Vector2 direction, float distance, int layerMask)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance, layerMask);
        RaycastHit2D best = default;
        float bestDistance = float.MaxValue;
        foreach (RaycastHit2D h in hits)
        {
            if (h.collider == null || IsUnsteady(h.collider) || h.distance >= bestDistance) continue;
            best = h;
            bestDistance = h.distance;
        }
        return best;
    }

    // ───────── 作る ─────────

    static bool Spawn(AppStagePlatformRow row, Transform parent)
    {
        if (!Finite(row.x) || !Finite(row.y) || !Finite(row.w) || !Finite(row.h) || row.w < 0.1f || row.h < 0.05f) return false;
        string kind = string.IsNullOrEmpty(row.behavior) ? "static" : row.behavior.Trim().ToLowerInvariant();
        AppStageParams p = row.@params ?? new AppStageParams();
        Sprite sprite = !string.IsNullOrEmpty(row.sprite) ? Resources.Load<Sprite>(row.sprite) : null;
        var size = new Vector2(row.w, row.h);
        var center = new Vector2(row.x, row.y);

        if (kind == "button")
        {
            AppStageButton.Create(parent, $"AppButton_{row.id}", center, size, sprite, sprite != null ? Color.white : PlateColor, ButtonOrder, p);
            return true;
        }

        bool moves = kind == "move" || kind == "scroll";
        var go = new GameObject($"AppPlatform_{row.id}");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (oneWay >= 0) go.layer = oneWay;
        go.transform.SetParent(parent, false);
        go.transform.position = center; // 当たり判定を付ける前に置く（物理の世界に、この位置で入る）
        SpriteRenderer art = AttackRunner.AddVisual(go.transform, sprite, sprite != null ? Color.white : PlateColor, size, ArtOrder, "Art");

        Rigidbody2D body = null;
        if (moves)
        {
            body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.None;
        }
        float top = Finite(row.top) && Mathf.Abs(row.top) > 0.0001f ? row.top : row.y + row.h * 0.5f;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(row.w, ColliderThick);
        col.offset = new Vector2(0f, top - row.y - ColliderThick * 0.5f); // 上の端だけ（背の高い絵でも、乗る面は上）

        AppPlatform platform;
        switch (kind)
        {
            case "move": platform = go.AddComponent<AppPlatformMove>(); break;
            case "blink": platform = go.AddComponent<AppPlatformBlink>(); break;
            case "scroll": platform = go.AddComponent<AppPlatformScroll>(); break;
            case "fall": platform = go.AddComponent<AppPlatformFall>(); break;
            default: platform = go.AddComponent<AppPlatform>(); break;
        }
        platform.Setup(row.id, col, art, body, p);
        return true;
    }

    /// <summary>
    /// ウィンドウのステージのウィンドウ 1枚を、動かない足場にする（WindowStage が呼ぶ。2026-11-20）。Spawn の static と同じ作り（絵の順番だけ引数）：
    /// AppPlatform_Win&lt;id&gt;・すり抜け床・子 "Art"（ワームと巣が名前で探す）・上の端だけの当たり判定（横・下は無い）
    /// </summary>
    internal static AppPlatform SpawnWindow(string id, Vector2 center, Vector2 size, Sprite sprite, int order, Transform parent)
    {
        if (sprite == null || parent == null || !Finite(center.x) || !Finite(center.y) || size.x < 0.1f || size.y < ColliderThick) return null;
        var go = new GameObject($"AppPlatform_Win{id}");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        if (oneWay >= 0) go.layer = oneWay;
        go.transform.SetParent(parent, false);
        go.transform.position = center; // 当たり判定を付ける前に置く
        SpriteRenderer art = AttackRunner.AddVisual(go.transform, sprite, Color.white, size, order, "Art");
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(size.x, ColliderThick);
        col.offset = new Vector2(0f, size.y * 0.5f - ColliderThick * 0.5f); // 上の端だけ
        AppPlatform platform = go.AddComponent<AppPlatform>();
        platform.Setup(id, col, art, null, new AppStageParams());
        return platform;
    }

    // フォルダの足場の上にいた最初の雑魚（シーンに置いてある物）を、下にある床・動かない足場の上に置き直す。
    // 撃つ雑魚は重さが無いので、そのままだと宙に浮く
    static void ReseatWave(EnemyWave wave)
    {
        if (wave == null) return;
        int mask = LayerMask.GetMask("Ground", "OneWayPlatform");
        foreach (Health h in wave.GetComponentsInChildren<Health>())
        {
            if (h == null || h.GetComponentInParent<BossController>() != null) continue;
            Vector2 feet = h.transform.position;
            // 足元の少し上から下へ、いちばん近い床・動かない足場の上にのせる（足場の上の端が 3.0 より少し高くても、中に埋まらないように）
            if (!StandsOnSteady(Physics2D.RaycastAll(feet + Vector2.up * 0.5f, Vector2.down, 30f, mask), out Vector2 point)) continue;
            h.transform.position = point; // Warp は使わない（雑魚の Awake の前かもしれないため）
        }
        Physics2D.SyncTransforms();
    }

    static bool StandsOnSteady(RaycastHit2D[] hits, out Vector2 point)
    {
        point = default;
        float best = float.MaxValue;
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.distance <= 0f || IsUnsteady(hit.collider)) continue;
            if (hit.distance < best) { best = hit.distance; point = hit.point; }
        }
        return best < float.MaxValue;
    }

    // そのシーンの中の、その名前の足場（読み込み中の Awake でも使える探し方。StageTheme と同じ）
    static GameObject FindInScene(Scene scene, string name)
    {
        foreach (BoxCollider2D col in UnityEngine.Object.FindObjectsOfType<BoxCollider2D>(true))
        {
            if (col != null && col.gameObject.name == name && col.gameObject.scene == scene) return col.gameObject;
        }
        return null;
    }

    static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}

// ───────── 表（render.py の stage_json と同じ形） ─────────

[Serializable]
public class AppStageFile
{
    public string label;
    public bool hideFolders = true;
    public AppStagePlatformRow[] platforms;
}

[Serializable]
public class AppStagePlatformRow
{
    public string id;
    public float x, y, w, h, top;
    public string sprite;
    public string behavior;
    public AppStageParams @params;
}

/// <summary>動きの数（長さ・速さはマス、時間は秒。dy は上が +）。書いていない物は 0（使う所で決まった値にする）</summary>
[Serializable]
public class AppStageParams
{
    public float dx, dy, speed, range, on, off, warn, period, offset, delay, respawn, cooldown;
    public string dir, effect;

    public static float Pos(float v, float fallback, float min = 0.05f) => float.IsNaN(v) || float.IsInfinity(v) || v <= 0f ? fallback : Mathf.Max(min, v);
    public static float Num(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
}
