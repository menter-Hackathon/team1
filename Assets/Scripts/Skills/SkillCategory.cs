/// <summary>
/// スキル（＝感染したアプリ）のカテゴリ。
/// 【提案】一覧はまだ相談中で、確定しているのは AI だけ。
/// アセットには数値で保存されるので、値は明示的に書いておく。項目を消すときは欠番にして、ほかの値は変えないこと。
/// </summary>
public enum SkillCategory
{
    Unknown = 0,     // 対応表にないアプリ
    Browser = 1,     // ブラウザ
    SocialChat = 2,  // SNS・チャット
    Dev = 3,         // 開発
    AI = 4,          // AI（2026-11-15 まさぴ〜案で確定）
    Office = 5,      // Office・仕事
    Game = 6,        // ゲーム
    Creative = 7,    // クリエイティブ
    Media = 8,       // 音楽・動画
}
