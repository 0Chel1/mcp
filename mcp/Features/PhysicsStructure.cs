using MCP.Features;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace mcp.Features;

public class PhysicsStructure
{
    public List<(Vector3 Pos, int Type)> Blocks { get; private set; } = new();
    public Vector3 Velocity = Vector3.Zero;
    public Vector3 WorldPosition { get; set; }
    public Vector3 AngularVelocity { get; set; } = Vector3.Zero;
    public Matrix Rotation = Matrix.Identity;

    public PhysicsStructure(List<(Vector3, int)> blocks)
    {
        Blocks = blocks;
        CalculateCenter();
    }

    private void CalculateCenter()
    {
        Vector3 sum = Vector3.Zero;
        foreach (var (pos, _) in Blocks)
            sum += pos + new Vector3(0.5f);

        WorldPosition = sum / Blocks.Count;
    }

    public void Update(float dt, BlocksManagement bm)
    {
        Velocity.Y -= 9.8f * dt;

        if (AngularVelocity.Length() > 0.001f)
        {
            float angle = AngularVelocity.Length() * dt;
            Vector3 axis = Vector3.Normalize(AngularVelocity);
            Rotation *= Matrix.CreateFromAxisAngle(axis, angle);

            AngularVelocity *= 0.98f;
        }

        for (int i = 0; i < Blocks.Count; i++)
        {
            var (pos, type) = Blocks[i];
            Vector3 local = pos - Blocks[0].Pos;
            Vector3 rotated = Vector3.Transform(local, Rotation);
            Blocks[i] = (Blocks[0].Pos + rotated + Velocity * dt, type);
        }
        CheckGround(bm);
    }

    private void CheckGround(BlocksManagement bm)
    {
        bool grounded = false;

        foreach (var (pos, _) in Blocks)
        {
            Vector3 below = new Vector3(MathF.Round(pos.X), MathF.Round(pos.Y - 0.6f), MathF.Round(pos.Z));
            if (bm.HasBlock(below) || pos.Y < 1.5f)
            {
                grounded = true;
                break;
            }
        }

        if (grounded)
        {
            Velocity = Vector3.Zero;
            AngularVelocity *= 0.4f;

            if (Velocity.Length() < 0.4f && AngularVelocity.Length() < 0.4f)
            {
                SnapToWorld(bm);
            }
        }
    }

    private void SnapToWorld(BlocksManagement bm)
    {
        HashSet<Vector3> added = new HashSet<Vector3>();

        foreach (var (pos, type) in Blocks)
        {
            Vector3 finalPos = new Vector3(
                MathF.Round(pos.X),
                MathF.Round(pos.Y),
                MathF.Round(pos.Z)
            );

            // Пропускаем, если позиция уже занята или уже добавлена в этом снапе
            if (bm.HasBlock(finalPos) || added.Contains(finalPos))
                continue;

            bm.WorldBlocks[finalPos] = type;
            bm.chunkManager.MarkChunkDirty(finalPos);
            added.Add(finalPos);
        }
    }
}
