using System.Numerics;

namespace CGA.Core.Entities;

public record Face
{
    public Face(int[] vertexIndexes)
    {
        VertexIndexes = vertexIndexes;
        VertexNormal = new Vector3();
    }
    
    public int[]? NormalIndexes; 
    public int[] VertexIndexes;
    public Vector3 VertexNormal;
}