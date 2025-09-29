using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

public static class MarchingCubes
{
    public static void CreateMeshOld(VoxelData[,,] grid, Mesh mesh, float isoLevel = 0)
    {
        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;
        List<Vector3> vertexList = new List<Vector3>();
        List<int> triangleList = new List<int>();
        List<Color> colorList = new List<Color>();

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        int depth = grid.GetLength(2);

        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                for (int z = 0; z < depth - 1; z++)
                {
                    VoxelData[] corners = new VoxelData[8];

                    int cubeIndex = 0;
                    //float[] cornerValues = new float[8];
                    //Vector3[] corners = new Vector3[8];

                    for (int i = 0; i < 8; i++)
                    {
                        /*corners[i] = new Vector3(x + CubePoints[i, 0], y + CubePoints[i, 1], z + CubePoints[i, 2]);
                        cornerValues[i] = grid[(int)corners[i].x, (int)corners[i].y, (int)corners[i].z].density;
                        if (cornerValues[i] < isoLevel)
                            cubeIndex |= 1 << i;*/
                        Vector3Int cornerPos = new Vector3Int(x + CubePoints[i, 0], y + CubePoints[i, 1], z + CubePoints[i, 2]);
                        corners[i] = grid[cornerPos.x, cornerPos.y, cornerPos.z];
                        if (corners[i].density > isoLevel)
                            cubeIndex |= 1 << i;
                    }

                    // This loop uses the corrected condition (i < 15) and does not have the faulty "edges" check.
                    for (int i = 0; i < 15; i += 3)
                    {
                        if (TriTable[cubeIndex, i] == -1) break;

                        int[] edgeIndices = { TriTable[cubeIndex, i], TriTable[cubeIndex, i + 1], TriTable[cubeIndex, i + 2] };

                        

                        for (int j = 0; j < 3; j++)
                        {
                            int edge = edgeIndices[j];
                            int v1Index = EdgeConnections[edge, 0];
                            int v2Index = EdgeConnections[edge, 1];

                            VoxelData v1 = corners[v1Index];
                            VoxelData v2 = corners[v2Index];
                            Vector3 v1Pos = new Vector3(x + CubePoints[v1Index, 0], y + CubePoints[v1Index, 1], z + CubePoints[v1Index, 2]);
                            Vector3 v2Pos = new Vector3(x + CubePoints[v2Index, 0], y + CubePoints[v2Index, 1], z + CubePoints[v2Index, 2]);

                            float t = (isoLevel - v1.density) / (v2.density - v1.density);
                            Vector3 vertPos = v1Pos + t * (v2Pos - v1Pos);

                            
                            vertexList.Add(vertPos);
                            triangleList.Add(vertexList.Count - 1);
                        }
                    }
                }
            }
        }

        mesh.vertices = vertexList.ToArray();
        mesh.triangles = triangleList.ToArray();
        mesh.colors = colorList.ToArray(); 
        mesh.RecalculateNormals();

        Debug.Log($"Verts: {vertexList.Count}, Tris: {triangleList.Count/3}");
    }

    public static void CreateMesh(VoxelData[,,] grid, Mesh mesh,float isoLevel = 0f, bool smoothShading = false, bool useSdfNormals = false)
    {
        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32; 

        int W = grid.GetLength(0);
        int H = grid.GetLength(1);
        int D = grid.GetLength(2);

        var vertexList = new List<Vector3>(W * H * D / 2);
        var triangleList = new List<int>(W * H * D);

        List<Vector3> normalList = useSdfNormals && smoothShading ? new List<Vector3>(W * H * D / 2) : null;

        if (!smoothShading)
        {

            for (int x = 0; x < W - 1; x++)
                for (int y = 0; y < H - 1; y++)
                    for (int z = 0; z < D - 1; z++)
                    {
           
                        int cubeIndex = 0;
                        VoxelData[] corners = new VoxelData[8];
                        for (int i = 0; i < 8; i++)
                        {
                            int xi = x + CubePoints[i, 0];
                            int yi = y + CubePoints[i, 1];
                            int zi = z + CubePoints[i, 2];
                            corners[i] = grid[xi, yi, zi];
                            if (corners[i].density > isoLevel) cubeIndex |= 1 << i;
                        }

                     
                        for (int t = 0; t < 15; t += 3)
                        {
                            int e0 = TriTable[cubeIndex, t];
                            if (e0 == -1) break;
                            int e1 = TriTable[cubeIndex, t + 1];
                            int e2 = TriTable[cubeIndex, t + 2];

                            AddEdgeVertex_NoShare(x, y, z, e0, grid, isoLevel, vertexList, triangleList);
                            AddEdgeVertex_NoShare(x, y, z, e1, grid, isoLevel, vertexList, triangleList);
                            AddEdgeVertex_NoShare(x, y, z, e2, grid, isoLevel, vertexList, triangleList);
                        }
                    }


            mesh.SetVertices(vertexList);
            mesh.SetTriangles(triangleList, 0, true);
            mesh.RecalculateNormals();   
            mesh.RecalculateBounds();
            return;
        }

        int[,,] edgeX = new int[W - 1, H, D]; 
        int[,,] edgeY = new int[W, H - 1, D]; 
        int[,,] edgeZ = new int[W, H, D - 1]; 
        Fill(edgeX, -1); Fill(edgeY, -1); Fill(edgeZ, -1);

        for (int x = 0; x < W - 1; x++)
            for (int y = 0; y < H - 1; y++)
                for (int z = 0; z < D - 1; z++)
                {
                    int cubeIndex = 0;
                    float[] den = new float[8];
                    for (int i = 0; i < 8; i++)
                    {
                        int xi = x + CubePoints[i, 0];
                        int yi = y + CubePoints[i, 1];
                        int zi = z + CubePoints[i, 2];
                        den[i] = grid[xi, yi, zi].density;
                        if (den[i] > isoLevel) cubeIndex |= 1 << i;
                    }
                    if (TriTable[cubeIndex, 0] == -1) continue;

                    for (int t = 0; t < 16; t += 3)
                    {
                        int e0 = TriTable[cubeIndex, t];
                        if (e0 == -1) break;
                        int e1 = TriTable[cubeIndex, t + 1];
                        int e2 = TriTable[cubeIndex, t + 2];

                        int i0 = GetOrCreateEdgeVertex_Share(x, y, z, e0, grid, isoLevel, vertexList, normalList, edgeX, edgeY, edgeZ, useSdfNormals);
                        int i1 = GetOrCreateEdgeVertex_Share(x, y, z, e1, grid, isoLevel, vertexList, normalList, edgeX, edgeY, edgeZ, useSdfNormals);
                        int i2 = GetOrCreateEdgeVertex_Share(x, y, z, e2, grid, isoLevel, vertexList, normalList, edgeX, edgeY, edgeZ, useSdfNormals);

                        triangleList.Add(i0); triangleList.Add(i1); triangleList.Add(i2);
                    }
                }

        mesh.SetVertices(vertexList);
        mesh.SetTriangles(triangleList, 0, true);

        if (useSdfNormals)
        {
            mesh.SetNormals(normalList);
        }
        else
        {
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();
    }

    private static void AddEdgeVertex_NoShare(int x, int y, int z, int edge, VoxelData[,,] grid, float isoLevel, List<Vector3> verts, List<int> tris)
    {

        int a = EdgeConnections[edge, 0];
        int b = EdgeConnections[edge, 1];

        Vector3 pa = new Vector3(x + CubePoints[a, 0], y + CubePoints[a, 1], z + CubePoints[a, 2]);
        Vector3 pb = new Vector3(x + CubePoints[b, 0], y + CubePoints[b, 1], z + CubePoints[b, 2]);

        float da = grid[(int)pa.x, (int)pa.y, (int)pa.z].density;
        float db = grid[(int)pb.x, (int)pb.y, (int)pb.z].density;

        float t = EdgeT(isoLevel, da, db);
        Vector3 p = Vector3.Lerp(pa, pb, t);

        verts.Add(p);
        tris.Add(verts.Count - 1);
    }

    private static int GetOrCreateEdgeVertex_Share(int x, int y, int z, int edge, VoxelData[,,] grid, float isoLevel, List<Vector3> verts, List<Vector3> normsOrNull,
        int[,,] edgeX, int[,,] edgeY, int[,,] edgeZ, bool bakeSdfNormals)
    {
        // edge corner indices
        int a = EdgeConnections[edge, 0];
        int b = EdgeConnections[edge, 1];

        // Corner positions in voxel coords
        Vector3 pa = new Vector3(x + CubePoints[a, 0], y + CubePoints[a, 1], z + CubePoints[a, 2]);
        Vector3 pb = new Vector3(x + CubePoints[b, 0], y + CubePoints[b, 1], z + CubePoints[b, 2]);

        // densities
        float da = grid[(int)pa.x, (int)pa.y, (int)pa.z].density;
        float db = grid[(int)pb.x, (int)pb.y, (int)pb.z].density;

        // interpolate position along edge
        float t = EdgeT(isoLevel, da, db);
        Vector3 p = Vector3.Lerp(pa, pb, t);

        //  which edge cache to use based on which axis changes between a and b
        Vector3Int ca = new Vector3Int(CubePoints[a, 0], CubePoints[a, 1], CubePoints[a, 2]);
        Vector3Int cb = new Vector3Int(CubePoints[b, 0], CubePoints[b, 1], CubePoints[b, 2]);
        int dx = cb.x - ca.x, dy = cb.y - ca.y, dz = cb.z - ca.z;

        if (dx != 0)
        {
            int ex = x + Mathf.Min(ca.x, cb.x);
            int ey = y + ca.y;
            int ez = z + ca.z;
            int cached = edgeX[ex, ey, ez];
            if (cached >= 0) return cached;

            int idx = verts.Count;
            verts.Add(p);

            if (bakeSdfNormals) normsOrNull!.Add(ComputeGradient(grid, p).normalized);

            edgeX[ex, ey, ez] = idx;
            return idx;
        }
        else if (dy != 0)
        {
            int ex = x + ca.x;
            int ey = y + Mathf.Min(ca.y, cb.y);
            int ez = z + ca.z;
            int cached = edgeY[ex, ey, ez];
            if (cached >= 0) return cached;

            int idx = verts.Count;
            verts.Add(p);

            if (bakeSdfNormals) normsOrNull!.Add(ComputeGradient(grid, p).normalized);

            edgeY[ex, ey, ez] = idx;
            return idx;
        }
        else
        {
            int ex = x + ca.x;
            int ey = y + ca.y;
            int ez = z + Mathf.Min(ca.z, cb.z);
            int cached = edgeZ[ex, ey, ez];
            if (cached >= 0) return cached;

            int idx = verts.Count;
            verts.Add(p);

            if (bakeSdfNormals) normsOrNull!.Add(ComputeGradient(grid, p).normalized);

            edgeZ[ex, ey, ez] = idx;
            return idx;
        }
    }


    private static Vector3 ComputeGradient(VoxelData[,,] grid, Vector3 p)
    {
        int W = grid.GetLength(0), H = grid.GetLength(1), D = grid.GetLength(2);

        float Sample(float xi, float yi, float zi)
        {
            xi = Mathf.Clamp(xi, 0f, W - 1.001f);
            yi = Mathf.Clamp(yi, 0f, H - 1.001f);
            zi = Mathf.Clamp(zi, 0f, D - 1.001f);

            int x0 = (int)xi, y0 = (int)yi, z0 = (int)zi;
            int x1 = Mathf.Min(x0 + 1, W - 1);
            int y1 = Mathf.Min(y0 + 1, H - 1);
            int z1 = Mathf.Min(z0 + 1, D - 1);
            float tx = xi - x0, ty = yi - y0, tz = zi - z0;

            float c000 = grid[x0, y0, z0].density;
            float c100 = grid[x1, y0, z0].density;
            float c010 = grid[x0, y1, z0].density;
            float c110 = grid[x1, y1, z0].density;
            float c001 = grid[x0, y0, z1].density;
            float c101 = grid[x1, y0, z1].density;
            float c011 = grid[x0, y1, z1].density;
            float c111 = grid[x1, y1, z1].density;

            float c00 = Mathf.Lerp(c000, c100, tx);
            float c10 = Mathf.Lerp(c010, c110, tx);
            float c01 = Mathf.Lerp(c001, c101, tx);
            float c11 = Mathf.Lerp(c011, c111, tx);

            float c0 = Mathf.Lerp(c00, c10, ty);
            float c1 = Mathf.Lerp(c01, c11, ty);

            return Mathf.Lerp(c0, c1, tz);
        }

        const float h = 0.5f; 
        float dx = Sample(p.x + h, p.y, p.z) - Sample(p.x - h, p.y, p.z);
        float dy = Sample(p.x, p.y + h, p.z) - Sample(p.x, p.y - h, p.z);
        float dz = Sample(p.x, p.y, p.z + h) - Sample(p.x, p.y, p.z - h);
        return -( new Vector3(dx, dy, dz)); 
    }


    private static void Fill(int[,,] a, int v)
    {
        for (int x = 0; x < a.GetLength(0); x++)
            for (int y = 0; y < a.GetLength(1); y++)
                for (int z = 0; z < a.GetLength(2); z++)
                    a[x, y, z] = v;
    }

    private static float EdgeT(float iso, float d1, float d2)
    {
        float denom = d2 - d1;
        if (Mathf.Abs(denom) < 1e-6f) return 0.5f; 
        return Mathf.Clamp01((iso - d1) / denom);
    }

    private static readonly int[,] EdgeConnections = {
        {0, 1}, {1, 2}, {2, 3}, {3, 0}, {4, 5}, {5, 6}, {6, 7}, {7, 4}, {0, 4}, {1, 5}, {2, 6}, {3, 7}
    };

    private static readonly int[,] CubePoints = {
        {0, 0, 0}, {1, 0, 0}, {1, 0, 1}, {0, 0, 1}, {0, 1, 0}, {1, 1, 0}, {1, 1, 1}, {0, 1, 1}
    };

    private static readonly short[,] TriTable = {
         {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
        {3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
        {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
        {9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
        {10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
        {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
        {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
        {2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
        {11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
        {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
        {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
        {11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
        {9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
        {6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
        {6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
        {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
        {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
        {3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
        {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
        {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
        {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
        {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
        {10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
        {10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
        {0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
        {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
        {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
        {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
        {3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
        {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
        {10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
        {7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
        {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
        {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
        {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
        {0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
        {7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
        {7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
        {10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
        {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
        {7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
        {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
        {6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
        {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
        {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
        {8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
        {1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
        {10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
        {10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
        {9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
        {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
        {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
        {7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
        {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
        {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
        {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
        {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
        {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
        {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
        {6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
        {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
        {6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
        {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
        {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
        {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
        {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
        {9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
        {1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
        {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
        {0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
        {5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
        {11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
        {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
        {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
        {2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
        {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
        {1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
        {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
        {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
        {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
        {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
        {9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
        {5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
        {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
        {9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
        {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
        {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
        {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
        {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
        {11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
        {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
        {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
        {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
        {1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
        {4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
        {0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
        {1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
    };
}

public static class MarchingTetrahedra
{
    // Match your existing cube corner layout (same as MarchingCubes.CubePoints)
    // 0:(0,0,0) 1:(1,0,0) 2:(1,0,1) 3:(0,0,1) 4:(0,1,0) 5:(1,1,0) 6:(1,1,1) 7:(0,1,1)
    static readonly int[,] CubePoints = {
        {0,0,0},{1,0,0},{1,0,1},{0,0,1},{0,1,0},{1,1,0},{1,1,1},{0,1,1}
    };

    // Split each cube into 6 tetrahedra along the 0->6 diagonal
    // This is deterministic and chunk-friendly.
    static readonly int[,] Tets = new int[,] {
        {0,1,2,6},
        {0,2,3,6},
        {0,3,7,6},
        {0,7,4,6},
        {0,4,5,6},
        {0,5,1,6},
    };

    // Edge list inside a single tetrahedron (local indices 0..3)
    // e0:(0-1), e1:(1-2), e2:(2-0), e3:(0-3), e4:(1-3), e5:(2-3)
    static readonly int[,] TetEdgeVerts = new int[,] {
        {0,1},{1,2},{2,0},{0,3},{1,3},{2,3}
    };

    // 16-case triangulation table for a single tetrahedron.
    // Each row lists edge indices; every group of three forms one triangle; -1 terminates.
    // Winding here assumes "positive-inside" and aims to produce outward-facing normals,
    // but we also provide an optional gradient-based flip to guarantee consistency.
    static readonly int[,] TriTable = new int[,] {
        { -1,-1,-1,-1,-1,-1 },           // 0  (0000) empty
        {  0, 3, 2, -1,-1,-1 },          // 1  (0001)
        {  0, 1, 4, -1,-1,-1 },          // 2  (0010)
        {  1, 4, 2,  2, 4, 3 },          // 3  (0011)
        {  1, 2, 5, -1,-1,-1 },          // 4  (0100)
        {  0, 3, 5,  0, 5, 1 },          // 5  (0101)
        {  0, 2, 5,  0, 5, 4 },          // 6  (0110)
        {  3, 5, 4, -1,-1,-1 },          // 7  (0111)
        {  3, 4, 5, -1,-1,-1 },          // 8  (1000)
        {  0, 4, 5,  0, 5, 2 },          // 9  (1001)
        {  0, 5, 3,  0, 1, 5 },          // 10 (1010)
        {  1, 5, 2, -1,-1,-1 },          // 11 (1011)
        {  2, 4, 3,  1, 2, 4 },          // 12 (1100)
        {  0, 4, 1, -1,-1,-1 },          // 13 (1101)
        {  0, 2, 3, -1,-1,-1 },          // 14 (1110)
        { -1,-1,-1,-1,-1,-1 },           // 15 (1111) full
    };

    /// <summary>
    /// Build a mesh from a VoxelData SDF (positive-inside). Simple, reliable defaults.
    /// </summary>
    public static void CreateMesh(
        VoxelData[,,] grid,
        Mesh mesh,
        float isoLevel = 0f,
        bool autoFlipWindingWithGradient = true  // keep true for bulletproof facing
    )
    {
        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;

        int W = grid.GetLength(0);
        int H = grid.GetLength(1);
        int D = grid.GetLength(2);

        var verts = new List<Vector3>(W * H * D / 3);
        var tris = new List<int>(W * H * D);

        // Walk all cubes (voxel cells)
        for (int x = 0; x < W - 1; x++)
            for (int y = 0; y < H - 1; y++)
                for (int z = 0; z < D - 1; z++)
                {
                    // Gather cube corner positions and densities
                    Vector3[] cpos = new Vector3[8];
                    float[] cd = new float[8];
                    for (int i = 0; i < 8; i++)
                    {
                        int cx = x + CubePoints[i, 0];
                        int cy = y + CubePoints[i, 1];
                        int cz = z + CubePoints[i, 2];
                        cpos[i] = new Vector3(cx, cy, cz);
                        cd[i] = grid[cx, cy, cz].density;
                    }

                    // Process the 6 tets
                    for (int t = 0; t < 6; t++)
                    {
                        int i0 = Tets[t, 0], i1 = Tets[t, 1], i2 = Tets[t, 2], i3 = Tets[t, 3];

                        // Local tet vertex arrays (positions and densities)
                        Vector3[] tp = { cpos[i0], cpos[i1], cpos[i2], cpos[i3] };
                        float[] td = { cd[i0], cd[i1], cd[i2], cd[i3] };

                        // Build a 4-bit case mask: bit set if vertex is INSIDE (solid) => density > isoLevel
                        int mask = 0;
                        if (td[0] < isoLevel) mask |= 1;
                        if (td[1] < isoLevel) mask |= 2;
                        if (td[2] < isoLevel) mask |= 4;
                        if (td[3] < isoLevel) mask |= 8;
                        if (mask == 0 || mask == 15) continue; // fully empty or full: no surface

                        // Compute up to 6 edge intersections for this tetrahedron
                        Vector3[] edgeV = new Vector3[6];
                        bool[] hit = new bool[6];

                        for (int e = 0; e < 6; e++)
                        {
                            int a = TetEdgeVerts[e, 0];
                            int b = TetEdgeVerts[e, 1];
                            float da = td[a], db = td[b];

                            // Edge crosses the iso-surface if one is inside and the other is outside
                            bool aInside = da > isoLevel;
                            bool bInside = db > isoLevel;
                            if (aInside != bInside)
                            {
                                float tLerp = EdgeT(isoLevel, da, db);        // NO clamp
                                edgeV[e] = Vector3.Lerp(tp[a], tp[b], tLerp);  // intersection point
                                hit[e] = true;
                            }
                        }

                        // Emit triangles for this case
                        for (int k = 0; k < 6; k += 3)
                        {
                            int e0 = TriTable[mask, k];
                            if (e0 == -1) break;
                            int e1 = TriTable[mask, k + 1];
                            int e2 = TriTable[mask, k + 2];

                            if (!hit[e0] || !hit[e1] || !hit[e2])
                                continue; // guard (shouldn't happen with correct table/signs)

                            // Triangle vertices
                            Vector3 a = edgeV[e0];
                            Vector3 b = edgeV[e1];
                            Vector3 c = edgeV[e2];

                            // Optional: ensure consistent outward-facing winding by using the density gradient
                            if (autoFlipWindingWithGradient)
                            {
                                Vector3 nGeom = Vector3.Cross(b - a, c - a);
                                Vector3 pCent = (a + b + c) * (1f / 3f);
                                Vector3 grad = -DensityGradient(grid, pCent); // points toward *increasing* density

                                // Positive-inside: outward should align with +grad. If opposite, flip b/c.
                                if (Vector3.Dot(nGeom, grad) < 0f)
                                {
                                    var tmp = b; b = c; c = tmp;
                                }
                            }

                            int baseIdx = verts.Count;
                            verts.Add(a); verts.Add(b); verts.Add(c);
                            tris.Add(baseIdx + 0);
                            tris.Add(baseIdx + 1);
                            tris.Add(baseIdx + 2);
                        }
                    }
                }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // --- helpers ---

    // Unclamped interpolation factor where the field crosses iso between d1 and d2.
    static float EdgeT(float iso, float d1, float d2)
    {
        float denom = d2 - d1;
        if (Mathf.Abs(denom) < 1e-6f) return 0.5f; // degenerate case
        return (iso - d1) / denom; // DO NOT clamp—prevents tiny cracks
    }

    // Trilinear sampling + central-difference gradient for flip check
    static Vector3 DensityGradient(VoxelData[,,] grid, Vector3 p)
    {
        const float h = 0.5f;
        float dx = SampleDensity(grid, p + new Vector3(h, 0, 0)) - SampleDensity(grid, p + new Vector3(-h, 0, 0));
        float dy = SampleDensity(grid, p + new Vector3(0, h, 0)) - SampleDensity(grid, p + new Vector3(0, -h, 0));
        float dz = SampleDensity(grid, p + new Vector3(0, 0, h)) - SampleDensity(grid, p + new Vector3(0, 0, -h));
        return new Vector3(dx, dy, dz);
    }

    static float SampleDensity(VoxelData[,,] grid, Vector3 p)
    {
        int W = grid.GetLength(0);
        int H = grid.GetLength(1);
        int D = grid.GetLength(2);

        // Clamp into valid cell for tri-linear
        float x = Mathf.Clamp(p.x, 0f, W - 1.001f);
        float y = Mathf.Clamp(p.y, 0f, H - 1.001f);
        float z = Mathf.Clamp(p.z, 0f, D - 1.001f);

        int x0 = (int)x, y0 = (int)y, z0 = (int)z;
        int x1 = Mathf.Min(x0 + 1, W - 1);
        int y1 = Mathf.Min(y0 + 1, H - 1);
        int z1 = Mathf.Min(z0 + 1, D - 1);

        float tx = x - x0, ty = y - y0, tz = z - z0;

        float c000 = grid[x0, y0, z0].density;
        float c100 = grid[x1, y0, z0].density;
        float c010 = grid[x0, y1, z0].density;
        float c110 = grid[x1, y1, z0].density;
        float c001 = grid[x0, y0, z1].density;
        float c101 = grid[x1, y0, z1].density;
        float c011 = grid[x0, y1, z1].density;
        float c111 = grid[x1, y1, z1].density;

        float c00 = Mathf.Lerp(c000, c100, tx);
        float c10 = Mathf.Lerp(c010, c110, tx);
        float c01 = Mathf.Lerp(c001, c101, tx);
        float c11 = Mathf.Lerp(c011, c111, tx);

        float c0 = Mathf.Lerp(c00, c10, ty);
        float c1 = Mathf.Lerp(c01, c11, ty);

        return Mathf.Lerp(c0, c1, tz);
    }
}

