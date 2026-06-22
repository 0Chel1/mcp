using IDEOS.Input;
using MCP.Features;
using MCP.Graphics;
using MCP.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
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
    MouseState prevMouseState;
    const float HighlightDecayPerSecond = 2f;
    BlocksManagement blocks = new BlocksManagement();

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
        blocks.faceVerts = blocks.BuildCubeFaceVertexArrays(u0, v0, u1, v1);

        blocks.cubes.Clear();
        blocks.cubes.Add(Matrix.Identity);

        blocks.faceEmission = new List<float[]>();
        foreach (var _ in blocks.cubes) blocks.faceEmission.Add(new float[6]);

        blocks.faceVisible = new List<bool[]>();
        for (int i = 0; i < blocks.cubes.Count; i++) blocks.faceVisible.Add(new bool[6] { true, true, true, true, true, true });
        blocks.UpdateVisibilityForAll();
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

        for (int i = 0; i < blocks.cubes.Count; i++)
        {
            blocks.HighlightFaceByRay(origin, dir, blocks.cubes[i], i);
        }

        if (Input.Mouse.WasButtonJustPressed(MouseButton.Right))
        {
            var n = blocks.currentFace switch { 0 => new Vector3(0, 0, -1), 1 => new Vector3(1, 0, 0), 2 => new Vector3(0, 0, 1), 3 => new Vector3(-1, 0, 0), 4 => new Vector3(0, 1, 0), 5 => new Vector3(0, -1, 0), _ => Vector3.Zero };
            var faceWorldNormal = Vector3.Normalize(Vector3.TransformNormal(n, blocks.cubes[blocks.currentCubeIndex]));
            blocks.AddCube(blocks.cubes[blocks.currentCubeIndex].Translation + faceWorldNormal);
        }
        else if (Input.Mouse.WasButtonJustPressed(MouseButton.Left))
        {
            blocks.RemoveCubeAt(blocks.currentCubeIndex);
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        for (int c = 0; c < blocks.faceEmission.Count; c++)
        {
            for (int i = 0; i < 6; i++)
            {
                blocks.faceEmission[c][i] = MathF.Max(0f, blocks.faceEmission[c][i] - HighlightDecayPerSecond * dt);
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
        for (int cubeIndex = 0; cubeIndex < blocks.cubes.Count; cubeIndex++)
        {
            var world = blocks.cubes[cubeIndex];
            for (int face = 0; face < 6; face++)
            {
                if (blocks.faceVisible != null && cubeIndex < blocks.faceVisible.Count && !blocks.faceVisible[cubeIndex][face]) continue;

                basicEffect.World = world;
                float emiss = MathF.Min(blocks.faceEmission[cubeIndex][face], 1f);
                basicEffect.EmissiveColor = new Vector3(emiss, emiss, emiss);

                foreach (var pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, blocks.faceVerts[face], 0, 2);
                }
            }
        }

        base.Draw(gameTime);
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