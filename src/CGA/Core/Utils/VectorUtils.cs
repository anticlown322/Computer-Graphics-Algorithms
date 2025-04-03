using System.Numerics;

namespace CGA.Core.Utils;

public static class VectorUtils
{
    public static Vector3 GetVector3XYZ(this Vector4 vertex)
    {
        return new Vector3(vertex.X, vertex.Y, vertex.Z);
    }
    
    public static Vector2 GetVector2XY(this Vector3 vertex)
    {
        return new Vector2(vertex.X, vertex.Y);
    }
}