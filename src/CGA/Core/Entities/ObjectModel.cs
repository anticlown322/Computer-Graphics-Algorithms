using System.Numerics;

namespace CGA.Core.Entities;



public class ObjectModel
{
    #region Vertices

    public List<Vector4> LocalVertices { get; set; } = [];
    public Vector4[] GlobalVertices { get; set; } = [];
    public Vector4[] ProjectionVertices { get; set; } = [];
    public List<Face> Faces { get; set; } = [];
    public List<Vector3> Normals { get; set; } = []; 


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

            ProjectionVertices[i] = vertexVector;
        }
    }

    public void CalcGlobalVertices(Matrix4x4 worldMatrix)
    {
        for (var i = 0; i < LocalVertices.Count; i++)
            GlobalVertices[i] = Vector4.Transform(LocalVertices[i], worldMatrix);
    }
    
    public void CalcNormals(Matrix4x4 transformMatrix)
    {
        Vector4[] tempVertices = new Vector4[LocalVertices.Count];
        for (var i = 0; i < LocalVertices.Count; i++)
            tempVertices[i] = Vector4.Transform(LocalVertices[i], transformMatrix);

        foreach (var face in Faces)
        {
            Vector3 v1 = new Vector3(
                tempVertices[face.vertexIndexes[1] - 1].X - tempVertices[face.vertexIndexes[0] - 1].X,
                tempVertices[face.vertexIndexes[1] - 1].Y - tempVertices[face.vertexIndexes[0] - 1].Y,
                tempVertices[face.vertexIndexes[1] - 1].Z - tempVertices[face.vertexIndexes[0] - 1].Z);
            
            Vector3 v2 = new Vector3(
                tempVertices[face.vertexIndexes[2] - 1].X - tempVertices[face.vertexIndexes[0] - 1].X,
                tempVertices[face.vertexIndexes[2] - 1].Y - tempVertices[face.vertexIndexes[0] - 1].Y,
                tempVertices[face.vertexIndexes[2] - 1].Z - tempVertices[face.vertexIndexes[0] - 1].Z);
            
            Vector3 surfaceNormal = Vector3.Normalize(Vector3.Cross(v1, v2));

            face.vertexNormal = surfaceNormal;
        }
    }
}