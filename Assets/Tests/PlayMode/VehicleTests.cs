using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class VehicleTests
{
    [UnityTest]
    public IEnumerator AirborneWheelsClearGroundForcesAndDoNotAcquireContactTorque()
    {
        var owner = new GameObject("airborne car");
        var wheel = new GameObject("test wheel");
        new GameObject("visual").transform.SetParent(wheel.transform);
        owner.SetActive(false);
        var car = owner.AddComponent<Car>();
        car.e = new Engine();
        car.wheelPrefab = wheel;
        car.wheels = new[] { new WheelProperties { normalForce = 200f, angularVelocity = 10f } };
        owner.transform.position = Vector3.up * 1000f;
        owner.GetComponent<Rigidbody>().useGravity = false;
        owner.SetActive(true);
        yield return null;
        yield return new WaitForFixedUpdate();
        Assert.That(car.wheels[0].normalForce, Is.Zero);
        Assert.That(car.wheels[0].slip, Is.Zero);
        Assert.That(car.wheels[0].angularVelocity, Is.EqualTo(10f).Within(0.001f));
        Object.Destroy(owner);
        Object.Destroy(wheel);
        yield return null;
    }

    static void SavePreview(string scene)
    {
        string directory = System.Environment.GetEnvironmentVariable("SIMPLECAR_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        var camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        var previous = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, scene + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }
    }

    [UnityTest]
    public IEnumerator BuildScenesHaveWorkingVehiclesAndCameraTargets()
    {
        foreach (string scene in new[] { "Road", "Offroad", "RoadF1" })
        {
            yield return SceneManager.LoadSceneAsync(scene);
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    Assert.That(component, Is.Not.Null, scene + " has a missing script");
            var cars = Object.FindObjectsOfType<Car>();
            Assert.That(cars.Length, Is.GreaterThan(0), scene + " needs an active vehicle");
            foreach (var car in cars)
            {
                Assert.That(car.enabled, Is.True, scene + ": " + car.name);
                Assert.That(car.rb, Is.Not.Null);
                Assert.That(float.IsNaN(car.rb.velocity.sqrMagnitude), Is.False);
                foreach (var wheel in car.wheels)
                {
                    Assert.That(wheel.wheelObject, Is.Not.Null);
                    Assert.That(float.IsNaN(wheel.slip) || float.IsInfinity(wheel.slip), Is.False);
                }
            }
            var camera = Object.FindObjectOfType<CameraController>();
            Assert.That(camera, Is.Not.Null, scene);
            Assert.That(camera.enabled, Is.True, scene);
            Assert.That(camera.target, Is.Not.Null, scene);
            Assert.That(camera.target.gameObject.activeInHierarchy, Is.True, scene);
            var previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            var previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Vector3 start = camera.target.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                Assert.That(cars[0].input.Move.Main.ReadValue<Vector2>().y, Is.GreaterThan(0f), "Virtual keyboard must reach the vehicle action");
                for (int i = 0; i < 100; i++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    InputSystem.Update();
                    yield return null;
                    yield return new WaitForFixedUpdate();
                }
                Vector3 displacement = camera.target.position - start;
                SavePreview(scene);
                string details = "";
                foreach (var car in cars)
                    details += $" {car.name}: input={car.userInput}, velocity={car.rb.velocity}, kinematic={car.rb.isKinematic}, wheelInput={car.wheels[0].input}, torque={car.wheels[0].torque}, normal={car.wheels[0].normalForce}, rpm={car.e.getRPM()}";
                Assert.That(new Vector2(displacement.x, displacement.z).magnitude, Is.GreaterThan(0.5f), scene + " must respond to throttle." + details);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
#endif
            }
        }
    }
}
