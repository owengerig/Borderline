using UnityEngine;

namespace Borderline
{
    /// <summary>
    /// Builds simple pointy-top hex meshes in code so the prototype needs no sprite art.
    /// Colours are written as vertex colours, which both "Sprites/Default" (Built-in)
    /// and "Universal Render Pipeline/2D/Sprite-Unlit-Default" (URP) multiply in.
    /// </summary>
    public static class HexMesh
    {
        /// <summary>Corner i (0..5) of a pointy-top hex with the given outer radius.</summary>
        public static Vector3 Corner(float radius, int i)
        {
            float angle = Mathf.Deg2Rad * (60f * i - 30f);
            return new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0f);
        }

        public static Mesh CreateHex(float radius)
        {
            var vertices = new Vector3[7];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < 6; i++) vertices[i + 1] = Corner(radius, i);

            var triangles = new int[18];
            for (int i = 0; i < 6; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % 6;
                triangles[i * 3 + 2] = 1 + i;
            }

            var mesh = new Mesh { name = "Hex" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            SetColor(mesh, Color.white);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A hexagonal ring (outline) between innerRadius and outerRadius.</summary>
        public static Mesh CreateRing(float innerRadius, float outerRadius)
        {
            var vertices = new Vector3[12];
            for (int i = 0; i < 6; i++)
            {
                vertices[i] = Corner(outerRadius, i);
                vertices[i + 6] = Corner(innerRadius, i);
            }

            var triangles = new int[36];
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                int t = i * 6;
                triangles[t] = i;
                triangles[t + 1] = i + 6;
                triangles[t + 2] = j;
                triangles[t + 3] = j;
                triangles[t + 4] = i + 6;
                triangles[t + 5] = j + 6;
            }

            var mesh = new Mesh { name = "HexRing" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            SetColor(mesh, Color.white);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void SetColor(Mesh mesh, Color color, Color[] buffer = null)
        {
            if (buffer == null || buffer.Length != mesh.vertexCount) buffer = new Color[mesh.vertexCount];
            for (int i = 0; i < buffer.Length; i++) buffer[i] = color;
            mesh.colors = buffer;
        }
    }
}
