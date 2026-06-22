using MCP.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MCP.Features;

public class BlocksManagement
{
    /// <summary>
    /// Faces of all cubes. Used for textures.
    /// </summary>
    public VertexPositionTexture[][] faceVerts { get; set; }

    /// <summary>
    /// Index of the current cube that gets hit by raycast or detected in some other way. Used to store cube that player looking at.
    /// </summary>
    public int currentCubeIndex { get; set; }

    /// <summary>
    /// Index of the face of the current cube. Used to store face of the current cube that player looking at.
    /// </summary>
    public int currentFace { get; set; }

    /// <summary>
    /// How bright will be selection on the current face.
    /// </summary>
    const float HighlightAdd = 1.0f;

    /// <summary>
    /// Used to highlight current face.
    /// </summary>
    public List<float[]> faceEmission { get; set; }

    /// <summary>
    /// List of all cubes that are currently placed.
    /// </summary>
    public List<Matrix> cubes = new List<Matrix>();

    /// <summary>
    /// List of faces of the cubes that are visible and does not have cubes in front of them.
    /// </summary>
    public List<bool[]> faceVisible { get; set; }

    /// <summary>
    /// Used to store oposite faces.(idk not my code)
    /// </summary>
    public static readonly int[] OppositeFace = new int[] { 2, 3, 0, 1, 5, 4 };

    /// <summary>
    /// Used to hide faces with cubes in front of them.
    /// </summary>
    public static readonly Vector3[] FaceOffsets = new Vector3[]
    {
        new Vector3(0, 0, -1), // front
        new Vector3(1, 0, 0),  // right
        new Vector3(0, 0, 1),  // back
        new Vector3(-1, 0, 0), // left
        new Vector3(0, 1, 0),  // top
        new Vector3(0, -1, 0)  // bottom
    };

    /// <summary>
    /// Builds cube from array of faces.
    /// </summary>
    /// <param name="u0"></param>
    /// <param name="v0"></param>
    /// <param name="u1"></param>
    /// <param name="v1"></param>
    /// <returns>Mesh of the cube.</returns>
    public VertexPositionTexture[][] BuildCubeFaceVertexArrays(float u0, float v0, float u1, float v1)
    {
        // cube corners (0..1)
        var p000 = new Vector3(0, 0, 0);
        var p001 = new Vector3(0, 0, 1);
        var p010 = new Vector3(0, 1, 0);
        var p011 = new Vector3(0, 1, 1);
        var p100 = new Vector3(1, 0, 0);
        var p101 = new Vector3(1, 0, 1);
        var p110 = new Vector3(1, 1, 0);
        var p111 = new Vector3(1, 1, 1);

        // Use UVs arranged/rotated as you prefer
        var uv00 = new Vector2(u0, v1);
        var uv10 = new Vector2(u0, v0);
        var uv01 = new Vector2(u1, v1);
        var uv11 = new Vector2(u1, v0);

        var faces = new VertexPositionTexture[6][];

        // Front (z=0)
        faces[0] = new VertexPositionTexture[]
        {
            new VertexPositionTexture(p000, uv01),
            new VertexPositionTexture(p010, uv11),
            new VertexPositionTexture(p110, uv10),
            new VertexPositionTexture(p000, uv01),
            new VertexPositionTexture(p110, uv10),
            new VertexPositionTexture(p100, uv00)
        };

        // Right (x=1)
        faces[1] = new VertexPositionTexture[]
        {
            new VertexPositionTexture(p100, uv01),
            new VertexPositionTexture(p110, uv11),
            new VertexPositionTexture(p111, uv10),
            new VertexPositionTexture(p100, uv01),
            new VertexPositionTexture(p111, uv10),
            new VertexPositionTexture(p101, uv00)
        };

        // Back (z=1)
        faces[2] = new VertexPositionTexture[]
        {
            new VertexPositionTexture(p101, uv01),
            new VertexPositionTexture(p111, uv11),
            new VertexPositionTexture(p011, uv10),
            new VertexPositionTexture(p101, uv01),
            new VertexPositionTexture(p011, uv10),
            new VertexPositionTexture(p001, uv00)
        };

        // Left (x=0)
        faces[3] = new VertexPositionTexture[]
        {
            new VertexPositionTexture(p001, uv01),
            new VertexPositionTexture(p011, uv11),
            new VertexPositionTexture(p010, uv10),
            new VertexPositionTexture(p001, uv01),
            new VertexPositionTexture(p010, uv10),
            new VertexPositionTexture(p000, uv00)
        };

        // Top (y=1)
        faces[4] = new VertexPositionTexture[]
        {
            new VertexPositionTexture(p010, uv01),
            new VertexPositionTexture(p011, uv11),
            new VertexPositionTexture(p111, uv10),
            new VertexPositionTexture(p010, uv01),
            new VertexPositionTexture(p111, uv10),
            new VertexPositionTexture(p110, uv00)
        };

        // Bottom (y=0)
        faces[5] = new VertexPositionTexture[]
        {
            new VertexPositionTexture(p100, uv01),
            new VertexPositionTexture(p101, uv11),
            new VertexPositionTexture(p001, uv10),
            new VertexPositionTexture(p100, uv01),
            new VertexPositionTexture(p001, uv10),
            new VertexPositionTexture(p000, uv00)
        };

        return faces;
    }

