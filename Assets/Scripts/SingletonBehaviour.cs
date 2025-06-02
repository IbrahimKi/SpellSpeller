using UnityEngine;

public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _applicationIsQuitting = false;
    private static Transform _cachedRootTransform; // Cache root to avoid repeated traversal

    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed on application quit. Won't create again.");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>();

                    if (_instance == null)
                    {
                        Debug.LogError($"[Singleton] An instance of {typeof(T)} is needed in the scene, but there is none.");
                        return null;
                    }
                }

                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            
            // OPTIMIZED: Cache and check direct parent only once
            if (_cachedRootTransform == null)
            {
                _cachedRootTransform = GetDirectParentOrSelf();
            }
            
            if (_cachedRootTransform.parent == null) // Direct parent must be root in scene
            {
                DontDestroyOnLoad(_cachedRootTransform.gameObject);
            }
            
            OnAwakeInitialize();
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[Singleton] Multiple instances of {typeof(T)} detected. Destroying duplicate.");
            Destroy(gameObject);
        }
    }

    // OPTIMIZED: Check only direct parent or self
    private Transform GetDirectParentOrSelf()
    {
        // If this object has no parent, it's already root
        if (transform.parent == null)
            return transform;
            
        // If the direct parent is root, return the parent
        if (transform.parent.parent == null)
            return transform.parent;
            
        // Otherwise, don't persist (too deeply nested)
        return transform; // Return self but won't be persisted
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            _cachedRootTransform = null; // Clear cache on destroy
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    protected abstract void OnAwakeInitialize();

    // OPTIMIZED: Inline property for better performance
    public static bool HasInstance => _instance != null && !_applicationIsQuitting;
}