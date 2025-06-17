using UnityEditor;
using UnityEngine;
using System.Linq;

public class BuildCommand
{
    public static void Build()
    {
        //プラットフォーム、オプション
        var isDevelopment = true;
        var platform = BuildTarget.StandaloneWindows;


        // 出力名とか
        var exeName = PlayerSettings.productName;
        var ext = ".exe";
        var outpath = @"C:\Build\";

        // ビルド対象シーンリスト
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        // コマンドラインの引数をパース
        var args = System.Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-projectPath":
                    outpath = args[i + 1] + "\\Build";
                    break;
                case "-devmode":
                    isDevelopment = args[i + 1] == "true";
                    break;
                case "-platform":
                    switch (args[i + 1])
                    {
                        case "Windows":
                            platform = BuildTarget.StandaloneWindows;
                            ext = ".exe";
                            break;
                        case "Mac":
                            platform = BuildTarget.StandaloneOSX;
                            ext = ".app";
                            // Macの場合は対象CPUアーキテクチャを設定する
                            PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, 2);
                            break;
                        case "Android":
                            platform = BuildTarget.Android;
                            ext = ".apk";
                            break;
                        case "Switch":
                            platform = BuildTarget.Switch;
                            ext = "";
                            break;
                    }
                    break;
                default:
                    break;
            }
        }

        //ビルドオプションの成型
        var option = new BuildPlayerOptions();
        option.scenes = scenes;
        option.locationPathName = outpath + "\\" + exeName + ext;
        if (isDevelopment)
        {
            //optionsはビットフラグなので、|で追加していくことができる
            option.options = BuildOptions.Development | BuildOptions.AllowDebugging;
        }
        option.target = platform; //ビルドターゲットを設定

        // 実行
        var report = BuildPipeline.BuildPlayer(option);

        // 結果出力
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("BUILD SUCCESS");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("BUILD FAILED");

            foreach(var step in report.steps)
            {
                Debug.Log(step.ToString());
            }

            Debug.LogError("Error Count: " + report.summary.totalErrors);
            EditorApplication.Exit(1);
        }
    }
}
