using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class CanvasSetupChecker : MonoBehaviour
{
    [Header("Recommended Settings")]
    [SerializeField] private bool autoFixOnStart = true;
    
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    
    void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvasScaler = GetComponent<CanvasScaler>();
        
        if (canvas != null && autoFixOnStart)
        {
            CheckAndFixCanvasSetup();
        }
    }
    
    [ContextMenu("Check Canvas Setup")]
    public void CheckAndFixCanvasSetup()
    {
        Debug.Log("=== CANVAS SETUP CHECK ===");
        
        // Ensure we have references
        if (canvas == null)
            canvas = GetComponent<Canvas>();
        if (canvasScaler == null)
            canvasScaler = GetComponent<CanvasScaler>();
            
        if (canvas == null)
        {
            Debug.LogError("No Canvas component found on this GameObject!");
            return;
        }
        
        // 1. Check Render Mode
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Debug.LogWarning($"Canvas Render Mode is {canvas.renderMode}. Recommended: ScreenSpaceOverlay for UI dragging");
            
            if (Application.isEditor)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Debug.Log("✓ Fixed: Set to ScreenSpaceOverlay");
            }
        }
        else
        {
            Debug.Log("✓ Render Mode: ScreenSpaceOverlay");
        }
        
        // 2. Check Canvas Scaler
        if (canvasScaler == null)
        {
            Debug.LogWarning("No CanvasScaler component found!");
            if (Application.isEditor)
            {
                canvasScaler = gameObject.AddComponent<CanvasScaler>();
                Debug.Log("✓ Added CanvasScaler");
            }
        }
        
        if (canvasScaler != null)
        {
            // Recommended settings for consistent UI
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
            
            Debug.Log("✓ CanvasScaler configured");
            Debug.Log($"  - Scale Mode: {canvasScaler.uiScaleMode}");
            Debug.Log($"  - Reference Resolution: {canvasScaler.referenceResolution}");
        }
        
        // 3. Check GraphicRaycaster
        var raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            Debug.LogWarning("No GraphicRaycaster found!");
            if (Application.isEditor)
            {
                gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log("✓ Added GraphicRaycaster");
            }
        }
        else
        {
            Debug.Log("✓ GraphicRaycaster present");
        }
        
        // 4. Check EventSystem
        var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogError("⚠️ No EventSystem found in scene! UI interaction won't work!");
            
            if (Application.isEditor)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("✓ Created EventSystem");
            }
        }
        else
        {
            Debug.Log("✓ EventSystem present");
        }
        
        Debug.Log("=== CANVAS CHECK COMPLETE ===");
    }
    
    [ContextMenu("Test Card Drag")]
    public void TestCardDrag()
    {
        // Find all cards
        var cards = GetComponentsInChildren<Card>(true);
        Debug.Log($"Found {cards.Length} cards");
        
        foreach (var card in cards)
        {
            var dragHandler = card.GetComponent<CardDragHandler>();
            if (dragHandler == null)
            {
                Debug.LogWarning($"Card '{card.name}' missing CardDragHandler!");
            }
            else
            {
                Debug.Log($"✓ Card '{card.name}' has drag handler");
            }
            
            // Check if card is properly setup for dragging
            var rectTransform = card.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Debug.Log($"  - Position: {rectTransform.anchoredPosition}");
                Debug.Log($"  - Parent: {rectTransform.parent?.name ?? "null"}");
            }
        }
    }
}