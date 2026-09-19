using UnityEngine;

public static class WheelDynamics
{
    public static float ApplyResistance(float angularVelocity, float torque, float inertia, float deltaTime)
    {
        if (inertia <= 0f || deltaTime <= 0f) return angularVelocity;
        return Mathf.MoveTowards(angularVelocity, 0f, Mathf.Max(0f, torque) / inertia * deltaTime);
    }

    public static float SlipRatio(float forceMagnitude, float tractionLimit)
    {
        return tractionLimit > 0f ? Mathf.Max(0f, forceMagnitude) / tractionLimit : 0f;
    }
}