    /// <summary>
    /// Performs ray-triangle tests against each face's two triangles in local space transformed by world. If hit, sets emission for that face on the given cubeIndex.
    /// </summary>
    /// <param name="origin">Ray start point.</param>
    /// <param name="direction">Direction in whick ray should tavel.</param>
    /// <param name="world">World matrix.</param>
    /// <param name="cubeIndex">Cube that vas hited.</param>
    public void HighlightFaceByRay(Vector3 origin, Vector3 direction, Matrix world, int cubeIndex)
    {
        if (faceVerts == null) return;

        float nearestT = float.PositiveInfinity;
        int hitFace = -1;

        Vector3 dir = Vector3.Normalize(direction);

        for (int f = 0; f < faceVerts.Length; f++)
        {
            var fv = faceVerts[f];

            for (int tri = 0; tri < 2; tri++)
            {
                int baseIdx = tri * 3;
                Vector3 a = Vector3.Transform(fv[baseIdx + 0].Position, world);
                Vector3 b = Vector3.Transform(fv[baseIdx + 1].Position, world);
                Vector3 c = Vector3.Transform(fv[baseIdx + 2].Position, world);

                if (Raycast.RayIntersectsTriangle(origin, dir, 5f, a, b, c, out float t, out float u, out float v, false))
                {
                    if (t >= 0f && t < nearestT)
                    {
                        nearestT = t;
                        hitFace = f;
                    }
                }
            }
        }

        if (hitFace >= 0 && cubeIndex >= 0 && cubeIndex < faceEmission.Count)
        {
            faceEmission[cubeIndex][hitFace] = MathF.Min(faceEmission[cubeIndex][hitFace] + HighlightAdd, 2f);
            currentCubeIndex = cubeIndex;
            currentFace = hitFace;
        }
    }

    /// <summary>
    /// Adds a cube at the given world position (position is cube-space origin)
    /// </summary>
    /// <param name="position"></param>
    public void AddCube(Vector3 position)
    {
        var p = new Vector3(MathF.Round(position.X), MathF.Round(position.Y), MathF.Round(position.Z));
        for (int i = 0; i < cubes.Count; i++) if (cubes[i].Translation == p) return;

        var vis = new bool[6] { true, true, true, true, true, true };
        for (int i = 0; i < cubes.Count; i++)
        {
            var pos = cubes[i].Translation;
            var delta = pos - p; // if pos == p + offset -> delta == offset
            for (int f = 0; f < FaceOffsets.Length; f++)
            {
                if (delta == FaceOffsets[f])
                {
                    vis[f] = false;
                    if (i < faceVisible.Count) faceVisible[i][OppositeFace[f]] = false;
                }
                else if (delta == -FaceOffsets[f])
                {
                    vis[OppositeFace[f]] = false;
                    if (i < faceVisible.Count) faceVisible[i][f] = false;
                }
            }
        }

        cubes.Add(Matrix.CreateTranslation(p));
        faceEmission.Add(new float[6]);
        faceVisible.Add(vis);
    }

    /// <summary>
    /// Recompute visibility for all cubes.
    /// </summary>
    public void UpdateVisibilityForAll()
    {
        for (int i = 0; i < faceVisible.Count; i++)
        {
            for (int f = 0; f < 6; f++) faceVisible[i][f] = true;
        }

        for (int i = 0; i < cubes.Count; i++)
        {
            var pi = cubes[i].Translation;
            for (int j = i + 1; j < cubes.Count; j++)
            {
                var pj = cubes[j].Translation;
                var d = pj - pi;
                for (int f = 0; f < FaceOffsets.Length; f++)
                {
                    if (d == FaceOffsets[f])
                    {
                        faceVisible[j][OppositeFace[f]] = false;
                        faceVisible[i][f] = false;
                    }
                    else if (d == -FaceOffsets[f])
                    {
                        faceVisible[j][f] = false;
                        faceVisible[i][OppositeFace[f]] = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Removes cube with specific index.
    /// </summary>
    /// <param name="index"></param>
    public void RemoveCubeAt(int index)
    {
        if (index < 0 || index >= cubes.Count) return;
        cubes.RemoveAt(index);
        faceEmission.RemoveAt(index);
        faceVisible.RemoveAt(index);
        UpdateVisibilityForAll();
    }
}
