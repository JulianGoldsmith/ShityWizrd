using UnityEngine;

public class CorridorBuilder : MonoBehaviour
{
    public Transform corridorParent;

    [Space(10)]
    [Tooltip("walls on the east and west sides, opening should face North")]
    public GameObject straightPiece;
    [Tooltip("walls on the south and west sides, opening should face North east")]
    public GameObject cornerPiece;
    [Tooltip("walls on three sides opening should face north")]
    public GameObject endPiece;
    [Tooltip("wall on the south sidem,  blabk face north,east, and west")]
    public GameObject tJunctionPiece;
    [Tooltip("4-way intersections.")]
    public GameObject intersectionPiece;

    public void BuildCorridors(int[,] grid, Vector2Int gridSize)
    {
        if (grid == null)
        {
            Debug.LogError("corridor builder - no grid");
            return;
        }

        if (corridorParent == null)
        {
            corridorParent = new GameObject("Corridors").transform;
            corridorParent.SetParent(this.transform);
        }
        for (int i = corridorParent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(corridorParent.GetChild(i).gameObject);
        }

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                if (grid[x, y] < 2)
                {
                    continue;
                }
                int mask = 0;
                // North
                if (y + 1 < gridSize.y && grid[x, y + 1] != 0) mask += 1;
                // East
                if (x + 1 < gridSize.x && grid[x + 1, y] != 0) mask += 2;
                // South
                if (y - 1 >= 0 && grid[x, y - 1] != 0) mask += 4;
                // West
                if (x - 1 >= 0 && grid[x - 1, y] != 0) mask += 8;

                PlaceCorridorPiece(x, y, mask);
            }
        }
    }

    private void PlaceCorridorPiece(int x, int y, int mask)
    {
        GameObject pieceToPlace = null;
        Quaternion rotation = Quaternion.identity;

        switch (mask)
        {
            case 0: pieceToPlace = intersectionPiece; break;

            case 1: pieceToPlace = endPiece; rotation = Quaternion.Euler(0, 0, 0); break;    // North
            case 2: pieceToPlace = endPiece; rotation = Quaternion.Euler(0, 90, 0); break;   // East
            case 4: pieceToPlace = endPiece; rotation = Quaternion.Euler(0, 180, 0); break;  // South
            case 8: pieceToPlace = endPiece; rotation = Quaternion.Euler(0, -90, 0); break;  // West

            case 5: pieceToPlace = straightPiece; rotation = Quaternion.Euler(0, 0, 0); break;    // North-South
            case 10: pieceToPlace = straightPiece; rotation = Quaternion.Euler(0, 90, 0); break;   // East-West

            case 3: pieceToPlace = cornerPiece; rotation = Quaternion.Euler(0, 0, 0); break;    // North-East
            case 6: pieceToPlace = cornerPiece; rotation = Quaternion.Euler(0, 90, 0); break;   // East-South
            case 12: pieceToPlace = cornerPiece; rotation = Quaternion.Euler(0, 180, 0); break;  // South-West
            case 9: pieceToPlace = cornerPiece; rotation = Quaternion.Euler(0, -90, 0); break;  // West-North

            case 7: pieceToPlace = tJunctionPiece; rotation = Quaternion.Euler(0, 90, 0); break;   // North-East-South
            case 11: pieceToPlace = tJunctionPiece; rotation = Quaternion.Euler(0, 0, 0); break;    // North-East-West
            case 13: pieceToPlace = tJunctionPiece; rotation = Quaternion.Euler(0, -90, 0); break;  // North-South-West
            case 14: pieceToPlace = tJunctionPiece; rotation = Quaternion.Euler(0, 180, 0); break;  // East-South-West

            case 15: pieceToPlace = intersectionPiece; break;
        }

        if (pieceToPlace != null)
        {
            Vector3 position = new Vector3(x , 0, y );
            Instantiate(pieceToPlace, position, rotation, corridorParent);
        }
        else
        {
            Debug.LogWarning($"<color=red>No piece found for bitmask {mask} - ({x},{y}) placed default floor</color>");
            Vector3 position = new Vector3(x , 0, y );
            Instantiate(intersectionPiece, position, Quaternion.identity, corridorParent);
        }
    }
}