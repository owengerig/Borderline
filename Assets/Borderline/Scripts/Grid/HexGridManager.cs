using System.Collections.Generic;
using UnityEngine;

namespace Borderline
{
    /// <summary>
    /// Generates a rectangular pointy-top hex board (odd-r offset layout, stored in axial q,r),
    /// centred on the world origin, and answers spatial queries (lookup, neighbours, pixel->hex).
    ///
    /// Later, for the GPS version, this class is what you replace with a lookup into a
    /// global hex index (e.g. H3 cells) - the rest of the game only talks to HexTile + these queries.
    /// </summary>
    public class HexGridManager : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField, Min(3)] int width = 15;
        [SerializeField, Min(3)] int height = 15;
        [Tooltip("Distance from a hex centre to a corner, in world units.")]
        [SerializeField, Min(0.1f)] float cellRadius = 0.5f;

        [Header("Prefab & Material")]
        [Tooltip("Optional. If empty, tiles are created from scratch.")]
        [SerializeField] HexTile tilePrefab;
        [Tooltip("Material using 'Universal Render Pipeline/2D/Sprite-Unlit-Default'.")]
        [SerializeField] Material tileMaterial;

        static readonly float Sqrt3 = Mathf.Sqrt(3f);

        readonly Dictionary<HexCoord, HexTile> tiles = new Dictionary<HexCoord, HexTile>();
        readonly List<HexTile> tileList = new List<HexTile>();
        Vector2 origin;
        Bounds worldBounds;

        public int Width => width;
        public int Height => height;
        public float CellRadius => cellRadius;
        public IReadOnlyList<HexTile> Tiles => tileList;
        public int TileCount => tileList.Count;
        public Bounds WorldBounds => worldBounds;

        public Material TileMaterial
        {
            get
            {
                if (tileMaterial == null)
                {
                    // URP (2D Renderer): the project's pipeline. Sprites/Default is only a last-resort fallback.
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    tileMaterial = new Material(shader) { name = "Hex (runtime)" };
                }
                return tileMaterial;
            }
        }

        // ------------------------------------------------------------------ generation

        public void Generate()
        {
            Clear();

            // Pass 1: find extents so the board can be centred on (0,0).
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            origin = Vector2.zero;
            for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
            {
                Vector2 p = AxialToWorld(OffsetToAxial(col, row));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            origin = -(min + max) * 0.5f;

            // Pass 2: spawn tiles.
            Material material = TileMaterial;
            for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
            {
                HexCoord coord = OffsetToAxial(col, row);
                HexTile tile = tilePrefab != null
                    ? Instantiate(tilePrefab, transform)
                    : new GameObject("Hex").AddComponent<HexTile>();
                tile.transform.SetParent(transform, false);
                tile.transform.localPosition = AxialToWorld(coord);
                tile.Init(coord, cellRadius, material);
                tiles.Add(coord, tile);
                tileList.Add(tile);
            }

            Vector2 size = (max - min) + new Vector2(cellRadius * 2f, cellRadius * 2f);
            worldBounds = new Bounds((Vector3)transform.position, new Vector3(size.x, size.y, 0f));
        }

        void Clear()
        {
            foreach (var tile in tileList)
                if (tile != null) Destroy(tile.gameObject);
            tiles.Clear();
            tileList.Clear();
        }

        // ------------------------------------------------------------------ coordinates

        /// <summary>"odd-r" offset (col,row) -> axial. Odd rows are shoved right by half a hex.</summary>
        public static HexCoord OffsetToAxial(int col, int row) => new HexCoord(col - (row - (row & 1)) / 2, row);

        /// <summary>Axial -> local position (pointy-top).</summary>
        public Vector2 AxialToWorld(HexCoord c)
        {
            float x = cellRadius * Sqrt3 * (c.q + c.r * 0.5f);
            float y = cellRadius * 1.5f * c.r;
            return new Vector2(x, y) + origin;
        }

        /// <summary>World position -> nearest axial coordinate.</summary>
        public HexCoord WorldToAxial(Vector2 world)
        {
            Vector2 p = world - (Vector2)transform.position - origin;
            float q = (Sqrt3 / 3f * p.x - 1f / 3f * p.y) / cellRadius;
            float r = (2f / 3f * p.y) / cellRadius;
            return HexCoord.Round(q, r);
        }

        // ------------------------------------------------------------------ queries

        public HexTile GetTile(HexCoord coord) => tiles.TryGetValue(coord, out var t) ? t : null;

        public HexTile GetTileAtOffset(int col, int row)
        {
            col = Mathf.Clamp(col, 0, width - 1);
            row = Mathf.Clamp(row, 0, height - 1);
            return GetTile(OffsetToAxial(col, row));
        }

        public bool TryGetTileAtWorld(Vector2 world, out HexTile tile) => tiles.TryGetValue(WorldToAxial(world), out tile);

        public void GetNeighbors(HexTile tile, List<HexTile> results)
        {
            results.Clear();
            for (int d = 0; d < 6; d++)
                if (tiles.TryGetValue(tile.Coord.Neighbor(d), out var n)) results.Add(n);
        }

        public bool IsAdjacentToOwner(HexTile tile, int owner)
        {
            for (int d = 0; d < 6; d++)
                if (tiles.TryGetValue(tile.Coord.Neighbor(d), out var n) && n.OwnerID == owner) return true;
            return false;
        }

        public int CountNeighborsOwnedBy(HexTile tile, int owner)
        {
            int count = 0;
            for (int d = 0; d < 6; d++)
                if (tiles.TryGetValue(tile.Coord.Neighbor(d), out var n) && n.OwnerID == owner) count++;
            return count;
        }

        public int CountOwned(int owner)
        {
            int count = 0;
            foreach (var t in tileList) if (t.OwnerID == owner) count++;
            return count;
        }

        public float TerritoryPercent(int owner) => tileList.Count == 0 ? 0f : 100f * CountOwned(owner) / tileList.Count;
    }
}
