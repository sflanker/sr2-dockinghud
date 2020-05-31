using System;
using UnityEngine;

namespace Assets.Scripts.Helpers {
    public static class MeshCreator {
        private const Single TwoPI = Mathf.PI * 2f;
        public static Mesh CreateTorus(Single radius1 = 1, Single radius2 = 0.3f, Int32 radialSegments = 24, Int32 sides = 18) {

            // Generate vertices and normals

            var vertices = new Vector3[(radialSegments + 1) * (sides + 1)];
            var normals = new Vector3[vertices.Length];

            for (var seg = 0; seg <= radialSegments; seg++) {
                var currSeg = seg == radialSegments ? 0 : seg;

                var t1 = (Single)currSeg / radialSegments * TwoPI;
                var r1 = new Vector3(Mathf.Cos(t1) * radius1, 0f, Mathf.Sin(t1) * radius1);

                for (var side = 0; side <= sides; side++) {
                    var currSide = side == sides ? 0 : side;

                    var t2 = (Single)currSide / sides * TwoPI;
                    var r2 =
                        Quaternion.AngleAxis(
                            -t1 * Mathf.Rad2Deg, Vector3.up) * new Vector3(Mathf.Sin(t2) * radius2,
                            Mathf.Cos(t2) * radius2
                        );

                    vertices[side + seg * (sides + 1)] = r1 + r2;
                    normals[side + seg * (sides + 1)] = (vertices[side + seg * (sides + 1)] - r1).normalized;
                }
            }

            // Create UVs

            var uvs = new Vector2[vertices.Length];
            for (var seg = 0; seg <= radialSegments; seg++)
            for (var side = 0; side <= sides; side++) {
                uvs[side + seg * (sides + 1)] = new Vector2((Single)seg / radialSegments, (Single)side / sides);
            }

            // Create Triangles

            var nbFaces = vertices.Length;
            var nbTriangles = nbFaces * 2;
            var nbIndexes = nbTriangles * 3;
            var triangles = new int[nbIndexes];

            var i = 0;
            for (var seg = 0; seg <= radialSegments; seg++) {
                for (var side = 0; side <= sides - 1; side++) {
                    var current = side + seg * (sides + 1);
                    var next = side + (seg < (radialSegments) ? (seg + 1) * (sides + 1) : 0);

                    if (i < triangles.Length - 6) {
                        triangles[i++] = current;
                        triangles[i++] = next;
                        triangles[i++] = next + 1;

                        triangles[i++] = current;
                        triangles[i++] = next + 1;
                        triangles[i++] = current + 1;
                    }
                }
            }

            // Create Mesh

            var mesh = new Mesh();
            mesh.Clear();

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            mesh.RecalculateBounds();
            mesh.Optimize();

            return mesh;
        }
    }
}
