using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Flat symbol plaques (circle, square, triangle, diamond) for the channels, saved as owned mesh
    /// assets. Each faces +Z, is about 0.5 m across and 0.06 m thick. Existing assets are updated in place.
    /// </summary>
    static class WorkshopMeshFactory
    {
        const float Size = 0.25f;
        const float HalfDepth = 0.03f;

        public static Mesh Circle => Save("Symbol Circle", Polygon(32, Size, 0f));
        public static Mesh Square => Save("Symbol Square", Polygon(4, Size * 1.3f, 45f));
        public static Mesh Triangle => Save("Symbol Triangle", Polygon(3, Size * 1.25f, 0f));
        public static Mesh Diamond => Save("Symbol Diamond", Polygon(4, Size * 1.25f, 0f));

        /// <summary>Outline points of a regular polygon in the XY plane; the first point is straight up, rotated by <paramref name="angle"/>.</summary>
        static Vector2[] Polygon(int sides, float radius, float angle)
        {
            var points = new Vector2[sides];
            for (var i = 0; i < sides; i++)
            {
                var a = Mathf.Deg2Rad * (90f + angle + i * 360f / sides);
                points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return points;
        }

        /// <summary>Extrudes a convex outline into a prism with flat-shaded front, back and sides.</summary>
        static void Build(Mesh mesh, Vector2[] outline)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            var n = outline.Length;

            void Cap(float z, Vector3 normal, bool reverse)
            {
                var start = vertices.Count;
                vertices.Add(new Vector3(0f, 0f, z));
                normals.Add(normal);
                foreach (var p in outline)
                {
                    vertices.Add(new Vector3(p.x, p.y, z));
                    normals.Add(normal);
                }
                for (var i = 0; i < n; i++)
                {
                    var a = start + 1 + i;
                    var b = start + 1 + (i + 1) % n;
                    if (reverse)
                        triangles.AddRange(new[] { start, b, a });
                    else
                        triangles.AddRange(new[] { start, a, b });
                }
            }

            Cap(-HalfDepth, Vector3.back, true);
            Cap(HalfDepth, Vector3.forward, false);

            for (var i = 0; i < n; i++)
            {
                var p0 = outline[i];
                var p1 = outline[(i + 1) % n];
                var edge = p1 - p0;
                var normal = new Vector3(edge.y, -edge.x, 0f).normalized;
                if (Vector2.Dot(new Vector2(normal.x, normal.y), (p0 + p1) * 0.5f) < 0f)
                    normal = -normal;
                var start = vertices.Count;
                vertices.Add(new Vector3(p0.x, p0.y, -HalfDepth));
                vertices.Add(new Vector3(p1.x, p1.y, -HalfDepth));
                vertices.Add(new Vector3(p1.x, p1.y, HalfDepth));
                vertices.Add(new Vector3(p0.x, p0.y, HalfDepth));
                for (var k = 0; k < 4; k++)
                    normals.Add(normal);
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            // Keep whichever winding faces outward for the camera: flip any triangle whose normal disagrees.
            FixWinding(mesh);
        }

        static void FixWinding(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = vertices[triangles[t]];
                var b = vertices[triangles[t + 1]];
                var c = vertices[triangles[t + 2]];
                var face = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(face, normals[triangles[t]]) < 0f)
                    (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
            }
            mesh.triangles = triangles;
        }

        static Mesh Save(string name, Vector2[] outline)
        {
            var path = $"{WorkshopPaths.Configuration}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var created = mesh == null;
            if (created)
                mesh = new Mesh { name = name };
            Build(mesh, outline);
            if (created)
                AssetDatabase.CreateAsset(mesh, path);
            else
                EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
