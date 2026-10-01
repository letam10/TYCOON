using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TYCOON.Editor
{
    public static class BuildTools
    {
        [MenuItem("TYCOON/Build Windows x64")]
        public static BuildReport BuildWindows()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before build.");
            if (!File.Exists(SceneBootstrap.ScenePath)) throw new FileNotFoundException("TYCOON scene is missing.");
            Directory.CreateDirectory("Build/Windows");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] {SceneBootstrap.ScenePath},
                locationPathName = "Build/Windows/TYCOON.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/build-result.json", JsonUtility.ToJson(new Result
            {
                result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings, bytes = report.summary.totalSize,
                seconds = report.summary.totalTime.TotalSeconds, output = report.summary.outputPath,
                unity = Application.unityVersion
            }, true));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed. See TestResults/build-result.json.");
            return report;
        }

        [Serializable] class Result
        {
            public string result, output, unity;
            public int errors, warnings;
            public ulong bytes;
            public double seconds;
        }
    }
}
