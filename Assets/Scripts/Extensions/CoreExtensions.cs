using UnityEngine;

/// <summary>
/// CoreExtensions - NUR NULL SAFETY + MANAGER ACCESS
/// Keine Duplikationen mit anderen Extensions
/// </summary>
public static class CoreExtensions
{
    // === NULL SAFETY (Unity-spezifisch) ===
    public static bool IsValidReference<T>(this T obj) where T : class
        => obj != null && !(obj is UnityEngine.Object unityObj && unityObj == null);
    
    public static bool IsActiveAndValid(this GameObject obj)
        => obj.IsValidReference() && obj.activeInHierarchy;
    
    public static bool IsActiveAndValid<T>(this T component) where T : Component
        => component.IsValidReference() && component.gameObject.IsActiveAndValid();
    
    // === MANAGER ACCESS PATTERN ===
    public static bool TryWithManager<T>(this Component context, System.Action<T> action) where T : SingletonBehaviour<T>
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
    
    public static TResult TryWithManager<T, TResult>(this Component context, System.Func<T, TResult> func) where T : SingletonBehaviour<T>
    {
        if (!SingletonBehaviour<T>.HasInstance) return default(TResult);
        var manager = SingletonBehaviour<T>.Instance;
        if (manager != null && manager is IGameManager gm && gm.IsReady)
        {
            return func(manager);
        }
        return default(TResult);
    }
    
    public static TResult TryWithManagerStatic<T, TResult>(Component context, System.Func<T, TResult> func) where T : SingletonBehaviour<T>
    {
        if (!SingletonBehaviour<T>.HasInstance) return default(TResult);
        var manager = SingletonBehaviour<T>.Instance;
        if (manager != null && manager is IGameManager gm && gm.IsReady)
        {
            return func(manager);
        }
        return default(TResult);
    }
    
    // === PERFORMANCE OPTIMIERTE MANAGER CHECKS ===
    public static bool IsManagerReady<T>() where T : SingletonBehaviour<T>
        => SingletonBehaviour<T>.HasInstance && 
           SingletonBehaviour<T>.Instance is IGameManager gm && 
           gm.IsReady;
    
    // === MANAGER ACCESS (statische Variante) ===
    public static T GetManager<T>() where T : SingletonBehaviour<T>
        => SingletonBehaviour<T>.HasInstance ? SingletonBehaviour<T>.Instance : null;
    
    public static bool TryWithManagerStatic<T>(System.Action<T> action) where T : SingletonBehaviour<T>
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
}