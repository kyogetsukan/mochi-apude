# BoothAutoUpdater（試作 0.2.0 / Windows）

購入済み・ギフト受取・無料配布の BOOTH 商品に新しいファイルが上がっていたら
`Downloads\VRC_Packages\` に保存する、常駐しない小さな exe。
Unity 側の VPM パッケージから `--check` で呼ばれる想定（Unity 側は次段）。

- 制作者側の作業は無し。BOOTH に今まで通りファイルを上げるだけ。
- 購入判定は BOOTH に任せる。自分のライブラリに無いものは何も取れない。
- ID・パスワードには触らない。ログインは内蔵 WebView2（Edge の中身）の BOOTH 画面で本人が一度だけ行い、
  Cookie は `%LOCALAPPDATA%\BoothAutoUpdater\profile` に残る。

## 必要なもの

- Windows 10/11（WebView2 ランタイムは通常入っている）
- 利用者側に .NET は不要（ランタイム同梱）。ビルドする側だけ .NET SDK 8.0 が要る
- ビルドするなら .NET SDK 8.0

## ビルド

`build.bat` を実行。`dist\BoothAutoUpdater.exe` ができる。

## 使い方

| コマンド | 動き |
|---|---|
| `BoothAutoUpdater.exe` | 設定ウィンドウ。商品 URL / 注文詳細 URL の登録、絞り込み、保存先、手動チェック |
| `BoothAutoUpdater.exe --login` | BOOTH のログイン画面を開く（初回のみ） |
| `BoothAutoUpdater.exe --check` | 登録商品を確認、新版を保存、結果 JSON を標準出力に出して終了 |
| `BoothAutoUpdater.exe --add URL` | 商品を登録して終了 |

登録データ: `%APPDATA%\BoothAutoUpdater\state.json`

### `--check` の出力例

```json
{
  "Checked": 2,
  "LoggedIn": true,
  "Downloaded": [ { "Item": "もちふぃった～", "File": "MochiFitter69.zip", "Path": "C:\\Users\\...\\Downloads\\VRC_Packages\\MochiFitter69.zip" } ],
  "Pending": [],
  "Errors": [],
  "DownloadDir": "C:\\Users\\...\\Downloads\\VRC_Packages"
}
```

`LoggedIn: false` なら Unity 側は「BOOTH にログイン」を促す。

## 仕組み

`accounts.booth.pm/library`（通常 / ギフト / 無料配布）を全ページ読み、
各ブロックの商品 ID・ファイル名・`downloadables/…` の ID を取る。
前回と downloadable ID が違えば新版として保存する。ページ間に 0.8 秒の間隔を置く。

## 制限

- BOOTH の HTML 構造（`js-download-button` / `data-href` / `text-14`）に依存。変わったら `BoothParser.cs` を直す。
- 制作者が同じファイルを上書きして downloadable ID が変わらなかった場合は検知できない。
- 未署名の exe なので初回は SmartScreen の警告が出る。
