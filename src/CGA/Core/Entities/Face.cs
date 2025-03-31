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