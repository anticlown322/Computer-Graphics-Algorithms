using System.Numerics;

namespace CGA.Core.Entities;

public record Face
{
    public Face(int[] vertexIndexes, string materialName)
    {
        VertexIndexes = vertexIndexes;
        VertexNormal = new Vector3();
        MaterialName = materialName;
    }
    
    public int[]? NormalIndexes; 
    public int[] VertexIndexes;
    public int[] TextureIndexes;
    public Vector3 VertexNormal;
    public string MaterialName = String.Empty;
}