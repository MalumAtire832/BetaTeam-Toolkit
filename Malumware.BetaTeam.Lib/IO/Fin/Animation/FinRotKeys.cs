using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // 3ds Max rotation keys keep the angle and axis they were made with next to the quaternion (engine: GetAngle,
    // GetAxis, GetQuaternion, GetExtraSpins)
    public abstract record FinRotKey(float Time, float Angle, Vector3 Axis, FinQuaternion Quaternion, int ExtraSpins, uint Unknown2C);

    public sealed record FinLinearRotKey(float Time, float Angle, Vector3 Axis, FinQuaternion Quaternion, int ExtraSpins, uint Unknown2C)
        : FinRotKey(Time, Angle, Axis, Quaternion, ExtraSpins, Unknown2C);

    public sealed record FinBezierRotKey(
        float Time, float Angle, Vector3 Axis, FinQuaternion Quaternion, int ExtraSpins, uint Unknown2C,
        FinQuaternion Unknown30, float Unknown40)
        : FinRotKey(Time, Angle, Axis, Quaternion, ExtraSpins, Unknown2C);

    public sealed record FinTcbRotKey(
        float Time, float Angle, Vector3 Axis, FinQuaternion Quaternion, int ExtraSpins, uint Unknown2C,
        float Tension, float Continuity, float Bias, FinQuaternion QuaternionA, FinQuaternion QuaternionB,
        float Unknown5C, float Unknown60)
        : FinRotKey(Time, Angle, Axis, Quaternion, ExtraSpins, Unknown2C);

    // Rotation as three separate float curves, one per axis
    public sealed record FinEulerRotKey(
        float Time, float Angle, Vector3 Axis, FinQuaternion Quaternion, int ExtraSpins, uint Unknown2C,
        ushort Unknown3C, FinKeyGroup<FinFloatKey> X, FinKeyGroup<FinFloatKey> Y, FinKeyGroup<FinFloatKey> Z)
        : FinRotKey(Time, Angle, Axis, Quaternion, ExtraSpins, Unknown2C);
}
