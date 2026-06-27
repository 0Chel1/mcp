using IDEOS.Input;
using mcp.Features;
using MCP.Features;
using MCP.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace MCP;

public class MainProg : Renderer
{
    Vector2 resolution = new Vector2(1640, 860);
    float fov = 90;
    Vector2 z = new Vector2(0.1f, 1000f);
    //Vector3 lightPos = new Vector3(1, 2, 4);

    // Camera
    Vector3 cameraPos = new Vector3(40, 31f, 40);
    Vector3 lookDir = new Vector3(1, 0, 0);
    Vector3 oldLookDir;
    Vector3 upDir = new Vector3(0, 1, 0);
    Vector3 right = new Vector3(1, 0, 0);
    float yaw = 0f;
    float pitch = 0f;
    const float mouseSens = 0.005f;
    const float maxPitch = 1.57f;
    float speed = 0.08f;
    bool speedUp = false;

    TextureAtlas atlas;
    const float HighlightDecayPerSecond = 2f;
    MapGeneration mapGen = new MapGeneration();
    BlocksManagement blocks = new BlocksManagement();

    int blockSelected = 0;
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
        List<TextureRegion> region = new List<TextureRegion>() { atlas.GetRegion("cobblestone") , atlas.GetRegion("grass"), atlas.GetRegion("dirt") };

        for(int i = 0; i < region.Count; i++)
        {
            float u0 = region[i].SourceRectangle.X / (float)atlas.Texture.Width;
            float v0 = region[i].SourceRectangle.Y / (float)atlas.Texture.Height;
            float u1 = (region[i].SourceRectangle.X + region[i].SourceRectangle.Width) / (float)atlas.Texture.Width;
            float v1 = (region[i].SourceRectangle.Y + region[i].SourceRectangle.Height) / (float)atlas.Texture.Height;
            blocks.faceVerts.Add(blocks.BuildBlockFaceVertexArrays(u0, v0, u1, v1));
        }

        //blocks.blocks.Clear();
        //blocks.AddBlock(Vector3.Zero, 1); раскоментировать если нужны тесты без карты.
        blocks.LoadWorld("world.dat");
        if (blocks.WorldBlocks.Count == 0) // если файла не было
        {
            mapGen.size = new Vector3(80, 30, 80);
            mapGen.GenMap(blocks);
        }

