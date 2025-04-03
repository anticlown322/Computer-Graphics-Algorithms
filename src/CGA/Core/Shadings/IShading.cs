using System.Numerics;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;

namespace CGA.Core.Shadings;

public interface IShading
{ 
    void DrawShading(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color, Vector3 eyePos, float[,] zBuffer);
}