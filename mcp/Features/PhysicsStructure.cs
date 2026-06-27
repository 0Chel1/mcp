using MCP.Features;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace mcp.Features;

public class PhysicsStructure
{
    public List<(Vector3 Pos, int Type)> Blocks { get; private set; } = new();
    public Vector3 Velocity = Vector3.Zero;

    public PhysicsStructure(List<(Vector3, int)> blocks)
    {
        Blocks = blocks;
    }

    public void Update(float dt, BlocksManagement bm)
    {
        Velocity.Y -= 18f * dt;   // гравитация

        for (int i = 0; i < Blocks.Count; i++)
        {
            var (pos, type) = Blocks[i];
            Blocks[i] = (pos + Velocity * dt, type);
        }

        // Проверка приземления
        CheckGround(bm);
    }

    private void CheckGround(BlocksManagement bm)
    {
        foreach (var (pos, _) in Blocks)
        {
            Vector3 below = new Vector3(MathF.Round(pos.X), MathF.Round(pos.Y - 0.6f), MathF.Round(pos.Z));
            if (bm.HasBlock(below) || pos.Y < 1f)
            {
                Velocity = Vector3.Zero;

                // Приземляем обратно в мир
                SnapToWorld(bm);
                return;
            }
        }
    }

    private void SnapToWorld(BlocksManagement bm)
    {
        foreach (var (pos, type) in Blocks)
        {
            Vector3 finalPos = new Vector3(MathF.Round(pos.X), MathF.Round(pos.Y), MathF.Round(pos.Z));
            if (!bm.HasBlock(finalPos))
            {
                bm.WorldBlocks[finalPos] = type;
                bm.chunkManager.MarkChunkDirty(finalPos);
            }
        }
    }
}
