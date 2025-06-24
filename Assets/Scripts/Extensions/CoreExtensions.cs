using UnityEngine;
using GameCore.Enums;
using GameCore.Data;
using System;
using System.Linq;

/// <summary>
/// CoreExtensions - Fundamentale Extensions für alle Systeme
/// CRITICAL FIX: Diese Klasse ist GLOBAL verfügbar ohne Namespace
/// LOCATION: Assets/Scripts/Extensions/CoreExtensions.cs
/// DEPENDENCIES: SharedEnums
/// </summary>
public static class CoreExtensions
{
    // === NULL SAFETY (Basis für alle Extensions) ===
    
    /// <summary>
    /// Sichere Null-Prüfung für Unity Objects
    /// PERFORMANCE: Inline Check ohne Boxing
    /// </summary>
    public static bool IsValidReference<T>(this T obj) where T : class
        => obj != null && !(obj is UnityEngine.Object unityObj && unityObj == null);
    
    /// <summary>
    /// GameObject ist aktiv UND gültig
    /// </summary>
    public static bool IsActiveAndValid(this GameObject obj)
        => obj.IsValidReference() && obj.activeInHierarchy;
    
    /// <summary>
    /// Component ist aktiv UND gültig
    /// </summary>
    public static bool IsActiveAndValid<T>(this T component) where T : Component
        => component.IsValidReference() && component.gameObject.IsActiveAndValid();
    
    // === MANAGER INTEGRATION (Zentrale TryWithManager Implementation) ===
    
