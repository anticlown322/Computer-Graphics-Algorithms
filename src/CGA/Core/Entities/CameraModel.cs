using System.Numerics;

namespace CGA.Core.Entities;

public class CameraModel
{
    public Vector3 EyePosition { get; set; } = Vector3.Zero;
    public Vector3 TargetPosition { get; set; } = Vector3.Zero;
    public Vector3 UpVector { get; set; } = Vector3.UnitY;
    public float AspectRatio { get; set; } = 16f / 9f;

    #region Distances

    public float ZNear { get; set; } = 0.01f;
    public float ZFar { get; set; } = 100f;
    public float Radius { get; set; } = 5f;

    #endregion Distances

    #region Angles

    public float Fov { get; set; } = MathF.PI / 2.0f;
    private float Zeta { get; set; } = (float)Math.PI / (float)2.3;
    private float Phi { get; set; } = (float)Math.PI / 2;

    #endregion

    public void ChangeEyePosition()
    {
        EyePosition = new Vector3(
            Radius * (float)Math.Cos(Phi) * (float)Math.Sin(Zeta),
            Radius * (float)Math.Cos(Zeta),
            Radius * (float)Math.Sin(Phi) * (float)Math.Sin(Zeta));
    }
}