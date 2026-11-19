using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【仮】のこり 10 種のウイルスの着せかえ（2026-11-20。docs/virus-types-design.md 5-1・7章 D）。
/// ・今ある雑魚のひな形を VirusArt.Clone で複製 → VirusArt.Dress で絵と当たりの大きさを入れかえる → AddTemplate（重み 0：今までの出し方では選ばれない）
///   トロイ←突っ込む／ランサムウェア←強い（HP3・拾える物）／スパイウェア←とぶ／アドウェア・フィッシング←撃つ（Template(1)）／
///   ボットネット←小さい（大きさは 1 倍にもどす）／ロジックボム・マイナー・ポリモーフィック・マクロ←歩く（Template(0)）
/// ・ZakoVariants も StagePacer.Started で作るので、着せかえは最初に聞かれたとき（VirusRoster.Pick。出す直前）に作る（順番にたよらない）
/// ・「作った」の印は spawner ごと（ステージが変わると spawner も変わる）。シーンが変わったら消す
/// ・いらない飾り（WingL・WingR・Horns・HeldIcon）は SetActive(false)（こわさない。部品が持っている）。強い雑魚の HP の赤い点は残す
/// ・名前は "Template_&lt;Type&gt;"（Small・Elite を入れない。IllustratorRule が名前で見ている）
/// 数値はすべて【仮】
/// </summary>
public static class VirusSkins
{
    /// <summary>もとにする雑魚</summary>
    enum Base { Walker, Shooter, Small, Elite, Flyer, Charger }

    // 5-1 の表
    static readonly (string type, Base from)[] Recipes =
    {
        ("Trojan", Base.Charger),
        ("Ransomware", Base.Elite),
        ("Spyware", Base.Flyer),
        ("Adware", Base.Shooter),
        ("Phish", Base.Shooter),
        ("Botnet", Base.Small),
        ("Bomb", Base.Walker),
        ("Miner", Base.Walker),
        ("Poly", Base.Walker),
        ("Macro", Base.Walker),
    };

    /// <summary>かくす飾り（ZakoVariants が足した物。WingL・WingR・Horns は Visual の下、HeldIcon は体の直下）</summary>
    static readonly string[] HiddenParts = { "WingL", "WingR", "Horns", "HeldIcon" };

