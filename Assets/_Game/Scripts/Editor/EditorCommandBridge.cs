using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TYCOON.Editor
{
    /// <summary>One local command writer; responses always include the matching command ID.</summary>
    [InitializeOnLoad]
    public static class EditorCommandBridge
    {
        private const string Key = "TYCOON.EditorBridge.";
        private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string CommandPath = Path.Combine(Root, "work", "editor-command.json");
        private static readonly string ResponsePath = Path.Combine(Root, "work", "editor-response.json");
        private static readonly TestCallbacks Callbacks = new TestCallbacks();
        private static Pending pending;
        private static double nextPoll;
        private static bool recovering = true;

        [Serializable]
        public sealed class Command
        {
            public string id;
            public string command;
            public string method;
            public string path;
            public string testMode;
            public string[] testNames;
            public string[] assemblyNames;
        }

        [Serializable]
        private sealed class Pending
        {
            public Command request;
            public string startedUtc;
            public string artifactPath;
            public string testRunId;
            public int reloadCount;
        }

        [Serializable]
        private sealed class Response
        {
            public string id, command, state, message, artifactPath, timestampUtc;
            public bool success;
            public Status status;
            public TestSummary tests;
        }

        [Serializable]
        private sealed class Status
        {
            public string unityVersion, graphicsDevice, graphicsVendor, graphicsApi;
            public string activeScene, activeScenePath, activeCommandId, activeCommand, lastReloadUtc;
            public string[] shaders;
            public bool isPlaying, isPaused, isCompiling, isUpdating, scriptCompilationFailed;
            public int graphicsMemoryMb, sceneRendererCount, materialCount, missingMaterialSlots, invalidShaderMaterials;
            public int consoleErrors, consoleExceptions, consoleAssertions, reloadCount;
            public CompilerIssues compilerIssues;
        }

        [Serializable]
        private sealed class CompilerIssues
        {
            public int total;
            public List<CompilerIssue> entries = new List<CompilerIssue>();
        }

        [Serializable]
        private sealed class CompilerIssue
        {
            public string file, code;
            public int line, column;
        }

        [Serializable]
        private sealed class TestSummary
        {
            public string result;
            public int passed, failed, skipped, inconclusive;
            public double durationSeconds;
        }

        static EditorCommandBridge()
        {
            SessionState.SetInt(Key + "ReloadCount", SessionState.GetInt(Key + "ReloadCount", 0) + 1);
            SessionState.SetString(Key + "LastReloadUtc", DateTime.UtcNow.ToString("O"));
            pending = ReadSession<Pending>("Pending");
            EditorApplication.update += Update;
            Application.logMessageReceived += RecordConsoleError;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            CompilationPipeline.compilationStarted += _ => SaveCompilerIssues(new CompilerIssues());
            CompilationPipeline.assemblyCompilationFinished += RecordCompilerErrors;
            TestRunnerApi.RegisterTestCallback(Callbacks);
            EditorApplication.delayCall += () => { RecoverPreviousResponse(); recovering = false; };
        }

        private static void RecoverPreviousResponse()
        {
            if (pending != null || !string.IsNullOrEmpty(SessionState.GetString(Key + "LastId", "")) || !File.Exists(ResponsePath))
                return;
            try
            {
                var previous = JsonUtility.FromJson<Response>(File.ReadAllText(ResponsePath));
                if (previous == null || string.IsNullOrEmpty(previous.id)) return;
                SessionState.SetString(Key + "LastId", previous.id);
                // Không chạy lại lệnh cũ sau khi Editor bị đóng hoặc crash.
                if (previous.state == "running" || previous.state == "reloading")
                    Reply(new Command { id = previous.id, command = previous.command }, "failed", false,
                        "Previous Editor session ended before this command completed.");
            }
            catch (IOException) { }
            catch (ArgumentException) { }
        }

        private static void Update()
        {
            if (recovering) return;
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25;
            try
            {
                CheckPending();
                if (!File.Exists(CommandPath)) return;
                var request = JsonUtility.FromJson<Command>(File.ReadAllText(CommandPath));
                if (request == null || string.IsNullOrEmpty(request.id) || string.IsNullOrEmpty(request.command)) return;
                if (request.id == SessionState.GetString(Key + "LastId", "")) return;
                if (pending != null && request.command != "status") return;
                if (request.command != "status" && (EditorApplication.isCompiling || EditorApplication.isUpdating)) return;
                SessionState.SetString(Key + "LastId", request.id);
                if (!Regex.IsMatch(request.id, "^[A-Za-z0-9_.-]{1,128}$"))
                {
                    Reply(request, "failed", false, "ID must contain 1-128 letters, digits, dots, underscores or hyphens.");
                    return;
                }
                Execute(request);
            }
            // Parent thay file nguyên tử; nếu đang ghi hoặc reload thì thử ở nhịp sau.
            catch (IOException) { }
            catch (ArgumentException) { }
        }

        private static void Execute(Command request)
        {
            try
            {
                if (request.command == "status")
                {
                    Reply(request, "complete", true, "Editor status.");
                    return;
                }
                Begin(request);
                switch (request.command)
                {
                    case "refresh":
                        Finish(true, "Asset refresh requested; inspect status before testing.");
                        EditorApplication.delayCall += () => AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        break;
                    case "exitEditor":
                        Finish(true, "Editor exit requested.");
                        EditorApplication.delayCall += () => EditorApplication.Exit(0);
                        break;
                    case "enterPlayMode":
                        if (EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Fix compiler errors before entering Play Mode.");
                        if (EditorApplication.isPlaying) Finish(true, "Already in Play Mode.");
                        else EditorApplication.isPlaying = true;
                        break;
                    case "exitPlayMode":
                        if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish(true, "Already in Edit Mode.");
                        else EditorApplication.isPlaying = false;
                        break;
                    case "executeStaticMethod":
                        InvokeProjectMethod(request.method);
                        Finish(true, "Static method completed; inspect status to verify resulting state.");
                        break;
                    case "captureScreenshot":
                        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Screenshot requires Play Mode.");
                        pending.artifactPath = OutputPath(request.path, "Screenshots", request.id + ".png", ".png");
                        SavePending();
                        EditorApplication.ExecuteMenuItem("Window/General/Game");
                        ScreenCapture.CaptureScreenshot(pending.artifactPath);
                        break;
                    case "runTests":
                    case "runEditModeTests":
                    case "runPlayModeTests":
                        RunTests(request);
                        break;
                    case "buildWindows":
                        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build requires Edit Mode.");
                        pending.artifactPath = Path.Combine(Root, "Build", "Windows", "TYCOON.exe");
                        SavePending();
                        var report = InvokeProjectMethod("TYCOON.Editor.BuildTools.BuildWindows") as BuildReport;
                        var built = report != null ? report.summary.result == BuildResult.Succeeded &&
                            string.Equals(Path.GetFullPath(report.summary.outputPath), pending.artifactPath, StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(pending.artifactPath) && new FileInfo(pending.artifactPath).Length > 0 : FreshArtifact();
                        Finish(built, built ? "Windows build completed." : "Build did not produce a successful report or fresh executable.");
                        break;
                    default:
                        Finish(false, "Unknown command.");
                        break;
                }
            }
            catch (Exception exception)
            {
                var cause = exception is TargetInvocationException invocation ? invocation.InnerException ?? exception : exception;
                Debug.LogException(cause);
                // Không đưa message/stack tùy ý của game hoặc dữ liệu cá nhân vào kênh JSON.
                Finish(false, "Command failed: " + cause.GetType().Name + ". Inspect the task-owned Unity log for details.");
            }
        }

        private static void Begin(Command request)
        {
            pending = new Pending { request = request, startedUtc = DateTime.UtcNow.ToString("O"),
                reloadCount = SessionState.GetInt(Key + "ReloadCount", 0) };
            SavePending();
            Reply(request, "running", false, "Command accepted.");
        }

        private static void CheckPending()
        {
            if (pending == null) return;
            var command = pending.request.command;
            if (command == "enterPlayMode" && EditorApplication.isPlaying)
                Finish(true, "Entered Play Mode.");
            else if (command == "exitPlayMode" && !EditorApplication.isPlayingOrWillChangePlaymode)
                Finish(true, "Exited Play Mode.");
            else if (command == "captureScreenshot" && FreshArtifact())
                Finish(true, "Screenshot saved from the running Game view.");
            else if ((command == "executeStaticMethod" || command == "buildWindows") &&
                     pending.reloadCount != SessionState.GetInt(Key + "ReloadCount", 0))
                Finish(false, "Domain reload interrupted the synchronous method; inspect its output before retrying.");
            else if ((command == "enterPlayMode" || command == "exitPlayMode" || command == "captureScreenshot") &&
                     (DateTime.UtcNow - StartedUtc()).TotalSeconds > 120)
                Finish(false, "Editor transition or screenshot timed out after 120 seconds.");
        }

        private static object InvokeProjectMethod(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("TYCOON.", StringComparison.Ordinal))
                throw new ArgumentException("Only TYCOON namespace methods are allowed.");
            var separator = name.LastIndexOf('.');
            var typeName = name.Substring(0, separator);
            var type = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().Select(assembly => assembly.GetType(typeName)).FirstOrDefault(value => value != null);
            var method = type?.GetMethod(name.Substring(separator + 1), BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            if (method == null || method.IsGenericMethod ||
                (method.ReturnType != typeof(void) && method.ReturnType != typeof(BuildReport)))
                throw new MissingMethodException("Expected public static parameterless void or BuildReport method.");
            return method.Invoke(null, null);
        }

        private static void RunTests(Command request)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start test runs in Edit Mode.");
            var modeName = request.command == "runEditModeTests" ? "EditMode" :
                request.command == "runPlayModeTests" ? "PlayMode" : request.testMode;
            if (modeName != "EditMode" && modeName != "PlayMode") throw new ArgumentException("testMode must be EditMode or PlayMode.");
            pending.artifactPath = OutputPath(request.path, "TestResults", modeName + "-" + request.id + ".xml", ".xml");
            SavePending();
            var testRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
            try
            {
                var runId = testRunner.Execute(new ExecutionSettings(new Filter
                {
                    testMode = modeName == "EditMode" ? TestMode.EditMode : TestMode.PlayMode,
                    testNames = request.testNames,
                    assemblyNames = request.assemblyNames
                }));
                if (pending != null) { pending.testRunId = runId; SavePending(); }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testRunner);
            }
        }

        private static string OutputPath(string requested, string folder, string fallback, string extension)
        {
            var directory = Path.Combine(Root, folder);
            var path = Path.GetFullPath(string.IsNullOrWhiteSpace(requested) ? Path.Combine(directory, fallback) : Path.Combine(Root, requested));
            if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output path must stay inside the requested artifact folder.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return path;
        }

        private static DateTime StartedUtc() => DateTime.Parse(pending.startedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        private static bool FreshArtifact() => !string.IsNullOrEmpty(pending.artifactPath) && File.Exists(pending.artifactPath) &&
            new FileInfo(pending.artifactPath).Length > 0 && File.GetLastWriteTimeUtc(pending.artifactPath) >= StartedUtc();
        private static void SavePending() => SessionState.SetString(Key + "Pending", JsonUtility.ToJson(pending));

        private static void Finish(bool success, string message, TestSummary tests = null)
        {
            if (pending == null) return;
            var completed = pending;
            pending = null;
            try
            {
                Reply(completed.request, success ? "complete" : "failed", success, message, completed.artifactPath, tests);
            }
            catch
            {
                pending = completed;
                throw;
            }
            SessionState.EraseString(Key + "Pending");
        }

        private static void Reply(Command request, string state, bool success, string message, string artifact = null, TestSummary tests = null)
        {
            var response = new Response { id = request.id, command = request.command, state = state, success = success,
                message = message, artifactPath = artifact, timestampUtc = DateTime.UtcNow.ToString("O"), status = GetStatus(), tests = tests };
            Directory.CreateDirectory(Path.GetDirectoryName(ResponsePath));
            var temporary = ResponsePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(response, true));
            // Thay file nguyên tử để parent không đọc nhầm JSON đang ghi dở.
            if (File.Exists(ResponsePath)) File.Replace(temporary, ResponsePath, null);
            else File.Move(temporary, ResponsePath);
        }

        private static Status GetStatus()
        {
            var scene = SceneManager.GetActiveScene();
            var renderers = Resources.FindObjectsOfTypeAll<Renderer>().Where(renderer => renderer.gameObject.scene.IsValid()).ToArray();
            var slots = renderers.SelectMany(renderer => renderer.sharedMaterials).ToArray();
            var materials = slots.Where(material => material != null).Distinct().ToArray();
            return new Status
            {
                unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsVendor = SystemInfo.graphicsDeviceVendor, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                graphicsMemoryMb = SystemInfo.graphicsMemorySize, activeScene = scene.name, activeScenePath = scene.path,
                isPlaying = EditorApplication.isPlaying, isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling, isUpdating = EditorApplication.isUpdating,
                scriptCompilationFailed = EditorUtility.scriptCompilationFailed,
                sceneRendererCount = renderers.Length, materialCount = materials.Length,
                missingMaterialSlots = slots.Count(material => material == null),
                invalidShaderMaterials = materials.Count(material => material.shader == null || !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader"),
                shaders = materials.Where(material => material.shader != null).Select(material => material.shader.name).Distinct().OrderBy(name => name).ToArray(),
                consoleErrors = SessionState.GetInt(Key + "Error", 0), consoleExceptions = SessionState.GetInt(Key + "Exception", 0),
                consoleAssertions = SessionState.GetInt(Key + "Assert", 0), compilerIssues = ReadSession<CompilerIssues>("CompilerIssues") ?? new CompilerIssues(),
                reloadCount = SessionState.GetInt(Key + "ReloadCount", 0), lastReloadUtc = SessionState.GetString(Key + "LastReloadUtc", ""),
                activeCommandId = pending?.request.id, activeCommand = pending?.request.command
            };
        }

        private static T ReadSession<T>(string name) where T : class
        {
            var json = SessionState.GetString(Key + name, "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json);
        }

        private static void BeforeReload()
        {
            if (pending != null) Reply(pending.request, "reloading", false, "Domain reload in progress.", pending.artifactPath);
            TestRunnerApi.UnregisterTestCallback(Callbacks);
        }

        private static void RecordConsoleError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                SessionState.SetInt(Key + type, SessionState.GetInt(Key + type, 0) + 1);
        }

        private static void SaveCompilerIssues(CompilerIssues issues) => SessionState.SetString(Key + "CompilerIssues", JsonUtility.ToJson(issues));

        private static void RecordCompilerErrors(string assembly, CompilerMessage[] messages)
        {
            var issues = ReadSession<CompilerIssues>("CompilerIssues") ?? new CompilerIssues();
            foreach (var message in messages.Where(value => value.type == CompilerMessageType.Error))
            {
                issues.total++;
                if (issues.entries.Count >= 50) continue;
                var file = message.file ?? "";
                if (Path.IsPathRooted(file)) file = file.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    ? file.Substring(Root.Length + 1) : Path.GetFileName(file);
                issues.entries.Add(new CompilerIssue { file = file, line = message.line, column = message.column,
                    code = Regex.Match(message.message ?? "", "CS[0-9]{4}").Value });
            }
            SaveCompilerIssues(issues);
        }

        private sealed class TestCallbacks : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void OnError(string message)
            {
                if (pending != null && pending.request.command.StartsWith("run", StringComparison.Ordinal))
                    Finish(false, "Unity Test Framework could not complete the run. Inspect the Unity log.");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (pending == null || !pending.request.command.StartsWith("run", StringComparison.Ordinal)) return;
                try
                {
                    TestRunnerApi.SaveResultToFile(result, pending.artifactPath);
                    Finish(result.FailCount == 0 && result.InconclusiveCount == 0 && result.PassCount > 0,
                        "Test run finished; XML contains the full result.", new TestSummary { result = result.ResultState,
                            passed = result.PassCount, failed = result.FailCount, skipped = result.SkipCount,
                            inconclusive = result.InconclusiveCount, durationSeconds = result.Duration });
                }
                catch (Exception exception)
                {
                    Finish(false, "Test result could not be written: " + exception.GetType().Name + ".");
                }
            }
        }
    }
}
