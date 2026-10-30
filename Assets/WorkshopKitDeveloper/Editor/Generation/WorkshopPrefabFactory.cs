using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Builds the kit's prefabs in memory and saves them with PrefabUtility at fixed owned paths.
    /// Re-saving an existing path keeps its GUID, so regeneration is repeatable.
    /// </summary>
    static class WorkshopPrefabFactory
    {
        // ---- Building blocks -----------------------------------------------------------------------

        public static GameObject Save(GameObject root, string path)
        {
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
                if (!success || prefab == null)
                    throw new System.InvalidOperationException($"Saving prefab '{path}' failed.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A primitive visual child. Its built-in collider is removed unless <paramref name="keepCollider"/>.</summary>
        public static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material,
            bool keepCollider = false, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void SetReference(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static ParticleSystem Burst(Transform parent, string name, Vector3 position, Material material, int count, float speed, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startRotation3D = false;
            main.gravityModifier = 0.6f;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return particles;
        }

        // ---- Environment ---------------------------------------------------------------------------

        /// <summary>Pivot at the centre of the walkable top surface.</summary>
        public static GameObject FloorTile(string path, string name, Material material)
        {
            var root = new GameObject(name);
            Shape(PrimitiveType.Cube, root.transform, "Surface", new Vector3(0f, -0.1f, 0f), new Vector3(4f, 0.2f, 4f), material, keepCollider: true);
            return Save(root, path);
        }

        /// <summary>Pivot at the bottom centre; the wall runs along local X.</summary>
        public static GameObject Wall(string path, string name, float length, float height, Material material, bool blocksOverheadView = false)
        {
            var root = new GameObject(name);
            Shape(PrimitiveType.Cube, root.transform, "Wall", new Vector3(0f, height * 0.5f, 0f), new Vector3(length, height, 0.4f), material, keepCollider: true);
            if (blocksOverheadView)
                Traits(root, requiresJump: false, blocksOverheadView: true);
            return Save(root, path);
        }

        /// <summary>A 2 × 2 m block, 1 m high, pivot at the bottom centre. Reachable only by jumping.</summary>
        public static GameObject Platform(string path, Material sides, Material top)
        {
            var root = new GameObject("Raised Platform");
            Shape(PrimitiveType.Cube, root.transform, "Block", new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.9f, 2f), sides, keepCollider: true);
            Shape(PrimitiveType.Cube, root.transform, "Top", new Vector3(0f, 0.95f, 0f), new Vector3(2f, 0.1f, 2f), top, keepCollider: true);
            Traits(root, requiresJump: true, blocksOverheadView: false);
            return Save(root, path);
        }

        static void Traits(GameObject root, bool requiresJump, bool blocksOverheadView)
        {
            var traits = root.AddComponent<PieceTraits>();
            var serialized = new SerializedObject(traits);
            serialized.FindProperty("m_RequiresJump").boolValue = requiresJump;
            serialized.FindProperty("m_BlocksOverheadView").boolValue = blocksOverheadView;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject Crate(string path, Material wood, Material trim)
        {
            var root = new GameObject("Crate");
            Shape(PrimitiveType.Cube, root.transform, "Box", new Vector3(0f, 0.5f, 0f), Vector3.one, wood, keepCollider: true);
            Shape(PrimitiveType.Cube, root.transform, "Band", new Vector3(0f, 0.5f, 0f), new Vector3(1.02f, 0.15f, 1.02f), trim);
            return Save(root, path);
        }

        public static GameObject Tree(string path, Material trunk, Material leaves)
        {
            var root = new GameObject("Tree");
            Shape(PrimitiveType.Cylinder, root.transform, "Trunk", new Vector3(0f, 0.8f, 0f), new Vector3(0.45f, 0.8f, 0.45f), trunk, keepCollider: true);
            Shape(PrimitiveType.Sphere, root.transform, "Leaves", new Vector3(0f, 2.1f, 0f), new Vector3(1.8f, 1.6f, 1.8f), leaves);
            Shape(PrimitiveType.Sphere, root.transform, "Leaves Top", new Vector3(0.2f, 2.8f, 0.1f), new Vector3(1.1f, 1f, 1.1f), leaves);
            return Save(root, path);
        }

        public static GameObject Rock(string path, Material stone)
        {
            var root = new GameObject("Rock");
            Shape(PrimitiveType.Sphere, root.transform, "Stone", new Vector3(0f, 0.3f, 0f), new Vector3(1.3f, 0.75f, 1.05f), stone, keepCollider: true, euler: new Vector3(0f, 20f, 8f));
            Shape(PrimitiveType.Sphere, root.transform, "Pebble", new Vector3(0.65f, 0.15f, 0.35f), new Vector3(0.5f, 0.35f, 0.45f), stone);
            return Save(root, path);
        }

        // ---- Gameplay ------------------------------------------------------------------------------

        public static GameObject StartPoint(string path, Material pad)
        {
            var root = new GameObject("Start Point");
            root.AddComponent<PlayerSpawn>();
            Shape(PrimitiveType.Cylinder, root.transform, "Pad", new Vector3(0f, 0.01f, 0f), new Vector3(1.4f, 0.01f, 1.4f), pad);
            Shape(PrimitiveType.Cube, root.transform, "Arrow", new Vector3(0f, 0.03f, 0.55f), new Vector3(0.25f, 0.02f, 0.5f), pad);
            return Save(root, path);
        }

        public static GameObject Collectible(string path, Material gem, AudioClip sound)
        {
            var root = new GameObject("Gem");
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.8f, 0f);
            trigger.radius = 0.6f;
            var collectible = root.AddComponent<Collectible>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            Shape(PrimitiveType.Cube, visual.transform, "Crystal", Vector3.zero, new Vector3(0.42f, 0.42f, 0.42f), gem, euler: new Vector3(45f, 0f, 45f));
            var burst = Burst(root.transform, "Pickup Burst", new Vector3(0f, 0.8f, 0f), gem, 16, 4f, 0.14f);

            SetReference(collectible, "m_Visual", visual);
            SetReference(collectible, "m_Burst", burst);
            SetReference(collectible, "m_Sound", sound);
            return Save(root, path);
        }

        public static GameObject SpikeHazard(string path, Material spike, Material plate, bool moving = false)
        {
            var root = new GameObject(moving ? "Moving Spikes" : "Spike Trap");
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.35f, 0f);
            trigger.size = new Vector3(2f, 0.7f, 2f);
            root.AddComponent<Hazard>();
            if (moving)
            {
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                root.AddComponent<HazardMover>();
            }

            Shape(PrimitiveType.Cube, root.transform, "Plate", new Vector3(0f, 0.04f, 0f), new Vector3(2f, 0.08f, 2f), plate);
            for (var x = -1; x <= 1; x++)
            for (var z = -1; z <= 1; z++)
                Shape(PrimitiveType.Cube, root.transform, $"Spike {x + 1}{z + 1}", new Vector3(x * 0.62f, 0.22f, z * 0.62f),
                    new Vector3(0.26f, 0.26f, 0.26f), spike, euler: new Vector3(45f, 0f, 45f));
            return Save(root, path);
        }

        public static GameObject ExitGoal(string path, Material locked, Material open)
        {
            var root = new GameObject("Exit Goal");
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(2f, 2f, 2f);
            var goal = root.AddComponent<GoalZone>();

            var lockedVisual = GoalVisual(root.transform, "Locked", locked, 0.6f);
            var openVisual = GoalVisual(root.transform, "Open", open, 1.6f);
            var burst = Burst(root.transform, "Win Burst", new Vector3(0f, 1f, 0f), open, 40, 6f, 0.2f);

            SetReference(goal, "m_LockedVisual", lockedVisual);
            SetReference(goal, "m_OpenVisual", openVisual);
            SetReference(goal, "m_WinBurst", burst);
            return Save(root, path);
        }

        static GameObject GoalVisual(Transform parent, string name, Material material, float postHeight)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            Shape(PrimitiveType.Cylinder, group.transform, "Pad", new Vector3(0f, 0.03f, 0f), new Vector3(2.1f, 0.03f, 2.1f), material);
            for (var i = 0; i < 4; i++)
            {
                var angle = i * 90f + 45f;
                var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.15f;
                Shape(PrimitiveType.Cube, group.transform, $"Post {i + 1}", offset + Vector3.up * (postHeight * 0.5f),
                    new Vector3(0.18f, postHeight, 0.18f), material);
            }
            return group;
        }

        // ---- Phase 2 mechanics ---------------------------------------------------------------------

        static MeshRenderer Plaque(Transform parent, string name, Vector3 position, Vector3 euler, float scale, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        static void SetArray(Object target, string property, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(property);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetupChannel(ChannelPiece piece, ChannelStyle style, Renderer[] tinted, MeshFilter[] symbols)
        {
            SetReference(piece, "m_Style", style);
            SetArray(piece, "m_Tinted", tinted);
            SetArray(piece, "m_Symbols", symbols);
            piece.ApplyChannelLook();
        }

        public static GameObject Checkpoint(string path, Material idle, Material active, Material pole, AudioClip sound)
        {
            var root = new GameObject("Checkpoint");
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(2f, 2f, 2f);
            var checkpoint = root.AddComponent<Checkpoint>();

            var pad = Shape(PrimitiveType.Cylinder, root.transform, "Pad", new Vector3(0f, 0.015f, 0f), new Vector3(1.5f, 0.015f, 1.5f), idle);
            Shape(PrimitiveType.Cylinder, root.transform, "Pole", new Vector3(-0.85f, 1.1f, 0f), new Vector3(0.08f, 1.1f, 0.08f), pole);
            var flag = Shape(PrimitiveType.Cube, root.transform, "Flag", new Vector3(-0.5f, 1.9f, 0f), new Vector3(0.65f, 0.4f, 0.05f), idle);
            var burst = Burst(root.transform, "Burst", new Vector3(-0.5f, 1.9f, 0f), active, 20, 3f, 0.12f);

            SetArray(checkpoint, "m_Flag", new Object[] { pad.GetComponent<Renderer>(), flag.GetComponent<Renderer>() });
            SetReference(checkpoint, "m_InactiveMaterial", idle);
            SetReference(checkpoint, "m_ActiveMaterial", active);
            SetReference(checkpoint, "m_Burst", burst);
            SetReference(checkpoint, "m_Sound", sound);
            return Save(root, path);
        }

        public static GameObject FallZone(string path)
        {
            var root = new GameObject("Fall Zone");
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.size = new Vector3(4f, 1f, 4f);
            root.AddComponent<FallZone>();
            return Save(root, path);
        }

        public static GameObject Key(string path, ChannelStyle style, AudioClip sound)
        {
            var root = new GameObject("Key");
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.9f, 0f);
            trigger.radius = 0.6f;
            var key = root.AddComponent<KeyPickup>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            var bow = Plaque(visual.transform, "Symbol", new Vector3(0f, 0.15f, 0f), Vector3.zero, 1.1f, style.SymbolFor(ChannelColor.Red), style.MaterialFor(ChannelColor.Red));
            var shaft = Shape(PrimitiveType.Cube, visual.transform, "Shaft", new Vector3(0f, -0.3f, 0f), new Vector3(0.09f, 0.55f, 0.06f), style.MaterialFor(ChannelColor.Red));
            var tooth = Shape(PrimitiveType.Cube, visual.transform, "Tooth", new Vector3(0.09f, -0.5f, 0f), new Vector3(0.16f, 0.08f, 0.06f), style.MaterialFor(ChannelColor.Red));
            var burst = Burst(root.transform, "Pickup Burst", new Vector3(0f, 1f, 0f), style.MaterialFor(ChannelColor.Red), 16, 4f, 0.12f);

            SetReference(key, "m_Visual", visual);
            SetReference(key, "m_Burst", burst);
            SetReference(key, "m_Sound", sound);
            SetupChannel(key, style,
                new Renderer[] { bow, shaft.GetComponent<Renderer>(), tooth.GetComponent<Renderer>(), burst.GetComponent<ParticleSystemRenderer>() },
                new[] { bow.GetComponent<MeshFilter>() });
            return Save(root, path);
        }

        public static GameObject Door(string path, ChannelStyle style, Material frame, Material symbol, AudioClip sound)
        {
            var root = new GameObject("Door");
            var blocker = root.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, 1f, 0f);
            blocker.size = new Vector3(2f, 2f, 0.3f);
            var door = root.AddComponent<Door>();

            Shape(PrimitiveType.Cube, root.transform, "Post Left", new Vector3(-1.1f, 1.1f, 0f), new Vector3(0.2f, 2.2f, 0.4f), frame, keepCollider: true);
            Shape(PrimitiveType.Cube, root.transform, "Post Right", new Vector3(1.1f, 1.1f, 0f), new Vector3(0.2f, 2.2f, 0.4f), frame, keepCollider: true);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var slab = Shape(PrimitiveType.Cube, panel.transform, "Slab", new Vector3(0f, 1f, 0f), new Vector3(1.96f, 1.96f, 0.24f), style.MaterialFor(ChannelColor.Red));
            var front = Plaque(panel.transform, "Symbol Front", new Vector3(0f, 1.15f, -0.14f), Vector3.zero, 1.4f, style.SymbolFor(ChannelColor.Red), symbol);
            var back = Plaque(panel.transform, "Symbol Back", new Vector3(0f, 1.15f, 0.14f), Vector3.zero, 1.4f, style.SymbolFor(ChannelColor.Red), symbol);

            SetReference(door, "m_Panel", panel.transform);
            SetReference(door, "m_Sound", sound);
            SetupChannel(door, style, new Renderer[] { slab.GetComponent<Renderer>() },
                new[] { front.GetComponent<MeshFilter>(), back.GetComponent<MeshFilter>() });
            return Save(root, path);
        }

        public static GameObject PressureSwitch(string path, ChannelStyle style, Material frame, Material symbol, AudioClip sound)
        {
            var root = new GameObject("Pressure Switch");
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.3f, 0f);
            trigger.size = new Vector3(1.6f, 0.6f, 1.6f);
            var plateSwitch = root.AddComponent<TriggerSwitch>();

            Shape(PrimitiveType.Cube, root.transform, "Base", new Vector3(0f, 0.025f, 0f), new Vector3(1.8f, 0.05f, 1.8f), frame);
            var plate = new GameObject("Plate");
            plate.transform.SetParent(root.transform, false);
            var top = Shape(PrimitiveType.Cube, plate.transform, "Top", new Vector3(0f, 0.09f, 0f), new Vector3(1.4f, 0.08f, 1.4f), style.MaterialFor(ChannelColor.Red));
            var mark = Plaque(plate.transform, "Symbol", new Vector3(0f, 0.14f, 0f), new Vector3(90f, 0f, 0f), 1.6f, style.SymbolFor(ChannelColor.Red), symbol);

            SetReference(plateSwitch, "m_Plate", plate.transform);
            SetReference(plateSwitch, "m_Sound", sound);
            SetupChannel(plateSwitch, style, new Renderer[] { top.GetComponent<Renderer>() }, new[] { mark.GetComponent<MeshFilter>() });
            return Save(root, path);
        }

        // ---- Systems -------------------------------------------------------------------------------

        public static GameObject GameSetup(string path, AudioClip win, AudioClip lose, AudioClip blocked, AudioClip respawn)
        {
            var root = new GameObject("Game Setup");
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            var context = root.AddComponent<WorkshopGameContext>();
            SetReference(context, "m_WinSound", win);
            SetReference(context, "m_LoseSound", lose);
            SetReference(context, "m_BlockedSound", blocked);
            SetReference(context, "m_RespawnSound", respawn);
            return Save(root, path);
        }

        public static GameObject TopDownPlayer(string path, MovementSettings movement, Material body, Material visor)
        {
            var root = new GameObject("Top-Down Player");
            var controller = root.AddComponent<CharacterController>();
            controller.radius = 0.4f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;
            var motor = root.AddComponent<PlayerMotor>();
            root.AddComponent<WorkshopPlayer>();
            SetReference(motor, "m_Settings", movement);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            Shape(PrimitiveType.Capsule, visual.transform, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), body);
            Shape(PrimitiveType.Cube, visual.transform, "Visor", new Vector3(0f, 1.42f, 0.3f), new Vector3(0.5f, 0.18f, 0.25f), visor);
            SetReference(root.AddComponent<PlayerVisualAdapter>(), "m_Visual", visual.transform);
            return Save(root, path);
        }

        /// <summary>
        /// Visible character that turns toward its movement and can jump. Its model is placed by a
        /// <see cref="PlayerVisualAdapter"/>, which is where an adopted model or Animator would be connected.
        /// </summary>
        public static GameObject ThirdPersonPlayer(string path, MovementSettings movement, Material body, Material visor, string name = "Third-Person Player")
        {
            var root = new GameObject(name);
            var controller = root.AddComponent<CharacterController>();
            controller.radius = 0.4f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;
            var motor = root.AddComponent<PlayerMotor>();
            var player = root.AddComponent<WorkshopPlayer>();
            SetReference(motor, "m_Settings", movement);
            var serialized = new SerializedObject(player);
            serialized.FindProperty("m_CanJump").boolValue = true;
            serialized.FindProperty("m_Facing").enumValueIndex = (int)PlayerFacing.MovementDirection;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            Shape(PrimitiveType.Capsule, visual.transform, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), body);
            Shape(PrimitiveType.Cube, visual.transform, "Visor", new Vector3(0f, 1.42f, 0.3f), new Vector3(0.5f, 0.18f, 0.25f), visor);
            Shape(PrimitiveType.Cube, visual.transform, "Backpack", new Vector3(0f, 1.05f, -0.42f), new Vector3(0.45f, 0.5f, 0.2f), visor);

            var adapter = root.AddComponent<PlayerVisualAdapter>();
            SetReference(adapter, "m_Visual", visual.transform);
            return Save(root, path);
        }

        public static GameObject SideScrollerCamera(string path)
        {
            var root = new GameObject("Side-Scroller Camera");
            var rig = root.AddComponent<SideScrollerCameraRig>();
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WorkshopMaterialFactory.Hex("#A9D3EE");
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();
            SetReference(rig, "m_Camera", camera);
            rig.ApplyCameraPose();
            return Save(root, path);
        }

        /// <summary>A 3 × 3 m slab, 0.4 m thick, pivot at the centre of its top surface. Reachable only by jumping.</summary>
        public static GameObject FloatingPlatform(string path, Material top, Material sides)
        {
            var root = new GameObject("Floating Platform");
            Shape(PrimitiveType.Cube, root.transform, "Slab", new Vector3(0f, -0.25f, 0f), new Vector3(3f, 0.3f, 3f), sides, keepCollider: true);
            Shape(PrimitiveType.Cube, root.transform, "Top", new Vector3(0f, -0.05f, 0f), new Vector3(3f, 0.1f, 3f), top, keepCollider: true);
            Traits(root, requiresJump: true, blocksOverheadView: false);
            return Save(root, path);
        }

        public static GameObject ThirdPersonCamera(string path)
        {
            var root = new GameObject("Third-Person Camera");
            var rig = root.AddComponent<ThirdPersonCameraRig>();
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WorkshopMaterialFactory.Hex("#A9D3EE");
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();
            SetReference(rig, "m_Camera", camera);
            rig.ApplyCameraPose();
            return Save(root, path);
        }

        /// <summary>
        /// Same capsule as the top-down player, but jumping, facing the camera direction, and with its body
        /// hidden from its own camera: the body renders shadows only, so it never blocks the view.
        /// </summary>
        public static GameObject FirstPersonPlayer(string path, MovementSettings movement, Material body)
        {
            var root = new GameObject("First-Person Player");
            var controller = root.AddComponent<CharacterController>();
            controller.radius = 0.4f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;
            var motor = root.AddComponent<PlayerMotor>();
            var player = root.AddComponent<WorkshopPlayer>();
            SetReference(motor, "m_Settings", movement);
            var serialized = new SerializedObject(player);
            serialized.FindProperty("m_CanJump").boolValue = true;
            serialized.FindProperty("m_Facing").enumValueIndex = (int)PlayerFacing.CameraDirection;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var capsule = Shape(PrimitiveType.Capsule, visual.transform, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), body);
            capsule.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            SetReference(root.AddComponent<PlayerVisualAdapter>(), "m_Visual", visual.transform);
            return Save(root, path);
        }

        public static GameObject FirstPersonCamera(string path)
        {
            var root = new GameObject("First-Person Camera");
            var rig = root.AddComponent<FirstPersonCameraRig>();
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WorkshopMaterialFactory.Hex("#A9D3EE");
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();
            SetReference(rig, "m_Camera", camera);
            rig.ApplyCameraPose();
            return Save(root, path);
        }

        public static GameObject TopDownCamera(string path)
        {
            var root = new GameObject("Top-Down Camera");
            var rig = root.AddComponent<TopDownCameraRig>();
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WorkshopMaterialFactory.Hex("#A9D3EE");
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();
            SetReference(rig, "m_Camera", camera);
            rig.ApplyCameraPose();
            return Save(root, path);
        }
    }
}
