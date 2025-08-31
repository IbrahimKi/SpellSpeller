using UnityEngine;

/// <summary>
/// CoreExtensions - NUR Manager Access Pattern
/// Keine Überschneidungen mit GameExtensions
/// </summary>
public static class CoreExtensions
{
    // === NULL SAFETY ===
    public static bool IsValidReference<T>(this T obj) where T : class
        => obj != null && !(obj is UnityEngine.Object unityObj && unityObj == null);
    
    public static bool IsActiveAndValid(this GameObject obj)
        => obj.IsValidReference() && obj.activeInHierarchy;
    
    public static bool IsActiveAndValid<T>(this T component) where T : Component
        => component.IsValidReference() && component.gameObject.IsActiveAndValid();
    
    // === MANAGER ACCESS PATTERN ===
    public static bool TryWithManager<T>(Component context, System.Action<T> action) where T : SingletonBehaviour<T>
    {
        if (!SingletonBehaviour<T>.HasInstance) return false;
        var manager = SingletonBehaviour<T>.Instance;
        if (manager != null && manager is IGameManager gm && gm.IsReady)
        {
            action(manager);
            return true;
        }
        return false;
    }
    
    public static TResult TryWithManager<T, TResult>(Component context, System.Func<T, TResult> func) where T : SingletonBehaviour<T>
    {
        if (!SingletonBehaviour<T>.HasInstance) return default(TResult);
        var manager = SingletonBehaviour<T>.Instance;
        if (manager != null && manager is IGameManager gm && gm.IsReady)
        {
            return func(manager);
        }
        return default(TResult);
    }
}