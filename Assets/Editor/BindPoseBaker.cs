using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// ==============================================================================
// CLASS 1: The User Tool (Manual trigger via the top menu)
// ==============================================================================
public class SceneMeshReimporter : Editor
{
    [MenuItem("Tools/Reimport Scene Meshes")]
    public static void ReimportMeshesInScene()
    {
        HashSet<string> uniqueMeshPaths = new HashSet<string>();

        // 1. Find Skinned Meshes
        SkinnedMeshRenderer[] skinnedMeshes = FindObjectsOfType<SkinnedMeshRenderer>();
        foreach (var smr in skinnedMeshes)
        {
            if (smr.sharedMesh != null)
            {
                string path = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (IsValidAssetPath(path)) uniqueMeshPaths.Add(path);
            }
        }

        // 2. Find Static Meshes
        MeshFilter[] staticMeshes = FindObjectsOfType<MeshFilter>();
        foreach (var mf in staticMeshes)
        {
            if (mf.sharedMesh != null)
            {
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (IsValidAssetPath(path)) uniqueMeshPaths.Add(path);
            }
        }

        if (uniqueMeshPaths.Count == 0)
        {
            Debug.LogWarning("No valid mesh assets found in the scene to reimport.");
            return;
        }

        // 3. Reimport with progress bar
        int count = 0;
        int total = uniqueMeshPaths.Count;

        foreach (string path in uniqueMeshPaths)
        {
            count++;
            EditorUtility.DisplayProgressBar("Baking Bind Poses", $"Importing {path} ({count}/{total})", (float)count / total);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        EditorUtility.ClearProgressBar();
        Debug.Log($"<color=#00FF00><b>Success:</b></color> Reimported and baked bind poses for {total} unique mesh asset(s).");
    }

    private static bool IsValidAssetPath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/");
    }
}

// ==============================================================================
// CLASS 2: The Pipeline Interceptor (Runs automatically on import/reimport)
// ==============================================================================
public class BindPoseBaker : AssetPostprocessor
{
    void OnPostprocessMesh(Mesh mesh)
    {
        // Extract the raw, unskinned X, Y, Z vertex coordinates
        List<Vector3> bindPosePositions = new List<Vector3>(mesh.vertices);

        // Save them into UV Channel 2
        mesh.SetUVs(2, bindPosePositions);
    }
}