    /// <summary>
    /// Sichere Manager-Operation mit Action
    /// CRITICAL: Einzige TryWithManager Implementation - alle anderen verwenden diese!
    /// </summary>
    public static bool TryWithManager<T>(this MonoBehaviour caller, System.Action<T> action) 
        where T : SingletonBehaviour<T>
    {
        if (!caller.IsValidReference() || action == null) return false;
        
        try
        {
            if (SingletonBehaviour<T>.HasInstance)
            {
                var manager = SingletonBehaviour<T>.Instance;
                if (manager != null && manager is IGameManager gameManager && gameManager.IsReady)
                {
                    action(manager);
                    return true;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CoreExtensions] TryWithManager<{typeof(T).Name}> failed: {ex.Message}");
        }
        
        return false;
    }
    
    /// <summary>
    /// Sichere Manager-Operation mit Func (Return Value)
    /// </summary>
    public static TResult TryWithManager<T, TResult>(this MonoBehaviour caller, System.Func<T, TResult> func, TResult defaultValue = default) 
        where T : SingletonBehaviour<T>
    {
        if (!caller.IsValidReference() || func == null) return defaultValue;
        
        try
        {
            if (SingletonBehaviour<T>.HasInstance)
            {
                var manager = SingletonBehaviour<T>.Instance;
                if (manager != null && manager is IGameManager gameManager && gameManager.IsReady)
                {
                    return func(manager);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CoreExtensions] TryWithManager<{typeof(T).Name}> failed: {ex.Message}");
        }
        
        return defaultValue;
    }
    
    /// <summary>
    /// Manager bereit für Operationen
    /// </summary>
    public static bool IsManagerReady<T>(this T manager) where T : MonoBehaviour, IGameManager
        => manager.IsValidReference() && manager.IsReady;
    
    // === LOGGING UTILITIES ===
    
    /// <summary>
    /// Debug-freundliches Logging mit Context
    /// </summary>
    public static void LogDebug<T>(this T obj, string message) where T : class
    {
        string context = obj?.GetType().Name ?? "Unknown";
        Debug.Log($"[{context}] {message}");
    }
    
    /// <summary>
    /// Error Logging mit Context
    /// </summary>
    public static void LogError<T>(this T obj, string message, System.Exception ex = null) where T : class
    {
        string context = obj?.GetType().Name ?? "Unknown";
        string fullMessage = ex != null ? $"[{context}] {message}: {ex.Message}" : $"[{context}] {message}";
        Debug.LogError(fullMessage);
    }
    
    // === RESOURCE VALIDATION (Für ResourceExtensions Integration) ===
    
    /// <summary>
    /// Resource ist gültig und verwendbar
    /// INTEGRATION: Basis für ResourceExtensions
    /// </summary>
    public static bool IsValidResource(this Resource resource)
        => resource.IsValidReference() && resource.MaxValue > 0;
    
    /// <summary>
    /// Resource hat verfügbare Menge
    /// </summary>
    public static bool HasAvailable(this Resource resource, int amount)
        => resource.IsValidResource() && resource.CurrentValue >= amount;
    
    // === SAFE OPERATIONS (Error-Resistant Patterns) ===
    
    /// <summary>
    /// Sichere Operation mit Retry-Logic
    /// PERFORMANCE: Inline try-catch ohne Overhead
    /// </summary>
    public static TResult SafeExecute<T, TResult>(this T obj, System.Func<T, TResult> operation, TResult fallback = default)
        where T : class
    {
        if (!obj.IsValidReference() || operation == null) return fallback;
        
        try
        {
            return operation(obj);
        }
        catch (System.Exception ex)
        {
            obj.LogError($"SafeExecute failed", ex);
            return fallback;
        }
    }
    
    /// <summary>
    /// Sichere Action-Ausführung
    /// </summary>
    public static bool SafeExecute<T>(this T obj, System.Action<T> operation) where T : class
    {
        if (!obj.IsValidReference() || operation == null) return false;
        
        try
        {
            operation(obj);
            return true;
        }
        catch (System.Exception ex)
        {
            obj.LogError($"SafeExecute action failed", ex);
            return false;
        }
    }
    
    // === UNITY COMPONENT UTILITIES ===
    
    /// <summary>
    /// Sichere Component-Abfrage
    /// PERFORMANCE: Cached Component Access
    /// </summary>
    public static bool TryGetComponentSafe<T>(this GameObject obj, out T component) where T : Component
    {
        component = null;
        if (!obj.IsActiveAndValid()) return false;
        
        component = obj.GetComponent<T>();
        return component != null;
    }
    
    /// <summary>
    /// Component sicher hinzufügen oder holen
    /// </summary>
    public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
    {
        if (!obj.IsActiveAndValid()) return null;
        
        var component = obj.GetComponent<T>();
        return component != null ? component : obj.AddComponent<T>();
    }
    
    // === COLLECTION SAFETY ===
    
    /// <summary>
    /// Sichere Collection-Iteration
    /// </summary>
    public static bool IsValidCollection<T>(this System.Collections.Generic.IEnumerable<T> collection)
        => collection != null;
    
    /// <summary>
    /// Collection hat gültige Elemente
    /// </summary>
    public static bool HasValidElements<T>(this System.Collections.Generic.IEnumerable<T> collection) where T : class
        => collection.IsValidCollection() && collection.Any(item => item.IsValidReference());
    
    // === TRANSFORM UTILITIES ===
    
    /// <summary>
    /// Sichere Transform-Operation
    /// </summary>
    public static bool TrySetPosition(this Transform transform, Vector3 position)
    {
        if (!transform.IsValidReference()) return false;
        
        try
        {
            transform.position = position;
            return true;
        }
        catch (System.Exception ex)
        {
            transform.LogError("Failed to set position", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Sichere Parent-Änderung
    /// </summary>
    public static bool TrySetParent(this Transform transform, Transform parent, bool worldPositionStays = false)
    {
        if (!transform.IsValidReference()) return false;
        
        try
        {
            transform.SetParent(parent, worldPositionStays);
            return true;
        }
        catch (System.Exception ex)
        {
            transform.LogError("Failed to set parent", ex);
            return false;
        }
    }
    
    // === PERFORMANCE HELPERS ===
    
    /// <summary>
    /// Einmaliger Invoke ohne Wiederholung
    /// PERFORMANCE: Verhindert redundante Calls
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> _invokedOnce = new System.Collections.Generic.HashSet<string>();
    
    public static bool InvokeOnce(this MonoBehaviour obj, string identifier, System.Action action)
    {
        if (!obj.IsValidReference() || action == null || string.IsNullOrEmpty(identifier)) return false;
        
        string key = $"{obj.GetInstanceID()}_{identifier}";
        
        if (_invokedOnce.Contains(key)) return false;
        
        _invokedOnce.Add(key);
        action();
        return true;
    }
    
    // === VALIDATION PATTERNS ===
    
    /// <summary>
    /// Mehrfach-Validation Pattern
    /// USAGE: obj.ValidateAll(condition1, condition2, condition3)
    /// </summary>
    public static bool ValidateAll(this object obj, params System.Func<bool>[] validators)
    {
        if (validators == null || validators.Length == 0) return false;
        
        foreach (var validator in validators)
        {
            if (validator == null || !validator()) return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Mindestens eine Validation erfolgreich
    /// </summary>
    public static bool ValidateAny(this object obj, params System.Func<bool>[] validators)
    {
        if (validators == null || validators.Length == 0) return false;
        
        foreach (var validator in validators)
        {
            if (validator != null && validator()) return true;
        }
        
        return false;
    }
}