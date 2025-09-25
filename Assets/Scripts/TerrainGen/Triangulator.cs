using UnityEngine;
using System.Collections.Generic;

//This is a copy of the Bowyer–Watson algorithm for delaunay triangulation

public static class DelaunayTriangulator
{
    public static List<Triangle> Triangulate(List<Vector2> points)
    {
        List<Triangle> triangles = new List<Triangle>();
        float minX = points[0].x;
        float minY = points[0].y;
        float maxX = minX;
        float maxY = minY;

        foreach (var point in points)
        {
            if (point.x < minX) minX = point.x;
            if (point.x > maxX) maxX = point.x;
            if (point.y < minY) minY = point.y;
            if (point.y > maxY) maxY = point.y;
        }

        float dx = maxX - minX;
        float dy = maxY - minY;
        float deltaMax = Mathf.Max(dx, dy);
        float midx = (minX + maxX) / 2f;
        float midy = (minY + maxY) / 2f;

        Vector2 p1 = new Vector2(midx - 20 * deltaMax, midy - deltaMax);
        Vector2 p2 = new Vector2(midx, midy + 20 * deltaMax);
        Vector2 p3 = new Vector2(midx + 20 * deltaMax, midy - deltaMax);

        triangles.Add(new Triangle(p1, p2, p3));

        foreach (var point in points)
        {
            List<Edge> polygon = new List<Edge>();
            List<Triangle> badTriangles = new List<Triangle>();

            foreach (var t in triangles)
            {
                if (t.IsPointInCircumcircle(point))
                {
                    badTriangles.Add(t);
                    polygon.Add(new Edge(t.p1, t.p2));
                    polygon.Add(new Edge(t.p2, t.p3));
                    polygon.Add(new Edge(t.p3, t.p1));
                }
            }

            triangles.RemoveAll(t => badTriangles.Contains(t));

            for (int i = 0; i < polygon.Count; i++)
            {
                for (int j = i + 1; j < polygon.Count; j++)
                {
                    if (polygon[i].Equals(polygon[j]))
                    {
                        Edge edge1 = polygon[i];
                        Edge edge2 = polygon[j];

                        edge1.isBad = true;
                        edge2.isBad = true;

                        polygon[i] = edge1;
                        polygon[j] = edge2;
                    }
                }
            }

            polygon.RemoveAll(e => e.isBad);

            foreach (var edge in polygon)
            {
                triangles.Add(new Triangle(edge.p1, edge.p2, point));
            }
        }

        triangles.RemoveAll(t => t.ContainsVertex(p1) || t.ContainsVertex(p2) || t.ContainsVertex(p3));
        return triangles;
    }
}

public struct Triangle
{
    public Vector2 p1, p2, p3;
    private Edge e1, e2, e3;
    private Vector2 circumcenter;
    private float circumradiusSq;

    public Triangle(Vector2 v1, Vector2 v2, Vector2 v3)
    {
        p1 = v1;
        p2 = v2;
        p3 = v3;
        e1 = new Edge(p1, p2);
        e2 = new Edge(p2, p3);
        e3 = new Edge(p3, p1);

        float D = 2 * (p1.x * (p2.y - p3.y) + p2.x * (p3.y - p1.y) + p3.x * (p1.y - p2.y));
        float ux = ((p1.x * p1.x + p1.y * p1.y) * (p2.y - p3.y) + (p2.x * p2.x + p2.y * p2.y) * (p3.y - p1.y) + (p3.x * p3.x + p3.y * p3.y) * (p1.y - p2.y)) / D;
        float uy = ((p1.x * p1.x + p1.y * p1.y) * (p3.x - p2.x) + (p2.x * p2.x + p2.y * p2.y) * (p1.x - p3.x) + (p3.x * p3.x + p3.y * p3.y) * (p2.x - p1.x)) / D;
        circumcenter = new Vector2(ux, uy);
        circumradiusSq = (p1.x - ux) * (p1.x - ux) + (p1.y - uy) * (p1.y - uy);
    }

    public bool IsPointInCircumcircle(Vector2 point)
    {
        float distSq = (point.x - circumcenter.x) * (point.x - circumcenter.x) + (point.y - circumcenter.y) * (point.y - circumcenter.y);
        return distSq < circumradiusSq;
    }

    public bool ContainsVertex(Vector2 v) => p1 == v || p2 == v || p3 == v;
}

public struct Edge
{
    public Vector2 p1, p2;
    public bool isBad;

    public Edge(Vector2 v1, Vector2 v2)
    {
        p1 = v1;
        p2 = v2;
        isBad = false;
    }

    public bool Equals(Edge other)
    {
        return (p1 == other.p1 && p2 == other.p2) || (p1 == other.p2 && p2 == other.p1);
    }
}