        blocks.meshNeedsRebuild = true;
    }

    protected override void Update(GameTime gameTime)
    {
        if (Input.Keyboard.WasKeyJustPressed(Keys.Escape)) 
        {
            //blocks.SaveWorld("world.dat");
            Exit();
        }
        // Controls
        if (Input.Keyboard.IsKeyDown(Keys.W)) cameraPos += lookDir * speed;
        if(Input.Keyboard.IsKeyDown(Keys.S)) cameraPos -= lookDir * speed;
        if(Input.Keyboard.IsKeyDown(Keys.A)) cameraPos -= Vector3.Normalize(Vector3.Cross(lookDir, upDir)) * speed;
        if(Input.Keyboard.IsKeyDown(Keys.D)) cameraPos += Vector3.Normalize(Vector3.Cross(lookDir, upDir)) * speed;
        if(Input.Keyboard.IsKeyDown(Keys.Space)) cameraPos += upDir * speed;
        if(Input.Keyboard.IsKeyDown(Keys.LeftShift)) cameraPos -= upDir * speed;

        if (Input.Keyboard.IsKeyDown(Keys.LeftControl)) speedUp = true;
        else speedUp = false;

        if (speedUp) speed = 0.2f;
        else speed = 0.08f;

        //if(Input.Keyboard.WasKeyJustPressed(Keys.F)) 

        // Relative mouse look
        var center = new Point(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
        var ms = Mouse.GetState();
        int dx = ms.X - center.X;
        int dy = ms.Y - center.Y;

        float bestDistance = 5f;
        blocks.currentBlockIndex = -1;
        blocks.currentFace = -1;
        Vector3 dir = Vector3.Normalize(lookDir);
        blocks.HighlightFaceByRay(cameraPos, dir, ref bestDistance);
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

            if (Input.Mouse.WasButtonJustPressed(MouseButton.Right) && blocks.HasHighlight)
            {
                Vector3 normal = BlocksManagement.FaceOffsets[blocks.HighlightFace];
                Vector3 placeCell = new Vector3(
                    MathF.Round(blocks.HighlightPos.X + normal.X),
                    MathF.Round(blocks.HighlightPos.Y + normal.Y),
                    MathF.Round(blocks.HighlightPos.Z + normal.Z)
                );
                blocks.AddBlock(placeCell, blockSelected);
            }
            else if (Input.Mouse.WasButtonJustPressed(MouseButton.Left) && blocks.HasHighlight)
            {
                Vector3 removeCell = new Vector3(
                    MathF.Round(blocks.HighlightPos.X),
                    MathF.Round(blocks.HighlightPos.Y),
                    MathF.Round(blocks.HighlightPos.Z)
                );
                blocks.RemoveBlock(removeCell);
                blocks.chunkManager.RebuildMeshes(GraphicsDevice);
            }
        }



        for (int i = 1; i <= 9; i++)
        {
            if (Input.Keyboard.WasKeyJustPressed((Keys)((int)Keys.D0 + i)))
            {
                switch (i)
                {
                    case 1: blockSelected = 0; break;
                    case 2: blockSelected = 1; break;
                    case 3: blockSelected = 2; break;
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

        //Физические структуры
        for (int i = blocks.ActiveStructures.Count - 1; i >= 0; i--)
        {
            var s = blocks.ActiveStructures[i];
            s.Update((float)gameTime.ElapsedGameTime.TotalSeconds, blocks);

            if (s.Velocity.Length() < 0.1f && s.Velocity.Length() < 0.1f)
            {
                blocks.ActiveStructures.RemoveAt(i);
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
            int primitiveCount = chunk.IndexBuffer.IndexCount / 3;

            foreach (var pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.SetVertexBuffer(chunk.VertexBuffer);
                GraphicsDevice.Indices = chunk.IndexBuffer;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, chunk.VertexCount, 0, primitiveCount);
            }
        }

        if (blocks.HasHighlight)
        {
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = true;

            var type = blocks.GetBlockType(blocks.HighlightPos);
            var faces = blocks.faceVerts[type % blocks.faceVerts.Count];
            var face = faces[blocks.HighlightFace];
            var highlightVerts = new VertexPositionColor[6];
            var worldMat = Matrix.CreateTranslation(blocks.HighlightPos);
            for (int i = 0; i < 6; i++)
            {
                var p = Vector3.Transform(face[i].Position, worldMat);
                highlightVerts[i] = new VertexPositionColor(p, new Color(0.7f, 0.7f, 0.7f, 0.5f));
            }

            foreach (var pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, highlightVerts, 0, 2);
            }

            GraphicsDevice.BlendState = BlendState.Opaque;
            basicEffect.TextureEnabled = true;
            basicEffect.VertexColorEnabled = false;
        }

        //Физические структуры
        if (blocks.ActiveStructures.Count > 0)
        {
            foreach (var structure in blocks.ActiveStructures)
            {
                foreach (var (pos, type) in structure.Blocks)
                {
                    var cubeFaces = blocks.faceVerts[type % blocks.faceVerts.Count];
                    Matrix worldMat = Matrix.CreateTranslation(pos);

                    for (int f = 0; f < 6; f++)
                    {
                        var face = cubeFaces[f];
                        var verts = new VertexPositionTexture[6];

                        for (int i = 0; i < 6; i++)
                        {
                            verts[i] = new VertexPositionTexture(
                                Vector3.Transform(face[i].Position, worldMat),
                                face[i].TextureCoordinate
                            );
                        }

                        foreach (var pass in basicEffect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, 2);
                        }
                    }
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