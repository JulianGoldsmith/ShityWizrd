using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public static class SDFGenerator
{

    public static void GenerateChunk(Chunk chunk, Vector3Int chunkSize, List<GeneratedRoom> rooms, List<Connection> pathNetwork, Vector3 boundsSize, Vector3Int gridSize, float tunnelRadius)
    {
        Vector3 scale = new Vector3((float)boundsSize.x / gridSize.x, (float)boundsSize.y / gridSize.y, (float)boundsSize.z / gridSize.z);
        Vector3 chunkWorldPos = new Vector3(
            chunk.coord.x * chunkSize.x * scale.x,
            chunk.coord.y * chunkSize.y * scale.y,
            chunk.coord.z * chunkSize.z * scale.z
        );

        for (int x = 0; x < chunk.grid.GetLength(0); x++)
        {
            for (int y = 0; y < chunk.grid.GetLength(1); y++)
            {
                for (int z = 0; z < chunk.grid.GetLength(2); z++)
                {
                    Vector3 currentPoint = chunkWorldPos + new Vector3(x * scale.x, y * scale.y, z * scale.z);

                    foreach (var room in rooms)
                    {

                        float distToRoomCenter = Vector3.Distance(currentPoint, room.pos);
                        if (distToRoomCenter < room.radius + 20) 
                        {
                            var v = chunk.grid[x, y, z];
                            v = room.profileClone.GenerateVoxel(currentPoint, room, v);
                            chunk.grid[x, y, z] = v;
                        }
                    }


                    foreach (var connection in pathNetwork)
                    {
                        // AABB check for tunnels isnt done neeed to implement this

                        SDFPrimitive capsule = new SDFPrimitive();
                        float tunnelSDF = -capsule.SdfCapsule(currentPoint, connection.p1, connection.p2, tunnelRadius);

                        if(tunnelSDF < 0)
                        {
                            chunk.grid[x, y, z].density = Mathf.Min(chunk.grid[x, y, z].density, tunnelSDF);
                        }
                    }

                    MaterialFields.ClearIfAir(ref chunk.grid[x, y, z]);
                }
            }
        }
    }

}

public static class SDFHelpers
{

    // union - keep either solid -> max
    public static float Union(float a, float b, float cap, bool softTruncate = true, float k = 0f)
    {
        float v = (k > 0f) ? SmoothMax(a, b, k) : Mathf.Max(a, b);
        return Truncate(v, cap, softTruncate);
    }

    // intersection - keep overlap only -> min
    public static float Intersect(float a, float b, float cap, bool softTruncate = true, float k = 0f)
    {
        float v = (k > 0f) ? SmoothMin(a, b, k) : Mathf.Min(a, b);
        return Truncate(v, cap, softTruncate);
    }

    // subtract - keep A, cut B -> min(A, -B)
    public static float Subtract(float a, float b, float cap, bool softTruncate = true, float k = 0f)
    {
        float v = (k > 0f) ? SmoothMin(a, -b, k) : Mathf.Min(a, -b);
        return Truncate(v, cap, softTruncate);
    }


    public static float Truncate(float v, float cap, bool soft)
    {
        if (cap <= 0f) return v; 

        if (!soft)
            return Mathf.Clamp(v, -cap, cap);

        const float SOFT_FRACTION = 0.1f; // 10 % soft shoulder near each end
        float a = Mathf.Max(0f, cap * (1f - SOFT_FRACTION)); 

        if (v > cap) return cap;
        if (v < -cap) return -cap;

        if (v > a)
        {
            float t = (v - a) / (cap - a);      // 0..1
            return Mathf.Lerp(a, cap, Smoothstep01(t));
        }
        if (v < -a)
        {
            float t = (-v - a) / (cap - a);     // 0..1
            return -Mathf.Lerp(a, cap, Smoothstep01(t));
        }

        return v; 
    }

 

    public static float SmoothMax(float a, float b, float k)
    {

        return -SmoothMinNegative(-a, -b, k);
    }

    public static float SmoothMin(float a, float b, float k)
    {

        return -SmoothMax(-a, -b, k);
    }


    private static float SmoothMax(float a, float b, float k, bool _)
    {
        return -SmoothMinNegative(-a, -b, k);
    }


