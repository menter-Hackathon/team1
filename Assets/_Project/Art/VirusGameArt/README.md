# VirusGame 素材一式

## 中身

```
VirusGameArt/                ← Unity に入れるのはこのフォルダだけ
├── AppBoss/                 アプリボス（Office / Unity / Steam）＋ README
├── SkillEffects/            スキルエフェクト（Office / Unity / Steam）＋ README
└── Enemies/
    ├── Minion/Design/       雑魚ウイルス Wave1〜3（デザイン確定版の静止画。アニメは未制作）
    └── FinalBoss/Design/    最終ボス v2（静止画）＋ Layers/（ウィンドウ・本体・触手・ノイズを分けたもの）
_Preview_NotForUnity/        確認用の GIF とプレビュー画像（Unity には入れない）
```

## 推奨する配置先

`Assets/_Project/Art/VirusGameArt/`

## 共通のインポート設定

- 全素材 **3倍書き出し**（既存のプレイヤー `charactar.png` と同じ密度）
- Texture Type = Sprite (2D and UI) / Filter Mode = **Point (no filter)** / Compression = **None** / Generate Mip Maps = Off
- **Pixels Per Unit はプレイヤーのスプライトと同じ値**にそろえる
- スプライトシートは Sprite Mode = Multiple → Sprite Editor → Slice → **Grid By Cell Size**（セルサイズは各 README）
- `_Virus` 付き＝アプリボス用のウイルス配色。プレイヤー版とセル・フレーム構成が同じなので、差し替えだけで使える