    /// <summary>作ってみた spawner（成功・失敗どちらでも。失敗したひな形を毎回作り直さない）</summary>
    static readonly HashSet<int> Attempted = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Attempted.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Attempted.Clear(); // 前のステージの印を残さない
    }

    /// <summary>着せかえの種類か（10 種）</summary>
    public static bool IsSkin(string type)
    {
        if (string.IsNullOrEmpty(type)) return false;
        foreach (var r in Recipes) if (r.type == type) return true;
        return false;
    }

    /// <summary>
    /// その spawner に 10 種のひな形を作る（spawner ごとに 1 回だけ）。VirusRoster.Pick が出す直前に呼ぶ。確認用のプローブからも呼んでよい。
    /// 絵が無い・もとの雑魚が無い種類は作らない（呼んだ側はスス玉で代わりに出す）。例外は外へ出さない
    /// </summary>
    public static void EnsureBuilt(ZakoSpawner spawner)
    {
        if (spawner == null || !VirusArt.Enabled) return;
        if (!Attempted.Add(spawner.GetInstanceID())) return;
        foreach (var r in Recipes)
        {
            try { Build(spawner, r.type, r.from); }
            catch (Exception e) { SkinDresser.LogOnce("VirusSkins.Build." + r.type, $"ウイルスの着せかえ（{r.type}）を作れません：{e}"); }
        }
    }

    static void Build(ZakoSpawner spawner, string type, Base from)
    {
        if (VirusArt.IndexOf(spawner, type) >= 0) return; // もうある
        if (!VirusArt.TryGetSpec(type, out VirusArt.Spec spec)) return;
        int baseIndex = BaseIndex(spawner, from);
        if (baseIndex < 0) return;

        GameObject t = VirusArt.Clone(spawner, baseIndex, type);
        if (t == null) return;
        bool ok = false;
        try
        {
            if (!VirusArt.Dress(t, type, spec.Fps)) return; // 絵が無い：作らない（下で捨てる）
            HideParts(t);

            // ボットネット：小さい雑魚（0.8 倍）の速さのまま、大きさは歩く雑魚と同じにもどす
            if (from == Base.Small)
            {
                GameObject walker = spawner.Template(0);
                t.transform.localScale = walker != null ? walker.transform.localScale : Vector3.one;
            }
            // 撃つ雑魚：頭の上の「!」を、新しい体の上に下ろす（体の上から 0.75 マス。今の雑魚と同じ間）
            if (from == Base.Shooter)
            {
                Transform mark = t.transform.Find("WindupMark");
                if (mark != null)
                {
                    Vector3 p = mark.localPosition;
                    p.y = spec.HitCenterY + spec.HitSize.y * 0.5f + 0.75f;
                    mark.localPosition = p;
                }
            }

            // 足す 1 つ（トロイ・ボム・マクロ）
            if (VirusTwist.Has(type))
            {
                var twist = t.AddComponent<VirusTwist>();
                twist.Configure(type);
                if (type == "Macro")
                {
                    // 落ちている書類から始める：歩かない・当たりなし（VirusTwist が 4 マス以内か 4 秒で起こす）
                    ZakoWalker w = t.GetComponent<ZakoWalker>();
                    if (w != null) w.enabled = false;
                    Transform body = VirusArt.BodyOf(t);
                    if (body != null) body.gameObject.SetActive(false);
                }
            }

            ok = spawner.AddTemplate(t, 0f) >= 0;
        }
        finally
        {
            if (!ok) UnityEngine.Object.Destroy(t); // 足せなかったひな形は捨てる
        }
    }

    /// <summary>もとにする雑魚のひな形の番号。無い・形がちがうなら -1</summary>
    static int BaseIndex(ZakoSpawner spawner, Base from)
    {
        switch (from)
        {
            case Base.Walker:
            {
                GameObject g = spawner.Template(0);
                return g != null && g.GetComponent<ZakoWalker>() != null && g.GetComponent<Health>() != null ? 0 : -1;
            }
            case Base.Shooter:
            {
                GameObject g = spawner.Template(1);
                return g != null && g.GetComponent<ZakoShooter>() != null && g.GetComponent<Health>() != null ? 1 : -1;
            }
            case Base.Small: return Valid(spawner, ZakoVariants.Small, typeof(ZakoWalker));
            case Base.Elite: return Valid(spawner, ZakoVariants.Elite, typeof(ZakoElite));
            case Base.Flyer: return Valid(spawner, ZakoVariants.Flyer, typeof(ZakoFlyer));
            case Base.Charger: return Valid(spawner, ZakoVariants.Charger, typeof(ZakoCharger));
        }
        return -1;
    }

    static int Valid(ZakoSpawner spawner, int index, Type need)
    {
        if (index < 0) return -1;
        GameObject g = spawner.Template(index);
        if (g == null || g.GetComponent<Health>() == null || g.GetComponent(need) == null) return -1;
        return index;
    }

    static void HideParts(GameObject t)
    {
        foreach (Transform tr in t.GetComponentsInChildren<Transform>(true))
        {
            if (tr == t.transform) continue;
            if (Array.IndexOf(HiddenParts, tr.name) >= 0) tr.gameObject.SetActive(false);
        }
    }
}

/// <summary>
/// 【仮】着せかえの「足す 1 つ」（5-1）。種類の名前で分ける。ほかの部品（ZakoCharger など）は書きかえない。
/// ・トロイ：ZakoWalker.enabled が false になった＝ため始め（ZakoCharger がそうしている）。ため 0.5 秒でコマ 1 → 2、走ったらコマ 3（割れて中身）のまま
/// ・ロジックボム：腹の数字が 1.5 秒ごとに 3 → 2 → 1 → ふくらんで 0.6 秒止まる（予告）→ 0.2 秒だけ体当たりの箱を 2.0×2.0 → 自分は倒れる
///   （Health.TryTakeDamage で倒す。倒れた数に入る。Destroy はしない：EnemyWave は Died だけを数えるので）
/// ・マクロウイルス：落ちている書類（コマ 2）で動かず、当たりなし（Visual/Body を切る）。プレイヤーが 4 マス以内か 4 秒で、0.4 秒ふるえて歩き出す
/// コマは SpriteFrameAnimation を止めて、SpriteRenderer.sprite を自分で入れる。数値はすべて【仮】
/// </summary>
[DisallowMultipleComponent]
public class VirusTwist : MonoBehaviour
{
    [SerializeField] string _type;

    const float Px = 1f / 18f;
    // トロイ
    const float TrojanStep = 0.25f;  // ため 0.5 秒（ZakoCharger）を半分ずつ、コマ 1 → 2
    // ロジックボム
    const float BombTick = 1.5f;     // 3 → 2 → 1
    const float BombFreeze = 0.6f;   // ふくらんで止まる（予告）
    const float BombBlast = 0.2f;    // 体当たりの箱が大きい間
    const float BombBlastSize = 2f;  // 2.0×2.0 マス
    const int BombKillTries = 30;
    // マクロウイルス
    const float MacroWakeRange = 4f;
    const float MacroWakeTime = 4f;
    const float MacroShake = 0.4f;
    const float MacroWalkFps = 6f;
    const float MacroFailsafe = 6f;  // 何があっても、この秒数で起きる（手の届かない敵を作らない）

