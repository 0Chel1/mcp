using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace MCP.Physics;

public struct RaycastHit
{
    public bool Hit;
    public float Distance;
    public Vector3 Point;
    public Vector3 Normal;
    public object Collider;
}

public class Raycast
{
    const float EPSILON = 1e-6f;
    // direction must be normalized. Returns true and t (distance) when hit and u,v barycentric coords.
    public static bool RayIntersectsTriangle(in Vector3 origin, in Vector3 direction, in float distance, in Vector3 v0, in Vector3 v1, in Vector3 v2, out float t, out float u, out float v, in bool infinite)
    {
        t = 0f; u = 0f; v = 0f;
        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 pvec = Vector3.Cross(Vector3.Normalize(direction), edge2);
        float det = Vector3.Dot(edge1, pvec);

        if (MathF.Abs(det) < EPSILON) return false; // parallel

        float invDet = 1f / det;
        Vector3 tvec = origin - v0;
        u = Vector3.Dot(tvec, pvec) * invDet;
        if (u < 0f || u > 1f) return false;

        Vector3 qvec = Vector3.Cross(tvec, edge1);
        v = Vector3.Dot(Vector3.Normalize(direction), qvec) * invDet;
        if (v < 0f || u + v > 1f) return false;

        t = Vector3.Dot(edge2, qvec) * invDet;
        return (t >= 0f) && (infinite || t <= distance);
    }
}
