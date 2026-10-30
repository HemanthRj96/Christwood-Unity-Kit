using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>
    /// Developer tool that (re)generates all owned kit content with Editor APIs: sounds, materials,
    /// movement presets, prefabs, the template, the catalog, the starter scene and the example scene.
    /// Writes only under Assets/WorkshopKit and never touches TeamGames. Safe to run repeatedly.
    /// </summary>
    public static partial class WorkshopContentGenerator
    {
        const string TopDownFolder = WorkshopPaths.Templates + "/TopDown";
        const string TopDownTemplatePath = TopDownFolder + "/Top-Down Template.asset";
        const string TopDownStarterPath = TopDownFolder + "/TopDown_Starter.unity";
        const string TopDownPreviewPath = TopDownFolder + "/Top-Down Preview.png";
        public const string TopDownExamplePath = WorkshopPaths.Examples + "/TopDown_Example.unity";

        const string FirstPersonFolder = WorkshopPaths.Templates + "/FirstPerson";
        const string FirstPersonTemplatePath = FirstPersonFolder + "/First-Person Template.asset";
        const string FirstPersonStarterPath = FirstPersonFolder + "/FirstPerson_Starter.unity";
        const string FirstPersonPreviewPath = FirstPersonFolder + "/First-Person Preview.png";
        public const string FirstPersonExamplePath = WorkshopPaths.Examples + "/FirstPerson_Example.unity";

        const string ThirdPersonFolder = WorkshopPaths.Templates + "/ThirdPerson";
        const string ThirdPersonTemplatePath = ThirdPersonFolder + "/Third-Person Template.asset";
        const string ThirdPersonStarterPath = ThirdPersonFolder + "/ThirdPerson_Starter.unity";
        const string ThirdPersonPreviewPath = ThirdPersonFolder + "/Third-Person Preview.png";
        public const string ThirdPersonExamplePath = WorkshopPaths.Examples + "/ThirdPerson_Example.unity";

        const string SideScrollerFolder = WorkshopPaths.Templates + "/SideScroller";
        const string SideScrollerTemplatePath = SideScrollerFolder + "/Side-Scroller Template.asset";
        const string SideScrollerStarterPath = SideScrollerFolder + "/SideScroller_Starter.unity";
        const string SideScrollerPreviewPath = SideScrollerFolder + "/Side-Scroller Preview.png";
        public const string SideScrollerExamplePath = WorkshopPaths.Examples + "/SideScroller_Example.unity";

        /// <summary>Every example scene, for the developer build.</summary>
        public static readonly string[] ExampleScenes = { TopDownExamplePath, FirstPersonExamplePath, ThirdPersonExamplePath, SideScrollerExamplePath };

        sealed class Kit
        {
            public WorkshopPalette Palette;
            public MovementSettings Standard, Calm, Speedy;
            public GameObject FloorLight, FloorDark, LowWall4, LowWall2, Boundary4, Crate, Tree, Rock;
            public GameObject TallWall4, TallWall2, Platform;
            public GameObject StartPoint, Gem, Spikes, Exit;
            public GameObject MovingSpikes, Checkpoint, FallZone, Key, Door, Switch;
            public ChannelStyle ChannelLook;
            public GameObject GameSetup, Player, Camera, Hud;
            public GameObject FirstPersonPlayer, FirstPersonCamera;
            public GameObject ThirdPersonPlayer, ThirdPersonCamera;
            public GameObject SideScrollerPlayer, SideScrollerCamera, FloatingPlatform;

            // Opening a scene in Single mode can unload assets that only managed code references,
            // so templates are always re-fetched by path rather than held.
            public GameTemplateDefinition TopDown => AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>(TopDownTemplatePath);
            public GameTemplateDefinition FirstPerson => AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>(FirstPersonTemplatePath);
            public GameTemplateDefinition ThirdPerson => AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>(ThirdPersonTemplatePath);
        }

        /// <summary>The template-specific parts of a scene: which template, player, camera and edge walls.</summary>
        /// <remarks>The template is held by path because opening a scene can unload it.</remarks>
        sealed class Rig
        {
            public string TemplatePath;
            public GameObject Player, Camera, Boundary;
        }

        static Rig TopDownRig(Kit kit) => new Rig { TemplatePath = TopDownTemplatePath, Player = kit.Player, Camera = kit.Camera, Boundary = kit.Boundary4 };

        static Rig FirstPersonRig(Kit kit) => new Rig { TemplatePath = FirstPersonTemplatePath, Player = kit.FirstPersonPlayer, Camera = kit.FirstPersonCamera, Boundary = kit.TallWall4 };

        static Rig ThirdPersonRig(Kit kit) => new Rig { TemplatePath = ThirdPersonTemplatePath, Player = kit.ThirdPersonPlayer, Camera = kit.ThirdPersonCamera, Boundary = kit.Boundary4 };

        static Rig SideScrollerRig(Kit kit) => new Rig { TemplatePath = SideScrollerTemplatePath, Player = kit.SideScrollerPlayer, Camera = kit.SideScrollerCamera, Boundary = kit.TallWall4 };

        [MenuItem(WorkshopPaths.MenuRoot + "Developer/Regenerate Kit Content", priority = 200)]
        public static void GenerateFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Regenerate Kit Content",
                    "Rebuilds the kit's own prefabs, materials, sounds, templates, starter and example scenes under Assets/WorkshopKit.\n\nTeam games are not touched.",
                    "Regenerate", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            GenerateAll();
        }

        public static void GenerateAll()
        {
            EnsureFolders();

            var kit = new Kit { Palette = WorkshopPalette.Create() };
            CreateMovementPresets(kit);
            CreatePrefabs(kit);
            CreateTopDownTemplate(kit);
            CreateFirstPersonTemplate(kit);
            CreateThirdPersonTemplate(kit);
            CreateSideScrollerTemplate(kit);
            CreateCatalog(kit);
            AssetDatabase.SaveAssets();

            BuildTopDownStarter(kit);
            BuildTopDownExample(kit);
            CapturePreview(TopDownTemplatePath, TopDownExamplePath, TopDownPreviewPath);
            BuildFirstPersonStarter(kit);
            BuildFirstPersonExample(kit);
            CapturePreview(FirstPersonTemplatePath, FirstPersonExamplePath, FirstPersonPreviewPath);
            BuildThirdPersonStarter(kit);
            BuildThirdPersonExample(kit);
            CapturePreview(ThirdPersonTemplatePath, ThirdPersonExamplePath, ThirdPersonPreviewPath);
            BuildSideScrollerStarter(kit);
            BuildSideScrollerExample(kit);
            CapturePreview(SideScrollerTemplatePath, SideScrollerExamplePath, SideScrollerPreviewPath);
            BuildRecoveryMilestones(kit);

            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log("[Workshop] Kit content generated.");
        }

        // ---- Assets --------------------------------------------------------------------------------

        static void EnsureFolders()
        {
            foreach (var folder in new[]
                     {
                         WorkshopPaths.Content, WorkshopPaths.Templates, TopDownFolder, FirstPersonFolder, ThirdPersonFolder, SideScrollerFolder, WorkshopPaths.Prefabs,
                         WorkshopPaths.SystemPrefabs, WorkshopPaths.EnvironmentPrefabs, WorkshopPaths.GameplayPrefabs,
                         WorkshopPaths.DecorationPrefabs, WorkshopPaths.Configuration, WorkshopPaths.Materials,
                         WorkshopPaths.Audio, WorkshopPaths.UI, WorkshopPaths.Examples, WorkshopPaths.Recovery,
                         WorkshopPaths.TeamGames, WorkshopPaths.AdoptedAssets,
                     })
                EnsureFolder(folder);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void CreateMovementPresets(Kit kit)
        {
            kit.Standard = Movement("Standard", "Balanced speed. A good default.", 5f, 40f, 50f, 720f);
            kit.Calm = Movement("Calm", "Slower and easier to steer. Good for tight mazes.", 3.5f, 25f, 35f, 540f);
            kit.Speedy = Movement("Speedy", "Fast and snappy. Harder to control.", 7f, 60f, 70f, 900f);
        }

        static MovementSettings Movement(string name, string description, float speed, float acceleration, float deceleration, float turn)
        {
            var asset = LoadOrCreate<MovementSettings>($"{WorkshopPaths.Configuration}/Movement {name}.asset");
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_DisplayName").stringValue = name;
            serialized.FindProperty("m_Description").stringValue = description;
            serialized.FindProperty("m_MoveSpeed").floatValue = speed;
            serialized.FindProperty("m_Acceleration").floatValue = acceleration;
            serialized.FindProperty("m_Deceleration").floatValue = deceleration;
            serialized.FindProperty("m_TurnSpeed").floatValue = turn;
            serialized.FindProperty("m_JumpHeight").floatValue = 1.2f;
            serialized.FindProperty("m_Gravity").floatValue = 20f;
            serialized.FindProperty("m_AirControl").floatValue = 0.6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        static void CreatePrefabs(Kit kit)
        {
            var p = kit.Palette;
            var env = WorkshopPaths.EnvironmentPrefabs;
            var deco = WorkshopPaths.DecorationPrefabs;
            var sys = WorkshopPaths.SystemPrefabs;

            kit.FloorLight = WorkshopPrefabFactory.FloorTile($"{env}/Floor Tile.prefab", "Floor Tile", p.FloorLight);
            kit.FloorDark = WorkshopPrefabFactory.FloorTile($"{env}/Floor Tile Dark.prefab", "Floor Tile Dark", p.FloorDark);
            kit.LowWall4 = WorkshopPrefabFactory.Wall($"{env}/Low Wall 4m.prefab", "Low Wall 4m", 4f, 1f, p.Wall);
            kit.LowWall2 = WorkshopPrefabFactory.Wall($"{env}/Low Wall 2m.prefab", "Low Wall 2m", 2f, 1f, p.Wall);
            kit.Boundary4 = WorkshopPrefabFactory.Wall($"{env}/Boundary Wall 4m.prefab", "Boundary Wall 4m", 4f, 2f, p.Boundary);
            kit.TallWall4 = WorkshopPrefabFactory.Wall($"{env}/Tall Wall 4m.prefab", "Tall Wall 4m", 4f, 3f, p.Wall, blocksOverheadView: true);
            kit.TallWall2 = WorkshopPrefabFactory.Wall($"{env}/Tall Wall 2m.prefab", "Tall Wall 2m", 2f, 3f, p.Wall, blocksOverheadView: true);
            kit.Platform = WorkshopPrefabFactory.Platform($"{env}/Raised Platform.prefab", p.Stone, p.FloorDark);

            kit.Crate = WorkshopPrefabFactory.Crate($"{deco}/Crate.prefab", p.Wood, p.Trunk);
            kit.Tree = WorkshopPrefabFactory.Tree($"{deco}/Tree.prefab", p.Trunk, p.Leaves);
            kit.Rock = WorkshopPrefabFactory.Rock($"{deco}/Rock.prefab", p.Stone);

            kit.StartPoint = WorkshopPrefabFactory.StartPoint(WorkshopPaths.StartPointPrefab, p.StartPad);
            kit.Gem = WorkshopPrefabFactory.Collectible($"{WorkshopPaths.GameplayPrefabs}/Gem.prefab", p.Gem, WorkshopAudioFactory.Pickup);
            kit.Spikes = WorkshopPrefabFactory.SpikeHazard($"{WorkshopPaths.GameplayPrefabs}/Spike Trap.prefab", p.HazardSpike, p.HazardBase);
            kit.Exit = WorkshopPrefabFactory.ExitGoal(WorkshopPaths.ExitGoalPrefab, p.GoalLocked, p.GoalOpen);

            var gameplay = WorkshopPaths.GameplayPrefabs;
            kit.ChannelLook = CreateChannelStyle(p);
            kit.MovingSpikes = WorkshopPrefabFactory.SpikeHazard($"{gameplay}/Moving Spikes.prefab", p.HazardSpike, p.HazardBase, moving: true);
            kit.Checkpoint = WorkshopPrefabFactory.Checkpoint($"{gameplay}/Checkpoint.prefab", p.CheckpointIdle, p.CheckpointActive, p.Frame, WorkshopAudioFactory.Checkpoint);
            kit.FallZone = WorkshopPrefabFactory.FallZone($"{gameplay}/Fall Zone.prefab");
            kit.Key = WorkshopPrefabFactory.Key($"{gameplay}/Key.prefab", kit.ChannelLook, WorkshopAudioFactory.Key);
            kit.Door = WorkshopPrefabFactory.Door($"{gameplay}/Door.prefab", kit.ChannelLook, p.Frame, p.Symbol, WorkshopAudioFactory.Door);
            kit.Switch = WorkshopPrefabFactory.PressureSwitch($"{gameplay}/Pressure Switch.prefab", kit.ChannelLook, p.Frame, p.Symbol, WorkshopAudioFactory.Switch);

            kit.GameSetup = WorkshopPrefabFactory.GameSetup(WorkshopPaths.GameSetupPrefab,
                WorkshopAudioFactory.Win, WorkshopAudioFactory.Lose, WorkshopAudioFactory.Blocked, WorkshopAudioFactory.Respawn);
            kit.Player = WorkshopPrefabFactory.TopDownPlayer($"{sys}/Top-Down Player.prefab", kit.Standard, p.PlayerBody, p.PlayerVisor);
            kit.Camera = WorkshopPrefabFactory.TopDownCamera($"{sys}/Top-Down Camera.prefab");
            kit.Hud = WorkshopHudFactory.Build(WorkshopPaths.HudPrefab);
            kit.FirstPersonPlayer = WorkshopPrefabFactory.FirstPersonPlayer($"{sys}/First-Person Player.prefab", kit.Standard, p.PlayerBody);
            kit.FirstPersonCamera = WorkshopPrefabFactory.FirstPersonCamera($"{sys}/First-Person Camera.prefab");
            kit.ThirdPersonPlayer = WorkshopPrefabFactory.ThirdPersonPlayer($"{sys}/Third-Person Player.prefab", kit.Standard, p.PlayerBody, p.PlayerVisor);
            kit.ThirdPersonCamera = WorkshopPrefabFactory.ThirdPersonCamera($"{sys}/Third-Person Camera.prefab");
            kit.SideScrollerPlayer = WorkshopPrefabFactory.ThirdPersonPlayer($"{sys}/Side-Scroller Player.prefab", kit.Standard, p.PlayerBody, p.PlayerVisor, "Side-Scroller Player");
            kit.SideScrollerCamera = WorkshopPrefabFactory.SideScrollerCamera($"{sys}/Side-Scroller Camera.prefab");
            kit.FloatingPlatform = WorkshopPrefabFactory.FloatingPlatform($"{env}/Floating Platform.prefab", p.FloorLight, p.Stone);
        }

        static ChannelStyle CreateChannelStyle(WorkshopPalette palette)
        {
            var style = LoadOrCreate<ChannelStyle>($"{WorkshopPaths.Configuration}/Channel Style.asset");
            var serialized = new SerializedObject(style);
            var materials = serialized.FindProperty("m_Materials");
            var symbols = serialized.FindProperty("m_Symbols");
            var colors = serialized.FindProperty("m_UiColors");
            var meshes = new[] { WorkshopMeshFactory.Circle, WorkshopMeshFactory.Square, WorkshopMeshFactory.Triangle, WorkshopMeshFactory.Diamond };
            materials.arraySize = symbols.arraySize = colors.arraySize = Channels.Count;
            for (var i = 0; i < Channels.Count; i++)
            {
                materials.GetArrayElementAtIndex(i).objectReferenceValue = palette.Channel[i];
                symbols.GetArrayElementAtIndex(i).objectReferenceValue = meshes[i];
                colors.GetArrayElementAtIndex(i).colorValue = palette.Channel[i].GetColor("_BaseColor");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return style;
        }

        static void CreateTopDownTemplate(Kit kit) => CreateTemplate(kit, TopDownTemplatePath, "Top-Down", TemplatePerspective.TopDown,
            "Look down on your level from above. Great for mazes, collecting and dodging. No jumping.",
            "WASD or Arrow keys: Move", false, kit.Player, kit.Camera);

        static void CreateFirstPersonTemplate(Kit kit) => CreateTemplate(kit, FirstPersonTemplatePath, "First-Person", TemplatePerspective.FirstPerson,
            "See through the player's eyes. Great for mazes, exploring and jumping challenges.",
            "WASD: Move\nMouse: Look\nSpace: Jump", true, kit.FirstPersonPlayer, kit.FirstPersonCamera);

        static void CreateThirdPersonTemplate(Kit kit) => CreateTemplate(kit, ThirdPersonTemplatePath, "Third-Person", TemplatePerspective.ThirdPerson,
            "Follow your character from behind. Great for adventure routes, jumps and exploring.",
            "WASD: Move\nMouse: Orbit camera\nSpace: Jump", true, kit.ThirdPersonPlayer, kit.ThirdPersonCamera);

        static void CreateSideScrollerTemplate(Kit kit) => CreateTemplate(kit, SideScrollerTemplatePath, "Side-Scroller", TemplatePerspective.SideScroller,
            "Watch from the side and run left and right. Great for platform jumping courses.",
            "A/D or Left/Right: Move\nSpace: Jump", true, kit.SideScrollerPlayer, kit.SideScrollerCamera);

        static void CreateTemplate(Kit kit, string path, string displayName, TemplatePerspective perspective, string description,
            string controls, bool canJump, GameObject player, GameObject camera)
        {
            var template = LoadOrCreate<GameTemplateDefinition>(path);
            var serialized = new SerializedObject(template);
            serialized.FindProperty("m_DisplayName").stringValue = displayName;
            serialized.FindProperty("m_Perspective").enumValueIndex = (int)perspective;
            serialized.FindProperty("m_Description").stringValue = description;
            serialized.FindProperty("m_Controls").stringValue = controls;
            serialized.FindProperty("m_CanJump").boolValue = canJump;
            serialized.FindProperty("m_PlayerPrefab").objectReferenceValue = player;
            serialized.FindProperty("m_CameraRigPrefab").objectReferenceValue = camera;
            var presets = serialized.FindProperty("m_MovementPresets");
            presets.arraySize = 3;
            presets.GetArrayElementAtIndex(0).objectReferenceValue = kit.Standard;
            presets.GetArrayElementAtIndex(1).objectReferenceValue = kit.Calm;
            presets.GetArrayElementAtIndex(2).objectReferenceValue = kit.Speedy;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateCatalog(Kit kit)
        {
            var catalog = LoadOrCreate<WorkshopCatalog>(WorkshopPaths.Catalog);
            catalog.entries = new List<CatalogEntry>
            {
                Entry("Floor Tile", "A 4 × 4 m floor square. Snap tiles together to make the ground.", "Snaps to a 4 m grid.", kit.FloorLight, CatalogCategory.Environment, 4f, "Environment"),
                Entry("Floor Tile (Dark)", "The same floor in a darker shade, for patterns and paths.", "Snaps to a 4 m grid.", kit.FloorDark, CatalogCategory.Environment, 4f, "Environment"),
                Entry("Low Wall 4 m", "A knee-high wall the camera can see over. Rotate with E to turn corners.", "Snaps to a 1 m grid.", kit.LowWall4, CatalogCategory.Environment, 1f, "Environment"),
                Entry("Low Wall 2 m", "A short knee-high wall for gaps and corners.", "Snaps to a 1 m grid.", kit.LowWall2, CatalogCategory.Environment, 1f, "Environment"),
                Entry("Boundary Wall 4 m", "A taller edge wall that keeps players inside the level.", "Snaps to a 1 m grid.", kit.Boundary4, CatalogCategory.Environment, 1f, "Environment"),
                Entry("Gem", "A collectible. Touch it to pick it up. Counts toward Collect All, Then Exit.", "Restart brings it back; respawning keeps it collected.", kit.Gem, CatalogCategory.Objectives, 1f, "Gameplay"),
                Entry("Exit Goal", "Walk in to win once the rule in Game Setup is met.", "Shows grey while locked and green when open.", kit.Exit, CatalogCategory.Objectives, 1f, "Gameplay"),
                Entry("Start Point", "Where the player begins and respawns. A game has exactly one.", "Use the blue ring to change which way the player faces.", kit.StartPoint, CatalogCategory.Objectives, 1f, "Gameplay"),
                Entry("Spike Trap", "Touching it sends the player back to the start.", "Items and time are kept. 2 × 2 m.", kit.Spikes, CatalogCategory.Challenges, 1f, "Gameplay"),
                Entry("Crate", "A 1 m wooden box. Solid; use it as an obstacle.", "", kit.Crate, CatalogCategory.Decoration, 1f, "Decoration"),
                Entry("Tree", "A tree. Only the trunk is solid.", "", kit.Tree, CatalogCategory.Decoration, 1f, "Decoration"),
                Entry("Rock", "A low rock. Solid.", "", kit.Rock, CatalogCategory.Decoration, 0.5f, "Decoration"),
                Entry("Moving Spikes", "Spikes that slide back and forth. Touching them sends the player back.", "Drag the orange end handle to set the path. Speed: Slow, Medium or Fast.", kit.MovingSpikes, CatalogCategory.Challenges, 1f, "Gameplay"),
                Entry("Fall Zone", "An invisible area that counts as falling out of the level, for pits and gaps.", "Invisible in the game. Resize with the purple handles.", kit.FallZone, CatalogCategory.Challenges, 1f, "Gameplay"),
                Entry("Checkpoint", "Touch it to make it your respawn point. Items and time are kept.", "Restart clears it. Use the green ring to set the respawn facing.", kit.Checkpoint, CatalogCategory.Progress, 1f, "Gameplay"),
                Entry("Key", "Opens every door with the same colour and symbol, for good.", "Choose Red, Blue, Green or Yellow in the Inspector.", kit.Key, CatalogCategory.Progress, 1f, "Gameplay"),
                Entry("Door", "Blocks the way until a key or switch with the same colour and symbol opens it.", "2 m wide. Never closes on the player.", kit.Door, CatalogCategory.Progress, 1f, "Gameplay"),
                Entry("Pressure Switch", "Stand on it to open doors with the same colour and symbol.", "Doors close when you step off, unless Latch is on.", kit.Switch, CatalogCategory.Progress, 1f, "Gameplay"),
                Entry("Tall Wall 4 m", "A 3 m wall nobody can see or jump over. Builds real mazes in first- and third-person.", "Hidden in Top-Down, where it would hide the player.", kit.TallWall4, CatalogCategory.Environment, 1f, "Environment"),
                Entry("Tall Wall 2 m", "A short 3 m wall for gaps and corners.", "Hidden in Top-Down, where it would hide the player.", kit.TallWall2, CatalogCategory.Environment, 1f, "Environment"),
                Entry("Raised Platform", "A 2 × 2 m block 1 m high. Jump on to reach things.", "Only offered in templates that can jump.", kit.Platform, CatalogCategory.Environment, 1f, "Environment"),
                Entry("Floating Platform", "A 3 × 3 m slab that floats where you put it. Jump between them to cross gaps.", "Only offered in templates that can jump. Its top is where you place it.", kit.FloatingPlatform, CatalogCategory.Environment, 0.5f, "Environment"),
            };
            EditorUtility.SetDirty(catalog);
        }

        static CatalogEntry Entry(string name, string description, string constraints, GameObject prefab, CatalogCategory category, float grid, string group) =>
            new CatalogEntry
            {
                displayName = name, description = description, constraints = constraints, prefab = prefab,
                category = category, gridSize = grid, groupName = group,
            };

        // ---- Scenes --------------------------------------------------------------------------------

        sealed class SceneBuilder
        {
            readonly Kit m_Kit;
            readonly Rig m_Rig;
            public readonly Scene Scene;
            public WorkshopGameContext Context;
            // The orbit camera is placed after the level exists, so its wall check sees the walls.
            ThirdPersonCameraRig m_PendingOrbit;
            Pose m_PendingOrbitPose;

            public SceneBuilder(Kit kit, Rig rig)
            {
                m_Kit = kit;
                m_Rig = rig;
                Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.78f, 0.84f, 0.92f);
                RenderSettings.ambientEquatorColor = new Color(0.62f, 0.64f, 0.66f);
                RenderSettings.ambientGroundColor = new Color(0.42f, 0.4f, 0.36f);
                RenderSettings.skybox = null;
                RenderSettings.fog = false;

                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            }

            public GameObject Add(GameObject prefab, Vector3 position, float yaw = 0f, string group = null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
                instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                if (group != null)
                    instance.transform.SetParent(Group(group), true);
                return instance;
            }

            Transform Group(string name)
            {
                Transform level = null;
                foreach (var root in Scene.GetRootGameObjects())
                    if (root.name == WorkshopPlacement.LevelRootName)
                        level = root.transform;
                if (level == null)
                {
                    var go = new GameObject(WorkshopPlacement.LevelRootName);
                    SceneManager.MoveGameObjectToScene(go, Scene);
                    level = go.transform;
                }
                var group = level.Find(name);
                if (group != null)
                    return group;
                var groupObject = new GameObject(name);
                groupObject.transform.SetParent(level, false);
                return groupObject.transform;
            }

            public void Systems(string title, string objective, GoalRule rule, Vector3 start, float startYaw, TimerPreset timer = TimerPreset.Off)
            {
                Context = Add(m_Kit.GameSetup, Vector3.zero).GetComponent<WorkshopGameContext>();
                var serialized = new SerializedObject(Context);
                serialized.FindProperty("m_Rules.title").stringValue = title;
                serialized.FindProperty("m_Rules.objective").stringValue = objective;
                serialized.FindProperty("m_Rules.goalRule").enumValueIndex = (int)rule;
                serialized.FindProperty("m_Rules.collectibleName").stringValue = "Gem";
                serialized.FindProperty("m_Rules.timer").enumValueIndex = (int)timer;
                serialized.FindProperty("m_Template").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>(m_Rig.TemplatePath);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Add(m_Kit.StartPoint, start, startYaw, "Gameplay");
                Add(m_Rig.Player, start + Vector3.up * PlayerSpawn.SpawnLift, startYaw);
                // The Game view shows the opening view before Play: first-person cameras sit at eye height.
                var camera = Add(m_Rig.Camera, start, startYaw).GetComponent<WorkshopCameraRig>();
                if (camera is FirstPersonCameraRig)
                {
                    camera.transform.position = start + Vector3.up * FirstPersonCameraRig.EyeHeight;
                    camera.ApplyCameraPose();
                }
                else if (camera is SideScrollerCameraRig side)
                {
                    var serializedSide = new SerializedObject(side);
                    serializedSide.FindProperty("m_PlaneDepth").floatValue = start.z;
                    serializedSide.ApplyModifiedPropertiesWithoutUndo();
                    side.FrameFeet(start);
                }
                else if (camera is ThirdPersonCameraRig orbit)
                {
                    m_PendingOrbit = orbit;
                    m_PendingOrbitPose = new Pose(start, Quaternion.Euler(0f, startYaw, 0f));
                }
                else
                {
                    camera.transform.rotation = Quaternion.identity;
                    camera.ApplyCameraPose();
                }
                Add(m_Kit.Hud, Vector3.zero);
            }

            /// <summary>Square floor of 4 m tiles centred on the origin, with a checker pattern and boundary walls.</summary>
            public void Floor(int tilesPerSide)
            {
                var half = tilesPerSide * 2f;
                for (var x = 0; x < tilesPerSide; x++)
                for (var z = 0; z < tilesPerSide; z++)
                {
                    var position = new Vector3(-half + 2f + x * 4f, 0f, -half + 2f + z * 4f);
                    Add((x + z) % 2 == 0 ? m_Kit.FloorLight : m_Kit.FloorDark, position, 0f, "Environment");
                }
                for (var i = 0; i < tilesPerSide; i++)
                {
                    var along = -half + 2f + i * 4f;
                    Add(m_Rig.Boundary, new Vector3(along, 0f, half + 0.2f), 0f, "Environment");
                    Add(m_Rig.Boundary, new Vector3(along, 0f, -half - 0.2f), 0f, "Environment");
                    Add(m_Rig.Boundary, new Vector3(half + 0.2f, 0f, along), 90f, "Environment");
                    Add(m_Rig.Boundary, new Vector3(-half - 0.2f, 0f, along), 90f, "Environment");
                }
            }

            public void Save(string path)
            {
                if (m_PendingOrbit != null)
                    m_PendingOrbit.PlaceBehind(m_PendingOrbitPose);
                foreach (var root in Scene.GetRootGameObjects())
                    WorkshopPlacement.AssignContext(root, Context);
                if (!EditorSceneManager.SaveScene(Scene, path))
                    throw new System.InvalidOperationException($"Saving scene '{path}' failed.");
            }
        }

        static void BuildTopDownStarter(Kit kit)
        {
            var builder = new SceneBuilder(kit, TopDownRig(kit));
            builder.Floor(3);
            builder.Systems("My Top-Down Game", "Collect every gem, then reach the exit.", GoalRule.CollectAllThenExit, new Vector3(-4f, 0f, -4f), 0f);
            builder.Add(kit.Gem, new Vector3(-4f, 0f, 3f), 0f, "Gameplay");
            builder.Add(kit.Gem, new Vector3(0f, 0f, 0f), 0f, "Gameplay");
            builder.Add(kit.Gem, new Vector3(3f, 0f, -4f), 0f, "Gameplay");
            builder.Add(kit.Exit, new Vector3(4f, 0f, 4f), 0f, "Gameplay");
            builder.Save(TopDownStarterPath);

            var serialized = new SerializedObject(kit.TopDown);
            serialized.FindProperty("m_StarterSceneGuid").stringValue = AssetDatabase.AssetPathToGUID(TopDownStarterPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static void BuildTopDownExample(Kit kit)
        {
            var builder = new SceneBuilder(kit, TopDownRig(kit));
            builder.Floor(5);
            builder.Systems("Gem Maze",
                "Collect all 6 gems in 3 minutes. Grab the red key and step on the blue switch to open the doors to the exit.",
                GoalRule.CollectAllThenExit, new Vector3(-8f, 0f, -8f), 0f, TimerPreset.Relaxed);

            // Three lanes: up the left, down the middle, up the right through two doors to the exit.
            foreach (var z in new[] { -8f, -4f, 0f })
                builder.Add(kit.LowWall4, new Vector3(-4f, 0f, z), 90f, "Environment");
            foreach (var z in new[] { 0f, 4f, 8f })
                builder.Add(kit.LowWall4, new Vector3(4f, 0f, z), 90f, "Environment");
            builder.Add(kit.LowWall4, new Vector3(6f, 0f, 2f), 0f, "Environment");
            builder.Add(kit.LowWall4, new Vector3(6f, 0f, 6f), 0f, "Environment");
            SetChannel(builder.Add(kit.Door, new Vector3(9f, 0f, 2f), 0f, "Gameplay"), ChannelColor.Blue);
            SetChannel(builder.Add(kit.Door, new Vector3(9f, 0f, 6f), 0f, "Gameplay"), ChannelColor.Red);

            foreach (var gem in new[]
                     {
                         new Vector3(-8f, 0f, -2f), new Vector3(-6f, 0f, 6f), new Vector3(0f, 0f, 6f),
                         new Vector3(0f, 0f, -6f), new Vector3(8f, 0f, -4f), new Vector3(6f, 0f, 4f),
                     })
                builder.Add(kit.Gem, gem, 0f, "Gameplay");

            builder.Add(kit.Spikes, new Vector3(-8f, 0f, 1f), 0f, "Gameplay");
            builder.Add(kit.Spikes, new Vector3(-2f, 0f, -2f), 0f, "Gameplay");
            builder.Add(kit.Spikes, new Vector3(5.5f, 0f, -4.5f), 0f, "Gameplay");
            var mover = builder.Add(kit.MovingSpikes, new Vector3(-3f, 0f, 2f), 0f, "Gameplay").GetComponent<HazardMover>();
            var moverSettings = new SerializedObject(mover);
            moverSettings.FindProperty("m_Travel").vector3Value = new Vector3(6f, 0f, 0f);
            moverSettings.FindProperty("m_Speed").enumValueIndex = (int)MoveSpeed.Medium;
            moverSettings.ApplyModifiedPropertiesWithoutUndo();

            builder.Add(kit.Checkpoint, new Vector3(-7f, 0f, 8f), 90f, "Gameplay");
            builder.Add(kit.Checkpoint, new Vector3(6f, 0f, -8f), 0f, "Gameplay");
            SetChannel(builder.Add(kit.Key, new Vector3(2f, 0f, -8f), 0f, "Gameplay"), ChannelColor.Red);
            SetChannel(builder.Add(kit.Switch, new Vector3(8f, 0f, -1f), 0f, "Gameplay"), ChannelColor.Blue, latch: true);
            builder.Add(kit.Exit, new Vector3(7f, 0f, 8f), 0f, "Gameplay");

            builder.Add(kit.Tree, new Vector3(2.8f, 0f, 8.5f), 0f, "Decoration");
            builder.Add(kit.Tree, new Vector3(-9f, 0f, -5f), 40f, "Decoration");
            builder.Add(kit.Rock, new Vector3(-2.5f, 0f, -9f), 15f, "Decoration");
            builder.Add(kit.Rock, new Vector3(-9.2f, 0f, 3.5f), 70f, "Decoration");
            builder.Add(kit.Crate, new Vector3(9f, 0f, -9f), 10f, "Decoration");
            builder.Add(kit.Crate, new Vector3(2.8f, 0f, 4.5f), 30f, "Decoration");
            builder.Save(TopDownExamplePath);
        }

        static void SetChannel(GameObject instance, ChannelColor channel, bool latch = false)
        {
            var piece = instance.GetComponent<ChannelPiece>();
            var serialized = new SerializedObject(piece);
            serialized.FindProperty(ChannelPiece.ChannelPropertyName).enumValueIndex = (int)channel;
            var latchProperty = serialized.FindProperty(TriggerSwitch.LatchPropertyName);
            if (latchProperty != null)
                latchProperty.boolValue = latch;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            piece.ApplyChannelLook();
        }

        /// <summary>Renders the example scene from its gameplay camera into the template preview image.</summary>
        static void CapturePreview(string templatePath, string scenePath, string pngPath)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.Log("[Workshop] No graphics device (-nographics); template preview image not updated.");
                return;
            }
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var rig = WorkshopSceneQuery.FindActive<WorkshopCameraRig>(scene)[0];
            if (rig is SideScrollerCameraRig side)
                side.FrameFeet(WorkshopSceneQuery.FindActive<PlayerSpawn>(scene)[0].SpawnPose.position);
            else if (rig is ThirdPersonCameraRig orbit)
                orbit.PlaceBehind(WorkshopSceneQuery.FindActive<PlayerSpawn>(scene)[0].SpawnPose);
            else if (rig is TopDownCameraRig)
            {
                // Pull back over the whole level; first-person previews keep the opening view.
                rig.transform.position = Vector3.zero;
                var serializedRig = new SerializedObject(rig);
                serializedRig.FindProperty("m_Distance").floatValue = 26f;
                serializedRig.ApplyModifiedPropertiesWithoutUndo();
            }
            rig.ApplyCameraPose();

            var camera = rig.Camera;
            // Explicit sRGB target so the PNG is not gamma-encoded twice.
            var texture = new RenderTexture(256, 256, 24, GraphicsFormat.R8G8B8A8_SRGB) { antiAliasing = 4 };
            var previous = camera.targetTexture;
            camera.aspect = 1f;
            camera.targetTexture = texture;
            // In batch mode the first renders draw before per-material data is uploaded, so warm up first.
            for (var i = 0; i < 3; i++)
                camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = previous;
            File.WriteAllBytes(Path.GetFullPath(pngPath), image.EncodeToPNG());
            Object.DestroyImmediate(image);
            texture.Release();
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            // The scene is left unsaved, so the example keeps its authored camera settings.
            var serializedTemplate = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>(templatePath));
            serializedTemplate.FindProperty("m_Preview").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            serializedTemplate.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
