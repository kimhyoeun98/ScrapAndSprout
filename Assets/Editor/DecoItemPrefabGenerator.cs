using UnityEditor;
using UnityEngine;
using System.IO;

public class DecoItemPrefabGenerator
{
    [MenuItem("Tools/Generate Deco Prefabs")]
    public static void GenerateAllPrefabs()
    {
        string decoPath = "Assets/Resources/deco";
        string[] pngFiles = Directory.GetFiles(decoPath, "*.png");

        int createdCount = 0;

        foreach (string pngPath in pngFiles)
        {
            // .png 파일만 처리 (메타 파일 제외)
            if (pngPath.EndsWith(".meta")) continue;

            string fileName = Path.GetFileNameWithoutExtension(pngPath);
            string prefabPath = Path.Combine(decoPath, fileName + "_0.prefab");

            // 이미 프리팹이 있으면 스킵
            if (File.Exists(prefabPath))
            {
                Debug.Log($"[Skip] {fileName} - 이미 프리팹 있음");
                continue;
            }

            // Sprite 로드
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Error] {fileName} - Sprite 로드 실패");
                continue;
            }

            // GameObject 생성
            GameObject go = new GameObject(fileName);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            // 프리팹 저장
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            createdCount++;
            Debug.Log($"[Created] {fileName} ✓");
        }

        Debug.Log($"\n✅ 완료! {createdCount}개의 프리팹이 생성되었습니다.");
        AssetDatabase.Refresh();
    }
}
