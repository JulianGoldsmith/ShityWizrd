using UnityEngine;
using System.Collections.Generic;

public static class RandomDistributionPointSampler
{

    public static List<Vector3> GeneratePoints(float roomRadius, float minRadius, Vector3Int boundsSize, int numSamplesBeforeRejection = 30, Vector3? startPoint = null)
    {

        //means only one point per cell max 
        float cellSize = minRadius / Mathf.Sqrt(3); 
        int[,,] grid = new int[Mathf.CeilToInt(boundsSize.x / cellSize), Mathf.CeilToInt(boundsSize.y / cellSize), Mathf.CeilToInt(boundsSize.z / cellSize)];

        List<Vector3> points = new List<Vector3>();
        List<Vector3> activePoints = new List<Vector3>();

        Vector3 initialPoint;
        if (startPoint.HasValue)
        {
            initialPoint = startPoint.Value;
        }
        else
        {
            initialPoint = new Vector3(Random.Range(roomRadius, boundsSize.x - roomRadius), Random.Range(roomRadius, boundsSize.y - roomRadius), Random.Range(roomRadius, boundsSize.z - roomRadius));
        }


        activePoints.Add(initialPoint);
        points.Add(initialPoint);

        int initialGridX = (int)(initialPoint.x / cellSize);
        int initialGridY = (int)(initialPoint.y / cellSize);
        int initialGridZ = (int)(initialPoint.z / cellSize);
        grid[initialGridX, initialGridY, initialGridZ] = points.Count;

        while (activePoints.Count > 0)
        {
            int randomIndex = Random.Range(0, activePoints.Count);
            Vector3 currentPoint = activePoints[randomIndex];
            bool foundValidPoint = false;

            for (int i = 0; i < numSamplesBeforeRejection; i++)
            {
                Vector3 randomDirection = Random.onUnitSphere; 
                float randomDistance = Random.Range(minRadius, 2 * minRadius);
                Vector3 candidatePoint = currentPoint + randomDirection * randomDistance;

                if (IsValid(candidatePoint, roomRadius,boundsSize, cellSize, minRadius, points, grid))
                {
                    points.Add(candidatePoint);
                    activePoints.Add(candidatePoint);
                    int gridX = (int)(candidatePoint.x / cellSize);
                    int gridY = (int)(candidatePoint.y / cellSize);
                    int gridZ = (int)(candidatePoint.z / cellSize);
                    grid[gridX, gridY, gridZ] = points.Count;

                    foundValidPoint = true;
                    break; 
                }
            }

            if (!foundValidPoint)
            {
                activePoints.RemoveAt(randomIndex);
            }
        }

        return points;
    }

    static bool IsValid(Vector3 candidate, float roomRadius, Vector3Int bounds, float cellSize, float minRadius, List<Vector3> points, int[,,] grid)
    {
        if (candidate.x - roomRadius < 0 || candidate.x + roomRadius >= bounds.x ||
        candidate.y - roomRadius < 0 || candidate.y + roomRadius >= bounds.y ||
        candidate.z - roomRadius < 0 || candidate.z + roomRadius >= bounds.z)
        {
            return false;
        }

        int gridX = (int)(candidate.x / cellSize);
        int gridY = (int)(candidate.y / cellSize);
        int gridZ = (int)(candidate.z / cellSize);


        for (int z = -2; z <= 2; z++)
        {
            for (int y = -2; y <= 2; y++)
            {
                for (int x = -2; x <= 2; x++)
                {
                    int checkX = gridX + x;
                    int checkY = gridY + y;
                    int checkZ = gridZ + z;

                    if (checkX >= 0 && checkX < grid.GetLength(0) && checkY >= 0 && checkY < grid.GetLength(1) && checkZ >= 0 && checkZ < grid.GetLength(2))
                    {
                        int pointIndex = grid[checkX, checkY, checkZ] - 1;

                        if (pointIndex != -1)
                        {
                            float dist = Vector3.Distance(candidate, points[pointIndex]);
                            if (dist < minRadius)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
        }

        return true;
    }
}
