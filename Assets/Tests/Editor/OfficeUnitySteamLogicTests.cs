using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Office・Unity・Steam のスキルのロジック（EditMode）。Test Runner ＞ EditMode で実行する。
/// ・Office：使うたびに 棒グラフ → 円グラフ → 折れ線グラフ の順に切り替わる
/// ・Unity：同時に置ける数の上限（超えたら古い物から消す）、形の順番
/// ・Steam：実績の判定（1回の戦闘で1度だけ）、バフの延長・重ね
/// </summary>
public class OfficeUnitySteamLogicTests
{
    readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
        _created.Clear();
    }

    T Make<T>(string name) where T : ScriptableObject
    {
        var o = ScriptableObject.CreateInstance<T>();
        o.name = name;
        _created.Add(o);
        return o;
    }

    // ───────── Office ─────────

    [Test]
    public void Office_SkillCyclesBarPieLineInOrder()
    {
        AttackData bar = Make<AttackData>("Bar"), pie = Make<AttackData>("Pie"), line = Make<AttackData>("Line");
        var skill = Make<SkillDefinition>("Office");
        var so = new SerializedObject(skill);
        so.FindProperty("_attack").objectReferenceValue = bar;
        var arr = so.FindProperty("_attackCycle");
        arr.arraySize = 3;
        arr.GetArrayElementAtIndex(0).objectReferenceValue = bar;
        arr.GetArrayElementAtIndex(1).objectReferenceValue = pie;
        arr.GetArrayElementAtIndex(2).objectReferenceValue = line;
        so.ApplyModifiedPropertiesWithoutUndo();

        Assert.IsTrue(skill.HasAttackCycle);
        var expected = new[] { bar, pie, line, bar, pie, line, bar };
        for (int turn = 0; turn < expected.Length; turn++) Assert.AreSame(expected[turn], skill.GetAttack(turn), $"{turn} 回目");
    }

    [Test]
    public void Office_SkillWithoutCycleAlwaysUsesAttack()
    {
        AttackData only = Make<AttackData>("Only");
        var skill = Make<SkillDefinition>("Plain");
        var so = new SerializedObject(skill);
        so.FindProperty("_attack").objectReferenceValue = only;
        so.ApplyModifiedPropertiesWithoutUndo();

        Assert.IsFalse(skill.HasAttackCycle);
        Assert.AreSame(only, skill.GetAttack(0));
        Assert.AreSame(only, skill.GetAttack(5));
    }

    [Test]
    public void SequenceCycle_AdvancesWrapsAndNotifiesNext()
    {
        var cycle = new SequenceCycle<string>(new[] { "bar", "pie", "line" });
        var notified = new List<string>();
        cycle.NextChanged += n => notified.Add(n);

        Assert.AreEqual("bar", cycle.Peek);
        Assert.AreEqual("bar", cycle.Advance());
        Assert.AreEqual("pie", cycle.Advance());
        Assert.AreEqual("line", cycle.Advance());
        Assert.AreEqual("bar", cycle.Peek);
        CollectionAssert.AreEqual(new[] { "pie", "line", "bar" }, notified);

        cycle.Advance();
        cycle.Reset();
        Assert.AreEqual("bar", cycle.Peek);
    }

    [Test]
    public void SpriteFrames_RowPicksColorRowAndWraps()
    {
        var frames = new Sprite[40];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            frames[i].name = $"f{i}";
            _created.Add(frames[i]);
        }

        Sprite[] row1 = SpriteFrames.Row(frames, 10, 1);
        Assert.AreEqual(10, row1.Length);
        Assert.AreSame(frames[10], row1[0]);
        Assert.AreSame(frames[19], row1[9]);
        Assert.AreSame(frames[0], SpriteFrames.Row(frames, 10, 4)[0], "5本目は1行目の色に戻る");
        Assert.AreEqual(40, SpriteFrames.Row(frames, 0, 3).Length, "行が無ければ全部");
    }

    [Test]
    public void SpriteFrames_IndexLoopsOrHoldsLast()
    {
        Assert.AreEqual(0, SpriteFrames.Index(0f, 12f, 10, false));
        Assert.AreEqual(1, SpriteFrames.Index(1f / 12f + 0.001f, 12f, 10, false), "棒グラフの F2");
        Assert.AreEqual(9, SpriteFrames.Index(5f, 12f, 10, false), "最後のコマで止まる");
        Assert.AreEqual(2, SpriteFrames.Index(6f / 16f + 0.001f, 16f, 4, true), "繰り返す");
        Assert.AreEqual(-1, SpriteFrames.Index(0f, 12f, 0, false));
    }

    // ───────── Unity ─────────

    class Item { public string Name; public override string ToString() => Name; }

    [Test]
    public void Unity_SpawnLimitEvictsOldestFirst()
    {
        var list = new BoundedSpawnList<Item>(3);
        var a = new Item { Name = "Cube" };
        var b = new Item { Name = "Sphere" };
        var c = new Item { Name = "Capsule" };
        var d = new Item { Name = "Cube2" };

        Assert.IsEmpty(list.Add(a));
        Assert.IsEmpty(list.Add(b));
        Assert.IsEmpty(list.Add(c));
        Assert.AreEqual(3, list.Count);

        var evicted = list.Add(d);
        Assert.AreEqual(1, evicted.Count);
        Assert.AreSame(a, evicted[0], "一番古い物が消える");
        CollectionAssert.AreEqual(new[] { b, c, d }, list.Items);
    }

    [Test]
    public void Unity_SpawnLimitPrunesGoneObjects()
    {
        var list = new BoundedSpawnList<Item>(3);
        var a = new Item { Name = "a" };
        var b = new Item { Name = "b" };
        list.Add(a);
        list.Add(b);
        list.Prune(i => i == a); // 寿命で消えた
        Assert.AreEqual(1, list.Count);
        Assert.IsEmpty(list.Add(new Item()));
        Assert.IsEmpty(list.Add(new Item()));
        Assert.AreEqual(1, list.Add(new Item()).Count);
    }

    [Test]
    public void Unity_ShapeOrderIsCubeSphereCapsule()
    {
        var shapes = new SequenceCycle<PrimitiveShape>(new[] { PrimitiveShape.Box, PrimitiveShape.Circle, PrimitiveShape.Capsule });
        CollectionAssert.AreEqual(
            new[] { PrimitiveShape.Box, PrimitiveShape.Circle, PrimitiveShape.Capsule, PrimitiveShape.Box },
            new[] { shapes.Advance(), shapes.Advance(), shapes.Advance(), shapes.Advance() });
    }

    // ───────── Steam：実績 ─────────

    static AchievementTracker Tracker(params AchievementSpec[] specs)
    {
        var t = new AchievementTracker(specs);
        t.Begin(0f);
        return t;
    }

    [Test]
    public void Steam_KillsWithinWindow()
    {
        var t = Tracker(new AchievementSpec("5体", AchievementCondition.KillsWithin, 5, 10f));
        var unlocked = new List<int>();
        t.Unlocked += unlocked.Add;

        // 3秒おきでは、10秒の間に4体しか入らない
        foreach (float time in new[] { 0f, 3f, 6f, 9f, 12f }) t.Report(AchievementEvent.Kill, time);
        Assert.IsEmpty(unlocked);

        // 13秒に倒すと、3〜13秒の10秒間で5体になる。そのあと倒しても、もう解除しない
        t.Report(AchievementEvent.Kill, 13f);
        CollectionAssert.AreEqual(new[] { 0 }, unlocked);
        foreach (float time in new[] { 14f, 15f, 16f }) t.Report(AchievementEvent.Kill, time);
        CollectionAssert.AreEqual(new[] { 0 }, unlocked);
    }

    [Test]
    public void Steam_NoDamageForResetsOnDamage()
    {
        var t = Tracker(new AchievementSpec("無傷", AchievementCondition.NoDamageFor, 1, 15f));
        t.Tick(10f);
        t.Report(AchievementEvent.DamageTaken, 10f);
        t.Tick(24f);
        Assert.IsFalse(t.IsUnlocked(0), "ダメージから14秒");
        t.Tick(25f);
        Assert.IsTrue(t.IsUnlocked(0), "ダメージから15秒");
    }

    [Test]
    public void Steam_SkillUsesAndFirstBossDamage()
    {
        var t = Tracker(
            new AchievementSpec("3回", AchievementCondition.SkillUses, 3),
            new AchievementSpec("初ダメージ", AchievementCondition.FirstBossDamage));
        t.Report(AchievementEvent.SkillUsed, 1f);
        t.Report(AchievementEvent.SkillUsed, 2f);
        Assert.IsFalse(t.IsUnlocked(0));
        t.Report(AchievementEvent.SkillUsed, 3f);
        Assert.IsTrue(t.IsUnlocked(0));

        Assert.IsFalse(t.IsUnlocked(1));
        t.Report(AchievementEvent.BossDamaged, 4f);
        Assert.IsTrue(t.IsUnlocked(1));
    }

    [Test]
    public void Steam_UnlocksOncePerBattleAndBeginResets()
    {
        var t = Tracker(new AchievementSpec("初ダメージ", AchievementCondition.FirstBossDamage));
        int count = 0;
        t.Unlocked += _ => count++;
        t.Report(AchievementEvent.BossDamaged, 1f);
        t.Report(AchievementEvent.BossDamaged, 2f);
        Assert.AreEqual(1, count, "同じ戦闘では1度だけ");
        Assert.IsFalse(t.ForceUnlock(0));

        t.Begin(10f); // 次の戦闘
        t.Report(AchievementEvent.BossDamaged, 11f);
        Assert.AreEqual(2, count);
    }

    [Test]
    public void Steam_BossAchievements_HitsLandedAndTimeElapsed()
    {
        var t = Tracker(
            new AchievementSpec("3回当てる", AchievementCondition.HitsLanded, 3),
            new AchievementSpec("20秒", AchievementCondition.TimeElapsed, 1, 20f));
        for (int i = 0; i < 2; i++) t.Report(AchievementEvent.HitLanded, i);
        Assert.IsFalse(t.IsUnlocked(0));
        t.Report(AchievementEvent.HitLanded, 3f);
        Assert.IsTrue(t.IsUnlocked(0));

        t.Tick(19.9f);
        Assert.IsFalse(t.IsUnlocked(1));
        t.Tick(20f);
        Assert.IsTrue(t.IsUnlocked(1));
    }

    // ───────── Steam：バフ ─────────

    [Test]
    public void Steam_BuffExtendsByDefault()
    {
        var buffs = new BuffStack();
        BuffSpec spec = BuffSpec.Default; // +30%・+15%・8秒・延長
        buffs.Apply("steam", spec, 0f);
        Assert.AreEqual(1.3f, buffs.AttackMultiplier(1f), 0.0001f);
        Assert.AreEqual(1.15f, buffs.SpeedMultiplier(1f), 0.0001f);

        buffs.Apply("steam", spec, 5f); // 残り3秒に8秒を足す → 16秒まで
        Assert.AreEqual(1.3f, buffs.AttackMultiplier(6f), 0.0001f, "延長では効果は重ならない");
        Assert.AreEqual(10f, buffs.Remaining("steam", 6f), 0.0001f);
        Assert.IsTrue(buffs.Has("steam", 15.9f));
        Assert.IsFalse(buffs.Has("steam", 16f));
        Assert.AreEqual(1f, buffs.AttackMultiplier(16f), 0.0001f);
    }

    [Test]
    public void Steam_BuffStackModeAddsEffects()
    {
        var buffs = new BuffStack();
        BuffSpec spec = BuffSpec.Default;
        spec.StackMode = BuffStackMode.Stack;
        buffs.Apply("steam", spec, 0f);
        buffs.Apply("steam", spec, 4f);
        Assert.AreEqual(1.6f, buffs.AttackMultiplier(5f), 0.0001f);
        Assert.AreEqual(1.3f, buffs.AttackMultiplier(9f), 0.0001f, "1つ目は8秒で切れる");
        Assert.AreEqual(1f, buffs.AttackMultiplier(12f), 0.0001f);
    }

    [Test]
    public void Steam_ToastShowsGainedParameters()
    {
        BuffSpec spec = BuffSpec.Default;
        Assert.AreEqual("攻撃+30% 速度+15% 8秒", AchievementToast.FormatBuff(spec, false, 8f, false));
        Assert.AreEqual("攻撃+30% 速度+15% +8秒(残り12秒)", AchievementToast.FormatBuff(spec, true, 12f, false));
        Assert.AreEqual("全員 攻撃+30% 速度+15% 8秒", AchievementToast.FormatBuff(spec, false, 8f, true));
    }

    [Test]
    public void Steam_AttackMultiplierScalesIntegerValues()
    {
        Assert.AreEqual(4, StatModifiers.Scale(3, 1.3f));
        Assert.AreEqual(13, StatModifiers.Scale(10, 1.3f));
        Assert.AreEqual(0, StatModifiers.Scale(0, 1.3f), "ゲージが増えない攻撃は 0 のまま");
        Assert.AreEqual(5, StatModifiers.Scale(5, 1f));
    }
}
