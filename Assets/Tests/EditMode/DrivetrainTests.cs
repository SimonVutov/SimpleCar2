using NUnit.Framework;
using UnityEngine;

public class DrivetrainTests
{
    [TestCase(10f, 9f)]
    [TestCase(-10f, -9f)]
    [TestCase(0.2f, 0f)]
    [TestCase(-0.2f, 0f)]
    [TestCase(0f, 0f)]
    public void ResistanceSlowsBothDirectionsWithoutReversing(float velocity, float expected)
    {
        Assert.That(WheelDynamics.ApplyResistance(velocity, 20f, 2f, 0.1f), Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void AirborneSlipIsFiniteAndGroundedSlipRetainsItsScale()
    {
        Assert.That(WheelDynamics.SlipRatio(0f, 0f), Is.Zero);
        Assert.That(WheelDynamics.SlipRatio(20f, 0f), Is.Zero);
        Assert.That(WheelDynamics.SlipRatio(20f, 10f), Is.EqualTo(2f));
    }

    [Test]
    public void RPMIsClampedAndReverseRotationStillDrivesEngine()
    {
        var engine = new Engine();
        engine.SetRPM(0f);
        Assert.That(engine.getRPM(), Is.EqualTo(engine.idleRPM));
        engine.SetRPM(-100f);
        Assert.That(engine.getRPM(), Is.GreaterThan(engine.idleRPM));
        engine.SetRPM(float.MaxValue);
        Assert.That(engine.getRPM(), Is.EqualTo(engine.maxRPM));
        engine.SetRPM(float.NaN);
        Assert.That(engine.getRPM(), Is.EqualTo(engine.idleRPM));
    }

    [Test]
    public void ManualTransmissionDoesNotAutomaticallyShiftAtRedline()
    {
        var engine = new Engine { automaticTransmission = false };
        engine.SetRPM(0f);
        engine.SetRPM(100000f);
        engine.checkGearSwitching(null, 1f);
        Assert.That(engine.getCurrentGear(), Is.EqualTo(1));
        Assert.That(engine.isSwitchingGears(), Is.False);
    }

    [Test]
    public void AudioInputsRejectNonfiniteAndOutOfRangeValues()
    {
        var owner = new GameObject("audio test");
        try
        {
            var audio = owner.AddComponent<CarAudioController>();
            audio.UpdateAudioValues(float.NaN, 2f, -3f, float.PositiveInfinity, false);
            Assert.That(audio.currentRPM, Is.EqualTo(audio.minEngineRPM));
            Assert.That(audio.throttleInput, Is.EqualTo(1f));
            Assert.That(audio.currentSpeed, Is.Zero);
            Assert.That(audio.lateralSlip, Is.Zero);
        }
        finally { Object.DestroyImmediate(owner); }
    }
}
