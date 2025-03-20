using System.Numerics;

namespace CGA.Core.Entities;

public record Face
{
    public Face(int[] vertexIndexes) 
    { 
        this.vertexIndexes = vertexIndexes; 
        vertexNormal = new Vector3();
    }
    public int[] vertexIndexes;
    public Vector3 vertexNormal;
}

public class ObjectModel
{
    #region Vertices
    public List<Vector4> LocalVertices { get; set; } = [];
    public Vector4[] ProjectionVertices { get; set; } = [];
    public List<Face> Faces { get; set; } = [];
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



    public void calcNormals(Matrix4x4 transformMatrix, Vector3 eyePosition)
    {
        Vector4[] tempVertices = new Vector4[LocalVertices.Count];
        for (var i = 0; i < LocalVertices.Count; i++)
            tempVertices[i] = Vector4.Transform(LocalVertices[i], transformMatrix); 

        for(var i = 0; i < Faces.Count; i++) {
            
            Vector3 v1 = new Vector3(tempVertices[Faces[i].vertexIndexes[1] - 1].X - tempVertices[Faces[i].vertexIndexes[0] - 1].X, tempVertices[Faces[i].vertexIndexes[1] - 1].Y - tempVertices[Faces[i].vertexIndexes[0] - 1].Y, tempVertices[Faces[i].vertexIndexes[1] - 1].Z - tempVertices[Faces[i].vertexIndexes[0] - 1].Z);
            Vector3 v2 = new Vector3(tempVertices[Faces[i].vertexIndexes[2] - 1].X - tempVertices[Faces[i].vertexIndexes[0] - 1].X, tempVertices[Faces[i].vertexIndexes[2] - 1].Y - tempVertices[Faces[i].vertexIndexes[0] - 1].Y, tempVertices[Faces[i].vertexIndexes[2] - 1].Z - tempVertices[Faces[i].vertexIndexes[0] - 1].Z);
            Vector3 surfaceNormal = Vector3.Normalize(Vector3.Cross(v1, v2));
            if (Vector3.Dot(surfaceNormal, Vector3.Normalize(eyePosition)) > 0)
            {
                surfaceNormal = -surfaceNormal;
            }
            Faces[i].vertexNormal = surfaceNormal;
        }
}}