    Health _health;
    ZakoWalker _walker;
    SpriteRenderer _art;
    SpriteFrameAnimation _anim;
    Transform _body;
    Vector3 _bodyScale;
    Vector3 _bodyPos;
    Sprite[] _frames;
    float _born;
    int _frame = -1;
    bool _shaking;

    // トロイ
    float _windupAt = -1f;
    bool _broken;
    // ボム（0 数える／1 ふくらむ／2 はじける／3 おわり）
    int _bombStage;
    float _bombAt;
    int _killTries;
    // マクロ（0 落ちている／1 ふるえる／2 歩く）
    int _macroStage;
    float _macroAt;

    /// <summary>足す 1 つがある種類か</summary>
    public static bool Has(string type) => type == "Trojan" || type == "Bomb" || type == "Macro";

    public string Type => _type;

    /// <summary>ひな形を作るときに入れる（VirusSkins）</summary>
    public void Configure(string type) => _type = type;

    void Awake()
    {
        if (string.IsNullOrEmpty(_type)) _type = VirusArt.TypeOf(gameObject);
        _health = GetComponent<Health>();
        _walker = GetComponent<ZakoWalker>();
        _art = VirusArt.ArtOf(gameObject);
        _anim = _art != null ? _art.GetComponent<SpriteFrameAnimation>() : null;
        _body = VirusArt.BodyOf(gameObject);
        if (_body != null)
        {
            _bodyScale = _body.localScale;
            _bodyPos = _body.localPosition;
        }
        _frames = VirusArt.Frames(_type);
    }

    void OnEnable()
    {
        _born = Time.time;
        _frame = -1;
        _windupAt = -1f;
        _broken = false;
        _bombStage = 0;
        _killTries = 0;
        _macroStage = 0;
        try
        {
            if (_anim != null) _anim.enabled = false; // 状態でコマを決める
            if (_type == "Macro")
            {
                SetBody(false);
                if (_walker != null) _walker.enabled = false;
                SetFrame(2);
            }
            else SetFrame(0);
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("VirusTwist.OnEnable." + _type, $"ウイルスの足す 1 つで例外：{e}");
            Recover();
        }
    }

    void OnDisable() => ResetShake();

    // ロジックボムの腹の数字が、左を向いたとき（Visual の左右反転）に鏡文字（3 → E）にならないように、絵だけ反転し直す（2026-11-20）
    void LateUpdate()
    {
        if (_type != "Bomb" || _art == null) return;
        bool mirrored = _art.transform.lossyScale.x < 0f;
        if (_art.flipX != mirrored) _art.flipX = mirrored;
    }

    void Update()
    {
        try
        {
            if (_health != null && _health.IsDead)
            {
                ResetShake();
                return;
            }
            switch (_type)
            {
                case "Trojan": TickTrojan(); break;
                case "Bomb": TickBomb(); break;
                case "Macro": TickMacro(); break;
            }
        }
        catch (Exception e)
        {
            SkinDresser.LogOnce("VirusTwist.Update." + _type, $"ウイルスの足す 1 つで例外：{e}");
            Recover();
        }
    }

    // ───────── トロイの木馬 ─────────

    void TickTrojan()
    {
        if (_broken)
        {
            SetFrame(3);
            return;
        }
        bool winding = _walker != null && !_walker.enabled; // ZakoCharger がため・走る間は ZakoWalker を止める
        if (!winding)
        {
            _windupAt = -1f;
            SetFrame(0);
            return;
        }
        if (_windupAt < 0f) _windupAt = Time.time;
        float t = Time.time - _windupAt;
        if (t >= TrojanStep * 2f)
        {
            _broken = true; // 走った：もう化けの皮ははがれたまま
            SetFrame(3);
        }
        else SetFrame(t < TrojanStep ? 1 : 2);
    }

    // ───────── ロジックボム ─────────

    void TickBomb()
    {
        float now = Time.time;
        switch (_bombStage)
        {
            case 0:
            {
                int n = Mathf.FloorToInt((now - _born) / BombTick);
                if (n < 3)
                {
                    SetFrame(n); // 3 → 2 → 1
                    break;
                }
                _bombStage = 1; // ふくらんで止まる（予告）
                _bombAt = now;
                SetFrame(3);
                if (_walker != null) _walker.enabled = false;
                break;
            }
            case 1:
                Shake();
                if (now - _bombAt >= BombFreeze)
                {
                    ResetShake();
                    Blast();
                    _bombStage = 2;
                    _bombAt = now;
                }
                break;
            case 2:
                if (now - _bombAt >= BombBlast) KillSelf();
                break;
        }
    }

