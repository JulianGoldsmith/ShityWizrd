using System.Collections.Generic;
using UnityEngine;

public class RuntimeDebugRenderer : MonoBehaviour
{
    public static RuntimeDebugRenderer Instance;

    [Header("Assign Default Unity Primitives")]
    public Mesh sphereMesh;
    public Mesh boxMesh;
    public Mesh capsuleMesh;
    public Mesh cylinderMesh; 

    [Header("Material")]
    [Tooltip("Must be a Standard/URP material with 'Enable GPU Instancing' CHECKED.")]
    public Material instancedMaterial;

    private class InstancedBatch
    {
        public List<Matrix4x4> matrices = new List<Matrix4x4>(1023);
        public List<Vector4> colors = new List<Vector4>(1023);
    }

    private Dictionary<Mesh, InstancedBatch> _batches = new Dictionary<Mesh, InstancedBatch>();
    private MaterialPropertyBlock _propertyBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color"); // "_BaseColor" for URP!

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _propertyBlock = new MaterialPropertyBlock();

        _batches[sphereMesh] = new InstancedBatch();
        _batches[boxMesh] = new InstancedBatch();
        _batches[capsuleMesh] = new InstancedBatch();
        _batches[cylinderMesh] = new InstancedBatch();
    }

    private Vector4[] _colorBuffer = new Vector4[1023];

    private void LateUpdate()
    {
        foreach (var kvp in _batches)
        {
            Mesh mesh = kvp.Key;
            InstancedBatch batch = kvp.Value;

            if (mesh == null || batch.matrices.Count == 0) continue;

            int count = batch.matrices.Count;
            for (int i = 0; i < count; i += 1023)
            {
                int batchSize = Mathf.Min(1023, count - i);

                // Get the chunk of matrices
                var subMatrices = batch.matrices.GetRange(i, batchSize);

                // FIX: Copy our colors into the fixed-size buffer
                for (int j = 0; j < batchSize; j++)
                {
                    _colorBuffer[j] = batch.colors[i + j];
                }

                // ALWAYS pass the full 1023-length array. Unity will lock the size to 1023 permanently!
                _propertyBlock.SetVectorArray(ColorProperty, _colorBuffer);

                Graphics.DrawMeshInstanced(mesh, 0, instancedMaterial, subMatrices, _propertyBlock);
            }

            batch.matrices.Clear();
            batch.colors.Clear();
        }
    }

    private static void QueueDraw(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Color color)
    {
        if (Instance == null || mesh == null) return;

        var batch = Instance._batches[mesh];
        batch.matrices.Add(Matrix4x4.TRS(pos, rot, scale));
        batch.colors.Add(color);
    }

    public static void DrawSphere(Vector3 pos, float radius, Color color) =>
        QueueDraw(Instance.sphereMesh, pos, Quaternion.identity, Vector3.one * (radius * 2), color);

    public static void DrawBox(Vector3 pos, Quaternion rot, Vector3 size, Color color) =>
        QueueDraw(Instance.boxMesh, pos, rot, size, color);

    public static void DrawCapsule(Vector3 pos, Quaternion rot, float radius, float height, Color color) =>
        QueueDraw(Instance.capsuleMesh, pos, rot, new Vector3(radius * 2, height / 2f, radius * 2), color);

    public static void DrawLine(Vector3 start, Vector3 end, float thickness, Color color)
    {
        Vector3 dir = end - start;
        float length = dir.magnitude;
        if (length < 0.001f) return;

        Quaternion rot = Quaternion.LookRotation(dir);
        Vector3 center = start + (dir / 2f);
        QueueDraw(Instance.boxMesh, center, rot, new Vector3(thickness, thickness, length), color);
    }

    public static void DrawArrow(Vector3 start, Vector3 end, float thickness, Color color)
    {
        DrawLine(start, end, thickness, color);

        Vector3 dir = end - start;
        if (dir.magnitude < 0.01f) return;

        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0); // cylinders face UP in unity
        QueueDraw(Instance.cylinderMesh, end, rot, new Vector3(thickness * 4, thickness * 2, thickness * 4), color);
    }
}