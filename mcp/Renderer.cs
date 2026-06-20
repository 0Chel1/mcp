using Microsoft.Xna.Framework.Graphics;
using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Matrix4x4 = System.Numerics.Matrix4x4;
using System.Runtime.InteropServices;
using System.Linq;

namespace MCP;

public class Renderer : Core
{
    private VertexPositionColor[] triangleVertices;
    public BasicEffect basicEffect;
    public Vector2 Resolution { get; set; } = new Vector2(1640, 860);
    public float Fov { get; set; } = 90f;
    public Vector2 ZRange { get; set; } = new Vector2(0.1f, 1000f);
    private Matrix4x4 projMat;
    private bool projectionDirty = true;
    public void SetProjectionParameters(Vector2 resolution, float fov, Vector2 z)
    {
        Resolution = resolution;
        Fov = fov;
        ZRange = z;
        projectionDirty = true;
    }

    public Renderer() : base("mcp", 1640, 860, false) { }
    public void DrawTriangle(Vector2[] p, Color color)
    {
        if (triangleVertices == null) triangleVertices = new VertexPositionColor[3];

        for(int i = 0; i < 3; i++) triangleVertices[i] = new VertexPositionColor(new Vector3(p[i].X, p[i].Y, 0), color);

        foreach (var pass in basicEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, triangleVertices, 0, 1);
        }
    }

    public Matrix4x4 GetProjectionMatrix()
    {
        if (!projectionDirty) return projMat;
        float aspect = Resolution.X / Resolution.Y;
        float tanHalfFov = MathF.Tan(Fov * MathF.PI / 180f * 0.5f);
        float f = 1f / tanHalfFov;

        projMat = new Matrix4x4(
        f / aspect, 0, 0, 0,
        0, 1f / tanHalfFov, 0, 0,
        0, 0, ZRange.Y / (ZRange.Y - ZRange.X), 1,
        0, 0, -ZRange.Y * ZRange.X / (ZRange.Y - ZRange.X), 0);

        projectionDirty = false;
        return projMat;
    }

    public Vector3 GetPoints(Vector3 i, Matrix4x4 m)
    {
        var v4 = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(i.X, i.Y, i.Z, 1f), m);

        if (v4.W != 0f)
        {
            v4.X /= v4.W;
            v4.Y /= v4.W;
            v4.Z /= v4.W;
        }

        return new Vector3(v4.X, v4.Y, v4.Z);
    }

    public Vector3[] Combine(Vector3[] mesh, Vector3 pivot, Matrix4x4 rot, Vector3 cameraPos)
    {
        Vector3[] trans = new Vector3[mesh.Length];
        var proj = GetProjectionMatrix();

        for (int i = 0; i < mesh.Length; i++)
        {
            Vector3 rotated = RotateAroundPivot(mesh[i], pivot, rot);
            Vector3 cameraSpace = rotated - cameraPos;
            Vector3 ndc = GetPoints(cameraSpace, proj);
            float sx = (ndc.X / 2 + 0.5f) * Resolution.X;
            float sy = (-ndc.Y / 2 + 0.5f) * Resolution.Y;

            trans[i] = new Vector3(sx, sy, ndc.Z);
        }

        return trans;
    }

    public Matrix4x4 getRotX(float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Matrix4x4(
            1, 0, 0, 0,
            0, cos, -sin, 0,
            0, sin, cos, 0,
            0, 0, 0, 1);
    }

    public Matrix4x4 getRotY(float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Matrix4x4(
            cos, 0, sin, 0,
            0, 1, 0, 0,
            -sin, 0, cos, 0,
            0, 0, 0, 1);
    }
    public Matrix4x4 getRotZ(float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Matrix4x4(
            cos, -sin, 0, 0,
            sin, cos, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1);
    }

    public Vector3 RotateAroundPivot(Vector3 vertex, Vector3 pivot, Matrix4x4 rot)
    {
        var v = new System.Numerics.Vector4(vertex.X - pivot.X, vertex.Y - pivot.Y, vertex.Z - pivot.Z, 1f);
        var r = System.Numerics.Vector4.Transform(v, rot);
        return new Vector3(r.X + pivot.X, r.Y + pivot.Y, r.Z + pivot.Z);
    }

    public void DrawMeshWithCpuCulling(Vector3[] mesh, Matrix4x4 rot, Color color, Vector3 cameraPos, Matrix4x4 viewMat, Vector3 lightPos)
    {
        var proj = GetProjectionMatrix();

        Vector3 pivot = new Vector3(0.5f, 0.5f, 0.5f);

        for (int i = 0; i < mesh.Length; i += 3)
        {
            var aW = RotateAroundPivot(mesh[i], pivot, rot);
            var bW = RotateAroundPivot(mesh[i + 1], pivot, rot);
            var cW = RotateAroundPivot(mesh[i + 2], pivot, rot);

            var normal = Vector3.Cross(bW - aW, cW - aW);
            var centroid = (aW + bW + cW) / 3f;
            var viewDir = cameraPos - centroid;
            if (Vector3.Dot(normal, viewDir) < 0f) continue;

            var paCamV = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(aW.X, aW.Y, aW.Z), viewMat);
            var pbCamV = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(bW.X, bW.Y, bW.Z), viewMat);
            var pcCamV = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(cW.X, cW.Y, cW.Z), viewMat);
            /*Vector3[] tri = new Vector3[] { new Vector3(paCamV.X, paCamV.Y, paCamV.Z), new Vector3(pbCamV.X, pbCamV.Y, pbCamV.Z), new Vector3(pcCamV.X, pcCamV.Y, pcCamV.Z) };
            Vector3[] clipped = ClipTriangle(tri, new Vector3(0, 0, 0.2f), new Vector3(0, 0, 1));

            for(int n = 0; n < clipped.Length; n += 3)
            {
                for(int m = 0; m < 3; m++)
                {
                    clipped[n + m] = GetPoints(clipped[n + m], proj);
                }
            }

            List<Vector3> Q = clipped.ToList();
            for(int x = 0; x < 4; x++)
            {
                List<Vector3> temp = new List<Vector3>();
                for(int y = 0; y < Q.Count; y++)
                {
                    Vector3[] newT = new Vector3[4];
                    switch (x)
                    {
                        case 0: newT = ClipTriangle(Q.ToArray(), new Vector3(1, 0, 0), new Vector3(-1, 0, 0)); break;
                        case 1: newT = ClipTriangle(Q.ToArray(), new Vector3(-1, 0, 0), new Vector3(1, 0, 0)); break;
                        case 2: newT = ClipTriangle(Q.ToArray(), new Vector3(0, 1, 0), new Vector3(0, -1, 0)); break;
                        case 3: newT = ClipTriangle(Q.ToArray(), new Vector3(0, -1, 0), new Vector3(0, 1, 0)); break;
                    }

                    for(int z = 0; z < newT.Length; z++)
                    {
                        temp.Add(newT[z]);
                    }
                }
                Q = temp;
            }*/

            var pa = GetPoints(new Vector3(paCamV.X, paCamV.Y, paCamV.Z), proj);
            var pb = GetPoints(new Vector3(pbCamV.X, pbCamV.Y, pbCamV.Z), proj);
            var pc = GetPoints(new Vector3(pcCamV.X, pcCamV.Y, pcCamV.Z), proj);

            float sxA = (pa.X * 0.5f + 0.5f) * Resolution.X;
            float syA = (-pa.Y * 0.5f + 0.5f) * Resolution.Y;
            float sxB = (pb.X * 0.5f + 0.5f) * Resolution.X;
            float syB = (-pb.Y * 0.5f + 0.5f) * Resolution.Y;
            float sxC = (pc.X * 0.5f + 0.5f) * Resolution.X;
            float syC = (-pc.Y * 0.5f + 0.5f) * Resolution.Y;

            Vector2[] p = new Vector2[] { new Vector2(sxA, syA), new Vector2(sxB, syB), new Vector2(sxC, syC) };

            DrawTriangle(p, color * MathF.Max(0.5f, Vector3.Dot(normal, lightPos - centroid)));
        }
    }

    public Matrix4x4 Invert(Matrix4x4 mat)
    {
        Matrix4x4 mat2 = new Matrix4x4();
        mat2.M11 = mat.M11; mat2.M12 = mat.M21; mat2.M13 = mat.M31; mat2.M14 = 0f;
        mat2.M21 = mat.M12; mat2.M22 = mat.M22; mat2.M23 = mat.M32; mat2.M24 = 0f;
        mat2.M31 = mat.M13; mat2.M32 = mat.M23; mat2.M33 = mat.M33; mat2.M34 = 0f;
        mat2.M41 = -(mat.M41 * mat2.M11 + mat.M42 * mat2.M21 + mat.M43 * mat2.M31);
        mat2.M42 = -(mat.M41 * mat2.M12 + mat.M42 * mat2.M22 + mat.M43 * mat2.M32);
        mat2.M43 = -(mat.M41 * mat2.M13 + mat.M42 * mat2.M23 + mat.M43 * mat2.M33);
        return mat2;
    }

    public Matrix4x4 getPointAtMat(Vector3 pos, Vector3 target, Vector3 up)
    {
        Vector3 forward = Vector3.Normalize(target - pos);

        Vector3 a = forward * Vector3.Dot(up, forward);
        Vector3 newUp = Vector3.Normalize(up - a);
        Vector3 right = Vector3.Cross(newUp, forward);
        Matrix4x4 mat = new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            newUp.X, newUp.Y, newUp.Z, 0,
            forward.X, forward.Y, forward.Z, 0,
            pos.X, pos.Y, pos.Z, 1
        );
        return mat;
    }

    public Vector3 insertPlane(Vector3 planeP, Vector3 planeN, Vector3 lineStart, Vector3 lineEnd)
    {
        planeN = Vector3.Normalize(planeN);
        float planeD = -Vector3.Dot(planeN, planeP);
        float ad = Vector3.Dot(lineStart, planeN);
        float bd = Vector3.Dot(lineEnd, planeN);
        float t = (-planeD - ad) / (bd - ad);
        Vector3 lineStartToEnd = lineEnd - lineStart;
        Vector3 lineToIntersect = lineStartToEnd * t;
        return lineStart + lineToIntersect;
    }

    public Vector3[] ClipTriangle(Vector3[] tri, Vector3 planeP, Vector3 planeN)
    {
        if (tri == null || tri.Length != 3) return Array.Empty<Vector3>();
        planeN = Vector3.Normalize(planeN);
        Func<Vector3, float> dist = p => Vector3.Dot(planeN, p) - Vector3.Dot(planeN, planeP);

        var inside = new List<Vector3>(3);
        var outside = new List<Vector3>(3);

        for (int i = 0; i < 3; i++)
        {
            if (dist(tri[i]) >= 0f) inside.Add(tri[i]);
            else outside.Add(tri[i]);
        }

        if (inside.Count == 0) return Array.Empty<Vector3>();

        if (inside.Count == 3) return inside.ToArray();

        var result = new List<Vector3>();

        // 1 inside, 2 outside -> form one triangle (inside, i->o0 intersection, i->o1 intersection)
        if (inside.Count == 1 && outside.Count == 2)
        {
            result.Add(inside[0]);
            result.Add(insertPlane(planeP, planeN, inside[0], outside[0]));
            result.Add(insertPlane(planeP, planeN, inside[0], outside[1]));
            return result.ToArray();
        }

        // 2 inside, 1 outside -> form two triangles (i0, i1, i0->o intersection) etc.
        if (inside.Count == 2 && outside.Count == 1)
        {
            result.Add(inside[0]);
            result.Add(inside[1]);
            result.Add(insertPlane(planeP, planeN, inside[0], outside[0]));

            result.Add(inside[1]);
            result.Add(result[2]);
            result.Add(insertPlane(planeP, planeN, inside[1], outside[0]));

            return result.ToArray();
        }

        return Array.Empty<Vector3>();
    }
}