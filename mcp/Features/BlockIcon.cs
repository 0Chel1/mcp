using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace MCP.Features;
public class BlockIcon
{
    private readonly BlocksManagement _blocks;

    public int Size = 48;

    public Vector2 Offset = new Vector2(-24, 200);

    public float FaceLightTop = 1.0f;
    public float FaceLightRight = 0.85f;
    public float FaceLightFront = 0.65f;

    public float Pitch = MathHelper.Pi / 6f;  // 30°

    public float Yaw = MathHelper.Pi / 4f;     // 45°

    public BlockIcon(BlocksManagement blocks)
    {
        _blocks = blocks;
    }

    public void Draw(GraphicsDevice gd, BasicEffect effect, Texture2D atlas, int blockType, Vector2 screenCenter)
    {
        if (blockType < 0 || blockType >= _blocks.faceVerts.Count) return;

        var faces = _blocks.faceVerts[blockType];
        if (faces == null || faces.Length < 6) return;

        var drawList = new List<(int FaceIndex, float Light)>
        {
            (4, FaceLightTop), //4
            (1, FaceLightRight), //1
            (0, FaceLightFront), //0
        };

        float scale = Size * 0.5f;

        Matrix rotY = Matrix.CreateRotationY(Yaw);
        Matrix rotX = Matrix.CreateRotationX(Pitch);
        Matrix toOrigin = Matrix.CreateTranslation(-0.5f, -0.5f, -0.5f);
        // Масштаб
        Matrix scaleMat = Matrix.CreateScale(scale);
        Vector3 iconCenter = new Vector3(screenCenter.X, screenCenter.Y, 0f);
        Matrix toScreen = Matrix.CreateTranslation(iconCenter);

        Matrix world = toScreen * scaleMat * rotX * rotY * toOrigin;

        var prevWorld = effect.World;
        var prevView = effect.View;
        var prevProj = effect.Projection;
        var prevTextureEnabled = effect.TextureEnabled;
        var prevVertexColorEnabled = effect.VertexColorEnabled;
        var prevAlpha = effect.Alpha;

        effect.World = world;
        effect.View = Matrix.Identity;
        effect.Projection = Matrix.CreateOrthographicOffCenter(0, gd.Viewport.Width, gd.Viewport.Height, 0, -1000f, 1000f);
        effect.TextureEnabled = true;
        effect.Texture = atlas;
        effect.VertexColorEnabled = false;


        foreach (var (faceIdx, light) in drawList)
        {
            var face = faces[faceIdx];
            if (face == null) continue;

            var verts = new VertexPositionTexture[6];
            for (int i = 0; i < 6; i++) 
                verts[i] = new VertexPositionTexture(Vector3.Transform(face[i].Position, world), face[i].TextureCoordinate);

            effect.Alpha = light;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, 2);
            }
        }

        effect.World = prevWorld;
        effect.View = prevView;
        effect.Projection = prevProj;
        effect.TextureEnabled = prevTextureEnabled;
        effect.VertexColorEnabled = prevVertexColorEnabled;
        effect.Alpha = prevAlpha;
    }

    
    /*public void DrawInSlot(SpriteBatch spriteBatch, GraphicsDevice gd, BasicEffect effect,
        Texture2D atlas, int blockType, int slotIndex, int slotSize,
        Vector2 hotbarStart, int hotbarY) {
        Vector2 iconCenter = new Vector2(hotbarStart.X + slotIndex * slotSize + slotSize / 2f, hotbarY + slotSize / 2f);
        Draw(spriteBatch, gd, effect, atlas, blockType, iconCenter);
    }*/
}