    private static float SmoothMinNegative(float a, float b, float k)
    {
        if (k <= 0f) return Mathf.Min(a, b);
        float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
        // original iq formula
        return Mathf.Lerp(b, a, h) - k * h * (1f - h);
    }

    private static float Smoothstep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}

//if future me youre stuck with these i kept getting values the worng way around so maybe check here
[System.Serializable]
public class SDFPrimitive
{
    public enum ShapeType { Sphere, Box }
    public enum BlendOperation { Union, Subtract }

    public ShapeType shape = ShapeType.Sphere;
    public BlendOperation operation = BlendOperation.Union;
    public Vector3 offset = Vector3.zero;
    public Vector3 size = Vector3.one * 10f;

    public float Generate(Vector3 _pos, Vector3 _roomPos)
    {
        float value = 0.0f;

        switch (shape)
        {
            case ShapeType.Sphere:

                return SdfSphere(_pos, _roomPos + offset, size.x);

            case ShapeType.Box:

                return SdfBox(_pos, _roomPos + offset, size);

        }
        return value;
    }

    //returns solid in the middle think radius = 30, then 30 - distance from center ie ( 10-10) 
    public float SdfSphere(Vector3 p, Vector3 center, float r)
    {
        return r - Vector3.Distance(p, center);
    }

    //returns solid in the middle 
    public float SdfCapsule(Vector3 p, Vector3 a, Vector3 b, float r)
    {
        Vector3 pa = p - a;
        Vector3 ba = b - a;
        float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
        return r - (pa - ba * h).magnitude;
    }
    public float SdfBox(Vector3 p, Vector3 center, Vector3 size)
    {
        Vector3 q = new Vector3(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y), Mathf.Abs(p.z - center.z)) - size;
        return -(new Vector3(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0), Mathf.Max(q.z, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y, q.z), 0));
    }
}

[System.Serializable]
public class NoiseLayer
{
    public bool enabled = false;
    public float frequency = 0.1f;
    public float amplitude = 4.0f;
    public List<EnvironmentInfluence> influences;
}

public enum VoxelDataType { Humidity, Temperature, Slope, Verticality }

[System.Serializable]
public class EnvironmentInfluence
{
    public bool enabled = true;
    public VoxelDataType sourceType; 

    [Range(-1, 1)]
    public float influence = 1f;
}


//noise function from wiki
public static class Noise
{

    private static int[] p = new int[512];
    private static int[] perm = { 151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,
        23,190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,
        174,20,125,136,171,168,68,175,74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,
        133,230,220,105,92,41,55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,
        89,18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,52,217,226,250,124,123,5,
        202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,119,
        248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,129,22,39,253,19,98,108,110,79,113,224,232,
        178,185,112,104,218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,
        14,239,107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,
        93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180};

    static Noise()
    {
        for (int i = 0; i < 256; i++)
        {
            p[i] = p[i + 256] = perm[i];
        }
    }

    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private static float Lerp(float t, float a, float b)
    {
        return a + t * (b - a);
    }

    private static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    public static float Generate(float x, float y, float z)
    {
        int X = Mathf.FloorToInt(x) & 255;
        int Y = Mathf.FloorToInt(y) & 255;
        int Z = Mathf.FloorToInt(z) & 255;

        x -= Mathf.Floor(x);
        y -= Mathf.Floor(y);
        z -= Mathf.Floor(z);

        float u = Fade(x);
        float v = Fade(y);
        float w = Fade(z);

        int A = p[X] + Y, AA = p[A] + Z, AB = p[A + 1] + Z;
        int B = p[X + 1] + Y, BA = p[B] + Z, BB = p[B + 1] + Z;

        return Lerp(w, Lerp(v, Lerp(u, Grad(p[AA], x, y, z), Grad(p[BA], x - 1, y, z)),
                                Lerp(u, Grad(p[AB], x, y - 1, z), Grad(p[BB], x - 1, y - 1, z))),
                       Lerp(v, Lerp(u, Grad(p[AA + 1], x, y, z - 1), Grad(p[BA + 1], x - 1, y, z - 1)),
                                Lerp(u, Grad(p[AB + 1], x, y - 1, z - 1), Grad(p[BB + 1], x - 1, y - 1, z - 1))));
    }
}
