using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

// Steam スキルの AchievementSpec（グローバル名前空間）とぶつからないよう、実績システムの名前空間の中に置く
namespace Team1.Achievements.Tests
{

    /// <summary>
    /// 実績システムの判定（EditMode）。Test Runner ＞ EditMode で実行する。
    /// 偽の ScreenTimeSnapshot を作って、PCの使い方の判定を確かめる。プレイ・アプリ図鑑の判定も少し確かめる。
    /// 判定のロジックは UnityEngine を使わないので、このファイルも UnityEngine を使わない。
    /// </summary>
    public class AchievementScreenTimeTests
    {
        // 2026-11-15 は日曜、11-16 は月曜
        static readonly DateTime Sun = new DateTime(2026, 11, 15);
        static readonly DateTime Mon = new DateTime(2026, 11, 16);

        static AchievementSpec Spec(string id, AchievementTrigger trigger, IAchievementCondition condition, double target) => new AchievementSpec
        {
            Id = id, Name = id, Trigger = trigger, Condition = condition, Target = target,
            Category = trigger == AchievementTrigger.LaunchScan ? AchievementCategory.PcUsage : AchievementCategory.Play
        };

        static AchievementEngine Engine(params AchievementSpec[] specs) => new AchievementEngine(specs, new AchievementSaveData());

        static AppCategoryMap Categories() => new AppCategoryMap()
            .Add(AppUsageCategory.Entertainment, "YouTube", "Netflix")
            .Add(AppUsageCategory.Sns, "Discord", "X")
            .Add(AppUsageCategory.Work, "Visual Studio Code", "Slack");

        static List<AchievementUnlock> Scan(AchievementEngine engine, ScreenTimeSnapshot st, DateTime now) =>
            engine.Evaluate(AchievementTrigger.LaunchScan, new AchievementContext { Now = now, ScreenTime = st, Categories = Categories() });

        // ───────── 朝型 ─────────

        [Test]
        public void Morning_UsageAt0859_Counts()
        {
            var engine = Engine(Spec("M", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 1));
            // 8:50 から 10 分（8:59 台まで）。8時台に 10 分
            var st = new ScreenTimeSnapshot(Mon.AddHours(12)).AddSession("Safari", Mon.AddHours(8).AddMinutes(50), 600);
            var unlocks = Scan(engine, st, Mon.AddHours(12));
            Assert.AreEqual(1, unlocks.Count);
            Assert.AreEqual(1, engine.Record("M").count);
        }

