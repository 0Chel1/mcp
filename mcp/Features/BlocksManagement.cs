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

    private Dictionary<Vector3, int> positionToIndex = new Dictionary<Vector3, int>();

    public BlocksManagement()
    {
        chunkManager = new ChunkManager(this);
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
    /// Performs ray-triangle tests against each face's two triangles in local space transformed by world. 
    /// If hit, sets emission for that face on the given cubeIndex.
    /// </summary>
    /// <param name="origin">Ray start point.</param>
    /// <param name="direction">Direction in whick ray should tavel.</param>
    /// <param name="world">World matrix.</param>
    /// <param name="cubeIndex">Cube that vas hited.</param>
    public bool HighlightFaceByRay(Vector3 origin, Vector3 direction, ref float bestDistance)
    {
        currentCubeIndex = -1;
        currentFace = -1;

        Vector3 dir = Vector3.Normalize(direction);

        foreach (var chunk in chunkManager.Chunks.Values)
        {
            // Простая отсечка по расстоянию
            if ((chunk.WorldPos + new Vector3(8, 8, 8) - origin).LengthSquared() > 300f)
                continue;

            // Проверяем все блоки в чанке (можно оптимизировать AABB позже)
            for (int lx = 0; lx < Chunk.SIZE; lx++)
                for (int ly = 0; ly < Chunk.SIZE; ly++)
                    for (int lz = 0; lz < Chunk.SIZE; lz++)
                    {
                        if (chunk.Blocks[lx, ly, lz] == 0) continue;

                        Vector3 blockWorldPos = chunk.WorldPos + new Vector3(lx, ly, lz);
                        Matrix world = Matrix.CreateTranslation(blockWorldPos);

                        // Вызываем старый метод для конкретного блока
                        int tempIndex = -1; // пока не используем глобальный индекс
                        if (HighlightFaceByRaySingle(origin, dir, world, ref bestDistance, ref tempIndex, chunk, lx, ly, lz))
                            return true;
                    }
        }
        return false;
    }

    private bool HighlightFaceByRaySingle(Vector3 origin, Vector3 dir, Matrix world, ref float bestDistance, ref int hitFace, Chunk chunk, int lx, int ly, int lz)
    {
        var fvArray = faceVerts[0]; // предполагаем 0 = cobblestone

        for (int f = 0; f < 6; f++)
        {
            var fv = fvArray[f];
            for (int tri = 0; tri < 2; tri++)
            {
                int baseIdx = tri * 3;
                Vector3 a = Vector3.Transform(fv[baseIdx].Position, world);
                Vector3 b = Vector3.Transform(fv[baseIdx + 1].Position, world);
                Vector3 c = Vector3.Transform(fv[baseIdx + 2].Position, world);

                if (Raycast.RayIntersectsTriangle(origin, dir, 200f, a, b, c, out float t, out _, out _, false))
                {
                    if (t >= 0f && t < bestDistance)
                    {
                        bestDistance = t;
                        currentFace = f;
                        // currentCubeIndex можно убрать позже — он больше не нужен
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Adds a cube at the given world position (position is cube-space origin)
    /// </summary>
    /// <param name="position"></param>
    public void AddCube(Vector3 position, int blockType)
    {
        chunkManager.AddBlock(position, blockType);
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
    private void UpdateLocalVisibility(Vector3 center)
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
    public void RemoveCube(Vector3 worldPos)
    {
        chunkManager.RemoveBlock(worldPos);
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
