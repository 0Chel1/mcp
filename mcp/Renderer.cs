using Microsoft.Xna.Framework.Graphics;
using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Matrix4x4 = System.Numerics.Matrix4x4;
using System.Runtime.InteropServices;
using System.Linq;
using System.Diagnostics;

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
            if (Vector3.Dot(normal, viewDir) < 0f) continue; // backface cull

            // world -> camera (you already have viewMat)
            var paCam = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(aW.X, aW.Y, aW.Z), viewMat);
            var pbCam = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(bW.X, bW.Y, bW.Z), viewMat);
            var pcCam = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(cW.X, cW.Y, cW.Z), viewMat);

            // camera-space -> clip-space (Vector4)
            var clipA = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(paCam.X, paCam.Y, paCam.Z, 1f), proj);
            var clipB = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(pbCam.X, pbCam.Y, pbCam.Z, 1f), proj);
            var clipC = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(pcCam.X, pcCam.Y, pcCam.Z, 1f), proj);

            // clip the triangle in clip-space (works with Vector4)
            var clippedClipPoly = ClipPolygonInClipSpace(new System.Numerics.Vector4[] { clipA, clipB, clipC });
            if (clippedClipPoly == null || clippedClipPoly.Count < 3) continue;

            // triangulate fan, do perspective divide and draw
            for (int t = 1; t + 1 < clippedClipPoly.Count; t++)
            {
                var v0 = clippedClipPoly[0];
                var v1 = clippedClipPoly[t];
                var v2 = clippedClipPoly[t + 1];

                // perspective divide -> NDC
                var ndc0 = new Vector3(v0.X / v0.W, v0.Y / v0.W, v0.Z / v0.W);
                var ndc1 = new Vector3(v1.X / v1.W, v1.Y / v1.W, v1.Z / v1.W);
                var ndc2 = new Vector3(v2.X / v2.W, v2.Y / v2.W, v2.Z / v2.W);

                // to screen
                float sxA = (ndc0.X * 0.5f + 0.5f) * Resolution.X;
                float syA = (-ndc0.Y * 0.5f + 0.5f) * Resolution.Y;
                float sxB = (ndc1.X * 0.5f + 0.5f) * Resolution.X;
                float syB = (-ndc1.Y * 0.5f + 0.5f) * Resolution.Y;
                float sxC = (ndc2.X * 0.5f + 0.5f) * Resolution.X;
                float syC = (-ndc2.Y * 0.5f + 0.5f) * Resolution.Y;

                Vector2[] p = new Vector2[] {
                    new Vector2(sxA, syA),
                    new Vector2(sxB, syB),
                    new Vector2(sxC, syC)
                };

                DrawTriangle(p, color * MathF.Max(0.5f, Vector3.Dot(normal, lightPos - centroid)));
            }
        }
    }

    // Clip polygon in clip-space (Vector4). Returns convex polygon in clip-space (still Vector4).
    // Clip test: inside if
    //   left  -> x >= -w  (dist = x + w >= 0)
    //   right -> x <=  w  (dist = w - x >= 0)
    //   bottom-> y >= -w  (dist = y + w >= 0)
    //   top   -> y <=  w  (dist = w - y >= 0)
    //   near  -> z >= 0   (dist = z >= 0)
    //   far   -> z <= w   (dist = w - z >= 0)
    private List<System.Numerics.Vector4> ClipPolygonInClipSpace(System.Numerics.Vector4[] poly)
    {
        if (poly == null || poly.Length == 0) return null;

        var verts = new List<System.Numerics.Vector4>(poly);

        // array of plane distance functions: positive = inside
        var planes = new Func<System.Numerics.Vector4, float>[]
        {
            v => v.X + v.W,
            v => v.W - v.X,
            v => v.Y + v.W,
            v => v.W - v.Y,
            v => v.Z,
            v => v.W - v.Z
        };

        foreach (var planeDist in planes)
        {
            verts = ClipAgainstPlane(verts, planeDist);
            if (verts.Count < 3) return null;
        }

        return verts;
    }

    // Sutherland–Hodgman step for clip-space, using signed distance function
    private List<System.Numerics.Vector4> ClipAgainstPlane(List<System.Numerics.Vector4> input, Func<System.Numerics.Vector4, float> distFunc)
    {
        var output = new List<System.Numerics.Vector4>();
        if (input.Count == 0) return output;

        for (int i = 0; i < input.Count; i++)
        {
            var curr = input[i];
            var prev = input[(i + input.Count - 1) % input.Count];

            float dCurr = distFunc(curr);
            float dPrev = distFunc(prev);

            bool insideCurr = dCurr >= 0f;
            bool insidePrev = dPrev >= 0f;

            if (insideCurr)
            {
                if (!insidePrev)
                {
                    // prev outside -> add intersection
                    output.Add(IntersectClipEdge(prev, curr, dPrev, dCurr));
                }
                output.Add(curr);
            }
            else if (insidePrev)
            {
                // prev inside, curr outside -> add intersection
                output.Add(IntersectClipEdge(prev, curr, dPrev, dCurr));
            }
        }

        return output;
    }

    // Intersect two clip-space Vector4s. Uses linear interpolation on Vector4 components.
    // d1 = dist(prev), d2 = dist(curr)
    private System.Numerics.Vector4 IntersectClipEdge(System.Numerics.Vector4 p1, System.Numerics.Vector4 p2, float d1, float d2)
    {
        float denom = (d1 - d2);
        float t;
        if (MathF.Abs(denom) < 1e-6f)
        {
            t = 0f;
        }
        else
        {
            t = d1 / denom; // solve p(t) where dist(p(t)) == 0
            t = Math.Clamp(t, 0f, 1f);
        }
        return new System.Numerics.Vector4(
            p1.X + t * (p2.X - p1.X),
            p1.Y + t * (p2.Y - p1.Y),
            p1.Z + t * (p2.Z - p1.Z),
            p1.W + t * (p2.W - p1.W)
        );
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
}