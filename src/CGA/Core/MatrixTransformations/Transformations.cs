using System.Numerics;

namespace CGA.Core.MatrixTransformations;

public static class Transformations
{
    public static Matrix4x4 CreateTransformMatrix(Vector3 objTranslation, Vector3 objRotation, Vector3 objScale)
    {
        Matrix4x4 translation = Matrix4x4.CreateTranslation(objTranslation);
        Matrix4x4 rotation    = Matrix4x4.CreateFromYawPitchRoll(objRotation.Y, objRotation.X, objRotation.Z); 
        Matrix4x4 scale       = Matrix4x4.CreateScale(objScale);
        
        return translation * rotation * scale;
    }

    public static Matrix4x4 CreateViewMatrix(Vector3 eye, Vector3 target, Vector3 up)
    {
        var zAxis = Vector3.Normalize(eye - target);
        var xAxis = Vector3.Normalize(Vector3.Cross(up, zAxis));
        var yAxis = Vector3.Cross(zAxis, xAxis);

        float tx = -Vector3.Dot(xAxis, eye);
        float ty = -Vector3.Dot(yAxis, eye);
        float tz = -Vector3.Dot(zAxis, eye);

        var viewMatrix = new Matrix4x4(
            xAxis.X, xAxis.Y, xAxis.Z, tx,
            yAxis.X, yAxis.Y, yAxis.Z, ty,
            zAxis.X, zAxis.Y, zAxis.Z, tz,
            0.0f, 0.0f, 0.0f, 1.0f);
        
        viewMatrix = Matrix4x4.Transpose(viewMatrix);

        return viewMatrix;
    }
    
    public static Matrix4x4 CreateProjectionMatrix(float fov, float aspect, float znear, float zfar)
    {
        float tanHalfFov = MathF.Tan(fov / 2);
        
        float m00 = 1 / (aspect * tanHalfFov);
        float m11 = 1 / tanHalfFov;
        float m22 = zfar / (znear - zfar);
        float m32 = (znear * zfar) / (znear - zfar);
        
        var perspectiveMatrix = new Matrix4x4(
            m00, 0, 0, 0,
            0, m11, 0, 0,
            0, 0, m22, m32,
            0, 0, -1, 0
        );
        
        perspectiveMatrix = Matrix4x4.Transpose(perspectiveMatrix);

        return perspectiveMatrix;
    }
    
    public static Matrix4x4 CreateViewportMatrix(float width, float height, float xmin, float ymin)
    {
        float m00 = width / 2;
        float m03 = (xmin + width) / 2;
        float m11 = - height / 2;
        float m13 = (ymin + height) / 2;
        
        var viewportMatrix = new Matrix4x4(
            m00,  0, 0, m03,
            0, m11, 0, m13,
            0, 0, 1, 0,
            0, 0, 0, 1
        );
        
        viewportMatrix = Matrix4x4.Transpose(viewportMatrix);
        
        return viewportMatrix;
    }
}