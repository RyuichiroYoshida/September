#if UNITY_EDITOR
using System;
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
        var profileName = "";

        // コマンドラインの引数をパース
        var args = Environment.GetCommandLineArgs();
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
                            profileName = "windows";
                            ext = ".exe";
                            break;
                        case "Mac":
                            profileName = "macOS";
                            ext = ".app";
                            break;
                        case "Android":
                            profileName = "android";
                            ext = ".apk";
                            break;
                        case "Switch":
                            profileName = "switch";
                            ext = "";
                            break;
                    }

                    break;
            }
        }

        // プラットフォームの設定
        BuildProfile buildProfile = null;
        if (!string.IsNullOrEmpty(profileName))
        {
            var assetPath = "Assets/Settings/Build Profiles/" + profileName + ".asset";
            buildProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>(assetPath);

            if (buildProfile == null)
            {
                Debug.LogWarning($"Build profile not found at path: {assetPath}");
            }
        }

        // Build profileが見つからない場合、またはprofileNameが空の場合はアクティブなプロファイルを使用
        profile.buildProfile = buildProfile ?? BuildProfile.GetActiveBuildProfile();

        // Build profileが有効かチェック
        if (profile.buildProfile == null)
        {
            Debug.LogError("No valid build profile found. Please ensure a build profile is active or the specified profile exists.");
            EditorApplication.Exit(1);
            return;
        }

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