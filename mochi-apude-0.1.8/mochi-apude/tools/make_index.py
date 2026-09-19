#!/usr/bin/env python3
"""VPM リポジトリの index.json を作る / 更新する。

使い方:
  python tools/make_index.py <package.json> <zip の公開URL> <出力 index.json>
既存の index.json があれば読み込み、同じパッケージの版を追加する。
"""
import json, sys, pathlib

pkg_path, zip_url, out_path = sys.argv[1:4]
pkg = json.load(open(pkg_path, encoding="utf-8"))
pkg["url"] = zip_url

out = pathlib.Path(out_path)
if out.exists():
    index = json.load(open(out, encoding="utf-8"))
else:
    index = {
        "name": "境月館 VPM Repository",
        "id": "jp.kyogetsukan.vpm",
        "author": "境月館",
        "url": "https://kyogetsukan.github.io/mochi-apude/index.json",
        "packages": {},
    }

versions = index["packages"].setdefault(pkg["name"], {}).setdefault("versions", {})
versions[pkg["version"]] = pkg
out.parent.mkdir(parents=True, exist_ok=True)
json.dump(index, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print(f"index.json: {pkg['name']} {pkg['version']} -> {out}")
