using mcp.Features;
using MCP.Graphics;
using MCP.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MCP.Features;

public class BlocksManagement
{
    /// <summary>
    /// Faces of all blocks. Used for textures.
    /// </summary>
    public List<VertexPositionTexture[][]> faceVerts = new List<VertexPositionTexture[][]>();

    /// <summary>
    /// Index of the current block that gets hit by raycast or detected in some other way. Used to store cube that player looking at.
    /// </summary>
    public int currentBlockIndex { get; set; }

    /// <summary>
    /// Index of the face of the current block. Used to store face of the current cube that player looking at.
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
    /// List of all blocks that are currently placed.
    /// </summary>
    public List<Blocks> blocks = new List<Blocks>();

    /// <summary>
    /// List of faces of the blocks that are visible and does not have blocks in front of them.
    /// </summary>
    public List<bool[]> faceVisible { get; set; }

    /// <summary>
    /// Used to store oposite faces.(idk not my code)
    /// </summary>
    public static readonly int[] OppositeFace = new int[] { 2, 3, 0, 1, 5, 4 };

    public ChunkManager chunkManager;

    /// <summary>
    /// Used to hide faces with blocks in front of them.
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

    public List<PhysicsStructure> ActiveStructures = new List<PhysicsStructure>();

    public BlocksManagement()
    {
        chunkManager = new ChunkManager(this);
        faceEmission = new List<float[]>();
    }

