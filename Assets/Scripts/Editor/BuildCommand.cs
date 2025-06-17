#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.Build.Profile;

public class BuildCommand
{
    public static void Build()
    {
        var profile = new BuildPlayerWithProfileOptions();
        
        //プラットフォーム、オプション
        var isDevelopment = true;

        // 出力名とか
        var exeName = PlayerSettings.productName;
        var ext = "";
        var outputPath = @"C:\Build\";

        // コマンドラインの引数をパース
        var args = System.Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-projectPath":
                    outputPath = args[i + 1] + "\\Build";
                    break;
                case "-devmode":
                    isDevelopment = args[i + 1] == "true";
                    break;
                case "-platform":
                    switch (args[i + 1])
                    {
                        case "Windows":
                            ext = ".exe";
                            break;
                        case "Mac":
                            ext = ".app";
                            var specificBuildProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profile/macOS.asset");
                            if (specificBuildProfile != null)
                            {
                                BuildProfile.SetActiveBuildProfile(specificBuildProfile);
                            }
                            else
                            {
                                Debug.LogWarning("macOS build profile not found, using default.");
                            }
                            // PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, 2);
                            break;
                        case "Android":
                            ext = ".apk";
                            break;
                        case "Switch":
                            ext = "";
                            break;
                    }

                    break;
                default:
                    break;
            }
        }
        // プラットフォームの設定
        profile.buildProfile = BuildProfile.GetActiveBuildProfile();
        
        // ビルド成果物の出力パスを設定
        profile.locationPathName = outputPath + "\\" + exeName + ext;
        
        if (isDevelopment)
        {
            //optionsはビットフラグなので、|で追加していくことができる
            profile.options = BuildOptions.AllowDebugging | BuildOptions.Development;
        }

        // 実行
        var report = BuildPipeline.BuildPlayer(profile);

        // 結果出力
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("BUILD SUCCESS");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("BUILD FAILED");

            foreach (var step in report.steps)
            {
                Debug.Log(step.ToString());
            }

            Debug.LogError("Error Count: " + report.summary.totalErrors);
            EditorApplication.Exit(1);
        }
    }
}
#endif