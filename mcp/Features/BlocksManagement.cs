using MCP.Graphics;
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
    public List<VertexPositionTexture[][]> faceVerts = new List<VertexPositionTexture[][]>();

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
    const float HighlightAdd = 0.5f;

    /// <summary>
    /// Used to highlight current face.
    /// </summary>
    public List<float[]> faceEmission { get; set; }

    /// <summary>
    /// List of all cubes that are currently placed.
    /// </summary>
    public List<Cubes> cubes = new List<Cubes>();

    /// <summary>
    /// List of faces of the cubes that are visible and does not have cubes in front of them.
    /// </summary>
    public List<bool[]> faceVisible { get; set; }

    /// <summary>
    /// Used to store oposite faces.(idk not my code)
    /// </summary>
    public static readonly int[] OppositeFace = new int[] { 2, 3, 0, 1, 5, 4 };

    public ChunkManager chunkManager;

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

    public VertexPositionTexture[] combinedVertices;
    public int[] combinedIndices;
    public DynamicVertexBuffer vertexBuffer;
    public IndexBuffer indexBuffer;
    public bool meshNeedsRebuild = true;

    public bool HasHighlight { get; private set; } = false;
    public Vector3 HighlightPos { get; private set; }
    public int HighlightFace { get; private set; } = -1;
    public Vector3 HighlightHitPoint { get; private set; }

    public Dictionary<Vector3, int> WorldBlocks = new Dictionary<Vector3, int>();

    public BlocksManagement()
    {
        chunkManager = new ChunkManager(this);
        faceEmission = new List<float[]>();
    }

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
    /// Улучшенный DDA raycast. Значительно быстрее brute-force.
    /// </summary>
    public bool HighlightFaceByRay(Vector3 origin, Vector3 direction, ref float bestDistance)
    {
        currentCubeIndex = -1;
        currentFace = -1;
        HasHighlight = false;
        HighlightFace = -1;
        HighlightHitPoint = Vector3.Zero;

        Vector3 dir = Vector3.Normalize(direction);

        Vector3 currentVoxel = new Vector3(
            MathF.Floor(origin.X),
            MathF.Floor(origin.Y),
            MathF.Floor(origin.Z)
        );

        // Направление шага (+1 или -1)
        Vector3 step = new Vector3(
            dir.X > 0 ? 1 : (dir.X < 0 ? -1 : 0),
            dir.Y > 0 ? 1 : (dir.Y < 0 ? -1 : 0),
            dir.Z > 0 ? 1 : (dir.Z < 0 ? -1 : 0)
        );

        // Расстояние до следующей грани по каждой оси
        Vector3 tDelta = new Vector3(
            step.X == 0 ? float.PositiveInfinity : MathF.Abs(1f / dir.X),
            step.Y == 0 ? float.PositiveInfinity : MathF.Abs(1f / dir.Y),
            step.Z == 0 ? float.PositiveInfinity : MathF.Abs(1f / dir.Z)
        );

        // Расстояние до первой грани
        Vector3 tMax = new Vector3(
            step.X > 0 ? (currentVoxel.X + 1 - origin.X) / dir.X :
                  step.X < 0 ? (currentVoxel.X - origin.X) / dir.X : float.PositiveInfinity,
            step.Y > 0 ? (currentVoxel.Y + 1 - origin.Y) / dir.Y :
                  step.Y < 0 ? (currentVoxel.Y - origin.Y) / dir.Y : float.PositiveInfinity,
            step.Z > 0 ? (currentVoxel.Z + 1 - origin.Z) / dir.Z :
                  step.Z < 0 ? (currentVoxel.Z - origin.Z) / dir.Z : float.PositiveInfinity
        );

        int maxSteps = 64; // защита от бесконечного цикла
        int steps = 0;

        while (steps < maxSteps)
        {
            Vector3 voxelPos = new Vector3(
                MathF.Round(currentVoxel.X),
                MathF.Round(currentVoxel.Y),
                MathF.Round(currentVoxel.Z)
            );

            if (HasBlock(voxelPos))
            {
                // Нашли блок — теперь определяем, по какой грани попали
                float t = MathF.Min(tMax.X, MathF.Min(tMax.Y, tMax.Z));
                Vector3 hitPoint = origin + dir * t;

                // Определяем нормаль (какая грань была пробита)
                int face = -1;
                float minDist = float.MaxValue;

                for (int f = 0; f < 6; f++)
                {
                    Vector3 neighbor = voxelPos + FaceOffsets[f];
                    if (!HasBlock(neighbor)) // только видимые грани
                    {
                        // Простая проверка, какая грань ближе всего к точке попадания
                        float distToPlane = Vector3.Dot(hitPoint - voxelPos, FaceOffsets[f]);
                        if (distToPlane >= 0 && distToPlane < 1.01f && distToPlane < minDist)
                        {
                            minDist = distToPlane;
                            face = f;
                        }
                    }
                }

                if (face != -1 && t < bestDistance)
                {
                    bestDistance = t;
                    currentFace = face;
                    HasHighlight = true;
                    HighlightPos = voxelPos;
                    HighlightFace = face;
                    HighlightHitPoint = hitPoint;
                    return true;
                }
            }

            // Переходим в следующую клетку
            if (tMax.X < tMax.Y)
            {
                if (tMax.X < tMax.Z)
                {
                    tMax.X += tDelta.X;
                    currentVoxel.X += step.X;
                }
                else
                {
                    tMax.Z += tDelta.Z;
                    currentVoxel.Z += step.Z;
                }
            }
            else
            {
                if (tMax.Y < tMax.Z)
                {
                    tMax.Y += tDelta.Y;
                    currentVoxel.Y += step.Y;
                }
                else
                {
                    tMax.Z += tDelta.Z;
                    currentVoxel.Z += step.Z;
                }
            }

            steps++;

            // Выходим, если ушли слишком далеко
            if ((voxelPos - origin).Length() > bestDistance + 2f)
                break;
        }

        return HasHighlight;
    }

    /// <summary>
    /// Adds a cube at the given world position (position is cube-space origin)
    /// </summary>
    /// <param name="position"></param>
    public void AddCube(Vector3 position, int blockType)
    {
        var p = new Vector3(MathF.Round(position.X), MathF.Round(position.Y), MathF.Round(position.Z));
        if (WorldBlocks.ContainsKey(p)) return;

        WorldBlocks[p] = blockType;
        chunkManager.MarkChunkDirty(p);
    }

    /// <summary>
    /// Быстрая версия AddCube специально для генерации карты (без лишних проверок)
    /// </summary>
    public void AddCubeOptimized(Vector3 position, int blockType)
    {
        var p = new Vector3(MathF.Round(position.X), MathF.Round(position.Y), MathF.Round(position.Z));

        var vis = new bool[6] { true, true, true, true, true, true };

        for (int i = 0; i < cubes.Count; i++)
        {
            var delta = cubes[i].cubes.Translation - p;
            if (MathF.Abs(delta.X) > 1 && MathF.Abs(delta.Y) > 1 && MathF.Abs(delta.Z) > 1)
                continue;

            for (int f = 0; f < 6; f++)
            {
                if (delta == FaceOffsets[f])
                {
                    vis[f] = false;
                    faceVisible[i][OppositeFace[f]] = false;
                }
                else if (delta == -FaceOffsets[f])
                {
                    vis[OppositeFace[f]] = false;
                    faceVisible[i][f] = false;
                }
            }
        }

        cubes.Add(new Cubes { cubes = Matrix.CreateTranslation(p), cubesTypes = blockType });
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
            var pi = cubes[i].cubes.Translation;
            for (int j = i + 1; j < cubes.Count; j++)
            {
                var pj = cubes[j].cubes.Translation;
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
    /// Recompute visibility for all cubes but in 1 block radius around the position.
    /// </summary>
    public void UpdateLocalVisibility(Vector3 center)
    {
        var p = new Vector3(MathF.Round(center.X), MathF.Round(center.Y), MathF.Round(center.Z));

        // Сбрасываем видимость только у блоков в радиусе 1
        for (int i = 0; i < cubes.Count; i++)
        {
            var pos = cubes[i].cubes.Translation;
            var d = pos - p;
            if (MathF.Abs(d.X) > 1 || MathF.Abs(d.Y) > 1 || MathF.Abs(d.Z) > 1) continue;

            for (int f = 0; f < 6; f++)
                faceVisible[i][f] = true;
        }

        // Проверяем соседей
        for (int i = 0; i < cubes.Count; i++)
        {
            var pi = cubes[i].cubes.Translation;
            var d = pi - p;
            if (MathF.Abs(d.X) > 1 || MathF.Abs(d.Y) > 1 || MathF.Abs(d.Z) > 1) continue;

            for (int j = i + 1; j < cubes.Count; j++)
            {
                var pj = cubes[j].cubes.Translation;
                var delta = pj - pi;

                for (int f = 0; f < FaceOffsets.Length; f++)
                {
                    if (delta == FaceOffsets[f])
                    {
                        faceVisible[j][OppositeFace[f]] = false;
                        faceVisible[i][f] = false;
                    }
                    else if (delta == -FaceOffsets[f])
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
    public void RemoveCube(Vector3 position)
    {
        var p = new Vector3(MathF.Round(position.X), MathF.Round(position.Y), MathF.Round(position.Z));
        WorldBlocks.Remove(p);
        chunkManager.MarkChunkDirty(p);
    }

    public bool HasBlock(Vector3 position)
    {
        var p = new Vector3(MathF.Round(position.X), MathF.Round(position.Y), MathF.Round(position.Z));
        return WorldBlocks.ContainsKey(p);
    }

    public int GetBlockType(Vector3 position)
    {
        var p = new Vector3(MathF.Round(position.X), MathF.Round(position.Y), MathF.Round(position.Z));
        return WorldBlocks.TryGetValue(p, out int type) ? type : 0;
    }

    public void RebuildMesh(GraphicsDevice graphicsDevice)
    {
        chunkManager.RebuildMeshes(graphicsDevice);
    }
}

public class Cubes
{
    public Matrix cubes = new Matrix();
    public int cubesTypes;
}
