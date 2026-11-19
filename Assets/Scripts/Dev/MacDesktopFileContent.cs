using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>フォルダの内容。スキャン・今回のラン・保存済み図鑑だけを参照する。</summary>
public static class MacDesktopFileContent
{
    public sealed class File
    {
        public readonly string Name,Title,Body;
        public readonly bool OpenDex;
        public File(string name,string title,string body,bool openDex=false)
        { Name=name; Title=title; Body=body; OpenDex=openDex; }
    }

    public static List<File> Build(int folder,IEnumerable<AppUsage> usages,bool hasUsage,bool sample,
        RunState run,IEnumerable<AppDexEntry> history)
    {
        var files=new List<File>();
        var restored=(history??Enumerable.Empty<AppDexEntry>())
            .Where(e=>e!=null&&e.IsDefeated&&!string.IsNullOrWhiteSpace(e.name))
            .OrderBy(e=>e.name,StringComparer.OrdinalIgnoreCase).ToList();
        if(folder==0)
        {
            var rows=(usages??Enumerable.Empty<AppUsage>()).Where(u=>!string.IsNullOrWhiteSpace(u.DisplayName)&&
                u.UsageSeconds>0&&!double.IsNaN(u.UsageSeconds)&&!double.IsInfinity(u.UsageSeconds))
                .OrderByDescending(u=>u.UsageSeconds).ToList();
            string scan=!hasUsage?"まだ端末をスキャンしていません。\n\nタイトルからスキャンすると、読み取ったアプリと使用時間がここに記録されます。":
                (sample?"訓練データ（サンプル）":"スクリーンタイムから読み取り")+"\n\n"+
                (rows.Count==0?"有効なアプリの記録がありません。":string.Join("\n",rows.Select(u=>u.DisplayName+"  "+Duration(u.UsageSeconds))));
            files.Add(new File("端末スキャン.log","端末スキャン",scan));
            string plan=run==null?"駆除計画はまだありません。\n\nスキャンまたは訓練モードから開始すると、今回の駆除対象と順番が決まります。":
                "今回の駆除順\n\n"+string.Join("\n\n",run.Stages.Select((s,i)=>
                    (i+1)+". "+s.DisplayName+(s.IsFinal?"［最終］":"")+"\n"+
                    (i<run.CurrentIndex?"正常化済み":i==run.CurrentIndex?"次の駆除対象":"待機中")))+
                "\n\n正常化："+run.Cleared.Count+" / "+run.Stages.Count;
            files.Add(new File("駆除計画.txt","駆除計画",plan));
            var slots=new List<string>();
            if(run!=null) for(int i=0;i<run.Loadout.Capacity;i++)
            {
                var skill=run.Loadout.Get(i);
                slots.Add((i+1)+"："+(skill!=null?skill.DisplayName:"空き"));
            }
            string equipment=run==null?"相棒はまだ選ばれていません。\n\n開始後、使用時間上位のアプリから最初の能力を選びます。":
                string.Join("\n\n",slots)+"\n\n能力は最大3つ。\nアプリを正常化して、新しい能力を回収できます。";
            files.Add(new File("装備一覧.txt","装備中の能力",equipment));
            files.Add(new File("駆除手順.txt","駆除手順",
                "1. 雑魚を倒す、または道中を進む。\n\n2. ボスを攻撃して解除ゲージを満たす。\n\n3. 逃げ出したウイルス本体を倒す。\n\n4. アプリの能力を回収し、次のアプリへ。"));
        }
        else if(folder==1)
        {
            foreach(var app in restored.Where(e=>!string.IsNullOrWhiteSpace(e.skill)))
                files.Add(new File(app.name+".ability",app.skill,
                    "回収元："+app.name+"\n\n能力："+app.skill+"\n\n初回の正常化："+app.firstDefeated+
                    "\n正常化回数："+app.defeats+"\n\n正常化して回収した能力の記録です。現在の装備は「書類」の装備一覧で確認できます。",true));
        }
        else if(folder==2)
        {
            foreach(var app in restored)
                files.Add(new File(app.name+"_隔離記録.log",app.name+" の駆除記録",
                    "状態：ウイルス駆除済み\n\nアプリ："+app.name+"\n初回の正常化："+app.firstDefeated+
                    "\n正常化回数："+app.defeats+"\n\n感染していたアプリは正常化されました。ここには駆除の記録だけが残っています。",true));
        }
        return files;
    }

    public static string EmptyMessage(int folder) => folder==1?
        "まだ能力を回収していません。\n\nアプリボスを正常化すると、回収した能力がここに記録されます。":
        "まだ駆除の記録がありません。\n\nアプリを正常化すると、そのウイルスの隔離記録がここに残ります。";

    public static string Duration(double seconds)
    {
        if(seconds<=0||double.IsNaN(seconds)||double.IsInfinity(seconds)) return "0分";
        long minutes=(long)(Math.Min(seconds,7*24*3600)/60);
        return minutes>=60?minutes/60+"時間"+minutes%60+"分":minutes+"分";
    }
}