        [Test]
        public void Morning_UsageAt0900_DoesNotCount()
        {
            var engine = Engine(Spec("M", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 1));
            var st = new ScreenTimeSnapshot(Mon.AddHours(12)).AddSession("Safari", Mon.AddHours(9), 3600);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(12)).Count);
            Assert.AreEqual(0, engine.Record("M").count);
        }

        [Test]
        public void Morning_SessionCrossing0900_OnlyBeforeNineCounts()
        {
            var engine = Engine(Spec("M", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 1));
            // 8:55〜9:10：8時台は 5 分だけ → 10 分に足りない
            var st = new ScreenTimeSnapshot(Mon.AddHours(12)).AddSession("Safari", Mon.AddHours(8).AddMinutes(55), 15 * 60);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(12)).Count);
        }

        [Test]
        public void Morning_ExactlyTenMinutesTotal_Counts()
        {
            var engine = Engine(Spec("M", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 1));
            // 5時台に 4 分、7時台に 6 分（別のアプリ）＝ちょうど 10 分
            var st = new ScreenTimeSnapshot(Mon.AddHours(12))
                .AddSession("Safari", Mon.AddHours(5), 240)
                .AddSession("Mail", Mon.AddHours(7).AddMinutes(30), 360);
            Assert.AreEqual(1, Scan(engine, st, Mon.AddHours(12)).Count);
        }

        [Test]
        public void Morning_NineMinutesFiftyNineSeconds_DoesNotCount()
        {
            var engine = Engine(Spec("M", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 1));
            var st = new ScreenTimeSnapshot(Mon.AddHours(12)).AddSession("Safari", Mon.AddHours(6), 599);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(12)).Count);
        }

        [Test]
        public void Morning_UsageBefore5_DoesNotCount()
        {
            var engine = Engine(Spec("M", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 1));
            var st = new ScreenTimeSnapshot(Mon.AddHours(12)).AddSession("Safari", Mon.AddHours(4).AddMinutes(30), 1800);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(12)).Count);
        }

        [Test]
        public void Morning_SameDayScannedTwice_DoesNotDoubleCount()
        {
            var engine = Engine(Spec("M7", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 7));
            var st = new ScreenTimeSnapshot(Mon.AddHours(12)).AddSession("Safari", Mon.AddHours(6), 1200);
            Scan(engine, st, Mon.AddHours(12));
            Scan(engine, st, Mon.AddHours(15));
            Scan(engine, st, Mon.AddHours(20));
            Assert.AreEqual(1, engine.Record("M7").count);
            Assert.AreEqual(1, engine.Record("M7").progress);
        }

        [Test]
        public void Morning_TodayBeforeNine_IsNotSettledYet()
        {
            var engine = Engine(Spec("M7", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 7));
            // 7:00 に起動：今日の朝はまだ終わっていないので数えない。9:30 にもう一度起動したら数える（1回だけ）
            var early = new ScreenTimeSnapshot(Mon.AddHours(7)).AddSession("Safari", Mon.AddHours(6), 900);
            Scan(engine, early, Mon.AddHours(7));
            Assert.AreEqual(0, engine.Record("M7").count);
            var later = new ScreenTimeSnapshot(Mon.AddHours(9.5)).AddSession("Safari", Mon.AddHours(6), 900).AddSession("Safari", Mon.AddHours(8), 300);
            Scan(engine, later, Mon.AddHours(9.5));
            Assert.AreEqual(1, engine.Record("M7").count);
        }

        [Test]
        public void Morning_CountsAcrossScans_UntilTarget()
        {
            var engine = Engine(Spec("M2", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 2));
            var day1 = Mon.AddDays(-3);
            var st1 = new ScreenTimeSnapshot(day1.AddHours(20)).AddSession("Safari", day1.AddHours(6), 900);
            Assert.AreEqual(0, Scan(engine, st1, day1.AddHours(20)).Count);
            // 次の起動：同じ日も読めるが数えない。新しい日だけ数える
            var st2 = new ScreenTimeSnapshot(Mon.AddHours(20))
                .AddSession("Safari", day1.AddHours(6), 900)
                .AddSession("Safari", Mon.AddHours(6), 900);
            var unlocks = Scan(engine, st2, Mon.AddHours(20));
            Assert.AreEqual(1, unlocks.Count);
            Assert.AreEqual(2, engine.Record("M2").count);
        }

        // ───────── デトックス ─────────

        // 判定する週＝先週（Mon-7〜Sun）。比べる前の週＝Mon-14〜。今週は木曜の夜に起動
        static readonly DateTime DetoxNow = Mon.AddDays(3).AddHours(20);

        static ScreenTimeSnapshot DetoxData(string app, int weekBeforeSeconds, int lastWeekSeconds)
        {
            var lastWeekStart = Mon.AddDays(-7);
            var weekBefore = Mon.AddDays(-14);
            return new ScreenTimeSnapshot(DetoxNow, weekBefore)
                .AddSession(app, weekBefore.AddDays(2).AddHours(20), weekBeforeSeconds)
                .AddSession(app, lastWeekStart.AddDays(4).AddHours(20), lastWeekSeconds);
        }

        [Test]
        public void Detox_Exactly20Percent_Unlocks()
        {
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            var unlocks = Scan(engine, DetoxData("YouTube", 3000, 2400), DetoxNow);
            Assert.AreEqual(1, unlocks.Count);
            StringAssert.Contains("YouTube（エンタメ）", unlocks[0].Reason);
            StringAssert.Contains("20%", unlocks[0].Reason);
        }

        [Test]
        public void Detox_19Percent_DoesNotUnlock_AndWeekIsJudged()
        {
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            Assert.AreEqual(0, Scan(engine, DetoxData("YouTube", 3000, 2430), DetoxNow).Count);
            Assert.AreEqual(Mon.AddDays(-7), engine.Record("D").LastCountedDay); // 先週は判定し終えた（次は今週が終わってから）
        }

        [Test]
        public void Detox_InProgressWeek_DoesNotUnlock_EvenOnMonday()
        {
            // 先週は YouTube を見ていて、今週（月曜の朝）はまだ見ていない：週が終わるまで解除しない
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            var st = new ScreenTimeSnapshot(Mon.AddHours(9), Mon.AddDays(-7)).AddSession("YouTube", Mon.AddDays(-7).AddHours(20), 3600);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(9)).Count);
            Assert.Greater(engine.Record("D").progress, 0); // ペースとしては減っている
        }

        [Test]
        public void Detox_FailedWeekStartsOver_NextWeekCanUnlock()
        {
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            var a = Mon.AddDays(-21); var b = Mon.AddDays(-14); var c = Mon.AddDays(-7);
            // 週B は週A より 10% 減：達成できず
            var firstNow = c.AddDays(3).AddHours(20);
            var st1 = new ScreenTimeSnapshot(firstNow, a)
                .AddSession("YouTube", a.AddHours(20), 3000)
                .AddSession("YouTube", b.AddHours(20), 2700);
            Assert.AreEqual(0, Scan(engine, st1, firstNow).Count);
            Assert.AreEqual(b, engine.Record("D").LastCountedDay);
            // 週C は週B より 25% 減：やり直した週で達成
            var st2 = new ScreenTimeSnapshot(DetoxNow, a)
                .AddSession("YouTube", a.AddHours(20), 3000)
                .AddSession("YouTube", b.AddHours(20), 2700)
                .AddSession("YouTube", c.AddHours(20), 2025);
            var unlocks = Scan(engine, st2, DetoxNow);
            Assert.AreEqual(1, unlocks.Count);
            StringAssert.Contains("25%", unlocks[0].Reason);
        }

        [Test]
        public void Detox_JudgedWeekIsNotJudgedAgain()
        {
            // 週B（10%減）を判定した後で、週A と週C を比べ直したりしない：週C は週B と比べる（2700→2500 は 7%）
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            var a = Mon.AddDays(-21); var b = Mon.AddDays(-14); var c = Mon.AddDays(-7);
            var st = new ScreenTimeSnapshot(DetoxNow, a)
                .AddSession("YouTube", a.AddHours(20), 3000)
                .AddSession("YouTube", b.AddHours(20), 2700)
                .AddSession("YouTube", c.AddHours(20), 2500);
            Assert.AreEqual(0, Scan(engine, st, DetoxNow).Count);
            Assert.AreEqual(0, Scan(engine, st, DetoxNow.AddHours(1)).Count);
            Assert.AreEqual(c, engine.Record("D").LastCountedDay);
        }

        [Test]
        public void Detox_WorkAppIsNotTarget()
        {
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            Assert.AreEqual(0, Scan(engine, DetoxData("Visual Studio Code", 36000, 0), DetoxNow).Count);
        }

        [Test]
        public void Detox_AppNotInTableIsNotTarget()
        {
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            Assert.AreEqual(0, Scan(engine, DetoxData("Calculator", 36000, 0), DetoxNow).Count);
        }

        [Test]
        public void Detox_SnsWithSubtitleMatchesTable()
        {
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            var unlocks = Scan(engine, DetoxData("Discord - 一緒にしゃべっ…", 6000, 1200), DetoxNow);
            Assert.AreEqual(1, unlocks.Count);
            StringAssert.Contains("（SNS）", unlocks[0].Reason);
            StringAssert.Contains("80%", unlocks[0].Reason);
        }

        [Test]
        public void Detox_WithoutLastWeekData_IsSkipped()
        {
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            var st = new ScreenTimeSnapshot(Mon.AddHours(20), Mon).AddSession("YouTube", Mon.AddHours(19), 60);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(20)).Count);
        }

        // ───────── 週の区切り ─────────

        [Test]
        public void Week_StartsOnMonday()
        {
            Assert.AreEqual(Mon, WeekMath.WeekStart(Mon));
            Assert.AreEqual(Mon, WeekMath.WeekStart(Mon.AddDays(6).AddHours(23).AddMinutes(59)));
            Assert.AreEqual(Mon.AddDays(-7), WeekMath.WeekStart(Sun.AddHours(23).AddMinutes(59)));
        }

        [Test]
        public void Detox_SundayUsageBelongsToLastWeek_MondayToThisWeek()
        {
            var engine = Engine(Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            // 日曜 23:30 から 60 分見た：23:30〜23:59 は先週（日曜）、0:00〜0:30 は今週（月曜）
            var lastWeekStart = Mon.AddDays(-7);
            var st = new ScreenTimeSnapshot(Mon.AddHours(10), lastWeekStart).AddSession("YouTube", Sun.AddHours(23).AddMinutes(30), 3600);
            Assert.AreEqual(1800, st.SecondsOf("YouTube", lastWeekStart, Mon));
            Assert.AreEqual(1800, st.SecondsOf("YouTube", Mon, Mon.AddDays(1)));
            // 先週 30 分 → 今週 30 分：減っていない
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(10)).Count);
        }

        [Test]
        public void NewApp_SundayIsLastWeek_MondayIsThisWeek()
        {
            var engine = Engine(Spec("N", AchievementTrigger.LaunchScan, new NewAppCondition(), 1));
            var st = new ScreenTimeSnapshot(Mon.AddHours(10), Mon.AddDays(-14))
                .AddSession("Safari", Mon.AddDays(-10).AddHours(12), 600)
                .AddSession("Figma", Sun.AddHours(22), 600)      // 日曜に初めて使った：先週のこと
                .AddSession("Safari", Mon.AddHours(9), 600)
                .AddSession("Figma", Mon.AddHours(9), 600);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(10)).Count);

            var engine2 = Engine(Spec("N", AchievementTrigger.LaunchScan, new NewAppCondition(), 1));
            var st2 = new ScreenTimeSnapshot(Mon.AddHours(10), Mon.AddDays(-14))
                .AddSession("Safari", Mon.AddDays(-10).AddHours(12), 600)
                .AddSession("Figma", Mon.AddMinutes(5), 600);    // 月曜 0:05 に初めて：今週のこと
            var unlocks = Scan(engine2, st2, Mon.AddHours(10));
            Assert.AreEqual(1, unlocks.Count);
            Assert.AreEqual("Figma", engine2.Record("N").detail);
        }

        [Test]
        public void NewApp_WithoutEarlierRecords_IsSkipped()
        {
            var engine = Engine(Spec("N", AchievementTrigger.LaunchScan, new NewAppCondition(), 1));
            var st = new ScreenTimeSnapshot(Mon.AddHours(10), Mon).AddSession("Figma", Mon.AddHours(9), 600);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(10)).Count);
        }

        // ───────── そのほかの PC の使い方 ─────────

        [Test]
        public void AppsPerDay_TenApps_Unlocks()
        {
            var engine = Engine(Spec("A", AchievementTrigger.LaunchScan, new AppsPerDayCondition(), 10));
            var st = new ScreenTimeSnapshot(Mon.AddHours(22));
            for (int i = 0; i < 9; i++) st.AddSession("App" + i, Mon.AddHours(10 + i), 60);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(22)).Count);
            st.AddSession("App9", Mon.AddHours(21), 60);
            Assert.AreEqual(1, Scan(engine, st, Mon.AddHours(22)).Count);
        }

        [Test]
        public void Streak_SevenDays_Unlocks_AndGapResets()
        {
            var engine = Engine(Spec("S", AchievementTrigger.LaunchScan, new StreakDaysCondition(), 7));
            var start = Mon.AddDays(-10);
            var st = new ScreenTimeSnapshot(Mon.AddHours(12), start);
            for (int i = 0; i < 4; i++) st.AddSession("Safari", start.AddDays(i).AddHours(12), 60); // 4日
            // 1日あける（start+4）
            for (int i = 5; i <= 10; i++) st.AddSession("Safari", start.AddDays(i).AddHours(10), 60); // 6日（今日まで）
            Assert.AreEqual(0, Scan(engine, st, Mon.AddHours(12)).Count);
            Assert.AreEqual(6, engine.Record("S").count);

            // 次の日に使えば 7 日
            var tomorrow = Mon.AddDays(1);
            st = new ScreenTimeSnapshot(tomorrow.AddHours(12), start).AddSession("Safari", tomorrow.AddHours(9), 60);
            for (int i = 5; i <= 10; i++) st.AddSession("Safari", start.AddDays(i).AddHours(10), 60);
            var unlocks = Scan(engine, st, tomorrow.AddHours(12));
            Assert.AreEqual(1, unlocks.Count);
            Assert.AreEqual(7, engine.Record("S").count);
        }

        [Test]
        public void Streak_SameDayScannedTwice_DoesNotDoubleCount()
        {
            var engine = Engine(Spec("S", AchievementTrigger.LaunchScan, new StreakDaysCondition(), 7));
            var st = new ScreenTimeSnapshot(Mon.AddHours(12), Mon).AddSession("Safari", Mon.AddHours(9), 60);
            Scan(engine, st, Mon.AddHours(12));
            Scan(engine, st, Mon.AddHours(18));
            Assert.AreEqual(1, engine.Record("S").count);
        }

        [Test]
        public void Weekend_WorkSixtyMinutesOnSaturday_Unlocks_WeekdayDoesNot()
        {
            var sat = Mon.AddDays(-2);
            var engine = Engine(Spec("W", AchievementTrigger.LaunchScan, new WeekendWorkMinutesCondition(), 60));
            var weekday = new ScreenTimeSnapshot(Mon.AddHours(20)).AddSession("Visual Studio Code", Mon.AddHours(10), 7200);
            Assert.AreEqual(0, Scan(engine, weekday, Mon.AddHours(20)).Count);

            var engine2 = Engine(Spec("W", AchievementTrigger.LaunchScan, new WeekendWorkMinutesCondition(), 60));
            var st = new ScreenTimeSnapshot(Mon.AddHours(20))
                .AddSession("Visual Studio Code", sat.AddHours(10), 2400)
                .AddSession("Slack", sat.AddHours(15), 1200)        // 合わせて 60 分
                .AddSession("YouTube", sat.AddHours(20), 7200);     // エンタメは数えない
            var unlocks = Scan(engine2, st, Mon.AddHours(20));
            Assert.AreEqual(1, unlocks.Count);
            StringAssert.Contains("60分", unlocks[0].Reason);
        }

        [Test]
        public void LaunchScan_WithoutScreenTime_SkipsAndKeepsLastScan()
        {
            var engine = Engine(Spec("M", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 1));
            var unlocks = engine.Evaluate(AchievementTrigger.LaunchScan, new AchievementContext { Now = Mon, ScreenTime = null });
            Assert.AreEqual(0, unlocks.Count);
            Assert.IsNull(engine.Save.LastScanAt);
        }

        [Test]
        public void Unlocked_IsNotUnlockedTwice()
        {
            var engine = Engine(Spec("M", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 1));
            var st = new ScreenTimeSnapshot(Mon.AddDays(1)).AddSession("Safari", Mon.AddHours(6), 900).AddSession("Safari", Mon.AddDays(1).AddHours(6), 900);
            Assert.AreEqual(1, Scan(engine, st, Mon.AddHours(12)).Count);
            Assert.AreEqual(0, Scan(engine, st, Mon.AddDays(1).AddHours(12)).Count);
        }

        // ───────── プレイ・アプリ図鑑 ─────────

        [Test]
        public void Play_StageClearEvents()
        {
            var engine = Engine(
                Spec("WAVE", AchievementTrigger.InPlay, new WaveClearedCondition(), 1),
                Spec("NODMG", AchievementTrigger.InPlay, new NoDamageWaveCondition(), 1),
                Spec("SKILLS", AchievementTrigger.InPlay, new WaveClearedWithSkillsCondition(), 3),
                Spec("FINAL", AchievementTrigger.InPlay, new FinalBossDefeatedCondition(), 1));
            var hit = new PlayEvent { Kind = PlayEventKind.StageCleared, StageIndex = 0, TookDamage = true, EquippedSkills = 2, AppName = "Chrome" };
            var ids = engine.Evaluate(AchievementTrigger.InPlay, new AchievementContext { Play = hit }).Select(u => u.Spec.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { "WAVE" }, ids);
            Assert.AreEqual(2, engine.Record("SKILLS").progress);

            var clean = new PlayEvent { Kind = PlayEventKind.StageCleared, StageIndex = 3, IsFinalBoss = true, TookDamage = false, EquippedSkills = 3, AppName = "Terminal" };
            ids = engine.Evaluate(AchievementTrigger.InPlay, new AchievementContext { Play = clean }).Select(u => u.Spec.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { "NODMG", "SKILLS", "FINAL" }, ids);
        }

        [Test]
        public void Play_SamePartnerThreeClears()
        {
            var engine = Engine(Spec("P", AchievementTrigger.InPlay, new ClearsWithSamePartnerCondition(), 3));
            int unlocked = 0;
            foreach (var partner in new[] { "Slack", "Figma", "Slack", "Slack" })
            {
                engine.Save.AddPartnerClear(partner);
                unlocked += engine.Evaluate(AchievementTrigger.InPlay, new AchievementContext { Play = new PlayEvent { Kind = PlayEventKind.RunCompleted, Partner = partner } }).Count;
            }
            Assert.AreEqual(1, unlocked);
        }

        [Test]
        public void Dex_Top10ExcludesPartner()
        {
            var engine = Engine(new AchievementSpec { Id = "T", Trigger = AchievementTrigger.OnPurify, Condition = new PurifiedTop10Condition(), Target = 1, Category = AchievementCategory.Dex });
            var log = new PurifyLog { TopApps = new List<string> { "Chrome", "Slack", "Figma" }, Partner = "Figma" };
            log.PurifiedApps.Add("Chrome");
            Assert.AreEqual(0, engine.Evaluate(AchievementTrigger.OnPurify, new AchievementContext { Purify = log }).Count);
            Assert.AreEqual(0.5, engine.Record("T").progress, 1e-6);
            log.PurifiedApps.Add("Slack");
            Assert.AreEqual(1, engine.Evaluate(AchievementTrigger.OnPurify, new AchievementContext { Purify = log }).Count);
        }

        [Test]
        public void AlmostThere_PicksMostProgressedLocked()
        {
            var engine = Engine(
                Spec("A", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 7),
                Spec("B", AchievementTrigger.LaunchScan, new StreakDaysCondition(), 7));
            engine.Record("A").progress = 2;
            engine.Record("B").progress = 5;
            Assert.AreEqual("B", engine.AlmostThere().Id);
        }

        // ───────── 達成したアプリ ─────────

        [Test]
        public void Unlock_RecordsAchievingApp()
        {
            var engine = Engine(
                Spec("M", AchievementTrigger.LaunchScan, new MorningDaysCondition(), 1),
                Spec("D", AchievementTrigger.LaunchScan, new DetoxReductionCondition(), 0.2));
            // 朝はメールを 8 分・Safari を 4 分：いちばん使ったメールで達成
            var st = DetoxData("YouTube", 3000, 1000)
                .AddSession("メール", Mon.AddHours(6), 480)
                .AddSession("Safari", Mon.AddHours(7), 240);
            var unlocks = Scan(engine, st, DetoxNow);
            Assert.AreEqual(2, unlocks.Count);
            Assert.AreEqual("メール", engine.Record("M").app);
            Assert.AreEqual("YouTube", engine.Record("D").app);
            Assert.AreEqual("YouTube", unlocks.First(u => u.Spec.Id == "D").App);
        }

        [Test]
        public void Unlock_PlayRecordsStageApp_ForcedHasNone()
        {
            var engine = Engine(
                Spec("WAVE", AchievementTrigger.InPlay, new WaveClearedCondition(), 1),
                Spec("FINAL", AchievementTrigger.InPlay, new FinalBossDefeatedCondition(), 1));
            engine.Evaluate(AchievementTrigger.InPlay, new AchievementContext { Play = new PlayEvent { Kind = PlayEventKind.StageCleared, AppName = "Slack" } });
            Assert.AreEqual("Slack", engine.Record("WAVE").app);
            engine.ForceUnlock("FINAL", Mon);
            Assert.AreEqual("", engine.Record("FINAL").app);
        }

        [Test]
        public void Csv_ParsesBomQuotesAndKeys()
        {
            var t = CsvTable.Parse("﻿app_name,category,match_key\n\"Excel / Word / PowerPoint\",作業,\nDiscord,SNS,\"Discord|Discord PTB\"\n");
            Assert.AreEqual(2, t.Rows.Count);
            CollectionAssert.AreEqual(new[] { "Excel", "Word", "PowerPoint" }, AppCategoryMap.KeysOf(t.Get(t.Rows[0], "app_name"), t.Get(t.Rows[0], "match_key")));
            CollectionAssert.AreEqual(new[] { "Discord", "Discord PTB" }, AppCategoryMap.KeysOf(t.Get(t.Rows[1], "app_name"), t.Get(t.Rows[1], "match_key")));
        }
    }
}
