using UnityEngine;

namespace Borderline
{
    /// <summary>
    /// Pulsing ring that shows where a side is "physically" standing.
    /// In the prototype you move it with the Set Location button; later it follows GPS.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PresenceMarker : MonoBehaviour
    {
        [SerializeField] float pulseSpeed = 3f;
        [SerializeField] float pulseAmount = 0.06f;

        Mesh mesh;

        public HexTile Target { get; private set; }

        public static PresenceMarker Create(string name, float cellRadius, Material material, Color color, int sortingOrder)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            var marker = go.AddComponent<PresenceMarker>();
            marker.mesh = HexMesh.CreateRing(cellRadius * 0.74f, cellRadius * 0.93f);
            HexMesh.SetColor(marker.mesh, color);
            go.GetComponent<MeshFilter>().sharedMesh = marker.mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingOrder = sortingOrder;
            go.SetActive(false);
            return marker;
        }

        public void Follow(HexTile tile)
        {
            Target = tile;
            gameObject.SetActive(tile != null);
            if (tile != null) transform.position = tile.transform.position + new Vector3(0f, 0f, -0.02f);
        }

        void Update()
        {
            float s = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = new Vector3(s, s, 1f);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
