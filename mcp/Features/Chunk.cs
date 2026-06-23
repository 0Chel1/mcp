using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MCP.Features;

public class Chunk
{
    public const int SIZE = 16;
    public Vector3 ChunkCoord { get; private set; }
    public Vector3 WorldPos { get; private set; }

    public byte[,,] Blocks = new byte[SIZE, SIZE, SIZE];
    public DynamicVertexBuffer VertexBuffer;
    public IndexBuffer IndexBuffer;
    public int VertexCount = 0;
    public bool NeedsRebuild = true;

    public Chunk(Vector3 chunkCoord)
    {
        ChunkCoord = chunkCoord;
        WorldPos = chunkCoord * SIZE;
    }

    public void SetBlock(int lx, int ly, int lz, byte type)
    {
        if (lx < 0 || lx >= SIZE || ly < 0 || ly >= SIZE || lz < 0 || lz >= SIZE) return;
        Blocks[lx, ly, lz] = type;
        NeedsRebuild = true;
    }

    public byte GetBlock(int lx, int ly, int lz)
    {
        if (lx < 0 || lx >= SIZE || ly < 0 || ly >= SIZE || lz < 0 || lz >= SIZE) return 0;
        return Blocks[lx, ly, lz];
    }
}