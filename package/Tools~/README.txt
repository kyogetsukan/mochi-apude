ここに app\ フォルダを作り、exe\build.bat の出力 dist\ の中身を丸ごと入れる。
　package\Tools~\app\MochiApude.exe
　package\Tools~\app\（同じ場所にある dll や runtimes フォルダも全部）
フォルダ名末尾の ~ により Unity はこの中身をアセットとして扱わない。
GitHub Actions で配布する場合はこの配置は自動で行われる（手作業は不要）。