    /// <summary>
    /// Builds block from array of faces.
    /// </summary>
    /// <param name="u0"></param>
    /// <param name="v0"></param>
    /// <param name="u1"></param>
    /// <param name="v1"></param>
    /// <returns>Mesh of the block.</returns>
    public VertexPositionTexture[][] BuildBlockFaceVertexArrays(float u0, float v0, float u1, float v1)
    {
        // block corners (0..1)
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
    /// Highlight faces of blocks.
    /// </summary>
    /// <param name="origin">Origin of the ray.</param>
    /// <param name="direction">Direction of the ray.</param>
    /// <param name="maxDist">Max distance of the highlight.</param>
    /// <returns></returns>
    public bool HighlightFaceByRay(Vector3 origin, Vector3 direction, ref float maxDist)
    {
        currentBlockIndex = -1;
        currentFace = -1;
        HasHighlight = false;
        HighlightFace = -1;
        HighlightHitPoint = Vector3.Zero;

        Vector3 dir = Vector3.Normalize(direction);
        float maxDistSq = 36f;

        // Ограничиваем проверяемые чанки
        foreach (var chunk in chunkManager.Chunks.Values)
        {
            Vector3 chunkCenter = chunk.WorldPos + new Vector3(8, 8, 8);
            if ((chunkCenter - origin).LengthSquared() > maxDistSq + 200f) // отсекаем далеко
                continue;

            for (int lx = 0; lx < Chunk.SIZE; lx++)
            {
                for (int ly = 0; ly < Chunk.SIZE; ly++)
                {
                    for (int lz = 0; lz < Chunk.SIZE; lz++)
                    {
                        Vector3 blockWorldPos = chunk.WorldPos + new Vector3(lx, ly, lz);

                        // Быстрые проверки перед дорогим raycast
                        if (!HasBlock(blockWorldPos)) continue;

                        float distSq = (blockWorldPos + new Vector3(0.5f) - origin).LengthSquared();
                        if (distSq > maxDistSq) continue;   // ← главное ограничение

                        int type = GetBlockType(blockWorldPos);
                        var fvArray = faceVerts[type % faceVerts.Count];

                        Matrix world = Matrix.CreateTranslation(blockWorldPos);

                        for (int f = 0; f < 6; f++)
                        {
                            var fv = fvArray[f];
                            for (int tri = 0; tri < 2; tri++)
                            {
                                int baseIdx = tri * 3;
                                Vector3 a = Vector3.Transform(fv[baseIdx].Position, world);
                                Vector3 b = Vector3.Transform(fv[baseIdx + 1].Position, world);
                                Vector3 c = Vector3.Transform(fv[baseIdx + 2].Position, world);

                                if (Raycast.RayIntersectsTriangle(origin, dir, maxDist, a, b, c, out float t, out _, out _, false))
                                {
                                    if (t >= 0f && t < maxDist)
                                    {
                                        maxDist = t;
                                        currentFace = f;
                                        HasHighlight = true;
                                        HighlightPos = blockWorldPos;
                                        HighlightFace = f;
                                        HighlightHitPoint = origin + dir * t;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return HasHighlight;
    }

    public void AddBlock(Vector3 position, int blockType)
    {
        var p = new Vector3(MathF.Round(position.X), MathF.Round(position.Y), MathF.Round(position.Z));
        if (WorldBlocks.ContainsKey(p)) return;

        WorldBlocks[p] = blockType;
        chunkManager.MarkChunkDirty(p);
        //chunkManager.AddBlock(p, blockType);
    }

    public void RemoveBlock(Vector3 position)
    {
        var p = new Vector3(MathF.Round(position.X), MathF.Round(position.Y), MathF.Round(position.Z));
        WorldBlocks.Remove(p);
        chunkManager.RemoveBlock(p);
        //chunkManager.MarkChunkDirty(p);
        CheckAndDetachFloatingStructures(p);

    }

    /// <summary>
    /// Check if has block on this position.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
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

    public void SaveWorld(string filePath = "world.dat") //не совместимо с Minecraft мирами!
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            bw.Write(WorldBlocks.Count);

            foreach (var pair in WorldBlocks)
            {
                Vector3 pos = pair.Key;
                bw.Write(pos.X);
                bw.Write(pos.Y);
                bw.Write(pos.Z);
                bw.Write(pair.Value);
            }

            Debug.WriteLine($"Мир сохранён: {WorldBlocks.Count} блоков");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка сохранения: {ex.Message}");
        }
    }

    public void LoadWorld(string fileName = "world.dat")
    {
        try
        {
            WorldBlocks.Clear();
            chunkManager.Chunks.Clear();

            using var fs = new FileStream(fileName, FileMode.Open);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                float x = br.ReadSingle();
                float y = br.ReadSingle();
                float z = br.ReadSingle();
                int type = br.ReadInt32();

                Vector3 pos = new Vector3(x, y, z);
                WorldBlocks[pos] = type;
                chunkManager.MarkChunkDirty(pos);
            }

            Debug.WriteLine($"Мир загружен: {WorldBlocks.Count} блоков");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка загрузки: {ex.Message}");
        }
    }

    private void FindConnectedStructure(Vector3 start, HashSet<Vector3> visited, List<(Vector3, int)> structure)
    {
        var queue = new Queue<Vector3>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            Vector3 current = queue.Dequeue();
            int type = GetBlockType(current);
            structure.Add((current, type));

            foreach (var offset in FaceOffsets)
            {
                Vector3 neighbor = current + offset;
                if (!visited.Contains(neighbor) && HasBlock(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    private bool IsStructureSupported(List<(Vector3, int)> structureBlocks)
    {
        HashSet<Vector3> visited = new();
        Queue<Vector3> queue = new();

        foreach (var (pos, _) in structureBlocks)
        {
            Vector3 below = pos + new Vector3(0, -1, 0);
            if (HasBlock(below)) return true;

            if (!visited.Contains(pos))
            {
                visited.Add(pos);
                queue.Enqueue(pos);
            }
        }

        // Flood fill вниз в поисках опоры
        while (queue.Count > 0)
        {
            Vector3 current = queue.Dequeue();

            Vector3 below = current + new Vector3(0, -1, 0);
            if (HasBlock(below)) return true;

            foreach (var offset in FaceOffsets)
            {
                Vector3 neighbor = current + offset;
                if (!visited.Contains(neighbor) && HasBlock(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return false;
    }

    private void DetachStructure(List<(Vector3, int)> blocks)
    {
        var structure = new PhysicsStructure(blocks);

        /*structure.Velocity = new Vector3(...);
        structure.AngularVelocity = new Vector3(...);*/

        foreach (var (pos, type) in blocks)
        {
            WorldBlocks.Remove(pos);
            chunkManager.MarkChunkDirty(pos);
        }

        ActiveStructures.Add(structure);
    }

    public void CheckAndDetachFloatingStructures(Vector3 brokenPos)
    {
        const float maxCheckDistance =  32f;

        HashSet<Vector3> visited = new HashSet<Vector3>();
        Queue<Vector3> queue = new Queue<Vector3>();

        foreach (var offset in FaceOffsets)
        {
            Vector3 neighbor = brokenPos + offset;
            if (HasBlock(neighbor) && !visited.Contains(neighbor))
            {
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        List<(Vector3, int)> potentialStructure = new List<(Vector3, int)>();

        while (queue.Count > 0)
        {
            Vector3 current = queue.Dequeue();
            int type = GetBlockType(current);
            potentialStructure.Add((current, type));

            if (Vector3.DistanceSquared(current, brokenPos) > maxCheckDistance * maxCheckDistance)
                continue;

            foreach (var offset in FaceOffsets)
            {
                Vector3 neighbor = current + offset;
                if (!visited.Contains(neighbor) && HasBlock(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (potentialStructure.Count > 2 && !IsStructureSupported(potentialStructure))
        {
            DetachStructure(potentialStructure);
        }
    }
}

public class Blocks
{
    public Matrix blocks = new Matrix();
    public int blocksTypes;
}
