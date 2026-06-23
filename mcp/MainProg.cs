using IDEOS.Input;
using MCP.Features;
using MCP.Graphics;
using MCP.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
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
    Vector3 cameraPos = new Vector3(0, 5.5f, 0);
    Vector3 lookDir = new Vector3(1, 0, 0);
    Vector3 oldLookDir;
    Vector3 upDir = new Vector3(0, 1, 0);
    Vector3 right = new Vector3(1, 0, 0);
    float yaw = 0f;
    float pitch = 0f;
    const float mouseSens = 0.005f;
    const float maxPitch = 1.47f;

    TextureAtlas atlas;
    const float HighlightDecayPerSecond = 2f;
    BlocksManagement blocks = new BlocksManagement();
    MapGeneration mapGen = new MapGeneration();

    int blockSelected = 0;

    private int frameCount = 0;
    private float elapsedTime = 0f;
    private float fps = 0f;
    private SpriteFont spriteFont;

    private bool showChunkBorders = false;
    private bool meshesInitialized = false;
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
        spriteFont = Content.Load<SpriteFont>("Arial");
        atlas = TextureAtlas.FromFile(Content, "atlas-definition.xml");
        List<TextureRegion> region = new List<TextureRegion>() { atlas.GetRegion("cobblestone") , atlas.GetRegion("grass") };

        for(int i = 0; i < region.Count; i++)
        {
            float u0 = region[i].SourceRectangle.X / (float)atlas.Texture.Width;
            float v0 = region[i].SourceRectangle.Y / (float)atlas.Texture.Height;
            float u1 = (region[i].SourceRectangle.X + region[i].SourceRectangle.Width) / (float)atlas.Texture.Width;
            float v1 = (region[i].SourceRectangle.Y + region[i].SourceRectangle.Height) / (float)atlas.Texture.Height;
            blocks.faceVerts.Add(blocks.BuildCubeFaceVertexArrays(u0, v0, u1, v1));
        }

        blocks.cubes.Clear();
        blocks.AddCube(Vector3.Zero, 0);
        mapGen.size = new Vector3(16, 2, 16);
        mapGen.GenMap(blocks);

        //blocks.RebuildMesh(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        if (!meshesInitialized)
        {
            meshesInitialized = true;
            blocks.RebuildMesh(GraphicsDevice);
        }
        /*float dtt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // FPS Counter
        elapsedTime += dtt;
        frameCount++;
        if (elapsedTime >= 1.0f)
        {
            fps = frameCount;
            frameCount = 0;
            elapsedTime = 0f;
        }*/

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

        float bestDistance = 8f;
        blocks.currentCubeIndex = -1;
        blocks.currentFace = -1;
        Vector3 dir = Vector3.Normalize(lookDir);
        if (!IsMouseVisible)
        {
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

            if (Input.Mouse.WasButtonJustPressed(MouseButton.Right) && blocks.currentFace >= 0)
            {
                Vector3 normal = BlocksManagement.FaceOffsets[blocks.currentFace];
                Vector3 placePos = cameraPos + dir * bestDistance + normal * 0.5f; // грубое приближение
                blocks.AddCube(placePos, blockSelected);
            }
            else if (Input.Mouse.WasButtonJustPressed(MouseButton.Left) && blocks.currentFace >= 0)
            {
                Vector3 removePos = cameraPos + dir * bestDistance;
                blocks.RemoveCube(removePos);
            }
        }

        blocks.HighlightFaceByRay(cameraPos, dir, ref bestDistance);



        for (int i = 1; i <= 9; i++)
        {
            if (Input.Keyboard.WasKeyJustPressed((Keys)((int)Keys.D0 + i)))
            {
                switch (i)
                {
                    case 1: blockSelected = 0; break;
                    case 2: blockSelected = 1; break;
                }
                    
            }
        }



        if (Input.Keyboard.WasKeyJustPressed(Keys.Tab)) 
        {
            IsMouseVisible = !IsMouseVisible;
            if(IsMouseVisible) oldLookDir = lookDir;
            if (!IsMouseVisible) lookDir = oldLookDir;
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

        if (blocks.meshNeedsRebuild) blocks.RebuildMesh(GraphicsDevice);

        if (basicEffect == null) basicEffect = new BasicEffect(GraphicsDevice);

        basicEffect.VertexColorEnabled = false;
        basicEffect.TextureEnabled = true;
        basicEffect.Texture = atlas.Texture;
        basicEffect.World = Matrix.Identity;
        basicEffect.View = Matrix.CreateLookAt(cameraPos, cameraPos + lookDir, upDir);
        basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(fov), GraphicsDevice.Viewport.AspectRatio, z.X, z.Y);

        foreach (var chunk in blocks.chunkManager.Chunks.Values)
        {
            if (chunk.VertexBuffer == null || chunk.VertexCount == 0) continue;

            foreach (var pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.SetVertexBuffer(chunk.VertexBuffer);
                GraphicsDevice.Indices = chunk.IndexBuffer;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, chunk.VertexCount, 0, chunk.VertexCount / 3);
            }
        }

        // FPS
        /*SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);
        SpriteBatch.DrawString(spriteFont, $"FPS: {fps:F0}", new Vector2(10, 10), Color.Yellow);
        SpriteBatch.End();*/

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