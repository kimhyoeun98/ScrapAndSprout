using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BatchBuild
{
    // ─────────────────────────────────────────────────────────────
    //  에디터 메뉴에서 클릭하여 빌드 (x86_64 강제, 에디터 종료 안 함)
    //  메뉴: Build ▸ Build Windows x64 (64bit)
    //  Fusion의 nanosockets.dll은 64비트 전용이므로 반드시 x86_64로 빌드해야
    //  방 생성 시 무한로딩(소켓 로드 실패) 문제가 발생하지 않는다.
    // ─────────────────────────────────────────────────────────────
    [MenuItem("Build/Build Windows x64 (64bit)")]
    public static void BuildFromMenu()
    {
        string[] scenes = new string[]
        {
            "Assets/Scenes/LoginScene.unity",
            "Assets/Scenes/LobbyScene.unity",
            "Assets/Scenes/waitingRoomScene.unity",
            "Assets/Scenes/TrashZoneScene.unity",
            "Assets/Scenes/ResultScene.unity",
            "Assets/Scenes/TutorialScene.unity"
        };

        // 기존 깨진 32비트 빌드와 동일한 위치를 덮어써서 같은 exe를 실행하면 되도록 함
        string outPath = "D:/Scrap&Scrout_inte_ver2/build/Scrap & Scrout.exe";

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.StandaloneWindows64,   // ★ x86_64 (64비트) — Fusion 필수
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BUILD] ✅ 성공 (x86_64). 크기: {summary.totalSize / 1024 / 1024}MB → {outPath}");
            EditorUtility.RevealInFinder(outPath);
        }
        else
        {
            Debug.LogError($"[BUILD] ❌ 실패: {summary.result}");
        }
    }

    public static void Build()
    {
        string[] scenes = new string[]
        {
            "Assets/Scenes/LoginScene.unity",
            "Assets/Scenes/LobbyScene.unity",
            "Assets/Scenes/waitingRoomScene.unity",
            "Assets/Scenes/TrashZoneScene.unity",
            "Assets/Scenes/ResultScene.unity",
            "Assets/Scenes/TutorialScene.unity"
        };

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "D:/Scrap_Sprout_Build/ScrapAndSprout.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BUILD] Success. Size: {summary.totalSize / 1024 / 1024}MB");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[BUILD] Failed: {summary.result}");
            EditorApplication.Exit(1);
        }
    }

    public static void BuildTest()
    {
        // DevBootstrap 테스트용 — TrashZoneScene을 첫 번째로
        string[] scenes = new string[]
        {
            "Assets/Scenes/TrashZoneScene.unity",
            "Assets/Scenes/ResultScene.unity"
        };

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "D:/Scrap_Sprout_Build/ScrapAndSprout_Test.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BUILD] Success. Size: {summary.totalSize / 1024 / 1024}MB");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[BUILD] Failed: {summary.result}");
            EditorApplication.Exit(1);
        }
    }
}
