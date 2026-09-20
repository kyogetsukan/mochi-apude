using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kyogetsukan.MochiApude
{
    /// <summary>
    /// プロジェクト単位の設定と取り込み記録。ProjectSettings/MochiApude.json に保存。
    /// 商品の登録そのものは exe 側（ユーザー単位）が持つ。ここにはこのプロジェクトで
    /// 「取り込んだ / 見送った」ファイルの記録だけを置く。
    /// </summary>
    [Serializable]
    public class BauSettings
    {
        public bool confirmBeforeImport = true;  // ダイアログで確認する（false なら黙って取り込む）
        public List<FileRecord> imported = new List<FileRecord>();
        public List<string> skipped = new List<string>(); // このプロジェクトでは入れないと決めたファイル名

        [Serializable]
        public class FileRecord
        {
            public string fileName;
            public long size;
            public string importedAt;
        }

        static readonly string PathOnDisk = Path.GetFullPath("ProjectSettings/MochiApude.json");
        static BauSettings _cached;

        public static BauSettings Load()
        {
            if (_cached != null) return _cached;
            try
            {
                if (File.Exists(PathOnDisk))
                    _cached = JsonUtility.FromJson<BauSettings>(File.ReadAllText(PathOnDisk));
            }
            catch (Exception e) { Debug.LogWarning("[もちアプデ] 設定の読み込みに失敗: " + e.Message); }
            return _cached ??= new BauSettings();
        }

        public void Save()
        {
            try { File.WriteAllText(PathOnDisk, JsonUtility.ToJson(this, true)); }
            catch (Exception e) { Debug.LogWarning("[もちアプデ] 設定の保存に失敗: " + e.Message); }
        }

        public bool IsImported(string fileName, long size) =>
            imported.Exists(r => r.fileName == fileName && r.size == size);

        public bool IsSkipped(string fileName) => skipped.Contains(fileName);

        public void MarkImported(string fileName, long size)
        {
            imported.RemoveAll(r => r.fileName == fileName);
            imported.Add(new FileRecord { fileName = fileName, size = size, importedAt = DateTime.Now.ToString("s") });
            skipped.Remove(fileName);
        }
    }
}
