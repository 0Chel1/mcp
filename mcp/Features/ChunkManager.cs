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

    public void MarkChunkDirty(Vector3 worldPos)
    {
        Vector3 chunkCoord = GetChunkCoord(worldPos);
        if (!Chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {
            chunk = new Chunk(chunkCoord);
            Chunks[chunkCoord] = chunk;
        }
        chunk.NeedsRebuild = true;
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

        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    Vector3 neighborCoord = centerChunk.ChunkCoord + new Vector3(dx, dy, dz);
                    if (Chunks.TryGetValue(neighborCoord, out Chunk neighbor)) neighbor.NeedsRebuild = true;
                }
    }

    public void RebuildMeshes(GraphicsDevice gd)
    {
        foreach (var chunk in Chunks.Values)
        {
            if (chunk.NeedsRebuild) RebuildSingleChunk(chunk, gd);
        }
    }

    private void RebuildSingleChunk(Chunk chunk, GraphicsDevice gd)
    {
        var opaqueVerts = new List<VertexPositionTexture>();
        var opaqueInds = new List<int>();
        var transVerts = new List<VertexPositionTexture>();
        var transInds = new List<int>();

        for (int lx = 0; lx < Chunk.SIZE; lx++)
            for (int ly = 0; ly < Chunk.SIZE; ly++)
                for (int lz = 0; lz < Chunk.SIZE; lz++)
                {
                    Vector3 worldPos = chunk.WorldPos + new Vector3(lx, ly, lz);
                    if (!bm.HasBlock(worldPos)) continue;

                    int type = bm.GetBlockType(worldPos);
                    bool isTransparent = BlocksManagement.TransparentBlockTypes.Contains(type);
                    var blocksFaces = bm.faceVerts[type % bm.faceVerts.Count];

                    var verts = isTransparent ? transVerts : opaqueVerts;
                    var inds = isTransparent ? transInds : opaqueInds;

                    Matrix worldMat = Matrix.CreateTranslation(worldPos);

                    for (int f = 0; f < 6; f++)
                    {
                        Vector3 neighbor = worldPos + BlocksManagement.FaceOffsets[f];

                        if (bm.HasBlock(neighbor))
                        {
                            int neighborType = bm.GetBlockType(neighbor);
                            bool neighborTransparent = BlocksManagement.TransparentBlockTypes.Contains(neighborType);

                            if (!isTransparent && !neighborTransparent) continue;
                            if (isTransparent && neighborTransparent) continue;
                        }

                        var faceVertsLocal = blocksFaces[f];
                        int startIdx = verts.Count;

                        foreach (var v in faceVertsLocal)
                            verts.Add(new VertexPositionTexture(Vector3.Transform(v.Position, worldMat), v.TextureCoordinate));

                        for (int i = 0; i < 6; i++) inds.Add(startIdx + i);
                    }
                }

        //Opaque буфер
        UploadGeometry(gd, opaqueVerts, opaqueInds, ref chunk.VertexBuffer, ref chunk.IndexBuffer, ref chunk.VertexCount, ref chunk.IndexCount);

        //Transparent буфер
        UploadGeometry(gd, transVerts, transInds, ref chunk.TransparentVertexBuffer, ref chunk.TransparentIndexBuffer, ref chunk.TransparentVertexCount, ref chunk.TransparentIndexCount);

        chunk.NeedsRebuild = false;
    }

    private void UploadGeometry(GraphicsDevice gd, List<VertexPositionTexture> verts, List<int> inds, ref DynamicVertexBuffer vb,
        ref IndexBuffer ib, ref int vertexCount, ref int indexCount) {
        vertexCount = verts.Count;
        indexCount = inds.Count;

        if (vb == null || vb.VertexCount < verts.Count)
        {
            vb?.Dispose();
            vb = new DynamicVertexBuffer(gd, VertexPositionTexture.VertexDeclaration, Math.Max(verts.Count, 1024), BufferUsage.WriteOnly);
        }
        if (verts.Count > 0) vb.SetData(verts.ToArray(), 0, verts.Count, SetDataOptions.Discard);
        //else vb.GetData(new VertexPositionTexture[1], SetDataOptions.Discard);

        if (ib == null || ib.IndexCount < inds.Count)
        {
            ib?.Dispose();
            ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, Math.Max(inds.Count, 1024), BufferUsage.WriteOnly);
        }
        if (inds.Count > 0) ib.SetData(inds.ToArray(), 0, inds.Count);
        //else ib.SetData(new int[1], SetDataOptions.Discard);
    }
}