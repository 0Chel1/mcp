using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MCP.Features;

public class ChunkManager
{
    public Dictionary<Vector3, Chunk> Chunks = new();
    private BlocksManagement bm;

    public ChunkManager(BlocksManagement blocksManagement)
    {
        bm = blocksManagement;
    }

    public void AddBlock(Vector3 worldPos, int blockType)
    {
        Vector3 chunkCoord = GetChunkCoord(worldPos);
        Chunk chunk = GetOrCreateChunk(chunkCoord);

        Vector3 local = worldPos - chunk.WorldPos;
        chunk.SetBlock((int)local.X, (int)local.Y, (int)local.Z, (byte)blockType);

        RebuildChunkAndNeighbors(chunk);
    }

    public void RemoveBlock(Vector3 worldPos)
    {
        Vector3 chunkCoord = GetChunkCoord(worldPos);
        if (Chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {
            Vector3 local = worldPos - chunk.WorldPos;
            chunk.SetBlock((int)local.X, (int)local.Y, (int)local.Z, 0);
            RebuildChunkAndNeighbors(chunk);
        }
    }

    private Vector3 GetChunkCoord(Vector3 worldPos)
    {
        return new Vector3(
            MathF.Floor(worldPos.X / Chunk.SIZE),
            MathF.Floor(worldPos.Y / Chunk.SIZE),
            MathF.Floor(worldPos.Z / Chunk.SIZE));
    }

    private Chunk GetOrCreateChunk(Vector3 chunkCoord)
    {
        if (!Chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {
            chunk = new Chunk(chunkCoord);
            Chunks[chunkCoord] = chunk;
        }
        return chunk;
    }

    private void RebuildChunkAndNeighbors(Chunk centerChunk)
    {
        centerChunk.NeedsRebuild = true;

        // Перестраиваем соседние чанки (важно для границ)
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    Vector3 neighborCoord = centerChunk.ChunkCoord + new Vector3(dx, dy, dz);
                    if (Chunks.TryGetValue(neighborCoord, out Chunk neighbor))
                        neighbor.NeedsRebuild = true;
                }
    }

    public void RebuildMeshes(GraphicsDevice gd)
    {
        foreach (var chunk in Chunks.Values)
        {
            if (chunk.NeedsRebuild)
                RebuildSingleChunk(chunk, gd);
        }
    }

    private void RebuildSingleChunk(Chunk chunk, GraphicsDevice gd)
    {
        var verts = new List<VertexPositionTexture>(4096); // предвыделение
        var inds = new List<int>(6144);

        for (int lx = 0; lx < Chunk.SIZE; lx++)
            for (int ly = 0; ly < Chunk.SIZE; ly++)
                for (int lz = 0; lz < Chunk.SIZE; lz++)
                {
                    byte type = chunk.Blocks[lx, ly, lz];
                    if (type == 0) continue;

                    Vector3 worldBase = chunk.WorldPos + new Vector3(lx, ly, lz);
                    Matrix worldMat = Matrix.CreateTranslation(worldBase);

                    var cubeFaces = bm.faceVerts[type];

                    for (int f = 0; f < 6; f++)
                    {
                        // === CULLING ГРАНЕЙ ===
                        Vector3 neighborPos = worldBase + BlocksManagement.FaceOffsets[f];
                        bool isFaceVisible = true;

                        // Проверяем соседа внутри этого же чанка
                        Vector3 localNeighbor = neighborPos - chunk.WorldPos;
                        int nlx = (int)localNeighbor.X;
                        int nly = (int)localNeighbor.Y;
                        int nlz = (int)localNeighbor.Z;

                        if (nlx >= 0 && nlx < Chunk.SIZE &&
                            nly >= 0 && nly < Chunk.SIZE &&
                            nlz >= 0 && nlz < Chunk.SIZE)
                        {
                            // Сосед внутри чанка
                            if (chunk.Blocks[nlx, nly, nlz] != 0)
                                isFaceVisible = false;
                        }
                        else
                        {
                            // Сосед в соседнем чанке
                            Vector3 neighborChunkCoord = GetChunkCoord(neighborPos);
                            if (Chunks.TryGetValue(neighborChunkCoord, out Chunk neighborChunk))
                            {
                                Vector3 localInNeighbor = neighborPos - neighborChunk.WorldPos;
                                int nx = (int)localInNeighbor.X;
                                int ny = (int)localInNeighbor.Y;
                                int nz = (int)localInNeighbor.Z;

                                if (nx >= 0 && nx < Chunk.SIZE && ny >= 0 && ny < Chunk.SIZE && nz >= 0 && nz < Chunk.SIZE)
                                {
                                    if (neighborChunk.Blocks[nx, ny, nz] != 0)
                                        isFaceVisible = false;
                                }
                            }
                        }

                        if (!isFaceVisible) continue;

                        // Добавляем видимую грань
                        var faceVertsLocal = cubeFaces[f];
                        int start = verts.Count;

                        foreach (var v in faceVertsLocal)
                        {
                            verts.Add(new VertexPositionTexture(
                                Vector3.Transform(v.Position, worldMat),
                                v.TextureCoordinate));
                        }

                        for (int i = 0; i < 6; i++)
                            inds.Add(start + i);
                    }
                }

        chunk.VertexCount = verts.Count;

        // Vertex Buffer
        if (chunk.VertexBuffer == null || chunk.VertexBuffer.VertexCount < verts.Count)
        {
            chunk.VertexBuffer?.Dispose();
            chunk.VertexBuffer = new DynamicVertexBuffer(gd, VertexPositionTexture.VertexDeclaration, Math.Max(verts.Count, 1024), BufferUsage.WriteOnly);
        }
        if (verts.Count > 0)
            chunk.VertexBuffer.SetData(verts.ToArray());

        // Index Buffer
        if (chunk.IndexBuffer == null || chunk.IndexBuffer.IndexCount < inds.Count)
        {
            chunk.IndexBuffer?.Dispose();
            chunk.IndexBuffer = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits,
                Math.Max(inds.Count, 1024), BufferUsage.WriteOnly);
        }
        if (inds.Count > 0)
            chunk.IndexBuffer.SetData(inds.ToArray());

        chunk.NeedsRebuild = false;
    }
}