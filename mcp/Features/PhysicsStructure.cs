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

    public Quaternion Orientation = Quaternion.Identity;

    /// <summary>Структура ударилась о землю и почти остановилась — готова к парковке в мир.</summary>
    public bool Settled = false;

    /// <summary>Масса структуры (= кол-во блоков, масса блока = 1).</summary>
    public float Mass;

    /// <summary>Локальный тензор инерции (3x3 в верхнем левом блоке Matrix).</summary>
    private Matrix InertiaLocal;

    /// <summary>Обратный локальный тензор инерции.</summary>
    private Matrix InertiaLocalInv;

    public PhysicsStructure(List<(Vector3, int)> blocks)
    {
        if (blocks.Count == 0) throw new ArgumentException("PhysicsStructure: пустой список блоков", nameof(blocks));

        Vector3 com = Vector3.Zero;
        foreach (var (pos, _) in blocks) com += pos + new Vector3(0.5f);
        WorldPosition = com / blocks.Count;

        foreach (var (pos, type) in blocks) Blocks.Add((pos + new Vector3(0.5f) - WorldPosition, type));
        Mass = Blocks.Count;

        float Ixx = 0, Iyy = 0, Izz = 0;
        float Ixy = 0, Ixz = 0, Iyz = 0;

        foreach (var (local, _) in Blocks)
        {
            float x = local.X, y = local.Y, z = local.Z;
            float r2 = x * x + y * y + z * z;

            Ixx += 1f / 6f + (r2 - x * x);
            Iyy += 1f / 6f + (r2 - y * y);
            Izz += 1f / 6f + (r2 - z * z);
            Ixy += -x * y;
            Ixz += -x * z;
            Iyz += -y * z;
        }

        InertiaLocal = new Matrix(
            Ixx, Ixy, Ixz, 0,
            Ixy, Iyy, Iyz, 0,
            Ixz, Iyz, Izz, 0,
            0, 0, 0, 1
        );

        InertiaLocalInv = Matrix.Invert(InertiaLocal);
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
            Vector3 rotated = Vector3.Transform(localCenter, Orientation);
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
            Vector3 rotated = Vector3.Transform(localCenter, Orientation);
            yield return (WorldPosition + rotated, type);
        }
    }

    /// <summary>
    /// Возвращает обратный тензор инерции в мировых координатах.
    /// I_world_inv = R * I_local_inv * R^T
    /// Симметричен, поэтому можно использовать напрямую с Vector3.Transform.
    /// </summary>
    private Matrix GetWorldInertiaInv()
    {
        Matrix R = Matrix.CreateFromQuaternion(Orientation);
        Matrix Rt = Matrix.Transpose(R);
        return R * InertiaLocalInv * Rt;
    }

    public void Update(float dt, BlocksManagement bm)
    {
        if (Settled) return;

        Velocity.Y -= 9.8f * dt;

        float angSpeed = AngularVelocity.Length();
        if (angSpeed > 1e-5f)
        {
            Vector3 axis = AngularVelocity / angSpeed;
            float angle = angSpeed * dt;
            Quaternion dq = Quaternion.CreateFromAxisAngle(axis, angle);
            Orientation = Quaternion.Normalize(Quaternion.Concatenate(Orientation, dq));
        }

        WorldPosition += Velocity * dt;

        ResolveCollisions(bm);

        /*Velocity *= MathF.Max(0f, 1f - 0.05f * dt);
        AngularVelocity *= MathF.Max(0f, 1f - 0.05f * dt);*/

        /*if (Velocity.LengthSquared() < 0.04f && AngularVelocity.LengthSquared() < 0.09f)
        {
            bool stillGrounded = IsGrounded(bm);
            if (stillGrounded)
            {
                Settled = true;
                Velocity = Vector3.Zero;
                AngularVelocity = Vector3.Zero;
            }
        }*/
    }

    /// <summary>
    /// Проверяет, стоит ли структура на чём-то прямо сейчас.
    /// </summary>
    private bool IsGrounded(BlocksManagement bm)
    {
        foreach (var (worldCenter, _) in GetWorldBlockCenters())
        {
            float blockBottom = worldCenter.Y - 0.5f;
            if (blockBottom <= 0.05f) return true;

            int cellX = (int)MathF.Floor(worldCenter.X);
            int cellY = (int)MathF.Floor(worldCenter.Y);
            int cellZ = (int)MathF.Floor(worldCenter.Z);
            Vector3 belowCell = new Vector3(cellX, cellY - 1, cellZ);
            if (bm.HasBlock(belowCell)) return true;
        }
        return false;
    }

    public void ResolveCollisions(BlocksManagement bm)
    {
        const int iterations = 6;
        const float restitution = 0.05f;
        const float friction = 0.4f;
        const float slop = 0.01f;
        const float baumgarte = 0.2f;

        var contacts = new List<(Vector3 Point, Vector3 Normal, float Penetration)>();

        // 6 осевых направлений: +X, -X, +Y, -Y, +Z, -Z
        Vector3[] directions = {
         Vector3.UnitX, -Vector3.UnitX,
         Vector3.UnitY, -Vector3.UnitY,
         Vector3.UnitZ, -Vector3.UnitZ
    };

        foreach (var (worldCenter, _) in GetWorldBlockCenters())
        {
            float blockBottom = worldCenter.Y - 0.5f;
            if (blockBottom < 0.01f)
                contacts.Add((new Vector3(worldCenter.X, 0f, worldCenter.Z), Vector3.Up, MathF.Max(0f, -blockBottom)));

            foreach (var dir in directions)
            {
                Vector3 sample = worldCenter + dir * 0.5f;
                Vector3 cell = new Vector3(
                    MathF.Floor(sample.X),
                    MathF.Floor(sample.Y),
                    MathF.Floor(sample.Z)
                );

                if (!bm.HasBlock(cell)) continue;

                Vector3 cellCenter = cell + new Vector3(0.5f);

                Vector3 delta = worldCenter - cellCenter;
                float axisDelta;
                if (dir.X != 0) axisDelta = MathF.Abs(delta.X);
                else if (dir.Y != 0) axisDelta = MathF.Abs(delta.Y);
                else axisDelta = MathF.Abs(delta.Z);

                float penetration = 1.0f - axisDelta;
                if (penetration <= 0.001f) continue;

                Vector3 normal = -dir;
                Vector3 contactPoint = (worldCenter + cellCenter) * 0.5f;
                contacts.Add((contactPoint, normal, penetration));
            }
        }

        if (contacts.Count == 0) return;

        Matrix I_world_inv = GetWorldInertiaInv();

        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var (point, normal, _) in contacts)
            {
                Vector3 r = point - WorldPosition;
                Vector3 v_contact = Velocity + Vector3.Cross(AngularVelocity, r);
                float v_rel_n = Vector3.Dot(v_contact, normal);

                if (v_rel_n >= 0f) continue;

                Vector3 r_cross_n = Vector3.Cross(r, normal);
                Vector3 I_r_cross_n = Vector3.Transform(r_cross_n, I_world_inv);
                float denom = 1f / Mass + Vector3.Dot(I_r_cross_n, r_cross_n);
                if (denom < 1e-8f) continue;

                float j_scalar = -(1f + restitution) * v_rel_n / denom;
                Vector3 impulse = j_scalar * normal;
                Velocity += impulse / Mass;
                AngularVelocity += Vector3.Transform(Vector3.Cross(r, impulse), I_world_inv);

                v_contact = Velocity + Vector3.Cross(AngularVelocity, r);
                Vector3 v_tangent = v_contact - Vector3.Dot(v_contact, normal) * normal;
                float vt_len = v_tangent.Length();
                if (vt_len > 1e-4f)
                {
                    Vector3 t = v_tangent / vt_len;
                    Vector3 r_cross_t = Vector3.Cross(r, t);
                    Vector3 I_r_cross_t = Vector3.Transform(r_cross_t, I_world_inv);
                    float denom_t = 1f / Mass + Vector3.Dot(I_r_cross_t, r_cross_t);
                    if (denom_t > 1e-8f)
                    {
                        float jt = -vt_len / denom_t;
                        float maxFriction = friction * j_scalar;
                        jt = MathHelper.Clamp(jt, -maxFriction, maxFriction);

                        Vector3 frictionImpulse = jt * t;
                        Velocity += frictionImpulse / Mass;
                        AngularVelocity += Vector3.Transform(Vector3.Cross(r, frictionImpulse), I_world_inv);
                    }
                }
            }
        }

        foreach (var (_, normal, penetration) in contacts)
        {
            if (penetration > slop)
            {
                WorldPosition += (penetration - slop) * baumgarte * normal;
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
