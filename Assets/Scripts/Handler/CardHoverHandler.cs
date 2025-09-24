using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CardSystem.Extensions;

/// <summary>
/// Card Hover & Pointer Feedback System - FIXED
/// Keine Position-Konflikte mit HandLayoutManager
/// Unity 6 LTS optimiert, eventbasiert, modular
/// </summary>
[RequireComponent(typeof(Card))]
public class CardHoverHandler : MonoBehaviour, 
    IPointerEnterHandler, 
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Visual Feedback")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float hoverZOffset = 10f;  // CHANGED: Z-Offset statt Y-Position
    [SerializeField] private Color hoverTint = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float animationDuration = 0.15f;
    
    [Header("Selection Preview")]
    [SerializeField] private Color preSelectTint = new Color(0.8f, 0.9f, 1f, 1f);
    [SerializeField] private float preSelectScale = 1.05f;
    
    [Header("Layout Compatibility")]
    [SerializeField] private bool respectLayoutManager = true;  // NEW
    [SerializeField] private bool useCanvasGroupForDepth = true;  // NEW
    
    [Header("Cursor Settings")]
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Texture2D selectCursor;
    [SerializeField] private Texture2D dragCursor;
    
    // Components
    private Card _card;
    private CanvasGroup _canvasGroup;
    private Image _cardImage;
    private RectTransform _rectTransform;
    private Canvas _hoverCanvas;  // NEW: Separate Canvas for hover
    
    // State
    private bool _isHovering = false;
    private bool _isPointerDown = false;
    private Vector3 _originalScale;
    private Vector3 _originalPosition;  // CHANGED: Local Position tracking
    private Color _originalColor;
    private int _originalSortingOrder;
    
    // Animation
    private Coroutine _hoverAnimation;
    
    // Events
    public static event System.Action<Card> OnCardHoverStart;
    public static event System.Action<Card> OnCardHoverEnd;
    public static event System.Action<Card> OnCardPointerDown;
    public static event System.Action<Card> OnCardPointerUp;
    
    void Awake()
    {
        InitializeComponents();
    }
    
    void Start()
    {
        CacheOriginalValues();
        SetupHoverCanvas();
    }
    
    private void InitializeComponents()
    {
        _card = GetComponent<Card>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _cardImage = GetComponent<Image>();
        _rectTransform = GetComponent<RectTransform>();
        
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
    private void CacheOriginalValues()
    {
        _originalScale = transform.localScale;
        _originalPosition = transform.localPosition;  // CHANGED: Local Position
        
        if (_cardImage != null)
            _originalColor = _cardImage.color;
        else
            _originalColor = Color.white;
        
        // Cache sorting order
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            _originalSortingOrder = canvas.sortingOrder;
    }
    
    // NEW: Setup separate canvas for hover effects
    private void SetupHoverCanvas()
    {
        if (!useCanvasGroupForDepth) return;
        
        // Create hover canvas as child
        var hoverCanvasGO = new GameObject("HoverCanvas");
        hoverCanvasGO.transform.SetParent(transform, false);
        
        _hoverCanvas = hoverCanvasGO.AddComponent<Canvas>();
        _hoverCanvas.overrideSorting = true;
        _hoverCanvas.sortingOrder = _originalSortingOrder + 100;  // Above other cards
        _hoverCanvas.enabled = false;  // Disabled by default
        
        var graphicRaycaster = hoverCanvasGO.AddComponent<GraphicRaycaster>();
        graphicRaycaster.enabled = false;  // Don't interfere with input
    }
    
    // === POINTER EVENTS ===
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        
        _isHovering = true;
        SetCursor(hoverCursor);
        StartHoverAnimation(true);
        
        OnCardHoverStart?.Invoke(_card);
        
        // Visual Feedback basierend auf aktueller Aktion
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            ShowSelectionPreview();
        }
        else if (IsCardSelected())
        {
            ShowDragPreview();
        }
        else
        {
            ShowHoverEffect();
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        
        if (!_isPointerDown)
        {
            ResetCursor();
            StartHoverAnimation(false);
            OnCardHoverEnd?.Invoke(_card);
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        
        _isPointerDown = true;
        
        SetCursor(selectCursor);
        OnCardPointerDown?.Invoke(_card);
        
        // Visual Feedback für Click
        ShowClickFeedback();
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        _isPointerDown = false;
        
        // Hover weiterhin aktiv?
        if (_isHovering)
        {
            SetCursor(hoverCursor);
        }
        else
        {
            ResetCursor();
            StartHoverAnimation(false);
        }
        
        OnCardPointerUp?.Invoke(_card);
    }
    
    // === VISUAL EFFECTS ===
    
    private void ShowHoverEffect()
    {
        if (_cardImage != null)
        {
            _cardImage.color = Color.Lerp(_originalColor, hoverTint, 0.3f);
        }
        
        // Activate hover canvas for depth
        if (_hoverCanvas != null)
            _hoverCanvas.enabled = true;
    }
    
    private void ShowSelectionPreview()
    {
        if (_cardImage != null)
        {
            _cardImage.color = Color.Lerp(_originalColor, preSelectTint, 0.5f);
        }
        
        // Leichte Skalierung für Selection Preview (ohne Position ändern)
        transform.localScale = Vector3.Lerp(_originalScale, _originalScale * preSelectScale, 0.5f);
        
        if (_hoverCanvas != null)
            _hoverCanvas.enabled = true;
    }
    
    private void ShowDragPreview()
    {
        if (_cardImage != null)
        {
            _cardImage.color = Color.Lerp(_originalColor, Color.yellow, 0.3f);
        }
        
        SetCursor(dragCursor);
        
        if (_hoverCanvas != null)
            _hoverCanvas.enabled = true;
    }
    
    private void ShowClickFeedback()
    {
        // Kurzer Scale-Effekt ohne Position-Änderung
        if (_hoverAnimation != null)
            StopCoroutine(_hoverAnimation);
            
        _hoverAnimation = StartCoroutine(ClickFeedbackAnimation());
    }
    
    // === ANIMATIONS ===
    
    private void StartHoverAnimation(bool hoverIn)
    {
        if (_hoverAnimation != null)
            StopCoroutine(_hoverAnimation);
            
        _hoverAnimation = StartCoroutine(HoverAnimation(hoverIn));
    }
    
    private System.Collections.IEnumerator HoverAnimation(bool hoverIn)
    {
        Vector3 targetScale = hoverIn ? _originalScale * hoverScale : _originalScale;
        
        // FIXED: Nur Z-Position ändern für Depth, KEINE X/Y Position!
        Vector3 targetPosition = _originalPosition;
        if (hoverIn && !respectLayoutManager)
        {
            targetPosition.z = _originalPosition.z + hoverZOffset;
        }
        else
        {
            targetPosition.z = _originalPosition.z;
        }
        
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 startPosition = transform.localPosition;
        Color startColor = _cardImage?.color ?? Color.white;
        Color targetColor = hoverIn ? Color.Lerp(_originalColor, hoverTint, 0.2f) : _originalColor;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            
            // Smooth animation curve
            t = Mathf.SmoothStep(0f, 1f, t);
            
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            
            // FIXED: Respektiere Layout Manager
            if (respectLayoutManager)
            {
                // Nur Z-Position und Scale ändern, X/Y vom Layout Manager verwalten
                var currentPos = transform.localPosition;
                currentPos.z = Mathf.Lerp(startPosition.z, targetPosition.z, t);
                transform.localPosition = currentPos;
            }
            else
            {
                // Vollständige Position-Animation (nur wenn Layout Manager deaktiviert)
                transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            }
            
            if (_cardImage != null)
                _cardImage.color = Color.Lerp(startColor, targetColor, t);
            
            yield return null;
        }
        
        transform.localScale = targetScale;
        
        if (respectLayoutManager)
        {
            var currentPos = transform.localPosition;
            currentPos.z = targetPosition.z;
            transform.localPosition = currentPos;
        }
        else
        {
            transform.localPosition = targetPosition;
        }
        
        if (_cardImage != null)
            _cardImage.color = targetColor;
        
        // Disable hover canvas when not hovering
        if (_hoverCanvas != null && !hoverIn)
            _hoverCanvas.enabled = false;
        
        _hoverAnimation = null;
    }
    
    private System.Collections.IEnumerator ClickFeedbackAnimation()
    {
        Vector3 clickScale = _originalScale * 0.95f;
        
        // Scale down
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        
        while (elapsed < 0.1f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.1f;
            transform.localScale = Vector3.Lerp(startScale, clickScale, t);
            yield return null;
        }
        
        // Scale back up
        elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.1f;
            transform.localScale = Vector3.Lerp(clickScale, _originalScale * hoverScale, t);
            yield return null;
        }
        
        _hoverAnimation = null;
    }
    
    // === CURSOR MANAGEMENT ===
    
    private void SetCursor(Texture2D cursor)
    {
        if (cursor != null)
        {
            Cursor.SetCursor(cursor, Vector2.zero, CursorMode.Auto);
        }
    }
    
    private void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
    
    // === STATE HELPERS ===
    
    private bool IsInteractable()
    {
        return _card != null && 
               _card.IsPlayable() && 
               _canvasGroup.interactable && 
               _canvasGroup.blocksRaycasts;
    }
    
    private bool IsCardSelected()
    {
        if (_card == null) return false;
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager != null)
        {
            foreach (var selectedCard in selectionManager.SelectedCards)
            {
                if (selectedCard == _card) return true;
            }
        }
        
        return false;
    }
    
    // === PUBLIC API ===
    
    public void ForceResetVisual()
    {
        if (_hoverAnimation != null)
        {
            StopCoroutine(_hoverAnimation);
            _hoverAnimation = null;
        }
        
        transform.localScale = _originalScale;
        
        // FIXED: Reset Position korrekt
        if (respectLayoutManager)
        {
            var currentPos = transform.localPosition;
            currentPos.z = _originalPosition.z;
            transform.localPosition = currentPos;
        }
        else
        {
            transform.localPosition = _originalPosition;
        }
        
        if (_cardImage != null)
            _cardImage.color = _originalColor;
        
        if (_hoverCanvas != null)
            _hoverCanvas.enabled = false;
        
        ResetCursor();
        _isHovering = false;
        _isPointerDown = false;
    }
    
    public void SetInteractable(bool interactable)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.interactable = interactable;
            _canvasGroup.blocksRaycasts = interactable;
        }
        
        if (!interactable)
        {
            ForceResetVisual();
        }
    }
    
    public void UpdateVisualForState()
    {
        if (!_isHovering) return;
        
        if (IsCardSelected())
        {
            ShowDragPreview();
        }
        else
        {
            ShowHoverEffect();
        }
    }
    
    // NEW: Manual position update from Layout Manager
    public void OnLayoutPositionChanged(Vector3 newPosition)
    {
        if (!_isHovering)
        {
            _originalPosition = newPosition;
            if (respectLayoutManager)
            {
                var currentPos = transform.localPosition;
                currentPos.x = newPosition.x;
                currentPos.y = newPosition.y;
                transform.localPosition = currentPos;
            }
        }
    }
    
    // === CLEANUP ===
    
    void OnDestroy()
    {
        if (_hoverAnimation != null)
        {
            StopCoroutine(_hoverAnimation);
        }
        
        if (_hoverCanvas != null)
        {
            DestroyImmediate(_hoverCanvas.gameObject);
        }
        
        ResetCursor();
    }
    
    void OnDisable()
    {
        ForceResetVisual();
    }
    
    // === LAYOUT MANAGER INTEGRATION ===
    
    void OnTransformParentChanged()
    {
        // Update original position when parent changes
        if (!_isHovering)
        {
            _originalPosition = transform.localPosition;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Test Hover Effect")]
    private void TestHoverEffect()
    {
        StartCoroutine(TestHoverSequence());
    }
    
    private System.Collections.IEnumerator TestHoverSequence()
    {
        ShowHoverEffect();
        StartHoverAnimation(true);
        yield return new WaitForSeconds(1f);
        
        StartHoverAnimation(false);
        yield return new WaitForSeconds(0.5f);
        
        ShowClickFeedback();
    }
    
    [ContextMenu("Force Reset Visual")]
    private void DebugForceReset()
    {
        ForceResetVisual();
        Debug.Log("[CardHoverHandler] Visual reset");
    }
    
    [ContextMenu("Toggle Layout Respect")]
    private void ToggleLayoutRespect()
    {
        respectLayoutManager = !respectLayoutManager;
        Debug.Log($"[CardHoverHandler] Respect Layout Manager: {respectLayoutManager}");
    }
#endif
}