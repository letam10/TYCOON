using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TYCOON.Editor
{
    public static class SceneBootstrap
    {
        public const string ScenePath = "Assets/_Game/Scenes/Tycoon.unity";
        public const string CharacterPath = "Assets/_Game/Art/Imported/UserProvided/Character1/Character1.fbx";

        [MenuItem("TYCOON/Create Player Camera Stage")]
        public static void CreatePlayerStage()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before rebuilding the stage.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (source == null) throw new FileNotFoundException("Processed and visually inspected character is required.", CharacterPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.68f, 0.75f, 0.83f);
            RenderSettings.fog = false;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 45f;
            Application.targetFrameRate = 60;

            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pipeline == null) throw new InvalidOperationException("PC URP asset is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.80f);
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            // Sân kiểm tra tạm thời để đo va chạm trước khi xây Farm.
            Box("Movement Test Ground", new Vector3(0, -0.25f, 0), new Vector3(38, 0.5f, 38), Material("StageGrass", new Color(0.39f, 0.64f, 0.39f)));
            var path = Box("Walkway", new Vector3(0, 0.006f, 0), new Vector3(6, 0.01f, 30), Material("StagePath", new Color(0.91f, 0.82f, 0.64f)));
            UnityEngine.Object.DestroyImmediate(path.GetComponent<Collider>());
            Box("Collision Test Wall", new Vector3(5, 0.8f, 3), new Vector3(1.1f, 1.6f, 5), Material("StageCoral", new Color(0.77f, 0.36f, 0.27f)));
            Box("Collision Test Corner", new Vector3(3.6f, 0.5f, 5.2f), new Vector3(3.8f, 1f, 0.8f), Material("StageCream", new Color(0.93f, 0.89f, 0.72f)));

            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.08f, -3f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.75f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, 0.875f, 0f);
            controller.skinWidth = 0.03f;
            controller.stepOffset = 0.2f;
            controller.slopeLimit = 45f;
            controller.minMoveDistance = 0f;
            player.AddComponent<PlayerInputReader>();
            var motor = player.AddComponent<PlayerMotor>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, player.transform);
            model.name = "Character Visual";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = CreateAnimator();
            animator.applyRootMotion = false;
            var animation = player.AddComponent<PlayerAnimationDriver>();
            animation.Configure(animator);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.69f, 0.82f, 0.90f);
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<SmoothFollowCamera>();
            follow.Configure(player.transform, true);
            motor.Configure(camera.transform);

            PlayerSettings.companyName = "TYCOON";
            PlayerSettings.productName = "TYCOON";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(ScenePath, true)};
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("TYCOON: Player/Camera test scene created. Gameplay systems and final art are not yet implemented.");
        }

        static AnimatorController CreateAnimator()
        {
            const string path = "Assets/_Game/Art/Imported/UserProvided/Character1/Player.controller";
            var clips = AssetDatabase.LoadAllAssetsAtPath(CharacterPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            AnimationClip Clip(string name)
            {
                var clip = clips.FirstOrDefault(c => c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal));
                if (clip == null) throw new InvalidOperationException("Required character clip is missing: " + name);
                return clip;
            }
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Carrying", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Drop", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            AnimatorState Locomotion(string name, bool carry)
            {
                var state = machine.AddState(name);
                var blend = new BlendTree {name = name, blendParameter = "MoveSpeed", useAutomaticThresholds = false};
                AssetDatabase.AddObjectToAsset(blend, controller);
                blend.AddChild(Clip(carry ? "CarryIdle" : "Idle"), 0f);
                blend.AddChild(Clip(carry ? "CarryWalk" : "Walk"), 4.5f);
                blend.AddChild(Clip(carry ? "CarryWalk" : "Run"), 7f);
                state.motion = blend;
                return state;
            }
            var normal = Locomotion("Locomotion", false);
            var carryState = Locomotion("Carry", true);
            machine.defaultState = normal;
            var startCarry = normal.AddTransition(carryState);
            startCarry.hasExitTime = false;
            startCarry.duration = 0.15f;
            startCarry.AddCondition(AnimatorConditionMode.If, 0f, "Carrying");
            var stopCarry = carryState.AddTransition(normal);
            stopCarry.hasExitTime = false;
            stopCarry.duration = 0.15f;
            stopCarry.AddCondition(AnimatorConditionMode.IfNot, 0f, "Carrying");
            foreach (var name in new[] {"Pickup", "Drop"})
            {
                var action = machine.AddState(name);
                action.motion = Clip(name);
                var transition = machine.AddAnyStateTransition(action);
                transition.hasExitTime = false;
                transition.duration = 0.1f;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.If, 0f, name == "Pickup" ? "Interact" : "Drop");
                var finish = action.AddTransition(normal);
                finish.hasExitTime = true;
                finish.exitTime = 0.9f;
                finish.duration = 0.12f;
            }
            return controller;
        }

        static Material Material(string name, Color color)
        {
            var path = "Assets/_Game/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.18f);
            return material;
        }

        static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }
    }
}
