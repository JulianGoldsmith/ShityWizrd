using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;

using BoneSetup = NPCRagdollSetup.BoneSetup;
using ColliderShape = NPCRagdollSetup.ColliderShape;
using CapsuleAxis = NPCRagdollSetup.CapsuleAxis;


public class CreateRagdollNPCWindow : EditorWindow
{

    private enum SetupPage { Selection, Colliders, Joints }

    [SerializeField] private SetupPage currentPage;
    private Vector2 colliderScrollPosition;
    private Vector2 jointScrollPosition;


    [SerializeField] private GameObject npcRoot;
    [SerializeField] private Transform armatureRoot;
    [SerializeField] private Transform coreBone;

    [SerializeField] private List<BoneSetup> selectedBones = new List<BoneSetup>();
    [SerializeField] private List<Transform> proxiesToRemove = new List<Transform>();
    private Vector2 boneScrollPosition;


    [MenuItem("GameObject/Create Ragdoll NPC", false, 49)]
    [MenuItem("Assets/Create Ragdoll NPC", false, 2000)]
    private static void OpenWindow()
    {
        CreateRagdollNPCWindow window = GetWindow<CreateRagdollNPCWindow>("Create Ragdoll NPC");
        window.minSize = new Vector2(420f, 280f);
        window.npcRoot = Selection.activeGameObject;
        window.LoadSetup();
        window.Show();
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        using (new EditorGUI.DisabledScope(npcRoot == null || EditorUtility.IsPersistent(npcRoot)))
        {
            if (GUILayout.Button("Save Setup"))
            {
                SaveSetup();
            }
        }

        if (currentPage == SetupPage.Colliders)
        {
            DrawColliderPage();
            return;
        }
        if (currentPage == SetupPage.Joints)
        {
            DrawJointPage();
            return;
        }

        EditorGUILayout.LabelField("NPC Skeleton Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        GameObject selectedRoot = (GameObject)EditorGUILayout.ObjectField("NPC Root", npcRoot, typeof(GameObject), true);

        if (EditorGUI.EndChangeCheck())
        {
            npcRoot = selectedRoot;
            LoadSetup();
            SceneView.RepaintAll();
        }

        EditorGUI.BeginChangeCheck();

        armatureRoot = (Transform)EditorGUILayout.ObjectField("Armature Root", armatureRoot, typeof(Transform), true);
        coreBone = (Transform)EditorGUILayout.ObjectField("Core Bone", coreBone, typeof(Transform), true);

        if (EditorGUI.EndChangeCheck())
        {
            selectedBones.Clear();
        }

        if (npcRoot == null)
        {
            EditorGUILayout.HelpBox("Select the NPC prefab or its root in the Hierarchy.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();

        if (armatureRoot == null || coreBone == null)
        {
            EditorGUILayout.HelpBox("Assign the armature root and the bone that will become the physical core.", MessageType.Info);
            return;
        }

        if (armatureRoot != npcRoot.transform && !armatureRoot.IsChildOf(npcRoot.transform))
        {
            EditorGUILayout.HelpBox("The armature must belong to this NPC root.", MessageType.Error);
            return;
        }

        if (coreBone != armatureRoot && !coreBone.IsChildOf(armatureRoot))
        {
            EditorGUILayout.HelpBox("The core bone must belong to the selected armature.", MessageType.Error);
            return;
        }

        if (npcRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
        {
            EditorGUILayout.HelpBox("This NPC root contains no skinned mesh renderers.", MessageType.Warning);
            return;
        }

        selectedBones.RemoveAll(setup => setup.bone == null);

        if (!selectedBones.Exists(setup => setup.bone == coreBone))
        {
            BoneSetup coreSetup = new BoneSetup
            {
                bone = coreBone,
                endpoint = coreBone.childCount > 0 ? coreBone.GetChild(0) : null
            };

            selectedBones.Add(coreSetup);

            if (coreSetup.endpoint != null)
            {
                FitColliderToEndpoint(coreSetup);
            }
        }

        EditorGUILayout.LabelField("Physical Bones", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select bones that should have a physics proxy. Unchecked bones remain animated. The core is always included.", MessageType.Info);

        boneScrollPosition = EditorGUILayout.BeginScrollView(boneScrollPosition);
        DrawBoneTree(coreBone, 0);
        EditorGUILayout.EndScrollView();

        if (GUI.changed) SaveSetup();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selected Physical Bones", selectedBones.Count.ToString());

        if (GUILayout.Button("Next: Configure Colliders"))
        {
            if (UpdatePhysicsProxies())
            {
                currentPage = SetupPage.Colliders;
            }

            SceneView.RepaintAll();
            GUIUtility.ExitGUI();
        }
    }

    private void DrawBoneTree(Transform bone, int depth)
    {
        BoneSetup setup = selectedBones.Find(entry => entry.bone == bone);
        bool isCore = bone == coreBone;
        bool isSelected = setup != null;
        int previousIndent = EditorGUI.indentLevel;

        EditorGUI.indentLevel = depth;

        using (new EditorGUI.DisabledScope(isCore))
        {
            string label = isCore ? bone.name + " (Core)" : bone.name;
            bool shouldSelect = EditorGUILayout.ToggleLeft(label, isSelected);

            if (shouldSelect != isSelected)
            {
                if (shouldSelect)
                {
                    setup = new BoneSetup { bone = bone, endpoint = bone.childCount > 0 ? bone.GetChild(0) : null };
                    selectedBones.Add(setup);

                    if (setup.endpoint != null)
                    {
                        FitColliderToEndpoint(setup);
                    }
                }
                else
                {
                    if (setup.proxy != null)
                    {
                        proxiesToRemove.Add(setup.proxy);
                    }

                    selectedBones.Remove(setup);
                }
            }
        }

        EditorGUI.indentLevel = previousIndent;

        for (int i = 0; i < bone.childCount; i++)
        {
            DrawBoneTree(bone.GetChild(i), depth + 1);
        }
    }


    private void DrawColliderPage()
    {
        EditorGUILayout.LabelField("Configure Colliders", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        selectedBones.RemoveAll(setup => setup.bone == null);

        if (GUILayout.Button("Fit All to Endpoints"))
        {
            foreach (BoneSetup setup in selectedBones)
            {
                if (setup.endpoint == null || setup.endpoint == setup.bone || !setup.endpoint.IsChildOf(setup.bone)) continue;

                FitColliderToEndpoint(setup);
            }
        }

        float previousLabelWidth = EditorGUIUtility.labelWidth;
        float previousFieldWidth = EditorGUIUtility.fieldWidth;
        bool previousWideMode = EditorGUIUtility.wideMode;

        EditorGUIUtility.labelWidth = 45f;
        EditorGUIUtility.fieldWidth = 35f;
        EditorGUIUtility.wideMode = true;

        colliderScrollPosition = EditorGUILayout.BeginScrollView(colliderScrollPosition);

        foreach (BoneSetup setup in selectedBones)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        string label = setup.bone == coreBone ? "Core" : "Bone";
                        EditorGUILayout.ObjectField(label, setup.bone, typeof(Transform), true);
                    }

                    setup.shape = (ColliderShape)EditorGUILayout.EnumPopup("Shape", setup.shape, GUILayout.Width(155f));
                }

                switch (setup.shape)
                {
                    case ColliderShape.Capsule:
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            setup.axis = (CapsuleAxis)EditorGUILayout.EnumPopup("Axis", setup.axis);
                            setup.radius = Mathf.Max(0.001f, EditorGUILayout.FloatField("Radius", setup.radius));
                            setup.height = Mathf.Max(setup.radius * 2f, EditorGUILayout.FloatField("Height", setup.height));
                        }
                        break;

                    case ColliderShape.Sphere:
                        setup.radius = Mathf.Max(0.001f, EditorGUILayout.FloatField("Radius", setup.radius));
                        break;

                    case ColliderShape.Box:
                        setup.size = EditorGUILayout.Vector3Field("Size", setup.size);
                        setup.size = Vector3.Max(Vector3.one * 0.001f, setup.size);
                        break;
                }

                setup.centre = EditorGUILayout.Vector3Field("Centre", setup.centre);

                using (new EditorGUILayout.HorizontalScope())
                {
                    setup.endpoint = (Transform)EditorGUILayout.ObjectField("End", setup.endpoint, typeof(Transform), true);

                    bool validEndpoint = setup.endpoint != null && setup.endpoint != setup.bone && setup.endpoint.IsChildOf(setup.bone);

                    using (new EditorGUI.DisabledScope(!validEndpoint))
                    {
                        if (GUILayout.Button("Fit to Endpoint", GUILayout.Width(155f)))
                        {
                            FitColliderToEndpoint(setup);
                        }
                    }
                }
            }

            EditorGUILayout.Space(3f);
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            foreach (BoneSetup setup in selectedBones)
            {
                UpdateProxyCollider(setup);
            }

            SaveSetup();
            SceneView.RepaintAll();
        }


        EditorGUIUtility.labelWidth = previousLabelWidth;
        EditorGUIUtility.fieldWidth = previousFieldWidth;
        EditorGUIUtility.wideMode = previousWideMode;

        if (GUILayout.Button("Back: Select Bones"))
        {
            currentPage = SetupPage.Selection;
            SceneView.RepaintAll();
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("Next: Joint Connections"))
        {
            currentPage = SetupPage.Joints;
            SceneView.RepaintAll();
            GUIUtility.ExitGUI();
        }
    }

    

    private void DrawColliderPreviews(SceneView sceneView)
    {

        if (currentPage == SetupPage.Selection || npcRoot == null) return;
        if (EditorUtility.IsPersistent(npcRoot)) return;
        if (Event.current.type != EventType.Repaint) return;

        foreach (BoneSetup setup in selectedBones)
        {
            if (setup.bone == null) continue;
            if (!UnityEditor.SceneManagement.StageUtility.GetStageHandle(setup.bone.gameObject).Equals(UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle())) continue;

            if (currentPage == SetupPage.Joints && setup.bone != coreBone)
            {
                Transform parent = FindJointParent(setup.bone);

                if (parent != null)
                {
                    using (new Handles.DrawingScope(Color.yellow, Matrix4x4.identity))
                    {
                        Handles.DrawLine(parent.position, setup.bone.position);
                    }
                }
            }

            Matrix4x4 matrix = Matrix4x4.TRS(setup.bone.position, setup.bone.rotation, Vector3.one);

            using (new Handles.DrawingScope(Color.green, matrix))
            {
                switch (setup.shape)
                {
                    case ColliderShape.Box:
                        Handles.DrawWireCube(setup.centre, setup.size);
                        break;

                    case ColliderShape.Sphere:
                        Handles.DrawWireDisc(setup.centre, Vector3.right, setup.radius);
                        Handles.DrawWireDisc(setup.centre, Vector3.up, setup.radius);
                        Handles.DrawWireDisc(setup.centre, Vector3.forward, setup.radius);
                        break;

                    case ColliderShape.Capsule:
                        Vector3 axis = setup.axis == CapsuleAxis.X ? Vector3.right : setup.axis == CapsuleAxis.Y ? Vector3.up : Vector3.forward;
                        Vector3 side = setup.axis == CapsuleAxis.X ? Vector3.up : Vector3.right;
                        Vector3 otherSide = Vector3.Cross(axis, side);

                        float halfCylinder = Mathf.Max(0f, setup.height * 0.5f - setup.radius);
                        Vector3 top = setup.centre + axis * halfCylinder;
                        Vector3 bottom = setup.centre - axis * halfCylinder;

                        Handles.DrawWireDisc(top, axis, setup.radius);
                        Handles.DrawWireDisc(bottom, axis, setup.radius);

                        for (int i = 0; i < 2; i++)
                        {
                            Vector3 radial = i == 0 ? side : otherSide;
                            Vector3 arcNormal = Vector3.Cross(radial, axis);

                            Handles.DrawLine(top + radial * setup.radius, bottom + radial * setup.radius);
                            Handles.DrawLine(top - radial * setup.radius, bottom - radial * setup.radius);

                            Handles.DrawWireArc(top, arcNormal, radial, 180f, setup.radius);
                            Handles.DrawWireArc(bottom, -arcNormal, radial, 180f, setup.radius);
                        }
                        break;
                }
            }
        }
    }

    private void FitColliderToEndpoint(BoneSetup setup)
    {
        Vector3 worldOffset = setup.endpoint.position - setup.bone.position;
        float length = worldOffset.magnitude;

        if (length < 0.001f)
        {
            Debug.LogWarning("Choose an endpoint further away from the bone.", setup.bone);
            return;
        }

        Undo.RecordObject(this, "Fit NPC Collider");

        Vector3 localOffset = Quaternion.Inverse(setup.bone.rotation) * worldOffset;
        Vector3 absoluteOffset = new Vector3(Mathf.Abs(localOffset.x), Mathf.Abs(localOffset.y), Mathf.Abs(localOffset.z));

        setup.centre = localOffset * 0.5f;

        if (absoluteOffset.x >= absoluteOffset.y && absoluteOffset.x >= absoluteOffset.z)
        {
            setup.axis = CapsuleAxis.X;
        }
        else if (absoluteOffset.y >= absoluteOffset.z)
        {
            setup.axis = CapsuleAxis.Y;
        }
        else
        {
            setup.axis = CapsuleAxis.Z;
        }

        switch (setup.shape)
        {
            case ColliderShape.Capsule:
                setup.radius = Mathf.Max(0.001f, length * 0.15f);
                setup.height = Mathf.Max(setup.radius * 2f, length);
                break;

            case ColliderShape.Sphere:
                setup.radius = length * 0.5f;
                break;

            case ColliderShape.Box:
                float thickness = Mathf.Max(0.002f, length * 0.3f);
                setup.size = Vector3.Max(absoluteOffset, Vector3.one * thickness);
                break;
        }

        UpdateProxyCollider(setup);
        Repaint();
        SceneView.RepaintAll();
    }

    private Transform FindJointParent(Transform bone)
    {
        Transform parent = bone.parent;

        while (parent != null)
        {
            if (selectedBones.Exists(setup => setup.bone == parent))
            {
                return parent;
            }

            parent = parent.parent;
        }

        return null;
    }

    private void DrawJointPage()
    {
        EditorGUILayout.LabelField("Joint Connections", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Each physical bone connects to its nearest selected ancestor. The core has no parent joint.", MessageType.Info);

        selectedBones.RemoveAll(setup => setup.bone == null);

        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 45f;

        int jointCount = 0;
        jointScrollPosition = EditorGUILayout.BeginScrollView(jointScrollPosition);

        foreach (BoneSetup setup in selectedBones)
        {
            if (setup.bone == coreBone) continue;

            Transform parent = FindJointParent(setup.bone);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField("Parent", parent, typeof(Transform), true);
                        EditorGUILayout.ObjectField("Child", setup.bone, typeof(Transform), true);
                    }
                }

                if (parent == null)
                {
                    EditorGUILayout.HelpBox("This bone has no selected ancestor. Check your bone selection.", MessageType.Error);
                }
                else
                {
                    jointCount++;
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUIUtility.labelWidth = previousLabelWidth;

        EditorGUILayout.LabelField("Joint Count", jointCount.ToString());

        using (new EditorGUI.DisabledScope(jointCount == 0 || jointCount != selectedBones.Count - 1))
        {
            if (GUILayout.Button("Generate / Rebake XPBD Joints"))
            {
                GenerateXPBDJoints();
                GUIUtility.ExitGUI();
            }
        }

        if (GUILayout.Button("Back: Configure Colliders"))
        {
            currentPage = SetupPage.Colliders;
            SceneView.RepaintAll();
            GUIUtility.ExitGUI();
        }
    }

    private bool UpdatePhysicsProxies()
    {
        if (npcRoot == null || coreBone == null || EditorUtility.IsPersistent(npcRoot))
        {
            Debug.LogWarning("Open the NPC in Prefab Mode or use a scene instance before generating proxies.");
            return false;
        }

        if ((npcRoot.transform.lossyScale - Vector3.one).sqrMagnitude > 0.000001f)
        {
            Debug.LogWarning("The outer NPC root must have a world scale of 1, 1, 1.", npcRoot);
            return false;
        }

        if (selectedBones.Exists(setup => setup.bone == null))
        {
            Debug.LogWarning("A selected bone is missing. Check the bone selection.", npcRoot);
            return false;
        }

        Transform proxyContainer = npcRoot.transform.Find("PhysicsProxies");

        if (proxyContainer != null)
        {
            foreach (Transform child in proxyContainer)
            {
                if (selectedBones.Exists(setup => setup.proxy == child) || proxiesToRemove.Contains(child)) continue;

                Debug.LogWarning("An existing proxy is not recorded in this NPC's saved setup.", child);
                return false;
            }
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Update NPC Physics Proxies");

        if (npcRoot.GetComponent<NetworkObject>() == null)
        {
            Undo.AddComponent<NetworkObject>(npcRoot);
        }

        if (proxyContainer == null)
        {
            GameObject containerObject = new GameObject("PhysicsProxies");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(containerObject, npcRoot.scene);
            containerObject.transform.SetParent(npcRoot.transform, false);
            containerObject.SetActive(false);

            Undo.RegisterCreatedObjectUndo(containerObject, "Create Physics Proxies");
            proxyContainer = containerObject.transform;
        }

        foreach (Transform proxy in proxiesToRemove)
        {
            if (proxy != null && proxy.parent == proxyContainer)
            {
                Undo.DestroyObjectImmediate(proxy.gameObject);
            }
        }

        Undo.RecordObject(this, "Update Proxy Mappings");
        proxiesToRemove.Clear();

        foreach (Transform sourceBone in coreBone.GetComponentsInChildren<Transform>(true))
        {
            BoneSetup setup = selectedBones.Find(entry => entry.bone == sourceBone);
            if (setup == null) continue;

            if (setup.proxy == null)
            {
                GameObject proxyObject = new GameObject(sourceBone.name + "_Proxy");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(proxyObject, npcRoot.scene);
                proxyObject.transform.SetParent(proxyContainer, false);
                proxyObject.transform.SetPositionAndRotation(sourceBone.position, sourceBone.rotation);
                proxyObject.transform.localScale = Vector3.one;

                Undo.RegisterCreatedObjectUndo(proxyObject, "Create Bone Proxy");
                Undo.RecordObject(this, "Assign Bone Proxy");
                setup.proxy = proxyObject.transform;
            }

            UpdateProxyCollider(setup);
            ConfigureProxyPhysics(setup);
        }

        SaveSetup();
        Undo.CollapseUndoOperations(undoGroup);

        return true;
    }

    private void UpdateProxyCollider(BoneSetup setup)
    {
        if (setup.proxy == null) return;

        System.Type colliderType = setup.shape == ColliderShape.Capsule ? typeof(CapsuleCollider) : setup.shape == ColliderShape.Sphere ? typeof(SphereCollider) : typeof(BoxCollider);
        Collider collider = setup.proxy.GetComponent<Collider>();

        if (collider != null && collider.GetType() != colliderType)
        {
            Undo.DestroyObjectImmediate(collider);
            collider = null;
        }

        if (collider == null)
        {
            collider = (Collider)Undo.AddComponent(setup.proxy.gameObject, colliderType);
        }

        Undo.RecordObject(collider, "Edit NPC Collider");

        if (collider is CapsuleCollider capsule)
        {
            capsule.center = setup.centre;
            capsule.direction = (int)setup.axis;
            capsule.radius = setup.radius;
            capsule.height = setup.height;
        }
        else if (collider is SphereCollider sphere)
        {
            sphere.center = setup.centre;
            sphere.radius = setup.radius;
        }
        else if (collider is BoxCollider box)
        {
            box.center = setup.centre;
            box.size = setup.size;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(collider))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }

        EditorUtility.SetDirty(collider);
    }

    private void SaveSetup()
    {
        if (npcRoot == null || EditorUtility.IsPersistent(npcRoot)) return;

        NPCRagdollSetup savedSetup = npcRoot.GetComponent<NPCRagdollSetup>();

        if (savedSetup == null)
        {
            savedSetup = Undo.AddComponent<NPCRagdollSetup>(npcRoot);
        }

        Undo.RecordObject(savedSetup, "Save NPC Ragdoll Setup");

        savedSetup.armatureRoot = armatureRoot;
        savedSetup.coreBone = coreBone;
        savedSetup.selectedBones = selectedBones.ConvertAll(setup => setup.Copy());
        savedSetup.proxiesToRemove = new List<Transform>(proxiesToRemove);

        if (PrefabUtility.IsPartOfPrefabInstance(savedSetup))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(savedSetup);
        }

        EditorUtility.SetDirty(savedSetup);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(npcRoot.scene);
    }

    private void ConfigureProxyPhysics(BoneSetup setup)
    {
        GameObject proxyObject = setup.proxy.gameObject;

        Undo.RecordObject(proxyObject, "Configure Proxy Layer");
        proxyObject.layer = LayerMask.NameToLayer("Ragdoll");

        if (proxyObject.GetComponent<NetworkObject>() == null)
        {
            Undo.AddComponent<NetworkObject>(proxyObject);
        }

        Rigidbody rb = proxyObject.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = Undo.AddComponent<Rigidbody>(proxyObject);
            Undo.RecordObject(rb, "Configure Proxy Rigidbody");

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        NetworkRigidbody3D networkedRB = proxyObject.GetComponent<NetworkRigidbody3D>();

        if (networkedRB == null)
        {
            networkedRB = Undo.AddComponent<NetworkRigidbody3D>(proxyObject);
            Undo.RecordObject(networkedRB, "Configure Network Rigidbody");

            networkedRB.SyncParent = false;
            networkedRB.SyncScale = false;
        }

        if (networkedRB.InterpolationTarget == null)
        {
            Transform renderTarget = setup.proxy.Find("NetworkedRenderTarget");

            if (renderTarget == null)
            {
                GameObject targetObject = new GameObject("NetworkedRenderTarget");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(targetObject, npcRoot.scene);
                targetObject.transform.SetParent(setup.proxy, false);

                Undo.RegisterCreatedObjectUndo(targetObject, "Create Interpolation Target");
                renderTarget = targetObject.transform;
            }

            Undo.RecordObject(networkedRB, "Assign Interpolation Target");
            networkedRB.InterpolationTarget = renderTarget;
        }

        PhysicsObjectProperties properties = proxyObject.GetComponent<PhysicsObjectProperties>();

        if (properties == null)
        {
            properties = Undo.AddComponent<PhysicsObjectProperties>(proxyObject);

            SerializedObject serializedProperties = new SerializedObject(properties);
            serializedProperties.FindProperty("innateMaterial").intValue = 4;
            serializedProperties.FindProperty("_Material_label").intValue = 4;
            serializedProperties.ApplyModifiedProperties();
        }

        PhysicsObject physicsObject = proxyObject.GetComponent<PhysicsObject>();

        if (physicsObject == null)
        {
            physicsObject = Undo.AddComponent<PhysicsObject>(proxyObject);
        }

        Undo.RecordObject(physicsObject, "Configure Physics Object");
        physicsObject.rb = rb;
        physicsObject.physicsObjectProperties = properties;

        DraggableItem draggable = proxyObject.GetComponent<DraggableItem>();

        if (draggable == null)
        {
            draggable = Undo.AddComponent<DraggableItem>(proxyObject);
        }

        Undo.RecordObject(draggable, "Configure Draggable Bone");
        draggable.rb = rb;
        draggable.networkedRB = networkedRB;
        draggable.isCharacterObject = true;

        Object[] modifiedObjects = { proxyObject, rb, networkedRB, properties, physicsObject, draggable };

        foreach (Object modifiedObject in modifiedObjects)
        {
            EditorUtility.SetDirty(modifiedObject);

            if (PrefabUtility.IsPartOfPrefabInstance(modifiedObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(modifiedObject);
            }
        }
    }

    private void GenerateXPBDJoints()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Generate the ragdoll joints outside Play Mode.");
            return;
        }

        if (!UpdatePhysicsProxies()) return;

        foreach (BoneSetup setup in selectedBones)
        {
            if (setup.bone == coreBone) continue;

            if (FindJointParent(setup.bone) == null)
            {
                Debug.LogWarning("A selected bone has no physical parent. Check the bone selection.", setup.bone);
                return;
            }
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate NPC XPBD Joints");

        XPBDPosAndRotSolver solver = npcRoot.GetComponent<XPBDPosAndRotSolver>();

        if (solver == null)
        {
            solver = Undo.AddComponent<XPBDPosAndRotSolver>(npcRoot);
        }

        Undo.RecordObject(solver, "Configure NPC XPBD Joints");

        solver.targetArmatureRoot = coreBone;

        List<XPBDTestJoint> generatedJoints = new List<XPBDTestJoint>();

        foreach (Transform sourceBone in coreBone.GetComponentsInChildren<Transform>(true))
        {
            if (sourceBone == coreBone) continue;

            BoneSetup childSetup = selectedBones.Find(setup => setup.bone == sourceBone);
            if (childSetup == null) continue;

            Transform parentBone = FindJointParent(sourceBone);
            BoneSetup parentSetup = selectedBones.Find(setup => setup.bone == parentBone);

            Rigidbody parentBody = parentSetup.proxy.GetComponent<Rigidbody>();
            Rigidbody childBody = childSetup.proxy.GetComponent<Rigidbody>();

            XPBDTestJoint joint = solver.joints.Find(existing => existing.parent == parentBody && existing.child == childBody);
            bool isNewJoint = joint == null;

            if (isNewJoint)
            {
                joint = new XPBDTestJoint
                {
                    parent = parentBody,
                    child = childBody,
                    enablePosition = true,
                    enableRotation = true,
                    isRagdollJoint = false,
                    enableAngularLimits = true,
                    twistLimits = new Vector2(-45f, 45f),
                    swing1Limits = new Vector2(-60f, 60f),
                    swing2Limits = new Vector2(-60f, 60f)
                };
            }

            joint.parentTarget = parentBone;
            joint.childTarget = sourceBone;
            joint.BakeRestPose();

            if (isNewJoint)
            {
                solver.AutoAlignJointAxis(joint);
            }

            generatedJoints.Add(joint);
        }

        solver.joints = generatedJoints;
        solver.BakeJointsFromTargets();

        EditorUtility.SetDirty(solver);

        if (PrefabUtility.IsPartOfPrefabInstance(solver))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(solver);
        }

        SaveSetup();
        Undo.CollapseUndoOperations(undoGroup);

        EditorGUIUtility.PingObject(solver);
        SceneView.RepaintAll();

        Debug.Log($"Generated and baked {generatedJoints.Count} XPBD joints.", npcRoot);
    }

    private void LoadSetup()
    {
        armatureRoot = null;
        coreBone = null;
        selectedBones.Clear();
        proxiesToRemove.Clear();
        currentPage = SetupPage.Selection;

        if (npcRoot == null) return;

        NPCRagdollSetup savedSetup = npcRoot.GetComponent<NPCRagdollSetup>();
        if (savedSetup == null) return;

        armatureRoot = savedSetup.armatureRoot;
        coreBone = savedSetup.coreBone;
        selectedBones = savedSetup.selectedBones.ConvertAll(setup => setup.Copy());
        proxiesToRemove = new List<Transform>(savedSetup.proxiesToRemove);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawColliderPreviews;
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawColliderPreviews;
        Undo.undoRedoPerformed -= OnUndoRedo;
        SceneView.RepaintAll();
    }

    private void OnUndoRedo()
    {
        if (npcRoot != null && npcRoot.GetComponent<NPCRagdollSetup>() != null)
        {
            SetupPage previousPage = currentPage;
            LoadSetup();
            currentPage = previousPage;
        }

        Repaint();
        SceneView.RepaintAll();
    }
}