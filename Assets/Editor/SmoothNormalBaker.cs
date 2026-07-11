using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SmoothNormalBaker : AssetPostprocessor
{
    private const string TOON_TAG = "ToonOutlineEnabled";

    [MenuItem("Shader/Import Normals For Toon Outline (Selected Assets)", false, 1)]
    public static void EnableOnSelected()
    {
        int count = 0;
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer != null)
            {
                importer.userData = TOON_TAG;
                importer.SaveAndReimport();
                count++;
            }
        }
        Debug.Log($"[Toon Outline] Baked Tangent-Space smooth normals into UV3 on {count} selected models.");
    }

    [MenuItem("Shader/Enable All Toon Normals (Skip Existing)", false, 2)]
    public static void EnableOnAll()
    {
        bool proceed = EditorUtility.DisplayDialog("Enable All Toon Normals", "Search project for UNTAGGED 3D models and bake smooth normals into UV3?", "Yes", "Cancel");
        if (!proceed) return;

        string[] guids = AssetDatabase.FindAssets("t:Model");
        int count = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

                if (importer != null && importer.userData != TOON_TAG)
                {
                    EditorUtility.DisplayProgressBar("Baking", $"Processing {path}...", (float)i / guids.Length);
                    importer.userData = TOON_TAG;
                    importer.SaveAndReimport();
                    count++;
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Debug.Log($"[Toon Outline] Baked smooth normals on {count} new models.");
    }

    [MenuItem("Shader/Force Re-Bake All Tagged Models (Fresh Update)", false, 3)]
    public static void ForceRebakeAll()
    {
        bool proceed = EditorUtility.DisplayDialog("Force Re-Bake", "Force a fresh re-import on EVERY tagged model?", "Yes", "Cancel");
        if (!proceed) return;

        string[] guids = AssetDatabase.FindAssets("t:Model");
        int count = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

                if (importer != null && importer.userData == TOON_TAG)
                {
                    EditorUtility.DisplayProgressBar("Re-Baking", $"Updating {path}...", (float)i / guids.Length);
                    importer.SaveAndReimport();
                    count++;
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Debug.Log($"[Toon Outline] Forced fresh UV3 re-bake on {count} models.");
    }

    [MenuItem("Shader/Disable Normals On Selected Assets", false, 14)]
    public static void DisableOnSelected()
    {
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null) { importer.userData = ""; importer.SaveAndReimport(); }
        }
    }

    void OnPreprocessModel()
    {
        ModelImporter importer = assetImporter as ModelImporter;
        if (importer != null && importer.importSettingsMissing) importer.userData = TOON_TAG;
    }

    void OnPostprocessModel(GameObject g)
    {
        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null || importer.userData != TOON_TAG) return;

        MeshFilter[] meshFilters = g.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mf in meshFilters) if (mf.sharedMesh != null) BakeSmoothNormals(mf.sharedMesh);

        SkinnedMeshRenderer[] skinnedMeshes = g.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer smr in skinnedMeshes) if (smr.sharedMesh != null) BakeSmoothNormals(smr.sharedMesh);
    }

    private void BakeSmoothNormals(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;

        if (tangents == null || tangents.Length == 0) return;

        Dictionary<Vector3, Vector3> smoothNormalsMap = new Dictionary<Vector3, Vector3>();

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!smoothNormalsMap.ContainsKey(vertices[i])) smoothNormalsMap[vertices[i]] = normals[i];
            else smoothNormalsMap[vertices[i]] += normals[i];
        }

        // UPGRADE: We now use Vector4 to store the Magic Number signature
        List<Vector4> tangentSpaceSmoothNormals = new List<Vector4>(vertices.Length);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 smoothNormalOS = smoothNormalsMap[vertices[i]].normalized;
            Vector3 n = normals[i];
            Vector3 t = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
            float tSign = tangents[i].w;
            Vector3 b = Vector3.Cross(n, t) * tSign;

            float tsX = Vector3.Dot(t, smoothNormalOS);
            float tsY = Vector3.Dot(b, smoothNormalOS);
            float tsZ = Vector3.Dot(n, smoothNormalOS);

            // Pack X, Y, Z, and set W to the magic number 0.5
            tangentSpaceSmoothNormals.Add(new Vector4(tsX, tsY, tsZ, 0.5f));
        }

        mesh.SetUVs(2, tangentSpaceSmoothNormals);
    }
}