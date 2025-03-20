using CGA.Core.MatrixTransformations;
using System.Numerics;

namespace CGA.Core.Entities;

public class SceneManager
{
    public ObjectModel? ObjectModel { get; set; }
    public CameraModel CameraModel { get; set; } = new();
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }



    public void TransformObject()
    {


        if(ObjectModel is null)
            throw new NullReferenceException("Object model is null");
        
        var view = Transformations
            .CreateViewMatrix(CameraModel.EyePosition, CameraModel.TargetPosition, CameraModel.UpVector);
        
        var projection = Transformations
            .CreateProjectionMatrix(CameraModel.Fov, CameraModel.AspectRatio, CameraModel.ZNear, CameraModel.ZFar);
        
        var viewport = Transformations
            .CreateViewportMatrix(CanvasWidth, CanvasHeight, 0.0f, 0.0f);

        var world = Transformations
            .CreateTransformMatrix(ObjectModel.Position, ObjectModel.Rotation, ObjectModel.Scale);

        var transformMatrix = world * view;

        ObjectModel.calcNormals(transformMatrix, CameraModel.EyePosition);

        transformMatrix = world * view * projection * viewport;
        ObjectModel.Transform(transformMatrix, CameraModel.ZNear, CameraModel.ZFar);
        
    }
}