#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    /// <summary>
    /// Procedural mesh mutations are kept transactional so a later VFX Graph failure
    /// cannot leave partially-updated mesh assets behind.
    /// </summary>
    internal sealed class AbyssVFXMeshAssetTransaction
    {
        private sealed class Entry
        {
            internal string path;
            internal Mesh backup;
            internal bool existed;
        }

        private readonly List<Entry> entries = new();
        private bool finished;

        internal void RecordExisting(string path, Mesh existing)
        {
            if (finished || existing == null || Contains(path)) return;
            Mesh backup = UnityEngine.Object.Instantiate(existing);
            backup.name = existing.name;
            backup.hideFlags = HideFlags.HideAndDontSave;
            entries.Add(new Entry { path = path, backup = backup, existed = true });
        }

        internal void RecordCreated(string path)
        {
            if (finished || Contains(path)) return;
            entries.Add(new Entry { path = path, existed = false });
        }

        internal void Commit()
        {
            if (finished) return;
            finished = true;
            ReleaseBackups();
            entries.Clear();
        }

        internal void Rollback()
        {
            if (finished) return;
            finished = true;
            try
            {
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    Entry entry = entries[i];
                    if (entry.existed)
                    {
                        Mesh current = AssetDatabase.LoadAssetAtPath<Mesh>(entry.path);
                        if (current != null && entry.backup != null)
                        {
                            EditorUtility.CopySerialized(entry.backup, current);
                            current.name = entry.backup.name;
                            EditorUtility.SetDirty(current);
                        }
                    }
                    else if (AssetDatabase.LoadMainAssetAtPath(entry.path) != null)
                    {
                        AssetDatabase.DeleteAsset(entry.path);
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                ReleaseBackups();
                entries.Clear();
            }
        }

        private bool Contains(string path)
        {
            return entries.Exists(entry => string.Equals(entry.path, path, StringComparison.OrdinalIgnoreCase));
        }

        private void ReleaseBackups()
        {
            foreach (Entry entry in entries)
                if (entry.backup != null)
                    UnityEngine.Object.DestroyImmediate(entry.backup);
        }
    }

    internal static class AbyssVFXMeshAssetFactory
    {
        internal static void Prepare(
            AbyssVFXRecipe recipe,
            string folder,
            bool overwrite,
            List<string> diagnostics,
            AbyssVFXMeshAssetTransaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            for (int i = 0; i < recipe.systems.Count; i++)
            {
                AbyssVFXSystemRecipe system = recipe.systems[i];
                if (system == null || string.Equals(system.proceduralMesh, "Asset", StringComparison.OrdinalIgnoreCase)) continue;

                Mesh generated = Create(system) ?? throw new InvalidOperationException(system.name + ": Procedural Mesh 생성 실패.");
                string meshName = $"MSH_{AbyssVFXRecipeUtility.SanitizeAssetName(recipe.name)}_{i + 1:00}_{system.proceduralMesh}";
                generated.name = meshName;
                string path = $"{folder}/{meshName}.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

                if (existing != null)
                {
                    if (!overwrite)
                    {
                        path = AssetDatabase.GenerateUniqueAssetPath(path);
                    }
                    else
                    {
                        transaction.RecordExisting(path, existing);
                        EditorUtility.CopySerialized(generated, existing);
                        existing.name = meshName;
                        EditorUtility.SetDirty(existing);
                        UnityEngine.Object.DestroyImmediate(generated);
                        system.meshAssetPath = path;
                        system.meshSubAssetName = string.Empty;
                        diagnostics.Add("Mesh updated in place (GUID preserved): " + path);
                        continue;
                    }
                }

                transaction.RecordCreated(path);
                AssetDatabase.CreateAsset(generated, path);
                system.meshAssetPath = path;
                system.meshSubAssetName = string.Empty;
                diagnostics.Add("Mesh created: " + path);
            }
            AssetDatabase.SaveAssets();
        }

        private static Mesh Create(AbyssVFXSystemRecipe system)
        {
            if (system.proceduralMesh == "Torus")
                return CreateTorus(system.torusMajorRadius, system.torusMinorRadius, system.torusRadialSegments, system.torusTubeSegments);
            if (system.proceduralMesh == "ArcBlade") return CreateArcBlade();
            if (system.proceduralMesh == "Shard") return CreateShard();

            PrimitiveType primitive = system.proceduralMesh switch
            {
                "Cube" => PrimitiveType.Cube,
                "Cylinder" => PrimitiveType.Cylinder,
                "Capsule" => PrimitiveType.Capsule,
                _ => PrimitiveType.Sphere
            };
            GameObject temp = GameObject.CreatePrimitive(primitive);
            try
            {
                Mesh source = temp.GetComponent<MeshFilter>()?.sharedMesh;
                return source == null ? null : UnityEngine.Object.Instantiate(source);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private static Mesh CreateShard()
        {
            Vector3[] vertices =
            {
                new(0f, 0.65f, 0f), new(-0.16f, -0.45f, -0.12f),
                new(0.16f, -0.45f, -0.12f), new(0f, -0.45f, 0.18f)
            };
            int[] triangles = { 0, 1, 2, 0, 2, 3, 0, 3, 1, 1, 3, 2 };
            Mesh mesh = new() { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateArcBlade()
        {
            const int segments = 28;
            const float innerRadius = 0.32f;
            const float outerRadius = 0.78f;
            const float halfThickness = 0.035f;
            const float arcDegrees = 125f;
            const int stride = 4;

            Vector3[] vertices = new Vector3[(segments + 1) * stride];
            Vector2[] uvs = new Vector2[vertices.Length];
            List<int> triangles = new();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = (-arcDegrees * 0.5f + arcDegrees * t) * Mathf.Deg2Rad;
                Vector3 radial = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                int v = i * stride;
                vertices[v + 0] = radial * innerRadius + Vector3.forward * halfThickness;
                vertices[v + 1] = radial * outerRadius + Vector3.forward * halfThickness;
                vertices[v + 2] = radial * innerRadius - Vector3.forward * halfThickness;
                vertices[v + 3] = radial * outerRadius - Vector3.forward * halfThickness;
                uvs[v + 0] = new Vector2(t, 0f);
                uvs[v + 1] = new Vector2(t, 1f);
                uvs[v + 2] = new Vector2(t, 0f);
                uvs[v + 3] = new Vector2(t, 1f);
                if (i == segments) continue;

                int n = v + stride;
                AddQuad(triangles, v + 0, n + 0, n + 1, v + 1);
                AddQuad(triangles, v + 3, n + 3, n + 2, v + 2);
                AddQuad(triangles, v + 2, n + 2, n + 0, v + 0);
                AddQuad(triangles, v + 1, n + 1, n + 3, v + 3);
            }
            AddQuad(triangles, 0, 1, 3, 2);
            int end = segments * stride;
            AddQuad(triangles, end + 2, end + 3, end + 1, end + 0);
            Mesh mesh = new() { vertices = vertices, uv = uvs, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private static Mesh CreateTorus(float majorRadius, float minorRadius, int radialSegments, int tubeSegments)
        {
            radialSegments = Mathf.Clamp(radialSegments, 3, 256);
            tubeSegments = Mathf.Clamp(tubeSegments, 3, 128);
            int row = tubeSegments + 1;
            Vector3[] vertices = new Vector3[(radialSegments + 1) * row];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[radialSegments * tubeSegments * 6];
            int vi = 0;
            for (int radial = 0; radial <= radialSegments; radial++)
            {
                float u = radial / (float)radialSegments * Mathf.PI * 2f;
                Vector3 outward = new(Mathf.Cos(u), 0f, Mathf.Sin(u));
                Vector3 center = outward * majorRadius;
                for (int tube = 0; tube <= tubeSegments; tube++)
                {
                    float v = tube / (float)tubeSegments * Mathf.PI * 2f;
                    Vector3 normal = outward * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    vertices[vi] = center + normal * minorRadius;
                    normals[vi] = normal.normalized;
                    uvs[vi] = new Vector2(radial / (float)radialSegments, tube / (float)tubeSegments);
                    vi++;
                }
            }

            int ti = 0;
            for (int radial = 0; radial < radialSegments; radial++)
            {
                for (int tube = 0; tube < tubeSegments; tube++)
                {
                    int a = radial * row + tube;
                    int b = (radial + 1) * row + tube;
                    int c = b + 1;
                    int d = a + 1;
                    triangles[ti++] = a; triangles[ti++] = b; triangles[ti++] = c;
                    triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = d;
                }
            }
            Mesh mesh = new() { vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
