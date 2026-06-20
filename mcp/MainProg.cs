using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace MCP;

public class MainProg : Renderer
{
    Vector2 resolution = new Vector2(1640, 860);
    float fov = 90;
    Vector2 z = new Vector2(0.1f, 1000f);
    Vector3[] cube;
    Vector3 lightPos = new Vector3(1, 2, 4);

    //Camera
    Vector3 cameraPos = new Vector3(0, 0, 0);
    Vector3 lookDir = new Vector3(0, 0, 1);
    Vector3 upDir = new Vector3(0, 1, 0);
    Vector3 right = new Vector3(1, 0, 0);
    Matrix4x4 viewMat = new Matrix4x4();
    float yaw = 0f;
    float pitch = 0f;
    const float mouseSens = 0.005f;
    const float maxPitch = 1.47f;

    protected override void Initialize()
    {
        IsMouseVisible = false;
        base.Initialize();
        basicEffect = new BasicEffect(GraphicsDevice);
        basicEffect.VertexColorEnabled = true;
        basicEffect.Projection = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0, 1);
        GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.CullCounterClockwiseFace };
        SetProjectionParameters(resolution, fov, z);
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        cube = new Vector3[]
        {   new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
            new Vector3(0, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0),

            new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1),
            new Vector3(1, 0, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1),
            
            new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1),
            new Vector3(1, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1),
            
            new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0),
            new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0),
            
            new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1),
            new Vector3(0, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 1, 0),
            
            new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 0),
            new Vector3(1, 0, 1), new Vector3(0, 0, 0), new Vector3(1, 0, 0)};
    }

    protected override void Update(GameTime gameTime)
    {
        if (Input.Keyboard.WasKeyJustPressed(Keys.Escape)) Exit();
        //Controls
        const float speed = 0.05f;
        if (Input.Keyboard.IsKeyDown(Keys.W)) cameraPos += lookDir * speed;
        if(Input.Keyboard.IsKeyDown(Keys.S)) cameraPos -= lookDir * speed;
        if(Input.Keyboard.IsKeyDown(Keys.A)) cameraPos += Vector3.Normalize(Vector3.Cross(lookDir, upDir)) * speed;
        if(Input.Keyboard.IsKeyDown(Keys.D)) cameraPos -= Vector3.Normalize(Vector3.Cross(lookDir, upDir)) * speed;
        if(Input.Keyboard.IsKeyDown(Keys.Space)) cameraPos += upDir * speed;
        if(Input.Keyboard.IsKeyDown(Keys.LeftShift)) cameraPos -= upDir * speed;

        var center = new Point(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
        var ms = Mouse.GetState();
        float dx = ms.X - center.X;
        float dy = ms.Y - center.Y;

        if (dx != 0 || dy != 0)
        {
            yaw += dx * mouseSens;
            pitch += (-dy) * mouseSens;

            pitch = Math.Clamp(pitch, -maxPitch, maxPitch);
            float cosPitch = MathF.Cos(pitch);
            lookDir = new Vector3(MathF.Sin(yaw) * cosPitch, MathF.Sin(pitch), MathF.Cos(yaw) * cosPitch);
            lookDir = Vector3.Normalize(lookDir);

            right = Vector3.Normalize(Vector3.Cross(lookDir, Vector3.UnitY));
            upDir = Vector3.Normalize(Vector3.Cross(right, lookDir));

            Mouse.SetPosition(center.X, center.Y);
        }

        Vector3 target = cameraPos + lookDir;
        viewMat = Invert(getPointAtMat(cameraPos, target, upDir));

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        var rot = Matrix4x4.Multiply(getRotY(0), getRotX(0));
        DrawMeshWithCpuCulling(cube, rot, Color.LimeGreen, cameraPos, viewMat, lightPos);
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