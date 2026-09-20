# mochi-apude（開発・公開手順）

利用者向けの説明は `package/README.md`。ここは作る側の手順。

```
exe/        MochiApude.exe のソース（C# / WinForms / WebView2）
package/    VPM パッケージ（Unity Editor 拡張 + Tools~/ に exe を同梱）
tools/      index.json 生成スクリプト
.github/    タグを打つと exe ビルド → zip → Release → gh-pages の index.json 更新
```

## 開発中の確認（夜空の PC）

1. .NET SDK 8.0 を入れる（一回だけ）。`dotnet --list-sdks` に 8.0 が出れば済み。
2. `exe\build.bat` をダブルクリック → `exe\dist\MochiApude.exe`。
3. その exe を `package\Tools~\` にコピー。
4. 手元の Unity プロジェクトの `Packages\` に `package` フォルダをコピー（フォルダ名は何でもいい）。
   VCC を通さずローカルパッケージとして読み込まれる。
5. Unity を開くと自動確認が走る。メニューは Tools → 境月館 → もちアプデ。

## 公開の準備（一回だけ）

1. GitHub に `kyogetsukan/mochi-apude` を作り、このフォルダを push。
2. リポジトリの Settings → Pages で、Source を `gh-pages` ブランチ / `(root)` にする。
   （初回のタグ push で Actions が gh-pages を作る。作られた後に設定してもいい）
3. `package/package.json` の `url` と `package/README.md` の Add to VCC のリンク、`tools/make_index.py` の `url` を
   実際の GitHub ユーザー名に合わせる。
   既に境月館の VPM リポジトリ（Project Starter 用など）があるなら、そちらの index.json に
   `tools/make_index.py` で追記する形に変え、Add to VCC のリンクもそちらの URL にする。

## リリース

```
git tag v0.1.0
git push origin v0.1.0
```

Actions が exe をビルドし、`jp.kyogetsukan.mochi-apude-0.1.0.zip` を Release に置き、
`gh-pages` の `index.json` に版を足す。数分後には VCC 側に出る。

版を上げる時は `package/package.json` の `version` と `exe/MochiApude.csproj` の `Version` を揃えてからタグを打つ。

## BOOTH の商品ページ（無料）

入口として BOOTH に無料商品を出す。説明文の下書きは `docs/booth-description.md`。
ダウンロードファイルには `package/README.md` を PDF か txt にしたものと Add to VCC のリンクだけ入れる
（パッケージ本体は VCC 経由で配るので、zip を BOOTH に置く必要はない。置くならリリース zip をそのまま）。

## BOOTH の HTML が変わった時

`exe/BoothParser.cs` の正規表現を直す。目印は `js-download-button` / `data-href` / `text-14` / `font-bold text-16`。
ブラウザでライブラリページの HTML を見て合わせ、版を上げてタグを打てば利用者の VCC に更新が出る。
