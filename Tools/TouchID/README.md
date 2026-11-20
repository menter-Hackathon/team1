# ゲーム内ロック画面の Touch ID

ロック画面には「Enterキー または Touch IDで解除」と表示します。Enter はゲーム内のロックを直接解除します。Touch ID は利用可能な場合に自動で認証を開始し、センサーでの認証成功後に解除します。ゲームから本物のパスワード入力画面を開く導線はありません。

macOS 12 以降で Touch ID が利用できる場合は、Apple の `LAAuthenticationView` を案内文の下へ埋め込みます。標準の指紋アイコンを小さく表示します。埋め込み表示に失敗した場合は Enter での解除を案内し、標準ダイアログには戻しません。

- macOS の `LocalAuthentication` / `LAContext` を使用します。
- 指紋データや Mac のパスワードをゲーム側で取得・保存しません。
- Touch ID のキャンセル・認証失敗だけでは解除しません。Esc で認証をキャンセルできます。認証中でも Enter を押すと認証を閉じ、ゲーム内ロックを解除します。
- Touch ID がない・利用できない環境では「Enterキーで解除」と表示します。
- Editor の停止、シーンの破棄、スクリプト再読み込み時に認証をキャンセルします。

`Assets/Plugins/macOS/libTeam1TouchIdEmbedded.dylib` は Apple Silicon / Intel 両対応です。Unity の Mac Editor と macOS ビルドに含める設定です。

C# 側はプラグイン名だけで検索せず、Editor では `Application.dataPath/Plugins/macOS`、配布版ではアプリ内の Plugins / PlugIns 等から実ファイルを `dlopen` で読み込みます。読み込めない場合は Unity Console にパス解決・読み込みエラーを記録します。

## ネイティブ側の再ビルド

ネイティブプラグインがすでに読み込まれている場合は Unity を閉じてから実行します。

```sh
bash Tools/TouchID/build.sh
```

再ビルド後、Unity を起動し直してください。配布ビルドの署名は、ゲーム全体の署名設定に従って行います。
