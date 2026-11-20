# team1（VIRUS DESKTOP）

Mac の使用時間の長いアプリが、ウイルスに感染したボスになる 2D ドット絵アクション。Unity 2022.3.62f2。

## アプリボスの作業

**アプリボス（ボスの技・プレイヤーの能力・ステージのギミック）を足す・直す作業では、始める前に必ず [docs/app-boss-guide.md](docs/app-boss-guide.md) を読む。** 共通の基盤（キット）の使い方・さわってよいファイル・守る決まりが書いてある。

- 新しいアプリボスは、キット型（`Assets/Scripts/Kits/<Label>/` と `Assets/Scripts/Editor/Kits/<Label>KitAssets.cs`）で作る。
- 共通の部品（`Assets/Scripts/Kits/Common/`）・今ある表とビルダー・スキン（`Assets/Scripts/Visual/Skins/`）は変えない。変えたいときは人に相談する。
- 生成されるアセットは手で作らず、メニュー「Tools/Team1/アプリのキットを用意」で作る。

## 全体の決まり

- 文書・コメント・コミットメッセージは日本語。
- main に直接コミットしない。ブランチを切って PR にする。
- 設計メモは `docs/` にある。今の実装の様子は [docs/implementation-status.md](docs/implementation-status.md)。
- 本物のアプリのロゴ・アイコン・音・個人の情報は使わない（README のクレジットも参照）。
