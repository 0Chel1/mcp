using mcp.Features;
using MCP.Features;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mcp.Physics;

public class GravityGun
{
    public bool Active = false;
    public PhysicsStructure Held = null;

    /// <summary>Расстояние от камеры до структуры.</summary>
    public float HoldDistance = 4f;

    /// <summary>Скорость поворота клавишами, рад/сек.</summary>
    public float RotationSpeed = 2.0f;

    /// <summary>Сила отрыва при хватании блока мира.</summary>
    public float DetachImpulse = 4f;

    /// <summary>Сила толчка при ПКМ.</summary>
    public float ThrowImpulse = 12f;

    /// <summary>Жёсткость пружины, тянущей структуру к точке хвата (P-контроллер).</summary>
    public float SpringStiffness = 60f;

    /// <summary>Коэффициент демпфирования (чтобы не было колебаний).</summary>
    public float SpringDamping = 2f;

    /// <summary>Дополнительный поворот, который игрок применяет к структуре (накапливается).</summary>
    private Quaternion playerRotation = Quaternion.Identity;

    private Vector3 _lookDir;
    private Vector3 _cameraPos;

    /// <summary>
    /// Вызывается каждый кадр из MainProg.Update.
    /// </summary>
    public void Update(GameTime gameTime, BlocksManagement blocks, Vector3 cameraPos, Vector3 lookDir, Vector3 upDir,
        MouseState mouse, KeyboardState keyboard)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _lookDir = lookDir;
        _cameraPos = cameraPos;

        if (!Active) return;

        float rotStep = RotationSpeed * dt;
        bool rotated = false;

        if (keyboard.IsKeyDown(Keys.Q))
        {
            playerRotation = Quaternion.Concatenate(playerRotation, Quaternion.CreateFromAxisAngle(Vector3.Normalize(lookDir), rotStep * 10));
            rotated = true;
        }
        if (keyboard.IsKeyDown(Keys.E))
        {
            playerRotation = Quaternion.Concatenate(playerRotation, Quaternion.CreateFromAxisAngle(-Vector3.Normalize(lookDir), rotStep));
            rotated = true;
        }

        Vector3 rightDir = Vector3.Normalize(Vector3.Cross(lookDir, upDir));
        if (keyboard.IsKeyDown(Keys.R))
        {
            playerRotation = Quaternion.Concatenate(playerRotation, Quaternion.CreateFromAxisAngle(rightDir, rotStep));
            rotated = true;
        }
        if (keyboard.IsKeyDown(Keys.F))
        {
            playerRotation = Quaternion.Concatenate(playerRotation, Quaternion.CreateFromAxisAngle(-rightDir, rotStep));
            rotated = true;
        }

        if (rotated && Held != null)
        {
            Held.Orientation = Quaternion.Normalize(Held.Orientation);
        }

        int wheelDelta = mouse.ScrollWheelValue - _lastWheel;
        if (wheelDelta != 0)
        {
            HoldDistance += wheelDelta * 0.01f;
            HoldDistance = MathHelper.Clamp(HoldDistance, 2f, 30f);
        }
        _lastWheel = mouse.ScrollWheelValue;

        bool leftClick = mouse.LeftButton == ButtonState.Pressed && _prevLeftButton == ButtonState.Released;
        if (leftClick)
        {
            if (Held == null) TryGrab(blocks, cameraPos, lookDir);
            else Release(Held, throwVelocity: null);
        }

        bool rightClick = mouse.RightButton == ButtonState.Pressed && _prevRightButton == ButtonState.Released;
        if (rightClick && Held != null)
        {
            Vector3 throwVel = lookDir * ThrowImpulse;
            Release(Held, throwVel);
        }

        _prevLeftButton = mouse.LeftButton;
        _prevRightButton = mouse.RightButton;

        if (Held != null)
        {
            Vector3 targetPos = cameraPos + lookDir * HoldDistance;
            Vector3 toTarget = targetPos - Held.WorldPosition;
            Vector3 springForce = SpringStiffness * toTarget - SpringDamping * Held.Velocity;
            Held.Velocity += springForce * dt;
            //Held.Settled = false;

            Held.Velocity.Y = MathHelper.Lerp(Held.Velocity.Y, 0f, 0.5f);
            Held.AngularVelocity *= MathF.Max(0f, 1f - 4f * dt);
            Held.WorldPosition = targetPos;
        }
    }

    private int _lastWheel;
    private ButtonState _prevLeftButton = ButtonState.Released;
    private ButtonState _prevRightButton = ButtonState.Released;

    private void TryGrab(BlocksManagement blocks, Vector3 cameraPos, Vector3 lookDir)
    {
        PhysicsStructure best = null;
        float bestScore = float.MaxValue;

        foreach (var s in blocks.ActiveStructures)
        {
            Vector3 toStruct = s.WorldPosition - cameraPos;
            float dist = toStruct.Length();
            if (dist > 20f) continue;

            toStruct /= dist;
            float dot = Vector3.Dot(toStruct, lookDir);
            if (dot < 0.95f) continue;

            float score = (1f - dot) * 10f + dist * 0.1f;
            if (score < bestScore)
            {
                bestScore = score;
                best = s;
            }
        }

        if (best != null)
        {
            Held = best;
            playerRotation = Quaternion.Identity;
            return;
        }

        if (blocks.HasHighlight)
        {
            Vector3 hitPos = blocks.HighlightPos;
            var component = FindConnectedComponent(blocks, hitPos);
            if (component.Count > 0 && component.Count < 5)
            {
                var structure = new PhysicsStructure(component);
                foreach (var (pos, _) in component)
                {
                    blocks.WorldBlocks.Remove(pos);
                    blocks.chunkManager.MarkChunkDirty(pos);
                }
                blocks.ActiveStructures.Add(structure);
                Held = structure;
                playerRotation = Quaternion.Identity;

                Held.Velocity += lookDir * 0.5f;
            }
        }
    }

    private List<(Vector3, int)> FindConnectedComponent(BlocksManagement blocks, Vector3 start)
    {
        var result = new List<(Vector3, int)>();
        var visited = new HashSet<Vector3> { start };
        var queue = new Queue<Vector3>();
        queue.Enqueue(start);

        while (queue.Count > 0 && result.Count < 256)
        {
            Vector3 current = queue.Dequeue();
            result.Add((current, blocks.GetBlockType(current)));

            foreach (var off in BlocksManagement.FaceOffsets)
            {
                Vector3 n = current + off;
                if (!visited.Contains(n) && blocks.HasBlock(n))
                {
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }
        }
        return result;
    }

    public void Release(PhysicsStructure s, Vector3? throwVelocity)
    {
        if (throwVelocity.HasValue)
        {
            s.Velocity += throwVelocity.Value;
        }
        Held = null;
    }
}
