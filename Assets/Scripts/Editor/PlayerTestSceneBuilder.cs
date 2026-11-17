using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// プレイヤー操作を試すための仮ステージを自動で作るエディタ拡張。
/// メニュー「Tools/Team1/プレイヤー操作テストシーンを作成」から実行する。
/// 絵は白い四角の仮素材。デザインが来たら Player の Visual の下を差し替える。
/// 本物の能力（新しいタブ・ブレークポイント）を、スタート地点の近くに拾えるアイテムとして置く。
/// 【仮】G キーでも同じ2つを順番にもらえる（攻撃データはボス戦テストシーンと共通）。
/// </summary>
public static class PlayerTestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test/PlayerTest.unity";
    const string SpritePath = "Assets/Sprites/Placeholder/Square.png";

    static readonly Color PlayerColor = new Color(1.00f, 0.55f, 0.80f);
    static readonly Color EnemyColor = new Color(0.95f, 0.35f, 0.30f);
    static readonly Color HazardColor = new Color(1.00f, 0.80f, 0.20f);
    static readonly Color AbilityPickupColor = new Color(1.00f, 1.00f, 1.00f);

    [MenuItem("Tools/Team1/プレイヤー操作テストシーンを作成")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>コマンドラインから実行する用（-executeMethod PlayerTestSceneBuilder.Build）</summary>
    public static void Build()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (ground < 0 || oneWay < 0 || playerLayer < 0 || enemyLayer < 0)
        {
            Debug.LogError("レイヤー Ground / OneWayPlatform / Player / Enemy が見つかりません。Project Settings > Tags and Layers を確認してください。");
            return;
        }

        BossTestSceneBuilder.GetOrCreateAttacks(); // 近接・能力の攻撃データ（無ければ docs の数値で作る）
        ArtAssets.Apply(); // 絵の取り込み設定と、攻撃データ・能力への割り当て
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // アセットは新しいシーンを開いたあとに読み込む（取り込みやシーンの切り替えで、前に読んだ物が使えなくなることがある）
        Sprite square = GetOrCreateSquareSprite();
        var attacks = BossTestSceneBuilder.GetOrCreateAttacks();

        // --- 地形（床の上面が y=0） ---
        var terrain = new GameObject("Terrain").transform;
        CreateBlock("Floor", new Vector2(0f, -1f), new Vector2(44f, 2f), ground, ArtLib.BlockKind.Ground, terrain);
        CreateBlock("Wall_Left", new Vector2(-22.5f, 5f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        CreateBlock("Wall_Right", new Vector2(22.5f, 5f), new Vector2(1f, 14f), ground, ArtLib.BlockKind.Wall, terrain);
        CreateBlock("Step_Low", new Vector2(-8f, 0.5f), new Vector2(4f, 1f), ground, ArtLib.BlockKind.Floor, terrain);
        CreateBlock("Step_High", new Vector2(-13f, 2.5f), new Vector2(4f, 1f), ground, ArtLib.BlockKind.Floor, terrain);
        CreateBlock("Pillar", new Vector2(8f, 1.5f), new Vector2(1f, 3f), ground, ArtLib.BlockKind.Wall, terrain);
        CreateBlock("Ceiling", new Vector2(15f, 3.5f), new Vector2(6f, 1f), ground, ArtLib.BlockKind.Floor, terrain);
        CreateBlock("OneWay_1", new Vector2(0f, 2.5f), new Vector2(4f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);
        CreateBlock("OneWay_2", new Vector2(4f, 5f), new Vector2(3f, 0.3f), oneWay, ArtLib.BlockKind.Platform, terrain);

        // --- プレイヤー（原点＝足元） ---
        var abilities = new[] { attacks.SkillNewTab, attacks.SkillBreakpoint };
        var player = CreatePlayer(square, new Vector3(-3f, 0.5f, 0f), attacks.PlayerMelee, abilities);

        // 拾える能力（ボスの代わり）。スタート地点のすぐ左に置く
        var pickups = new GameObject("SkillPickups").transform;
        CreateSkillPickup(attacks.SkillNewTab, new Vector2(-5f, 0f), AbilityPickupColor, pickups, square, playerLayer);
        CreateSkillPickup(attacks.SkillBreakpoint, new Vector2(-8f, 1f), AbilityPickupColor, pickups, square, playerLayer); // 低い段の上

        // --- 的になる敵（HP3、触れるとダメージ） ---
        var enemy = new GameObject("Enemy_Dummy");
        enemy.layer = enemyLayer;
        enemy.transform.position = new Vector3(4f, 0f, 0f);
        var enemyHealth = enemy.AddComponent<Health>();
        SetProps(enemyHealth, so =>
        {
            so.FindProperty("_team").enumValueIndex = (int)Team.Enemy;
            so.FindProperty("_max").intValue = 3;
            so.FindProperty("_oneDamagePerHit").boolValue = false;
            so.FindProperty("_invincibleTimeAfterHit").floatValue = 0f;
        });
        enemy.AddComponent<DamageBlink>();
        enemy.AddComponent<DisableOnDeath>();
        var enemyLabel = enemy.AddComponent<HealthDebugLabel>();
        SetProps(enemyLabel, so => so.FindProperty("_offset").vector3Value = new Vector3(0f, 1.7f, 0f));
        var enemyBody = CreateSprite("Body", enemy.transform, new Vector2(0f, 0.6f), new Vector2(0.9f, 1.2f), EnemyColor, square, 0);
        enemyBody.gameObject.layer = enemyLayer;
        var enemyHurtbox = AddHurtbox(enemyBody.gameObject);
        // 見た目は歩く雑魚の絵（判定は四角のまま）
        GameObject enemyBodyGo = enemyBody.gameObject;
        ArtLib.RemoveRenderer(enemyBodyGo.transform);
        var enemyArt = ArtLib.AddArt(enemy.transform, "Art", null, Vector3.zero, 11, enemyLayer);
        ArtLib.Animate(enemyArt, ArtAssets.LoadFrames("Generated/Enemies/Zako_Walker.png"), 6f);
        enemyArt.transform.localScale = new Vector3(-1f, 1f, 1f); // プレイヤーの方（左）を向く
        SetProps(enemy.GetComponent<DamageBlink>(), so =>
        {
            var arr = so.FindProperty("_renderers");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = enemyArt;
        });
        var enemyDeath = enemy.AddComponent<DeathVfx>();
        SetProps(enemyDeath, so => so.FindProperty("_offset").vector2Value = new Vector2(0f, 0.5f));
        // 体当たり：被弾範囲と同じ形で、プレイヤーに当たり続ける（連続ヒットはプレイヤーの無敵時間で防ぐ）
        AddHitbox(enemyBodyGo, Team.Enemy, 1 << playerLayer, damage: 1,
            knockback: new Vector2(6f, 5f), hitStun: 0.25f, hitOnce: false, activeOnStart: true, area: enemyHurtbox);

        // --- トゲ（触れるとダメージ） ---
        var spikes = CreateSprite("Hazard_Spikes", terrain, new Vector2(10.5f, 0.2f), new Vector2(2f, 0.4f), HazardColor, square, 0);
        AddHitbox(spikes.gameObject, Team.Enemy, 1 << playerLayer, damage: 1,
            knockback: new Vector2(5f, 7f), hitStun: 0.25f, hitOnce: false, activeOnStart: true);

        // --- カメラ ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 2f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.AddComponent<AudioListener>();
        ArtLib.CreateBackground(cam);
        ArtLib.CreateHud(cam, player, null);
        var follow = camGo.AddComponent<CameraFollow2D>();
        var followSo = new SerializedObject(follow);
        followSo.FindProperty("_target").objectReferenceValue = player.transform;
        followSo.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = player;
        Debug.Log($"プレイヤー操作テストシーンを作成しました: {ScenePath}");
    }

    /// <summary>
    /// プレイヤーを作る（ボス用テストシーンでも使う）。原点＝足元。
    /// meleeAttack を渡すと、近接（J キー）の数値をその攻撃データから読む。null なら PlayerMeleeAttack の値のまま。
    /// debugSkills は【仮】G キーで順番にもらえる能力
    /// </summary>
    internal static GameObject CreatePlayer(Sprite square, Vector3 position, AttackData meleeAttack, SkillDefinition[] debugSkills)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int ground = LayerMask.NameToLayer("Ground");
        int oneWay = LayerMask.NameToLayer("OneWayPlatform");

        var player = new GameObject("Player");
        player.layer = playerLayer;
        player.transform.position = position;

        var rb = player.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var box = player.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.7f, 1.0f);
        box.offset = new Vector2(0f, 0.5f);

        var body = player.AddComponent<KinematicBody2D>();
        var bodySo = new SerializedObject(body);
        bodySo.FindProperty("_solidMask").intValue = 1 << ground;
        bodySo.FindProperty("_oneWayMask").intValue = 1 << oneWay;
        bodySo.ApplyModifiedPropertiesWithoutUndo();

        player.AddComponent<PlayerInputReader>();
        var controller = player.AddComponent<PlayerController>();
        player.AddComponent<PlayerAnimationBridge>();

        // 見た目（左右反転するのはこの Visual だけ）
        var visual = new GameObject("Visual").transform;
        visual.SetParent(player.transform, false);
        // 被弾範囲の物（0.7×1.0 の四角）。絵は外して、判定だけにする
        var bodySprite = CreateSprite("Body", visual, new Vector2(0f, 0.5f), new Vector2(0.7f, 1.0f), PlayerColor, square, 0);
        bodySprite.gameObject.layer = playerLayer;
        GameObject bodyGo = bodySprite.gameObject;
        ArtLib.RemoveRenderer(bodyGo.transform);
        // プレイヤーの絵（デザイン担当の絵。原点＝足元）。動きは Animator（PlayerAnimationBridge がパラメータを入れる）
        Sprite[] idleFrames = ArtAssets.LoadFrames("Generated/Player/Player_Idle.png");
        var art = ArtLib.AddArt(visual, "Art", idleFrames.Length > 0 ? idleFrames[0] : null, Vector3.zero, 15, playerLayer);
        var animator = art.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = ArtAssets.GetOrCreatePlayerController();

        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("_visualRoot").objectReferenceValue = visual;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        // --- ダメージ（ライフ制：3回当たるとアウト） ---
        var playerHealth = player.AddComponent<Health>();
        SetProps(playerHealth, so =>
        {
            so.FindProperty("_team").enumValueIndex = (int)Team.Player;
            so.FindProperty("_max").intValue = 3;
            so.FindProperty("_oneDamagePerHit").boolValue = true;
            so.FindProperty("_invincibleTimeAfterHit").floatValue = 1.0f; // 2026-11-16 まさぴ〜決定（1.5秒だと無敵の間に敵を倒せてしまう）
        });
        player.AddComponent<PlayerDamageReaction>();
        var playerBlink = player.AddComponent<DamageBlink>();
        SetProps(playerBlink, so =>
        {
            var arr = so.FindProperty("_renderers");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = art;
        });
        // ライフ0：煙になって消える（ライフは画面左上のハートで出す）
        var playerDeath = player.AddComponent<DeathVfx>();
        SetProps(playerDeath, so =>
        {
            so.FindProperty("_big").boolValue = true;
            so.FindProperty("_offset").vector2Value = new Vector2(0f, 0.6f);
            var arr = so.FindProperty("_hide");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = art;
        });

        // 被弾範囲＝体の大きさ（0.7×1.0。絵の頭から足まで）
        AddHurtbox(bodyGo);

        // 近接（J キー）。攻撃判定は Visual の子なので向きに合わせて反転する。数値は攻撃データから読む
        var attackSprite = CreateSprite("MeleeAttack", visual, new Vector2(0.75f, 0.5f), new Vector2(0.8f, 0.6f), new Color(1f, 1f, 1f, 0.5f), square, 2);
        attackSprite.gameObject.layer = playerLayer;
        GameObject attackGo = attackSprite.gameObject;
        ArtLib.RemoveRenderer(attackGo.transform);
        var attackHitbox = AddHitbox(attackGo, Team.Player, 1 << enemyLayer, damage: 1,
            knockback: new Vector2(4f, 3f), hitStun: 0.2f, hitOnce: true, activeOnStart: false);
        // 斬撃の絵：判定の物は 0.8×0.6 に拡大されているので、打ち消して大きさ1で描く
        var slash = ArtLib.AddArt(attackGo.transform, "Slash", null, Vector3.zero, 16, playerLayer);
        slash.transform.localScale = new Vector3(1f / 0.8f, 1f / 0.6f, 1f);
        var slashAnim = ArtLib.Animate(slash, ArtAssets.LoadFrames("Generated/Player/Fx_Slash.png"), 25f, loop: false, randomStart: false);
        slash.enabled = false;
        var melee = player.AddComponent<PlayerMeleeAttack>();
        SetProps(melee, so =>
        {
            so.FindProperty("_hitbox").objectReferenceValue = attackHitbox;
            so.FindProperty("_effect").objectReferenceValue = slash;
            so.FindProperty("_attack").objectReferenceValue = meleeAttack;
        });

        // --- スキル枠（3枠・捨てる・満杯時の入れ替え） ---
        var playerSkills = player.AddComponent<PlayerSkills>();
        // 能力の実行（攻撃データ）とクールタイム。能力は同時に使えるようにする
        var playerRunner = player.AddComponent<AttackRunner>();
        SetProps(playerRunner, so =>
        {
            so.FindProperty("_team").enumValueIndex = (int)Team.Player;
            so.FindProperty("_targetLayers").intValue = 1 << enemyLayer;
            so.FindProperty("_solidMask").intValue = 1 << ground;
            so.FindProperty("_groundMask").intValue = (1 << ground) | (1 << oneWay);
            so.FindProperty("_allowConcurrent").boolValue = true;
        });
        player.AddComponent<PlayerSkillExecutor>();
        // 【仮】G キーで能力を順番に獲得（ボスを倒さずに能力を試せる）
        var skillGiver = player.AddComponent<SkillDebugGiver>();
        SetProps(skillGiver, so =>
        {
            var arr = so.FindProperty("_skills");
            arr.arraySize = debugSkills != null ? debugSkills.Length : 0;
            for (int i = 0; i < arr.arraySize; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = debugSkills[i];
        });
        // 見た目の演出（斬撃・火花・ダッシュの残像・新しいタブのウィンドウ）
        var vfx = player.AddComponent<PlayerVfx>();
        SetProps(vfx, so =>
        {
            so.FindProperty("_art").objectReferenceValue = art;
            so.FindProperty("_slash").objectReferenceValue = slashAnim;
        });
        // 能力の枠・ライフは、カメラの子の画面表示（PixelHud）が出す

        return player;
    }

    internal static void CreateBlock(string name, Vector2 center, Vector2 size, int layer, Color color, Sprite sprite, Transform parent)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
    }

    /// <summary>地形のブロック（当たり判定は1×1を拡大、絵は ArtLib が子に付ける）</summary>
    internal static GameObject CreateBlock(string name, Vector2 center, Vector2 size, int layer, ArtLib.BlockKind kind, Transform parent)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        ArtLib.DecorateBlock(go, size, kind);
        return go;
    }

    internal static void SetProps(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>絵と同じ大きさのトリガーを付けて被弾範囲にする（絵は 1×1 ユニットの四角）</summary>
    internal static BoxCollider2D AddHurtbox(GameObject go)
    {
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.offset = Vector2.zero;
        col.isTrigger = true;
        go.AddComponent<Hurtbox>();
        return col;
    }

    internal static Hitbox AddHitbox(GameObject go, Team team, int targetMask, int damage, Vector2 knockback, float hitStun,
        bool hitOnce, bool activeOnStart, Collider2D area = null)
    {
        if (area == null)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.isTrigger = true;
            area = col;
        }
        var hitbox = go.AddComponent<Hitbox>();
        SetProps(hitbox, so =>
        {
            so.FindProperty("_team").enumValueIndex = (int)team;
            so.FindProperty("_area").objectReferenceValue = area;
            so.FindProperty("_targetLayers").intValue = targetMask;
            so.FindProperty("_damage").intValue = damage;
            so.FindProperty("_knockback").vector2Value = knockback;
            so.FindProperty("_hitStun").floatValue = hitStun;
            so.FindProperty("_hitOncePerActivation").boolValue = hitOnce;
            so.FindProperty("_activeOnStart").boolValue = activeOnStart;
        });
        return hitbox;
    }

    /// <summary>触れると拾えるスキル。surface は置く床の上面の位置</summary>
    internal static void CreateSkillPickup(SkillDefinition skill, Vector2 surface, Color color, Transform parent, Sprite sprite, int playerLayer, string suffix = "")
    {
        var go = new GameObject($"SkillPickup_{skill.name}{suffix}");
        go.transform.SetParent(parent, false);
        go.transform.position = surface + new Vector2(0f, 0.5f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = skill.Icon != null ? skill.Icon : sprite; // 能力のアイコン（無ければ仮の四角）
        sr.color = skill.Icon != null ? Color.white : color;
        sr.sortingOrder = 5;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.5f, 0.5f); // 拾える範囲は今までと同じ 0.5×0.5
        if (skill.Icon == null)
        {
            // アイコンが無いときは、今までどおり四角をひし形に見せる
            go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            col.size = Vector2.one;
        }
        col.isTrigger = true;
        var pickup = go.AddComponent<SkillPickup>();
        SetProps(pickup, so =>
        {
            so.FindProperty("_skill").objectReferenceValue = skill;
            so.FindProperty("_area").objectReferenceValue = col;
            so.FindProperty("_targetLayers").intValue = 1 << playerLayer;
        });
    }

    internal static SpriteRenderer CreateSprite(string name, Transform parent, Vector2 localPos, Vector2 size, Color color, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 10 + order;
        return sr;
    }

    internal static Sprite GetOrCreateSquareSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null) return existing;

        EnsureFolder(Path.GetDirectoryName(SpritePath).Replace('\\', '/'));
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var pixels = new Color32[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SpritePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f; // 16px = 1ユニット
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
