using UnityEngine;

public static class CoreExtensions
{
    // Basis Null-Check für Unity Objects
    public static bool IsValidReference<T>(this T obj) where T : class
        => obj != null && !(obj is UnityEngine.Object unityObj && unityObj == null);
    
    // GameObject aktiv und gültig
    public static bool IsActiveAndValid(this GameObject obj)
        => obj.IsValidReference() && obj.activeInHierarchy;
    
    // Component aktiv und gültig
    public static bool IsActiveAndValid<T>(this T component) where T : Component
        => component.IsValidReference() && component.gameObject.IsActiveAndValid();
}