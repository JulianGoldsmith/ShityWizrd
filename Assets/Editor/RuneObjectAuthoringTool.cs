using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RuneObjectAuthoringTool
{
    private const string MenuPath = "GameObject/Rune Rigs/Build Rune Object From Selected Core";
    private const string SocketInMaterialPath = "Assets/Models/RuneBase/Sockets/SocketInMat.mat";
    private const string SocketOutMaterialPath = "Assets/Models/RuneBase/Sockets/SocketIOutMat.mat";

    [MenuItem(MenuPath,false,20)]
    private static void BuildFromSelectedCore() {
        BuildRuneObject(Selection.activeGameObject);
    }

    [MenuItem(MenuPath,true)]
    private static bool CanBuildFromSelectedCore() {
        return Selection.activeGameObject != null && !EditorUtility.IsPersistent(Selection.activeGameObject);
    }

    [MenuItem("CONTEXT/MeshFilter/Build Rune Object From This Core")]
    private static void BuildFromMeshFilter(MenuCommand command) {
        MeshFilter meshFilter = command.context as MeshFilter;
        BuildRuneObject(meshFilter != null ? meshFilter.gameObject : null);
    }

    private static void BuildRuneObject(GameObject sourceCore) {
        if (sourceCore == null || EditorUtility.IsPersistent(sourceCore)) {
            EditorUtility.DisplayDialog("Build Rune Object","Drag the Blender model into the scene, then select its Cube object.","OK");
            return;
        }

        MeshFilter sourceMeshFilter = sourceCore.GetComponent<MeshFilter>();

        if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null) {
            EditorUtility.DisplayDialog("Build Rune Object","The selected core needs a MeshFilter with a mesh.","OK");
            return;
        }

        GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(sourceCore);
        GameObject runeRoot = prefabRoot != null ? prefabRoot : sourceCore.transform.parent != null ? sourceCore.transform.parent.gameObject : null;

        if (runeRoot == null || runeRoot == sourceCore) {
            EditorUtility.DisplayDialog("Build Rune Object","The selected Cube needs to be a child of the imported model root.","OK");
            return;
        }

        if (runeRoot.GetComponent<RuneObject>() != null) {
            EditorUtility.DisplayDialog("Build Rune Object",$"'{runeRoot.name}' is already a RuneObject.","OK");
            return;
        }

        List<Transform> inSockets = runeRoot.GetComponentsInChildren<Transform>(true)
            .Where(socket => socket != runeRoot.transform && socket.name.StartsWith("InSocket",StringComparison.OrdinalIgnoreCase))
            .OrderBy(socket => socket.name,StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<Transform> outSockets = runeRoot.GetComponentsInChildren<Transform>(true)
            .Where(socket => socket != runeRoot.transform && socket.name.StartsWith("OutSocket",StringComparison.OrdinalIgnoreCase))
            .OrderBy(socket => socket.name,StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (outSockets.Count != 1) {
            EditorUtility.DisplayDialog("Build Rune Object",$"Expected exactly one OutSocket plug, but found {outSockets.Count}.","OK");
            return;
        }

        if (inSockets.Count > RuneRigLimits.MaxBayCapacity) {
            EditorUtility.DisplayDialog("Build Rune Object",$"Found {inSockets.Count} InSockets, but a rune supports at most {RuneRigLimits.MaxBayCapacity}.","OK");
            return;
        }

        Material socketInMaterial = AssetDatabase.LoadAssetAtPath<Material>(SocketInMaterialPath);
        Material socketOutMaterial = AssetDatabase.LoadAssetAtPath<Material>(SocketOutMaterialPath);

        if (socketInMaterial == null || socketOutMaterial == null) {
            EditorUtility.DisplayDialog("Build Rune Object",$"Could not find the socket materials at:\n{SocketInMaterialPath}\n{SocketOutMaterialPath}","OK");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Rune Object");

        if (prefabRoot != null)
            PrefabUtility.UnpackPrefabInstance(prefabRoot,PrefabUnpackMode.Completely,InteractionMode.UserAction);

        Transform[] importedObjects = runeRoot.GetComponentsInChildren<Transform>(true);

        for (int objectIndex = importedObjects.Length - 1; objectIndex >= 0; objectIndex--) {
            Transform importedObject = importedObjects[objectIndex];

            if (importedObject != runeRoot.transform && importedObject.name.StartsWith("SocketCutter",StringComparison.OrdinalIgnoreCase))
                Undo.DestroyObjectImmediate(importedObject.gameObject);
        }

        GameObject visual = Object.Instantiate(sourceCore,sourceCore.transform.parent,false);
        Undo.RegisterCreatedObjectUndo(visual,"Create Rune Visual");
        visual.name = "Visual";
        visual.transform.localPosition = sourceCore.transform.localPosition;
        visual.transform.localRotation = sourceCore.transform.localRotation;
        visual.transform.localScale = sourceCore.transform.localScale;
        visual.transform.SetSiblingIndex(sourceCore.transform.GetSiblingIndex() + 1);

        Transform[] inheritedVisualSockets = visual.GetComponentsInChildren<Transform>(true);

        for (int socketIndex = inheritedVisualSockets.Length - 1; socketIndex >= 0; socketIndex--) {
            Transform inheritedSocket = inheritedVisualSockets[socketIndex];

            if (inheritedSocket != visual.transform && (inheritedSocket.name.StartsWith("InSocket",StringComparison.OrdinalIgnoreCase) || inheritedSocket.name.StartsWith("OutSocket",StringComparison.OrdinalIgnoreCase)))
                Undo.DestroyObjectImmediate(inheritedSocket.gameObject);
        }

        List<Transform> sourceSockets = new List<Transform>(inSockets.Count + outSockets.Count);
        sourceSockets.AddRange(inSockets);
        sourceSockets.AddRange(outSockets);

        List<Transform> logicalInSockets = new List<Transform>(inSockets.Count);
        List<Transform> logicalOutSockets = new List<Transform>(outSockets.Count);

        foreach (Transform socket in sourceSockets) {
            GameObject visualSocket = Object.Instantiate(socket.gameObject,visual.transform,true);
            Undo.RegisterCreatedObjectUndo(visualSocket,"Create Visual Socket");
            visualSocket.name = socket.name;
            visualSocket.transform.SetPositionAndRotation(socket.position,socket.rotation);

            bool isInputSocket = socket.name.StartsWith("InSocket",StringComparison.OrdinalIgnoreCase);
            Material socketMaterial = isInputSocket ? socketInMaterial : socketOutMaterial;

            foreach (Renderer socketRenderer in visualSocket.GetComponentsInChildren<Renderer>(true)) {
                Undo.RecordObject(socketRenderer,"Assign Socket Material");
                socketRenderer.sharedMaterial = socketMaterial;
            }

            GameObject logicalSocket = new GameObject(socket.name);
            Undo.RegisterCreatedObjectUndo(logicalSocket,"Create Logical Socket");
            logicalSocket.transform.SetParent(runeRoot.transform,true);
            logicalSocket.transform.SetPositionAndRotation(visualSocket.transform.position,visualSocket.transform.rotation);
            logicalSocket.transform.localScale = Vector3.one;

            if (isInputSocket)
                logicalInSockets.Add(logicalSocket.transform);
            else {
                logicalSocket.transform.rotation *= Quaternion.Euler(0f,180f,0f);
                logicalOutSockets.Add(logicalSocket.transform);
            }
        }

        foreach (Transform socket in sourceSockets)
            Undo.DestroyObjectImmediate(socket.gameObject);

        foreach (Collider visualCollider in visual.GetComponentsInChildren<Collider>(true))
            Undo.DestroyObjectImmediate(visualCollider);

        MeshCollider physicsCollider = sourceCore.GetComponent<MeshCollider>();

        if (physicsCollider == null)
            physicsCollider = Undo.AddComponent<MeshCollider>(sourceCore);

        Undo.RecordObject(physicsCollider,"Configure Rune Collider");
        physicsCollider.sharedMesh = sourceMeshFilter.sharedMesh;
        physicsCollider.convex = true;

        foreach (Renderer physicsRenderer in sourceCore.GetComponentsInChildren<Renderer>(true))
            Undo.DestroyObjectImmediate(physicsRenderer);

        foreach (MeshFilter physicsMeshFilter in sourceCore.GetComponentsInChildren<MeshFilter>(true))
            Undo.DestroyObjectImmediate(physicsMeshFilter);

        Undo.RecordObject(sourceCore,"Rename Rune Physics");
        sourceCore.name = "Physics";

        RuneObject runeObject = Undo.AddComponent<RuneObject>(runeRoot);
        Undo.RecordObject(runeObject,"Configure Rune Object");
        runeObject.VisualRoot = visual.transform;
        runeObject.RootConnectionTransform = logicalOutSockets[0];
        runeObject.Bays.Clear();

        for (int bayIndex = 0; bayIndex < logicalInSockets.Count; bayIndex++) {
            runeObject.Bays.Add(new RuneBay {
                BayIndex = (byte)bayIndex,
                BayTransform = logicalInSockets[bayIndex]
            });
        }

        int runeLayer = LayerMask.NameToLayer("Item");

        foreach (Transform child in runeRoot.GetComponentsInChildren<Transform>(true)) {
            Undo.RecordObject(child.gameObject,"Set Rune Layer And Tag");

            if (runeLayer >= 0)
                child.gameObject.layer = runeLayer;

            child.gameObject.tag = "Item";
        }

        EditorUtility.SetDirty(runeObject);
        EditorSceneManager.MarkSceneDirty(runeRoot.scene);
        Selection.activeGameObject = runeRoot;
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[RuneObjectAuthoring] Built '{runeRoot.name}' with {logicalInSockets.Count} bays.",runeRoot);
    }
}
