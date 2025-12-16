using UnityEngine;

public enum DeathCause
{
    HazardVolume,
    Spikes,
    Lava,
    Laser,
    Crusher,
    OutOfBounds
}

public readonly struct KillInfo
{
    public readonly DeathCause Cause;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly Component Source; // the hazard component (optional)

    public KillInfo(DeathCause cause, Vector3 point, Vector3 normal, Component source)
    {
        Cause = cause;
        Point = point;
        Normal = normal;
        Source = source;
    }
}

public interface IDeathReceiver
{
    bool IsDead { get; }
    void Kill(in KillInfo info);
}

