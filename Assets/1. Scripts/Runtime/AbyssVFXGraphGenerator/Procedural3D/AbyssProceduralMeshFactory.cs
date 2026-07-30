using UnityEngine;

namespace ProjectAbyss.VFX.Procedural3D
{
    public static class AbyssProceduralMeshFactory
    {
        public static Mesh CreateTorus(float majorRadius, float minorRadius, int radialSegments = 64, int tubeSegments = 18)
        {
            radialSegments = Mathf.Max(3, radialSegments);
            tubeSegments = Mathf.Max(3, tubeSegments);
            int vertCount = (radialSegments + 1) * (tubeSegments + 1);
            Vector3[] vertices = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] triangles = new int[radialSegments * tubeSegments * 6];
            int v = 0;
            for (int i = 0; i <= radialSegments; i++)
            {
                float u = i / (float)radialSegments * Mathf.PI * 2f;
                Vector3 center = new Vector3(Mathf.Cos(u) * majorRadius, 0f, Mathf.Sin(u) * majorRadius);
                Vector3 outward = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                Vector3 up = Vector3.up;
                for (int j = 0; j <= tubeSegments; j++)
                {
                    float t = j / (float)tubeSegments * Mathf.PI * 2f;
                    Vector3 localNormal = Mathf.Cos(t) * outward + Mathf.Sin(t) * up;
                    vertices[v] = center + localNormal * minorRadius;
                    normals[v] = localNormal.normalized;
                    uvs[v] = new Vector2(i / (float)radialSegments, j / (float)tubeSegments);
                    v++;
                }
            }
            int ti = 0;
            int row = tubeSegments + 1;
            for (int i = 0; i < radialSegments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    int a = i * row + j;
                    int b = (i + 1) * row + j;
                    int c = (i + 1) * row + j + 1;
                    int d = i * row + j + 1;
                    triangles[ti++] = a; triangles[ti++] = b; triangles[ti++] = c;
                    triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = d;
                }
            }
            Mesh mesh = new Mesh();
            mesh.name = "ProceduralTorus";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
#if UNITY_EDITOR
            Object.DestroyImmediate(temp);
#else
            Object.Destroy(temp);
#endif
            return mesh;
        }
    }
}
