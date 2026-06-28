using MCP.Features;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace mcp.Features;

public class PhysicsStructure
{
    /// <summary>
    /// Локальные позиции ЦЕНТРОВ блоков относительно COM.
    /// Хранятся отдельно от мировой позиции, чтобы поворот не "уплывал" между кадрами.
    /// </summary>
    public List<(Vector3 LocalCenter, int Type)> Blocks { get; private set; } = new();

    /// <summary>Мировая позиция центра масс структуры.</summary>
    public Vector3 WorldPosition;

    public Vector3 Velocity = Vector3.Zero;
    public Vector3 AngularVelocity = Vector3.Zero;
    public Matrix Rotation = Matrix.Identity;
    public float Pitch, Yaw, Roll;

    /// <summary>Структура ударилась о землю и почти остановилась — готова к парковке в мир.</summary>
    public bool Settled = false;

    public PhysicsStructure(List<(Vector3, int)> blocks)
    {
        if (blocks.Count == 0) throw new ArgumentException("PhysicsStructure: пустой список блоков", nameof(blocks));

        Vector3 com = Vector3.Zero;
        foreach (var (pos, _) in blocks) com += pos + new Vector3(0.5f);
        WorldPosition = com / blocks.Count;

        foreach (var (pos, type) in blocks) Blocks.Add((pos + new Vector3(0.5f) - WorldPosition, type));
    }

    /// <summary>
    /// Возвращает мировые позиции (НИЖНИЙ УГЛ блока, как привык остальной код)
    /// и типы блоков после применения поворота и смещения COM.
    /// Используется и для отрисовки, и для физики столкновений.
    /// </summary>
    public IEnumerable<(Vector3 WorldPos, int Type)> GetWorldBlocks()
    {
        foreach (var (localCenter, type) in Blocks)
        {
            Vector3 rotated = Vector3.Transform(localCenter, Rotation);
            yield return (WorldPosition + rotated + new Vector3(0.5f), type);
        }
    }

    /// <summary>
    /// Возвращает мировые ЦЕНТРЫ блоков (для коллизий — удобнее работать с центрами).
    /// </summary>
    public IEnumerable<(Vector3 WorldCenter, int Type)> GetWorldBlockCenters()
    {
        foreach (var (localCenter, type) in Blocks)
        {
            Vector3 rotated = Vector3.Transform(localCenter, Rotation);
            yield return (WorldPosition + rotated, type);
        }
    }

    public void Update(float dt, BlocksManagement bm)
    {
        if (Settled) return;

        Velocity.Y -= 9.8f * dt;

        float angSpeed = AngularVelocity.Length();
        if (angSpeed > 0.001f)
        {
            float angle = angSpeed * dt;
            Vector3 axis = AngularVelocity / angSpeed;
            Rotation *= Matrix.CreateFromAxisAngle(axis, angle);
            AngularVelocity *= MathF.Max(0f, 1f - 0.3f * dt);
        }

        WorldPosition += Velocity * dt;

        ResolveCollisions(bm, dt);
    }

    private void ResolveCollisions(BlocksManagement bm, float dt)
    {
        var contacts = new List<(Vector3 Point, float Penetration)>();

        foreach (var (worldCenter, _) in GetWorldBlockCenters())
        {
            float blockBottom = worldCenter.Y - 0.5f;

            if (blockBottom <= 0f)
            {
                contacts.Add((new Vector3(worldCenter.X, 0f, worldCenter.Z), -blockBottom));
                continue;
            }

            int cellX = (int)MathF.Floor(worldCenter.X);
            int cellY = (int)MathF.Floor(worldCenter.Y);
            int cellZ = (int)MathF.Floor(worldCenter.Z);
            Vector3 belowCell = new Vector3(cellX, cellY - 1, cellZ);

            if (bm.HasBlock(belowCell))
            {
                float topOfCell = cellY;
                float pen = topOfCell - blockBottom;
                if (pen >= -0.001f) contacts.Add((new Vector3(worldCenter.X, topOfCell, worldCenter.Z), MathF.Max(0, pen)));
            }
        }

        if (contacts.Count == 0) return;

        float maxPen = 0f;
        foreach (var c in contacts) if (c.Penetration > maxPen) maxPen = c.Penetration;

        if (maxPen > 0.001f) WorldPosition += Vector3.Up * maxPen;

        if (Velocity.Y < 0f)
        {
            Velocity.Y = -Velocity.Y * 0.05f;
            if (MathF.Abs(Velocity.Y) < 0.3f) Velocity.Y = 0f;
        }

        Velocity.X *= 0.85f;
        Velocity.Z *= 0.85f;

        
        Vector2 contactCentroidXZ = Vector2.Zero;
        foreach (var c in contacts)
        {
            contactCentroidXZ.X += c.Point.X;
            contactCentroidXZ.Y += c.Point.Z;
        }
        contactCentroidXZ /= contacts.Count;

        Vector2 comXZ = new Vector2(WorldPosition.X, WorldPosition.Z);
        Vector2 offset = comXZ - contactCentroidXZ;
        float offsetLen = offset.Length();

        if (offsetLen > 0.15f)
        {
            Vector3 offset3D = new Vector3(offset.X, 0f, offset.Y);
            Vector3 tipAxis = Vector3.Normalize(Vector3.Cross(Vector3.Up, offset3D));

            float tipAccel = offsetLen * 12f;

            AngularVelocity += tipAxis * tipAccel * dt;

            AngularVelocity *= MathF.Max(0f, 1f - 0.5f * dt);
        }
        else
        {
            AngularVelocity *= MathF.Max(0f, 5f * dt);

            if (Velocity.Length() < 0.3f && AngularVelocity.Length() < 0.5f)
            {
                //Settled = true;
                Velocity = Vector3.Zero;
                AngularVelocity = Vector3.Zero;
                contacts.Clear();
            }
        }
    }

    /// <summary>
    /// Паркует структуру обратно в мир: округляет позиции блоков до сетки
    /// и добавляет их в WorldBlocks. Конфликты (поверх уже занятой клетки) пропускаются.
    /// </summary>
    public void SnapToWorld(BlocksManagement bm)
    {
        HashSet<Vector3> added = new HashSet<Vector3>();

        foreach (var (worldPos, type) in GetWorldBlocks())
        {
            Vector3 finalPos = new Vector3(
                MathF.Round(worldPos.X),
                MathF.Round(worldPos.Y),
                MathF.Round(worldPos.Z)
            );

            if (bm.HasBlock(finalPos) || added.Contains(finalPos))
                continue;

            bm.WorldBlocks[finalPos] = type;
            bm.chunkManager.MarkChunkDirty(finalPos);
            added.Add(finalPos);
        }
    }
}
