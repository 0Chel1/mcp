using IDEOS.Input;
using MCP.Graphics;
using MCP.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Threading;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace MCP;

public class MainProg : Renderer
{
    Vector2 resolution = new Vector2(1640, 860);
    float fov = 90;
    Vector2 z = new Vector2(0.1f, 1000f);
    Vector3 lightPos = new Vector3(1, 2, 4);

    // Camera
    Vector3 cameraPos = new Vector3(0, 0, 0);
    Vector3 lookDir = new Vector3(0, 0, 1);
    Vector3 upDir = new Vector3(0, 1, 0);
    Vector3 right = new Vector3(1, 0, 0);
    float yaw = 0f;
    float pitch = 0f;
    const float mouseSens = 0.005f;
    const float maxPitch = 1.47f;

    TextureAtlas atlas;
    VertexPositionTexture[][] faceVerts;
    List<float[]> faceEmission;
    MouseState prevMouseState;
    List<Matrix> cubes = new List<Matrix>();
    List<bool[]> faceVisible;
    const float HighlightAdd = 1.0f;
    const float HighlightDecayPerSecond = 2f;
    int currentCubeIndex;
    int currentFace = -1;

    private static readonly Vector3[] FaceOffsets = new Vector3[]
    {
        new Vector3(0, 0, -1), // front
        new Vector3(1, 0, 0),  // right
        new Vector3(0, 0, 1),  // back
        new Vector3(-1, 0, 0), // left
        new Vector3(0, 1, 0),  // top
        new Vector3(0, -1, 0)  // bottom
    };

    private static readonly int[] OppositeFace = new int[] { 2, 3, 0, 1, 5, 4 };

    protected override void Initialize()
    {
        base.Initialize();

        GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.CullClockwiseFace };
        IsMouseVisible = false;

        SetProjectionParameters(resolution, fov, z);

        GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
    }

    protected override void LoadContent()
    {
        base.LoadContent();

        atlas = TextureAtlas.FromFile(Content, "atlas-definition.xml");
        var region = atlas.GetRegion("cobblestone");

        float u0 = region.SourceRectangle.X / (float)atlas.Texture.Width;
        float v0 = region.SourceRectangle.Y / (float)atlas.Texture.Height;
        float u1 = (region.SourceRectangle.X + region.SourceRectangle.Width) / (float)atlas.Texture.Width;
        float v1 = (region.SourceRectangle.Y + region.SourceRectangle.Height) / (float)atlas.Texture.Height;
        faceVerts = BuildCubeFaceVertexArrays(u0, v0, u1, v1);

        cubes.Clear();
        cubes.Add(Matrix.Identity);

        faceEmission = new List<float[]>();
        foreach (var _ in cubes) faceEmission.Add(new float[6]);

        faceVisible = new List<bool[]>();
        for (int i = 0; i < cubes.Count; i++) faceVisible.Add(new bool[6] { true, true, true, true, true, true });
        UpdateVisibilityForAll();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Input.Keyboard.WasKeyJustPressed(Keys.Escape)) Exit();
        // Controls
        const float speed = 0.05f;
        if (Input.Keyboard.IsKeyDown(Keys.W)) cameraPos += lookDir * speed;
        if(Input.Keyboard.IsKeyDown(Keys.S)) cameraPos -= lookDir * speed;
        if(Input.Keyboard.IsKeyDown(Keys.A)) cameraPos -= Vector3.Normalize(Vector3.Cross(lookDir, upDir)) * speed;
        if(Input.Keyboard.IsKeyDown(Keys.D)) cameraPos += Vector3.Normalize(Vector3.Cross(lookDir, upDir)) * speed;
        if(Input.Keyboard.IsKeyDown(Keys.Space)) cameraPos += upDir * speed;
        if(Input.Keyboard.IsKeyDown(Keys.LeftShift)) cameraPos -= upDir * speed;

        // Relative mouse look
        var center = new Point(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
        var ms = Mouse.GetState();
        int dx = ms.X - center.X;
        int dy = ms.Y - center.Y;
        if (dx != 0 || dy != 0)
        {
            yaw -= dx * mouseSens;
            pitch -= dy * mouseSens;
            pitch = Math.Clamp(pitch, -maxPitch, maxPitch);

            float cosPitch = MathF.Cos(pitch);
            lookDir = new Vector3(MathF.Sin(yaw) * cosPitch, MathF.Sin(pitch), MathF.Cos(yaw) * cosPitch);
            lookDir = Vector3.Normalize(lookDir);

            right = Vector3.Normalize(Vector3.Cross(lookDir, Vector3.UnitY));
            upDir = Vector3.Normalize(Vector3.Cross(right, lookDir));

            Mouse.SetPosition(center.X, center.Y);
        }

        Vector3 origin = cameraPos;
        Vector3 dir = Vector3.Normalize(lookDir);

        for (int i = 0; i < cubes.Count; i++)
        {
            HighlightFaceByRay(origin, dir, cubes[i], i);
        }

        if (Input.Mouse.WasButtonJustPressed(MouseButton.Right))
        {
            var n = currentFace switch { 0 => new Vector3(0, 0, -1), 1 => new Vector3(1, 0, 0), 2 => new Vector3(0, 0, 1), 3 => new Vector3(-1, 0, 0), 4 => new Vector3(0, 1, 0), 5 => new Vector3(0, -1, 0), _ => Vector3.Zero };
            var faceWorldNormal = Vector3.Normalize(Vector3.TransformNormal(n, cubes[currentCubeIndex]));
            AddCube(cubes[currentCubeIndex].Translation + faceWorldNormal);
        }
        else if (Input.Mouse.WasButtonJustPressed(MouseButton.Left))
        {
            RemoveCubeAt(currentCubeIndex);
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        for (int c = 0; c < faceEmission.Count; c++)
        {
            for (int i = 0; i < 6; i++)
            {
                faceEmission[c][i] = MathF.Max(0f, faceEmission[c][i] - HighlightDecayPerSecond * dt);
            }
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        basicEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = false,
            TextureEnabled = true
        };

        basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(fov), GraphicsDevice.Viewport.AspectRatio, z.X, z.Y);
        basicEffect.View = Matrix.CreateLookAt(cameraPos, cameraPos + lookDir, upDir);
        basicEffect.Texture = atlas.Texture;
        basicEffect.TextureEnabled = true;

        //draw all cubes
        for (int cubeIndex = 0; cubeIndex < cubes.Count; cubeIndex++)
        {
            var world = cubes[cubeIndex];
            for (int face = 0; face < 6; face++)
            {
                if (faceVisible != null && cubeIndex < faceVisible.Count && !faceVisible[cubeIndex][face]) continue;

                basicEffect.World = world;
                float emiss = MathF.Min(faceEmission[cubeIndex][face], 1f);
                basicEffect.EmissiveColor = new Vector3(emiss, emiss, emiss);

                foreach (var pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, faceVerts[face], 0, 2);
                }
            }
        }

        base.Draw(gameTime);
    }

    private VertexPositionTexture[][] BuildCubeFaceVertexArrays(float u0, float v0, float u1, float v1)
    {
        // cube corners (0..1)
        var p000 = new Vector3(0,0,0);
        var p001 = new Vector3(0,0,1);
        var p010 = new Vector3(0,1,0);
        var p011 = new Vector3(0,1,1);
        var p100 = new Vector3(1,0,0);
        var p101 = new Vector3(1,0,1);
        var p110 = new Vector3(1,1,0);
        var p111 = new Vector3(1,1,1);

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

    // Performs ray-triangle tests against each face's two triangles in local space transformed by world.
    // If hit, sets emission for that face on the given cubeIndex.
    private void HighlightFaceByRay(Vector3 origin, Vector3 direction, Matrix world, int cubeIndex)
    {
        if (faceVerts == null) return;

        float nearestT = float.PositiveInfinity;
        int hitFace = -1;

        Vector3 dir = Vector3.Normalize(direction);

        for (int f = 0; f < faceVerts.Length; f++)
        {
            var fv = faceVerts[f];

            for (int tri = 0; tri < 2; tri++)
            {
                int baseIdx = tri * 3;
                Vector3 a = Vector3.Transform(fv[baseIdx + 0].Position, world);
                Vector3 b = Vector3.Transform(fv[baseIdx + 1].Position, world);
                Vector3 c = Vector3.Transform(fv[baseIdx + 2].Position, world);

                if (Raycast.RayIntersectsTriangle(origin, dir, 5f, a, b, c, out float t, out float u, out float v, false))
                {
                    if (t >= 0f && t < nearestT)
                    {
                        nearestT = t;
                        hitFace = f;
                    }
                }
            }
        }

        if (hitFace >= 0 && cubeIndex >= 0 && cubeIndex < faceEmission.Count)
        {
            faceEmission[cubeIndex][hitFace] = MathF.Min(faceEmission[cubeIndex][hitFace] + HighlightAdd, 2f);
            currentCubeIndex = cubeIndex;
            currentFace = hitFace;
        }
    }

    // Adds a cube at the given world position (position is cube-space origin)
    private void AddCube(Vector3 position)
    {
        var p = new Vector3(MathF.Round(position.X), MathF.Round(position.Y), MathF.Round(position.Z));
        for (int i = 0; i < cubes.Count; i++) if (cubes[i].Translation == p) return;

        var vis = new bool[6] { true, true, true, true, true, true };
        for (int i = 0; i < cubes.Count; i++)
        {
            var pos = cubes[i].Translation;
            var delta = pos - p; // if pos == p + offset -> delta == offset
            for (int f = 0; f < FaceOffsets.Length; f++)
            {
                if (delta == FaceOffsets[f])
                {
                    vis[f] = false;
                    if (i < faceVisible.Count) faceVisible[i][OppositeFace[f]] = false;
                }
                else if (delta == -FaceOffsets[f])
                {
                    vis[OppositeFace[f]] = false;
                    if (i < faceVisible.Count) faceVisible[i][f] = false;
                }
            }
        }

        cubes.Add(Matrix.CreateTranslation(p));
        faceEmission.Add(new float[6]);
        faceVisible.Add(vis);
    }

    // recompute visibility for all cubes (used at startup)
    private void UpdateVisibilityForAll()
    {
        for (int i = 0; i < faceVisible.Count; i++)
        {
            for (int f = 0; f < 6; f++) faceVisible[i][f] = true;
        }

        for (int i = 0; i < cubes.Count; i++)
        {
            var pi = cubes[i].Translation;
            for (int j = i + 1; j < cubes.Count; j++)
            {
                var pj = cubes[j].Translation;
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

    private void RemoveCubeAt(int index)
    {
        if (index < 0 || index >= cubes.Count) return;
        cubes.RemoveAt(index);
        faceEmission.RemoveAt(index);
        faceVisible.RemoveAt(index);
        UpdateVisibilityForAll();
    }
}

internal static class Program
{
    public static void Main(string[] args)
    {
        using var game = new MainProg();
        game.Run();
    }
}