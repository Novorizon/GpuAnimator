// Test-only lifecycle API model derived from Framework verification. No native Unity rendering, baking or import is executed.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace UnityEngine
{
    public class Object
    {
        private static readonly List<Object> objects = new List<Object>();
        public bool Destroyed { get; private set; }
        public string name;
        protected Object() { objects.Add(this); }
        public static T FindObjectOfType<T>(bool includeInactive = false) where T : Object => objects.OfType<T>().FirstOrDefault(value => !value.Destroyed);
        public static T[] FindObjectsOfType<T>(bool includeInactive = false) where T : Object => objects.OfType<T>().Where(value => !value.Destroyed).ToArray();
        public static void DontDestroyOnLoad(Object value) { }
        public static void Destroy(Object value)
        {
            if (ReferenceEquals(value, null) || value.Destroyed) return;
            value.Destroyed = true;
            if (value is GameObject gameObject)
            {
                foreach (var child in gameObject.transform.Children.ToArray()) Destroy(child.gameObject);
                foreach (var component in gameObject.Components.ToArray()) Destroy(component);
                gameObject.transform.SetParent(null, false);
            }
            value.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(value, null);
        }
        public static bool operator ==(Object left, Object right) => ReferenceEquals(left, right) ||
            ((ReferenceEquals(left, null) || left.Destroyed) && (ReferenceEquals(right, null) || right.Destroyed));
        public static bool operator !=(Object left, Object right) => !(left == right);
        public static implicit operator bool(Object value) => value != null;
        public override bool Equals(object value) => this == value as Object;
        public override int GetHashCode() => base.GetHashCode();
        public static void ResetTestObjects() { objects.Clear(); }
        public static void AdvanceFrameForTest(float scaled, float unscaled)
        {
            Time.deltaTime = scaled; Time.unscaledDeltaTime = unscaled;
            Time.timeAsDouble += scaled; Time.realtimeSinceStartupAsDouble += unscaled;
            foreach (var component in objects.OfType<MonoBehaviour>().ToArray())
                if (!component.Destroyed && component.enabled && component.gameObject.activeInHierarchy)
                    component.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(component, null);
        }
        public static T Instantiate<T>(T value) where T : Object => Instantiate(value, null, false);
        public static T Instantiate<T>(T value, Transform parent, bool worldPositionStays) where T : Object
        {
            if (!(value is GameObject source)) return value;
            var clone = new GameObject(source.name + "(Clone)"); clone.SetActive(false);
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localRotation = source.transform.localRotation;
            clone.transform.localScale = source.transform.localScale;
            foreach (var component in source.Components)
            {
                var copy = clone.AddComponent(component.GetType());
                foreach (var field in component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    field.SetValue(copy, field.GetValue(component));
            }
            clone.SetActive(source.activeSelf);
            return (T)(Object)clone;
        }
    }
    public partial struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 one => new Vector3(1, 1, 1);
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    public partial struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
    }
    public class Transform
    {
        public GameObject gameObject;
        private Transform parentValue;
        public readonly List<Transform> Children = new List<Transform>();
        public Transform parent { get => parentValue; set => SetParent(value, false); }
        public Vector3 localPosition, position, localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity, rotation = Quaternion.identity;
        public void SetParent(Transform value, bool worldPositionStays)
        { parentValue?.Children.Remove(this); parentValue = value; value?.Children.Add(this); }
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) { this.position = position; this.rotation = rotation; }
    }
    public class GameObject : Object
    {
        public readonly Transform transform;
        public readonly List<MonoBehaviour> Components = new List<MonoBehaviour>();
        public bool activeSelf { get; private set; } = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name = "") { this.name = name; transform = new Transform { gameObject = this }; }
        internal MonoBehaviour AddComponent(Type type)
        {
            var result = (MonoBehaviour)Activator.CreateInstance(type, true);
            result.gameObject = this; Components.Add(result);
            if (activeInHierarchy) Activate(result);
            return result;
        }
        private static void Activate(MonoBehaviour value)
        {
            if (!value.Awoken)
            { value.Awoken = true; value.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(value, null); }
            if (!value.Destroyed) value.GetType().GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(value, null);
        }
        public T AddComponent<T>() where T : MonoBehaviour => (T)AddComponent(typeof(T));
        public T GetComponent<T>() where T : class => Components.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class
        {
            var result = new List<T>();
            if (includeInactive || activeInHierarchy) result.AddRange(Components.OfType<T>());
            foreach (var child in transform.Children) result.AddRange(child.gameObject.GetComponentsInChildren<T>(includeInactive));
            return result.ToArray();
        }
        public T GetComponentInChildren<T>(bool includeInactive) where T:class => GetComponentsInChildren<T>(includeInactive).FirstOrDefault();
        public void SetActive(bool value)
        {
            bool before = activeInHierarchy; activeSelf = value; bool after = activeInHierarchy;
            if (before == after) return;
            foreach (var component in Components.ToArray())
                if (after) Activate(component);
                else component.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(component, null);
        }
    }
    public class MonoBehaviour : Object
    {
        internal bool Awoken;
        public bool enabled = true;
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public static class Time
    {
        public static float deltaTime = 0.016f, unscaledDeltaTime = 0.016f;
        public static float timeScale = 1;
        public static double timeAsDouble, realtimeSinceStartupAsDouble;
    }
    public class ScriptableObject : Object
    { public static T CreateInstance<T>() where T : ScriptableObject, new() => new T(); }
    public class TextAsset : Object
    {
        public string text { get; }
        public TextAsset() : this(string.Empty) { }
        public TextAsset(string text) { this.text = text; }
    }
    public class Sprite : Object { }
    public class ShaderVariantCollection : Object { public void WarmUp() { } }
    public class AsyncOperation { public bool isDone = true; public event Action<AsyncOperation> completed; public void CompleteForTest() { isDone = true; completed?.Invoke(this); } }
    public static class Application
    {
        public static bool isPlaying = true;
        public static string persistentDataPath => "/tmp/framework-resource-tests";
        public static event Action quitting;
        public static void QuitForTest() { quitting?.Invoke(); }
    }
    // Compile-only immediate-mode GUI surface. No layout, drawing or input is simulated.
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
    public static class Screen { public static int width = 1280, height = 720; }
    public class GUIStyle { }
    public class GUISkin { public GUIStyle box = new GUIStyle(); }
    public static class GUI { public static bool enabled = true; public static GUISkin skin = new GUISkin(); }
    public sealed class GUILayoutOption { }
    public static class GUILayout
    {
        public static void BeginArea(Rect position) { }
        public static void BeginArea(Rect position, GUIStyle style) { }
        public static void BeginArea(Rect position, string text, GUIStyle style) { }
        public static void EndArea() { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => scrollPosition;
        public static void EndScrollView() { }
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static string TextField(string text, params GUILayoutOption[] options) => text;
        public static string TextField(string text, int maximumLength, params GUILayoutOption[] options) => text;
        public static string PasswordField(string text, char mask, int maximumLength, params GUILayoutOption[] options) => text;
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => value;
        public static void Space(float pixels) { }
        public static GUILayoutOption Width(float width) => new GUILayoutOption();
        public static GUILayoutOption Height(float height) => new GUILayoutOption();
    }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute
    { public string menuName; public string fileName; }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    public enum RuntimeInitializeLoadType { SubsystemRegistration, BeforeSceneLoad }
    [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { LoadType = loadType; }
        public RuntimeInitializeLoadType LoadType { get; }
    }
    public static class Resources
    {
        private static readonly Dictionary<string, Object> assets = new Dictionary<string, Object>();
        public static T Load<T>(string path) where T : Object => assets.TryGetValue(path, out var value) ? value as T : null;
        public static void RegisterTestAsset(string path, Object value) { assets[path] = value; }
        public static void ClearTestAssets() { assets.Clear(); }
        public static AsyncOperation UnloadUnusedAssets() => new AsyncOperation();
    }
    public interface ILogger { }
    public class Logger { }
    public static class Debug
    {
        public static readonly List<Exception> Exceptions = new List<Exception>();
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
        public static void LogException(Exception exception) { Exceptions.Add(exception); }
    }
}

namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single, Additive }
    public struct Scene { public int handle; public string name; public bool isLoaded; }
    public static class SceneManager
    {
        public static Scene GetActiveScene() => new Scene { handle = 1, name = "Test", isLoaded = true };
        public static void MoveGameObjectToScene(UnityEngine.GameObject value, Scene scene) { }
    }
}
namespace UnityEngine.U2D
{
    public class SpriteAtlas : UnityEngine.Object { public UnityEngine.Sprite GetSprite(string name) => new UnityEngine.Sprite(); }
    public static class SpriteAtlasManager
    {
        private static Action<string, Action<SpriteAtlas>> requested;
        public static int SubscriberCount { get; private set; }
        public static event Action<string, Action<SpriteAtlas>> atlasRequested
        {
            add { requested += value; SubscriberCount++; }
            remove { requested -= value; SubscriberCount--; }
        }
        public static void RequestForTest(string name, Action<SpriteAtlas> callback) { requested?.Invoke(name, callback); }
    }
}
