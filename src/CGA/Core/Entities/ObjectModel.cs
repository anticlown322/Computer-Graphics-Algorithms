using System.Numerics;

namespace CGA.Core.Entities;

public class ObjectModel
{
    #region Vertices
    public List<Vector4> LocalVertices { get; set; } = [];
    public Vector4[] GlobalVertices { get; set; } = [];
    public List<int[]> Faces { get; set; } = [];
    #endregion
    
    #region For transformations
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 Rotation { get; set; } = Vector3.Zero;
    public Vector3 Scale { get; set; } = new Vector3(2.0f, 2.0f, 2.0f);
    #endregion
    
    public void Transform(Matrix4x4 transformMatrix, float zNear, float zFar)
    {
        for (var i = 0; i < LocalVertices.Count; i++)
        {
            var vertexVector = Vector4.Transform(LocalVertices[i], transformMatrix);

            if (vertexVector.W > zNear && vertexVector.W < zFar)
                vertexVector /= vertexVector.W;

            GlobalVertices[i] = vertexVector;
        }
    }
}