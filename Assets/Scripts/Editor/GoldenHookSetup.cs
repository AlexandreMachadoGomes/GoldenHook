using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Editor tooling for GoldenHook project setup.
///
/// If using the XRI Starter Asset prefab (recommended):
///   Run 1 → 3 → 4   (skip 2 — the prefab replaces it)
///
/// If building the XR Origin manually:
///   Run 1 → 2 → 3
/// </summary>
public static class GoldenHookSetup
{
    const string GrappleActionsPath = "Assets/Input/GrappleInputActions.inputactions";
    const string OpenXRSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";

    const int LayerPlayer      = 8;
    const int LayerChainLink   = 9;
    const int LayerGrappleHook = 10;
    const int LayerThrowable   = 11;
    const int LayerArena       = 12;

    // =========================================================================
    // 1. Physics Layers
    // =========================================================================

    [MenuItem("GoldenHook/1. Setup Physics Layers", priority = 1)]
    static void SetupPhysicsLayers()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray)
        { Debug.LogError("[GoldenHook] Could not find layers in TagManager.asset"); return; }

        SetLayer(layers, LayerPlayer,      "Player");
        SetLayer(layers, LayerChainLink,   "ChainLink");
        SetLayer(layers, LayerGrappleHook, "GrappleHook");
        SetLayer(layers, LayerThrowable,   "Throwable");
        SetLayer(layers, LayerArena,       "Arena");
        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        Physics.IgnoreLayerCollision(LayerChainLink,   LayerChainLink,   true);
        Physics.IgnoreLayerCollision(LayerChainLink,   LayerPlayer,      true);
        Physics.IgnoreLayerCollision(LayerGrappleHook, LayerPlayer,      true);
        Physics.IgnoreLayerCollision(LayerGrappleHook, LayerChainLink,   true);
        Physics.IgnoreLayerCollision(LayerThrowable,   LayerChainLink,   true);
        Physics.IgnoreLayerCollision(LayerThrowable,   LayerGrappleHook, true);

        Debug.Log("[GoldenHook] Physics layers done.");
    }

    static void SetLayer(SerializedProperty layers, int index, string name)
    {
        if (index >= layers.arraySize) { Debug.LogError($"[GoldenHook] Layer {index} out of range."); return; }
        var el = layers.GetArrayElementAtIndex(index);
        if (!string.IsNullOrEmpty(el.stringValue) && el.stringValue != name)
            Debug.LogWarning($"[GoldenHook] Layer {index} was '{el.stringValue}', overwriting with '{name}'.");
        el.stringValue = name;
    }

    // =========================================================================
    // 2. Scene Setup
    // =========================================================================

    [MenuItem("GoldenHook/2. Setup Scene (SampleScene)", priority = 2)]
    static void SetupScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.name.Contains("Sample") && !EditorUtility.DisplayDialog(
            "GoldenHook Scene Setup",
            $"Active scene is '{scene.name}', not SampleScene. Continue?",
            "Yes", "Cancel"))
            return;

        if (Object.FindFirstObjectByType<VRPrototypeRigRefs>() != null)
        { EditorUtility.DisplayDialog("GoldenHook", "Scene already set up (VRPrototypeRigRefs found).", "OK"); return; }

        var grappleAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(GrappleActionsPath);
        if (grappleAsset == null)
        { Debug.LogError($"[GoldenHook] Cannot load {GrappleActionsPath}"); return; }

        CreateXRInteractionManager();
        CreateXROrigin(grappleAsset);
        CreateArena();
        CreateMonsterPlaceholder();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GoldenHook] Scene setup complete — all references wired.");
    }

    // =========================================================================
    // 3. Enable Quest Link (OpenXR Standalone profiles)
    // =========================================================================

    [MenuItem("GoldenHook/3. Enable Quest Link Settings", priority = 3)]
    static void EnableQuestLinkSettings()
    {
        // The Oculus Touch profile covers Quest 2 / Quest Pro.
        // The Meta Quest Touch Plus profile covers Quest 3 / Quest 3S.
        // Enable both so the project works regardless of headset model.
        var profilesToEnable = new[]
        {
            "OculusTouchControllerProfile Standalone",
            "MetaQuestTouchPlusControllerProfile Standalone",
            "MetaQuestTouchProControllerProfile Standalone",
        };

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(OpenXRSettingsPath);
        if (allAssets == null || allAssets.Length == 0)
        { Debug.LogError($"[GoldenHook] Cannot load {OpenXRSettingsPath}"); return; }

        int enabled = 0;
        foreach (var asset in allAssets)
        {
            if (asset == null) continue;
            var so       = new SerializedObject(asset);
            var nameProp = so.FindProperty("m_Name");
            if (nameProp == null) continue;

            string featureName = nameProp.stringValue;
            foreach (var target in profilesToEnable)
            {
                if (featureName != target) continue;
                var enabledProp = so.FindProperty("m_enabled");
                if (enabledProp == null) continue;
                if (!enabledProp.boolValue)
                {
                    enabledProp.boolValue = true;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[GoldenHook] Enabled: {featureName}");
                    enabled++;
                }
                else
                {
                    Debug.Log($"[GoldenHook] Already enabled: {featureName}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = enabled > 0
            ? $"Enabled {enabled} OpenXR profile(s) for Quest Link.\n\nQuest Link checklist:\n• Open the Meta Quest Link app on this PC\n• Put on the headset and approve the Link connection\n• Press Play in Unity"
            : "All Quest Link profiles were already enabled.";

        EditorUtility.DisplayDialog("GoldenHook — Quest Link Settings", msg, "OK");
    }

    // =========================================================================
    // XR Interaction Manager
    // =========================================================================

    static void CreateXRInteractionManager()
    {
        var go = new GameObject("XR Interaction Manager");
        go.AddComponent<XRInteractionManager>();
        Undo.RegisterCreatedObjectUndo(go, "Create XR Interaction Manager");
    }

    // =========================================================================
    // XR Origin — fully wired
    // =========================================================================

    static void CreateXROrigin(InputActionAsset grappleAsset)
    {
        // Root
        var originGO = new GameObject("XR Origin (XR Rig)");
        var xrOrigin = originGO.AddComponent<XROrigin>();
        Undo.RegisterCreatedObjectUndo(originGO, "Create XR Origin");

        // Camera Offset
        var offsetGO = new GameObject("Camera Offset");
        offsetGO.transform.SetParent(originGO.transform, false);
        xrOrigin.CameraFloorOffsetObject = offsetGO;

        // Main Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(offsetGO.transform, false);
        var cam = camGO.AddComponent<Camera>();
        cam.nearClipPlane = 0.01f;
        camGO.AddComponent<TrackedPoseDriver>();
        xrOrigin.Camera = cam;

        // Controllers
        var (leftCtrlGO,  leftCtrl,  leftPalm)  = CreateController(offsetGO.transform, "Left Controller");
        var (rightCtrlGO, rightCtrl, rightPalm) = CreateController(offsetGO.transform, "Right Controller");

        // Hand Anchor Providers (kinematic bodies for chain joints)
        var leftAnchor  = CreateHandAnchor(leftCtrlGO.transform,  "Left Hand Anchor");
        var rightAnchor = CreateHandAnchor(rightCtrlGO.transform, "Right Hand Anchor");

        // Grapple Input (shared)
        var inputGO = new GameObject("Grapple Input");
        inputGO.transform.SetParent(originGO.transform, false);
        var grappleInput = inputGO.AddComponent<XRHandGrappleInput>();
        WireGrappleInputActions(grappleInput, grappleAsset);

        // Grapple Hand Controllers
        var leftHandCtrl  = CreateHandController(originGO.transform, "Left Grapple Hand Controller",  Hand.Left,  grappleInput);
        var rightHandCtrl = CreateHandController(originGO.transform, "Right Grapple Hand Controller", Hand.Right, grappleInput);

        // VRPrototypeRigRefs (public fields — assign directly)
        var rigRefs = originGO.AddComponent<VRPrototypeRigRefs>();
        rigRefs.XROrigin           = xrOrigin;
        rigRefs.LeftControllerGO   = leftCtrlGO;
        rigRefs.RightControllerGO  = rightCtrlGO;
        rigRefs.LeftHandTransform  = leftPalm;
        rigRefs.RightHandTransform = rightPalm;

        // Wire HandAnchorProviders
        WireHandAnchor(leftAnchor,  Hand.Left,  rigRefs);
        WireHandAnchor(rightAnchor, Hand.Right, rigRefs);

        // Wire ActionBasedController tracking actions
        WireControllerTracking(leftCtrl,  grappleAsset, isLeft: true);
        WireControllerTracking(rightCtrl, grappleAsset, isLeft: false);
    }

    static (GameObject go, ActionBasedController ctrl, Transform palm)
        CreateController(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ctrl = go.AddComponent<ActionBasedController>();
        var palm = new GameObject("Palm Anchor");
        palm.transform.SetParent(go.transform, false);
        return (go, ctrl, palm.transform);
    }

    static HandAnchorProvider CreateHandAnchor(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<HandAnchorProvider>();
    }

    static GrappleHandController CreateHandController(
        Transform parent, string name, Hand hand, XRHandGrappleInput input)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ctrl = go.AddComponent<GrappleHandController>();
        WireHandController(ctrl, hand, input);
        return ctrl;
    }

    // =========================================================================
    // Reference Wiring
    // =========================================================================

    static void WireGrappleInputActions(XRHandGrappleInput component, InputActionAsset asset)
    {
        var map = new (string field, string mapName, string action)[]
        {
            ("leftFireAction",     "Grapple", "LeftGrappleFire"),
            ("rightFireAction",    "Grapple", "RightGrappleFire"),
            ("leftRetractAction",  "Grapple", "LeftRetract"),
            ("rightRetractAction", "Grapple", "RightRetract"),
        };

        var so = new SerializedObject(component);
        foreach (var (field, mapName, actionName) in map)
        {
            var actionRef = GetOrCreateActionRef(asset, mapName, actionName);
            if (actionRef == null) { Debug.LogError($"[GoldenHook] Missing action: {mapName}/{actionName}"); continue; }
            so.FindProperty(field).objectReferenceValue = actionRef;
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Wires position, rotation, isTracked, and trackingState onto an ActionBasedController.
    /// These are the minimum needed for Quest Link to drive controller transforms.
    /// </summary>
    static void WireControllerTracking(ActionBasedController ctrl, InputActionAsset asset, bool isLeft)
    {
        string prefix = isLeft ? "Left" : "Right";
        var map = new (string ctrlField, string actionName)[]
        {
            ("m_PositionAction",     $"{prefix}Position"),
            ("m_RotationAction",     $"{prefix}Rotation"),
            ("m_IsTrackedAction",    $"{prefix}IsTracked"),
            ("m_TrackingStateAction",$"{prefix}TrackingState"),
        };

        var so = new SerializedObject(ctrl);
        foreach (var (ctrlField, actionName) in map)
        {
            var actionRef = GetOrCreateActionRef(asset, "XRTracking", actionName);
            if (actionRef == null) { Debug.LogError($"[GoldenHook] Missing tracking action: XRTracking/{actionName}"); continue; }

            var prop = so.FindProperty(ctrlField);
            if (prop == null) { Debug.LogWarning($"[GoldenHook] Field not found on ActionBasedController: {ctrlField}"); continue; }

            prop.FindPropertyRelative("m_UseReference").boolValue = true;
            prop.FindPropertyRelative("m_Reference").objectReferenceValue = actionRef;
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    static void WireHandController(GrappleHandController ctrl, Hand hand, XRHandGrappleInput input)
    {
        var so = new SerializedObject(ctrl);
        so.FindProperty("hand").enumValueIndex           = (int)hand;
        so.FindProperty("input").objectReferenceValue    = input;
        so.ApplyModifiedProperties();
    }

    static void WireHandAnchor(HandAnchorProvider anchor, Hand hand, VRPrototypeRigRefs rigRefs)
    {
        var so = new SerializedObject(anchor);
        so.FindProperty("rigRefs").objectReferenceValue = rigRefs;
        so.FindProperty("hand").enumValueIndex          = (int)hand;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Gets or creates an InputActionReference sub-asset embedded inside the .inputactions file.
    /// Idempotent — won't create duplicates.
    /// </summary>
    static InputActionReference GetOrCreateActionRef(InputActionAsset asset, string mapName, string actionName)
    {
        var action = asset.FindActionMap(mapName)?.FindAction(actionName);
        if (action == null) return null;

        string path = AssetDatabase.GetAssetPath(asset);
        foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            if (sub is InputActionReference r && r.action?.id == action.id) return r;

        var reference = InputActionReference.Create(action);
        AssetDatabase.AddObjectToAsset(reference, asset);
        return reference;
    }

    // =========================================================================
    // Arena
    // =========================================================================

    static void CreateArena()
    {
        var root  = new GameObject("Arena");
        Undo.RegisterCreatedObjectUndo(root, "Create Arena");
        int layer = LayerMask.NameToLayer("Arena");
        if (layer < 0) { Debug.LogWarning("[GoldenHook] Arena layer missing — run Setup Physics Layers first."); layer = 0; }

        SpawnPanel(root.transform, "Floor",      Vector3.zero,              new Vector3(20, 0.2f, 20), layer);
        SpawnPanel(root.transform, "Wall_North", new Vector3(0,  5,  10),   new Vector3(20, 10, 0.2f), layer);
        SpawnPanel(root.transform, "Wall_South", new Vector3(0,  5, -10),   new Vector3(20, 10, 0.2f), layer);
        SpawnPanel(root.transform, "Wall_East",  new Vector3( 10, 5,  0),   new Vector3(0.2f, 10, 20), layer);
        SpawnPanel(root.transform, "Wall_West",  new Vector3(-10, 5,  0),   new Vector3(0.2f, 10, 20), layer);
    }

    static void SpawnPanel(Transform parent, string name, Vector3 pos, Vector3 scale, int layer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        go.layer = layer;
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        go.AddComponent<BoxCollider>();
    }

    // =========================================================================
    // Monster
    // =========================================================================

    static void CreateMonsterPlaceholder()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Monster (Placeholder)";
        go.transform.position = new Vector3(0, 1, 8);
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.5f; col.height = 2f;
        var spawn = new GameObject("ThrowSpawnPoint");
        spawn.transform.SetParent(go.transform, false);
        spawn.transform.localPosition = new Vector3(0, 0.5f, -1f);
        Undo.RegisterCreatedObjectUndo(go, "Create Monster Placeholder");
    }

    // =========================================================================
    // 4. Wire Grapple onto XRI Starter Prefab
    //    Run this after: dropping the XRI "XR Origin (XR Rig)" prefab into the scene.
    //    The prefab already has tracking wired — this only adds grapple components.
    // =========================================================================

    [MenuItem("GoldenHook/4. Wire Grapple onto XRI Prefab", priority = 4)]
    static void WireGrappleOntoXRIPrefab()
    {
        // --- Find XR Origin ---
        var xrOrigin = Object.FindFirstObjectByType<XROrigin>();
        if (xrOrigin == null)
        {
            EditorUtility.DisplayDialog("GoldenHook",
                "No XROrigin found in scene. Add the XRI 'XR Origin (XR Rig)' prefab first.", "OK");
            return;
        }

        // --- Find left/right controller GameObjects by name within XR Origin hierarchy ---
        // XRI 3.3.1 starter prefab uses XRControllerActionBasedMapping + TrackedPoseDriver,
        // NOT ActionBasedController. Search by name substring instead of component type.
        GameObject leftCtrlGO  = FindChildGOByName(xrOrigin.transform, "left");
        GameObject rightCtrlGO = FindChildGOByName(xrOrigin.transform, "right");

        if (leftCtrlGO == null || rightCtrlGO == null)
        {
            // List what was found to help diagnose
            var names = new System.Text.StringBuilder();
            CollectChildNames(xrOrigin.transform, names, 0);
            EditorUtility.DisplayDialog("GoldenHook",
                $"Could not find 'Left' and 'Right' controller GameObjects under '{xrOrigin.gameObject.name}'.\n\n" +
                $"Children found:\n{names}", "OK");
            return;
        }

        var grappleAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(GrappleActionsPath);
        if (grappleAsset == null)
        { Debug.LogError($"[GoldenHook] Cannot load {GrappleActionsPath}"); return; }

        // --- Hand transforms ---
        // XRI starter has an "Attach Point" child on each controller — ideal for chain attach.
        // Fall back to controller root transform if not found.
        Transform leftPalm  = FindChildByName(leftCtrlGO.transform,  "Attach Point") ?? leftCtrlGO.transform;
        Transform rightPalm = FindChildByName(rightCtrlGO.transform, "Attach Point") ?? rightCtrlGO.transform;

        // --- VRPrototypeRigRefs on the XR Origin root ---
        var rigRefs = xrOrigin.GetComponent<VRPrototypeRigRefs>()
                   ?? xrOrigin.gameObject.AddComponent<VRPrototypeRigRefs>();
        rigRefs.XROrigin           = xrOrigin;
        rigRefs.LeftControllerGO   = leftCtrlGO;
        rigRefs.RightControllerGO  = rightCtrlGO;
        rigRefs.LeftHandTransform  = leftPalm;
        rigRefs.RightHandTransform = rightPalm;
        EditorUtility.SetDirty(rigRefs);

        // --- XRHandGrappleInput on the XR Origin root ---
        var grappleInput = xrOrigin.GetComponent<XRHandGrappleInput>()
                        ?? xrOrigin.gameObject.AddComponent<XRHandGrappleInput>();
        WireGrappleInputActions(grappleInput, grappleAsset);

        // --- Per-hand: HandAnchorProvider + GrappleHandController ---
        AddHandComponents(leftCtrlGO,  Hand.Left,  grappleInput, rigRefs);
        AddHandComponents(rightCtrlGO, Hand.Right, grappleInput, rigRefs);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log($"[GoldenHook] Wired onto '{xrOrigin.gameObject.name}'. " +
                  $"Left: '{leftCtrlGO.name}' (palm: '{leftPalm.name}'), " +
                  $"Right: '{rightCtrlGO.name}' (palm: '{rightPalm.name}').");

        Selection.activeGameObject = xrOrigin.gameObject;
    }

    /// <summary>Adds HandAnchorProvider + GrappleHandController to one controller's subtree.</summary>
    static void AddHandComponents(
        GameObject ctrlGO, Hand hand,
        XRHandGrappleInput grappleInput, VRPrototypeRigRefs rigRefs)
    {
        // HandAnchorProvider lives on a dedicated child so its Rigidbody is isolated.
        string anchorName = hand == Hand.Left ? "Left Hand Anchor" : "Right Hand Anchor";
        Transform existing = ctrlGO.transform.Find(anchorName);
        GameObject anchorGO = existing != null ? existing.gameObject : new GameObject(anchorName);
        if (existing == null)
            anchorGO.transform.SetParent(ctrlGO.transform, false);

        var anchor = anchorGO.GetComponent<HandAnchorProvider>()
                  ?? anchorGO.AddComponent<HandAnchorProvider>();
        WireHandAnchor(anchor, hand, rigRefs);

        // GrappleHandController on the controller root.
        var handCtrl = ctrlGO.GetComponent<GrappleHandController>()
                    ?? ctrlGO.AddComponent<GrappleHandController>();
        WireHandController(handCtrl, hand, grappleInput);
    }

    /// <summary>Depth-first search for a child Transform whose name exactly matches.</summary>
    static Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChildByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Depth-first search for a child GameObject whose name contains the substring
    /// (case-insensitive). Returns the first match.
    /// </summary>
    static GameObject FindChildGOByName(Transform parent, string substring)
    {
        foreach (Transform child in parent)
        {
            if (child.name.ToLower().Contains(substring)) return child.gameObject;
            var found = FindChildGOByName(child, substring);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Appends child names (up to depth 3) for diagnostic dialogs.</summary>
    static void CollectChildNames(Transform t, System.Text.StringBuilder sb, int depth)
    {
        if (depth > 3) return;
        foreach (Transform child in t)
        {
            sb.AppendLine(new string(' ', depth * 2) + child.name);
            CollectChildNames(child, sb, depth + 1);
        }
    }

    // =========================================================================
    // 5. Batch 2 — Hook Prefab, Test Throwable, GrappleLauncher wiring
    //    Run after menu item 4.
    // =========================================================================

    [MenuItem("GoldenHook/5. Setup Batch 2 (Hook + Launcher)", priority = 5)]
    static void SetupBatch2()
    {
        var grappleAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(GrappleActionsPath);

        // ---- Hook prefab ----
        const string prefabFolder = "Assets/Prefabs";
        const string prefabPath   = prefabFolder + "/GrappleHook.prefab";

        if (!AssetDatabase.IsValidFolder(prefabFolder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject hookPrefab;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.Log("[GoldenHook] GrappleHook.prefab already exists — skipping creation.");
            hookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
        else
        {
            hookPrefab = CreateHookPrefab(prefabPath);
        }

        // ---- Test throwable sphere ----
        if (GameObject.Find("Test Throwable") == null)
            CreateTestThrowable();
        else
            Debug.Log("[GoldenHook] Test Throwable already in scene.");

        // ---- GrappleLauncher on each controller ----
        var rigRefs = Object.FindFirstObjectByType<VRPrototypeRigRefs>();
        if (rigRefs == null)
        {
            Debug.LogError("[GoldenHook] VRPrototypeRigRefs not found — run menu item 4 first.");
            return;
        }

        WireLauncher(rigRefs.LeftControllerGO,  Hand.Left,  hookPrefab, rigRefs);
        WireLauncher(rigRefs.RightControllerGO, Hand.Right, hookPrefab, rigRefs);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[GoldenHook] Batch 2 setup complete. Press Play, fire trigger to launch hook.");
    }

    static GameObject CreateHookPrefab(string path)
    {
        // Build the hook GO in the scene temporarily, then save as prefab.
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "GrappleHook";

        // Small visual — a real model replaces this later.
        go.transform.localScale = Vector3.one * 0.06f;

        // Replace the default MeshCollider with a small SphereCollider.
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        var col    = go.AddComponent<SphereCollider>();
        col.radius = 0.5f; // local space (scaled by 0.06 = 0.03m world radius)

        // Rigidbody for flight physics.
        var rb                      = go.AddComponent<Rigidbody>();
        rb.mass                     = 0.1f;
        rb.linearDamping            = 0f;
        rb.angularDamping           = 0f;
        rb.useGravity               = false; // straight-line flight; enable for arcing feel
        rb.collisionDetectionMode   = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation            = RigidbodyInterpolation.Interpolate;

        go.AddComponent<GrappleHookProjectile>();

        int hookLayer = LayerMask.NameToLayer("GrappleHook");
        if (hookLayer >= 0) go.layer = hookLayer;

        // Save as prefab asset.
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go); // remove temp scene object
        AssetDatabase.SaveAssets();

        Debug.Log($"[GoldenHook] Created {path}");
        return prefab;
    }

    static void CreateTestThrowable()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Test Throwable";
        go.transform.position = new Vector3(0f, 1.5f, 5f);

        Object.DestroyImmediate(go.GetComponent<SphereCollider>());
        var col    = go.AddComponent<SphereCollider>();
        col.radius = 0.5f;

        var rb              = go.AddComponent<Rigidbody>();
        rb.mass             = 0.5f;
        rb.linearDamping    = 0.5f;
        rb.angularDamping   = 0.5f;

        go.AddComponent<GrappleTarget>();

        int throwableLayer = LayerMask.NameToLayer("Throwable");
        if (throwableLayer >= 0) go.layer = throwableLayer;

        Undo.RegisterCreatedObjectUndo(go, "Create Test Throwable");
        Debug.Log("[GoldenHook] Test Throwable spawned at (0, 1.5, 5).");
    }

    static void WireLauncher(
        GameObject ctrlGO, Hand hand,
        GameObject hookPrefab, VRPrototypeRigRefs rigRefs)
    {
        if (ctrlGO == null) { Debug.LogError($"[GoldenHook] Controller GO null for {hand}"); return; }

        var launcher = ctrlGO.GetComponent<GrappleLauncher>()
                    ?? ctrlGO.AddComponent<GrappleLauncher>();

        var handCtrl   = ctrlGO.GetComponent<GrappleHandController>();
        var anchorGO   = ctrlGO.transform.Find(
            hand == Hand.Left ? "Left Hand Anchor" : "Right Hand Anchor");
        var anchor     = anchorGO != null
            ? anchorGO.GetComponent<HandAnchorProvider>()
            : null;

        var so = new SerializedObject(launcher);
        so.FindProperty("handController").objectReferenceValue = handCtrl;
        so.FindProperty("anchorProvider").objectReferenceValue = anchor;
        so.FindProperty("rigRefs").objectReferenceValue        = rigRefs;
        so.FindProperty("hookPrefab").objectReferenceValue     = hookPrefab;
        so.ApplyModifiedProperties();
    }

    // =========================================================================
    // 6. Batch 3 — GrappleChainConfig asset + wire into GrappleLaunchers
    //    Run after menu item 5.
    // =========================================================================

    [MenuItem("GoldenHook/6. Setup Batch 3 (Chain Config)", priority = 6)]
    static void SetupBatch3()
    {
        const string configPath = "Assets/Configs/GrappleChainConfig.asset";

        // Create the ScriptableObject asset if it doesn't exist.
        GrappleChainConfig config;
        config = AssetDatabase.LoadAssetAtPath<GrappleChainConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GrappleChainConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GoldenHook] Created {configPath} with defaults.");
        }
        else
        {
            Debug.Log("[GoldenHook] GrappleChainConfig already exists — skipping creation.");
        }

        // Wire chainConfig onto both GrappleLaunchers.
        var launchers = Object.FindObjectsByType<GrappleLauncher>(FindObjectsSortMode.None);
        if (launchers.Length == 0)
        {
            Debug.LogWarning("[GoldenHook] No GrappleLaunchers found — run menu item 5 first.");
            return;
        }

        foreach (var launcher in launchers)
        {
            var so = new SerializedObject(launcher);
            so.FindProperty("chainConfig").objectReferenceValue = config;
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log($"[GoldenHook] Batch 3 done. chainConfig wired on {launchers.Length} launcher(s). " +
                  "Tune values in Assets/Configs/GrappleChainConfig.asset.");

        // Select the config asset so the user can tune it immediately.
        Selection.activeObject = config;
    }

    // =========================================================================
    // 7. Batch 4 — Retraction, Tension, Detach, Release velocity
    //    Run after menu item 6.
    // =========================================================================

    [MenuItem("GoldenHook/7. Setup Batch 4 (Retraction + Release)", priority = 7)]
    static void SetupBatch4()
    {
        var rigRefs = Object.FindFirstObjectByType<VRPrototypeRigRefs>();
        if (rigRefs == null)
        { Debug.LogError("[GoldenHook] VRPrototypeRigRefs not found — run menu item 4 first."); return; }

        AddBatch4ToController(rigRefs.LeftControllerGO,  Hand.Left);
        AddBatch4ToController(rigRefs.RightControllerGO, Hand.Right);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[GoldenHook] Batch 4 done. Grip = retract chain. Release trigger to throw.");
    }

    static void AddBatch4ToController(GameObject ctrlGO, Hand hand)
    {
        if (ctrlGO == null) return;

        var handCtrl     = ctrlGO.GetComponent<GrappleHandController>();
        var launcher     = ctrlGO.GetComponent<GrappleLauncher>();
        var tensionAnalyzer = ctrlGO.GetComponent<GrappleTensionAnalyzer>()
                          ?? ctrlGO.AddComponent<GrappleTensionAnalyzer>();
        var lengthCtrl   = ctrlGO.GetComponent<GrappleLengthController>()
                        ?? ctrlGO.AddComponent<GrappleLengthController>();
        var retractMotor = ctrlGO.GetComponent<GrappleRetractionMotor>()
                        ?? ctrlGO.AddComponent<GrappleRetractionMotor>();
        var detachCtrl   = ctrlGO.GetComponent<GrappleDetachController>()
                        ?? ctrlGO.AddComponent<GrappleDetachController>();
        var releaseHelper = ctrlGO.GetComponent<GrappleReleaseVelocityHelper>()
                         ?? ctrlGO.AddComponent<GrappleReleaseVelocityHelper>();

        // TensionAnalyzer
        {
            var so = new SerializedObject(tensionAnalyzer);
            so.FindProperty("launcher").objectReferenceValue = launcher;
            so.ApplyModifiedProperties();
        }

        // RetractionMotor
        {
            var so = new SerializedObject(retractMotor);
            so.FindProperty("handController").objectReferenceValue  = handCtrl;
            so.FindProperty("launcher").objectReferenceValue        = launcher;
            so.FindProperty("lengthController").objectReferenceValue = lengthCtrl;
            so.ApplyModifiedProperties();
        }

        // DetachController
        {
            var so = new SerializedObject(detachCtrl);
            so.FindProperty("handController").objectReferenceValue   = handCtrl;
            so.FindProperty("tensionAnalyzer").objectReferenceValue  = tensionAnalyzer;
            so.ApplyModifiedProperties();
        }

        // Wire releaseVelocityHelper into GrappleLauncher
        if (launcher != null)
        {
            var so = new SerializedObject(launcher);
            so.FindProperty("releaseVelocityHelper").objectReferenceValue = releaseHelper;
            so.ApplyModifiedProperties();
        }
    }

    // =========================================================================
    // 8. Batch 5 — Combat loop
    //    Creates throwable prefab, pool, wires MonsterThrower/MonsterHitReceiver,
    //    adds ArenaRecycleZone trigger, upgrades Test Throwable.
    //    Run after menu item 7.
    // =========================================================================

    [MenuItem("GoldenHook/8. Setup Batch 5 (Combat Loop)", priority = 8)]
    static void SetupBatch5()
    {
        // ---- Throwable prefab ----
        const string prefabFolder    = "Assets/Prefabs";
        const string throwablePath   = prefabFolder + "/Throwable.prefab";

        if (!AssetDatabase.IsValidFolder(prefabFolder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject throwablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(throwablePath);
        if (throwablePrefab == null)
            throwablePrefab = CreateThrowablePrefab(throwablePath);
        else
            Debug.Log("[GoldenHook] Throwable.prefab already exists — skipping creation.");

        // ---- ThrowableObjectPool in scene ----
        var pool = Object.FindFirstObjectByType<ThrowableObjectPool>();
        if (pool == null)
        {
            var poolGO = new GameObject("Throwable Object Pool");
            pool = poolGO.AddComponent<ThrowableObjectPool>();
            Undo.RegisterCreatedObjectUndo(poolGO, "Create Throwable Object Pool");

            var so = new SerializedObject(pool);
            so.FindProperty("throwablePrefab").objectReferenceValue = throwablePrefab;
            // preloadCount defaults to 5 — leave as-is
            so.ApplyModifiedProperties();
            Debug.Log("[GoldenHook] Created Throwable Object Pool.");
        }
        else
        {
            Debug.Log("[GoldenHook] ThrowableObjectPool already in scene — skipping creation.");
        }

        // ---- Monster Placeholder — add MonsterThrower + MonsterHitReceiver ----
        var monsterGO = GameObject.Find("Monster (Placeholder)");
        if (monsterGO == null)
        {
            Debug.LogError("[GoldenHook] 'Monster (Placeholder)' not found. Run menu item 2 or add it manually.");
        }
        else
        {
            WireMonster(monsterGO, pool);
        }

        // ---- ArenaRecycleZone — large trigger box below the floor ----
        if (GameObject.Find("Arena Recycle Zone") == null)
            CreateArenaRecycleZone();
        else
            Debug.Log("[GoldenHook] Arena Recycle Zone already in scene.");

        // ---- Upgrade Test Throwable (from Batch 2) with combat components ----
        var testThrowable = GameObject.Find("Test Throwable");
        if (testThrowable != null)
            UpgradeTestThrowable(testThrowable);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[GoldenHook] Batch 5 done. Press Play: monster throws every ~3 s; hook → retract → release → hit monster.");
    }

    static GameObject CreateThrowablePrefab(string path)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Throwable";
        go.transform.localScale = Vector3.one * 0.25f;

        // Replace default MeshCollider with proper SphereCollider.
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        var col    = go.AddComponent<SphereCollider>();
        col.radius = 0.5f;

        var rb                    = go.AddComponent<Rigidbody>();
        rb.mass                   = 0.5f;
        rb.linearDamping          = 0.5f;
        rb.angularDamping         = 0.5f;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        go.AddComponent<GrappleTarget>();           // player can hook these
        go.AddComponent<ThrowablePhysicsObject>();
        go.AddComponent<ThrowableObjectStateTracker>();

        int throwableLayer = LayerMask.NameToLayer("Throwable");
        if (throwableLayer >= 0) go.layer = throwableLayer;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();

        Debug.Log($"[GoldenHook] Created {path}");
        return prefab;
    }

    static void WireMonster(GameObject monsterGO, ThrowableObjectPool pool)
    {
        // XROrigin for player target.
        var xrOrigin = Object.FindFirstObjectByType<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogError("[GoldenHook] XROrigin not found — MonsterThrower playerTarget left unwired.");
        }

        // ThrowSpawnPoint child.
        Transform spawnPoint = null;
        foreach (Transform child in monsterGO.transform)
        {
            if (child.name.Contains("Spawn"))
            { spawnPoint = child; break; }
        }
        if (spawnPoint == null)
        {
            // Create one if missing (scene was set up manually without item 2).
            var spawnGO = new GameObject("ThrowSpawnPoint");
            spawnGO.transform.SetParent(monsterGO.transform, false);
            spawnGO.transform.localPosition = new Vector3(0, 0.5f, -1f);
            spawnPoint = spawnGO.transform;
            Debug.Log("[GoldenHook] Created missing ThrowSpawnPoint on Monster.");
        }

        var thrower = monsterGO.GetComponent<MonsterThrower>()
                   ?? monsterGO.AddComponent<MonsterThrower>();
        {
            var so = new SerializedObject(thrower);
            so.FindProperty("pool").objectReferenceValue            = pool;
            so.FindProperty("throwSpawnPoint").objectReferenceValue = spawnPoint;
            if (xrOrigin != null)
                so.FindProperty("playerTarget").objectReferenceValue = xrOrigin.transform;
            so.ApplyModifiedProperties();
        }

        // MonsterHitReceiver — no external refs, default hitsToKill=5 is fine.
        if (monsterGO.GetComponent<MonsterHitReceiver>() == null)
            monsterGO.AddComponent<MonsterHitReceiver>();

        Debug.Log("[GoldenHook] Monster wired: MonsterThrower + MonsterHitReceiver.");
    }

    static void CreateArenaRecycleZone()
    {
        var go = new GameObject("Arena Recycle Zone");
        // Position well below the arena floor (floor is at y=0, panel height 0.2).
        go.transform.position = new Vector3(0, -10f, 0);

        var col         = go.AddComponent<BoxCollider>();
        col.isTrigger   = true;
        col.size        = new Vector3(60f, 2f, 60f); // wide enough to catch anything

        go.AddComponent<ArenaRecycleZone>();
        Undo.RegisterCreatedObjectUndo(go, "Create Arena Recycle Zone");
        Debug.Log("[GoldenHook] Arena Recycle Zone created at y=-10.");
    }

    static void UpgradeTestThrowable(GameObject go)
    {
        bool changed = false;

        if (go.GetComponent<ThrowablePhysicsObject>() == null)
        { go.AddComponent<ThrowablePhysicsObject>(); changed = true; }

        if (go.GetComponent<ThrowableObjectStateTracker>() == null)
        { go.AddComponent<ThrowableObjectStateTracker>(); changed = true; }

        if (changed)
            Debug.Log("[GoldenHook] Test Throwable upgraded with ThrowablePhysicsObject + ThrowableObjectStateTracker.");
        else
            Debug.Log("[GoldenHook] Test Throwable already has combat components.");
    }

    // =========================================================================
    // 9. Batch 6 — Debug, telemetry, haptics
    //    Adds GrappleDebugGizmos, GrappleTelemetry, GrapplePhysicsProfile,
    //    ImpactFeedback to a shared debug host; GrappleHapticsDriver + ChainStressTester
    //    to each controller.
    //    Run after menu item 8.
    // =========================================================================

    [MenuItem("GoldenHook/9. Setup Batch 6 (Debug + Haptics)", priority = 9)]
    static void SetupBatch6()
    {
        var rigRefs = Object.FindFirstObjectByType<VRPrototypeRigRefs>();
        if (rigRefs == null)
        { Debug.LogError("[GoldenHook] VRPrototypeRigRefs not found — run menu item 4 first."); return; }

        // ---- Shared debug host ----
        var debugHost = GameObject.Find("GoldenHook Debug");
        if (debugHost == null)
        {
            debugHost = new GameObject("GoldenHook Debug");
            Undo.RegisterCreatedObjectUndo(debugHost, "Create GoldenHook Debug host");
        }

        // GrappleDebugGizmos
        if (debugHost.GetComponent<GrappleDebugGizmos>() == null)
            debugHost.AddComponent<GrappleDebugGizmos>();

        // GrappleTelemetry — no wiring needed, subscribes to static events
        if (debugHost.GetComponent<GrappleTelemetry>() == null)
            debugHost.AddComponent<GrappleTelemetry>();

        // ImpactFeedback — needs AudioSource (auto-added by RequireComponent)
        if (debugHost.GetComponent<ImpactFeedback>() == null)
            debugHost.AddComponent<ImpactFeedback>();

        // GrapplePhysicsProfile — wire to left controller's launcher + tension analyzer
        var profile = debugHost.GetComponent<GrapplePhysicsProfile>()
                   ?? debugHost.AddComponent<GrapplePhysicsProfile>();

        var leftLauncher  = rigRefs.LeftControllerGO?.GetComponent<GrappleLauncher>();
        var leftTension   = rigRefs.LeftControllerGO?.GetComponent<GrappleTensionAnalyzer>();
        {
            var so = new SerializedObject(profile);
            so.FindProperty("launcher").objectReferenceValue        = leftLauncher;
            so.FindProperty("tensionAnalyzer").objectReferenceValue = leftTension;
            so.ApplyModifiedProperties();
        }

        // ---- Per-hand: GrappleHapticsDriver + ChainStressTester ----
        AddBatch6ToController(rigRefs.LeftControllerGO,  Hand.Left);
        AddBatch6ToController(rigRefs.RightControllerGO, Hand.Right);

        // Wire ChainStressTester on left controller to its own launcher + hand controller + test throwable
        var leftCtrl = rigRefs.LeftControllerGO;
        if (leftCtrl != null)
        {
            var stressTester = leftCtrl.GetComponent<ChainStressTester>();
            if (stressTester != null)
            {
                var testThrowableGO = GameObject.Find("Test Throwable");
                var so = new SerializedObject(stressTester);
                so.FindProperty("launcher").objectReferenceValue       = leftCtrl.GetComponent<GrappleLauncher>();
                so.FindProperty("handController").objectReferenceValue = leftCtrl.GetComponent<GrappleHandController>();
                if (testThrowableGO != null)
                    so.FindProperty("testTarget").objectReferenceValue = testThrowableGO.transform;
                so.ApplyModifiedProperties();
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[GoldenHook] Batch 6 done. " +
                  "Gizmos: Scene view chain visualization active in play mode. " +
                  "Telemetry: watch Console for event log. " +
                  "Haptics: firing on Quest. " +
                  "StressTester: right-click ChainStressTester → Run Stress Test.");
    }

    static void AddBatch6ToController(GameObject ctrlGO, Hand hand)
    {
        if (ctrlGO == null) return;

        // GrappleHapticsDriver
        var haptics = ctrlGO.GetComponent<GrappleHapticsDriver>()
                   ?? ctrlGO.AddComponent<GrappleHapticsDriver>();
        {
            var so = new SerializedObject(haptics);
            so.FindProperty("hand").enumValueIndex = (int)hand;
            so.ApplyModifiedProperties();
        }

        // ChainStressTester — only really needed on one hand, but add to both for flexibility
        if (ctrlGO.GetComponent<ChainStressTester>() == null)
            ctrlGO.AddComponent<ChainStressTester>();
    }
}