    void Blast()
    {
        if (_body != null)
        {
            _body.localScale = new Vector3(BombBlastSize, BombBlastSize, 1f);
            _body.localPosition = new Vector3(0f, _bodyPos.y, _bodyPos.z);
            Physics2D.SyncTransforms(); // 次の判定から大きい箱で当たる
        }
        Vector2 c = _body != null ? (Vector2)_body.position : (Vector2)transform.position + Vector2.up * 0.45f;
        FxKit.Ring(c, BombBlastSize, BombBlast + 0.1f, Color.white);
        FxKit.VirusDots(c, Vector2.up, 6);
        StageCamera.Shake(0.05f, 0.12f);
    }

    void KillSelf()
    {
        bool done = false;
        if (_health != null && !_health.IsDead)
        {
            Vector2 c = _body != null ? (Vector2)_body.position : (Vector2)transform.position;
            var info = new DamageInfo
            {
                Amount = Mathf.Max(1, _health.Current),
                GaugeGain = 0,
                SourceTeam = Team.Player, // 同じ陣営（Enemy）からのダメージは入らない
                Knockback = Vector2.zero,
                HitStun = 0f,
                HitPoint = c,
                Source = null,
                Skin = SkinId.None,
                ErrorKind = "",
                Data = null,
            };
            done = _health.TryTakeDamage(info);
        }
        else done = true;

        RestoreBody(); // 倒れなかったときに、大きい箱を残さない
        if (done)
        {
            _bombStage = 3;
            return;
        }
        if (++_killTries >= BombKillTries)
        {
            // 倒せなかった：ふつうの歩く雑魚にもどす（手の届く敵のまま）
            _bombStage = 3;
            if (_walker != null) _walker.enabled = true;
        }
    }

    void RestoreBody()
    {
        if (_body == null) return;
        _body.localScale = _bodyScale;
        _body.localPosition = _bodyPos;
    }

    // ───────── マクロウイルス ─────────

    void TickMacro()
    {
        float now = Time.time;
        switch (_macroStage)
        {
            case 0:
            {
                SetFrame(2);
                Transform p = RewardKit.PlayerOf(StagePacer.Current);
                bool near = p != null && Vector2.Distance(p.position, transform.position) <= MacroWakeRange;
                if (near || now - _born >= MacroWakeTime)
                {
                    _macroStage = 1;
                    _macroAt = now;
                }
                break;
            }
            case 1:
                SetFrame(2);
                Shake();
                if (now - _macroAt >= MacroShake) Wake();
                break;
            case 2:
                SetFrame(Mathf.FloorToInt((now - _macroAt) * MacroWalkFps) % 2);
                break;
        }
        if (_macroStage < 2 && now - _born >= MacroFailsafe) Wake();
    }

    void Wake()
    {
        ResetShake();
        SetBody(true);
        if (_walker != null && !_walker.enabled)
        {
            _walker.enabled = true;
            Transform p = RewardKit.PlayerOf(StagePacer.Current);
            if (p != null) _walker.SetDirection(p.position.x >= transform.position.x ? 1 : -1);
        }
        _macroStage = 2;
        _macroAt = Time.time;
    }

    // ───────── 道具 ─────────

    /// <summary>例外のあと：手の届く、ふつうの雑魚にもどす</summary>
    void Recover()
    {
        try
        {
            ResetShake();
            if (_type == "Macro") Wake();
            else if (_type == "Bomb")
            {
                RestoreBody();
                _bombStage = 3;
                if (_walker != null && _health != null && !_health.IsDead) _walker.enabled = true;
            }
        }
        catch (Exception) { /* ここではもう何もしない */ }
    }

    void SetBody(bool on)
    {
        if (_body != null && _body.gameObject.activeSelf != on) _body.gameObject.SetActive(on);
    }

    void SetFrame(int i)
    {
        if (_art == null || _frames == null || _frames.Length == 0) return;
        i = Mathf.Clamp(i, 0, _frames.Length - 1);
        if (i == _frame && _art.sprite == _frames[i]) return;
        _frame = i;
        _art.sprite = _frames[i];
    }

    /// <summary>±1px でぶるぶるふるえる（絵だけ。当たりは動かさない）</summary>
    void Shake()
    {
        if (_art == null) return;
        _shaking = true;
        float x = (Mathf.FloorToInt(Time.time / 0.04f) % 2 == 0 ? 1f : -1f) * Px;
        _art.transform.localPosition = new Vector3(x, 0f, 0f);
    }

    void ResetShake()
    {
        if (!_shaking || _art == null) return;
        _shaking = false;
        _art.transform.localPosition = Vector3.zero; // Dress が足元に置いた位置
    }
}
