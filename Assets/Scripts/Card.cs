using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public enum CardState
{
    Idle,
    Selected,
    Disabled
}

public class Card : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Card Data")]
    [SerializeField] private CardData cardData;
    [SerializeField] private bool isSelected = false;
    [SerializeField] private bool isInteractable = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private Image cardBackground;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color hoverColor = Color.gray;
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.5f);
    
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI letterValuesText;
    [SerializeField] private TextMeshProUGUI tierText;
    [SerializeField] private Image cardImage;
    
    // Events
    public static System.Action<Card> OnCardSelected;
    public static System.Action<Card> OnCardDeselected;
    public static System.Action<Card> OnCardHovered;
    public static System.Action<Card> OnCardUnhovered;
    public static System.Action<Card> OnCardPlayTriggered; // NEW: Direct play event
    
    // Cached components for performance
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    
    // Current state tracking
    private CardState currentState = CardState.Idle;
    private bool isHovered = false;
    
    // NEW: Play interaction tracking
    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f;
    private bool isMouseDown = false;
    private float mouseDownTime = 0f;
    private const float HOLD_THRESHOLD = 0.5f;
    
    // Properties
    public CardData CardData => cardData;
    public bool IsSelected => isSelected;
    public bool IsInteractable => isInteractable;
    public CardState CurrentState => currentState;
    public RectTransform RectTransform => rectTransform;
    
    private void Awake()
    {
        CacheComponents();
    }
    
    private void CacheComponents()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (cardBackground == null)
            cardBackground = GetComponent<Image>();
        
        // Cache CanvasGroup for alpha control
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
        // Auto-find UI components if not assigned
        cardNameText ??= GetComponentInChildren<TextMeshProUGUI>(true);
        
        // Find specific components by name for better organization
        var textComponents = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in textComponents)
        {
            switch (text.name.ToLowerInvariant())
            {
                case "cardname" when cardNameText == null:
                    cardNameText = text;
                    break;
                case "description" when descriptionText == null:
                    descriptionText = text;
                    break;
                case "lettervalues" when letterValuesText == null:
                    letterValuesText = text;
                    break;
                case "tier" when tierText == null:
                    tierText = text;
                    break;
            }
        }
        
        cardImage ??= GetComponentInChildren<Image>(true);
        if (cardImage == cardBackground) cardImage = null; // Avoid using background as card image
    }
    
    private void Start()
    {
        UpdateVisuals();
    }
    
    private void Update()
    {
        // Check for hold-to-play
        if (isMouseDown && Time.time - mouseDownTime >= HOLD_THRESHOLD)
        {
            isMouseDown = false; // Prevent multiple triggers
            TriggerCardPlay();
        }
    }
    
    private void OnDestroy()
    {
        // Notify layout manager to clean up references
        if (HandLayoutManager.HasInstance)
            HandLayoutManager.Instance.CleanupCardReference(this);
    }
    
    public void SetCardData(CardData data)
    {
        cardData = data;
        UpdateCardDisplay();
    }
    
    private void UpdateCardDisplay()
    {
        if (cardData == null) return;
        
        // Update text fields
        if (cardNameText != null) cardNameText.text = cardData.cardName;
        if (descriptionText != null) descriptionText.text = cardData.description;
        if (letterValuesText != null) letterValuesText.text = cardData.letterValues;
        if (tierText != null) tierText.text = cardData.tier.ToString();
        
        // Update card image
        if (cardImage != null)
        {
            if (cardData.cardImage != null)
            {
                cardImage.sprite = cardData.cardImage;
                cardImage.gameObject.SetActive(true);
            }
            else
            {
                cardImage.gameObject.SetActive(false);
            }
        }
        
        // Update object name for debugging
        gameObject.name = $"Card_{cardData.cardName}";
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isInteractable || eventData.button != PointerEventData.InputButton.Left) return;
        
        isMouseDown = true;
        mouseDownTime = Time.time;
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        isMouseDown = false;
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInteractable) return;
        
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            float timeSinceLastClick = Time.time - lastClickTime;
            
            // Check for double-click
            if (timeSinceLastClick <= DOUBLE_CLICK_TIME)
            {
                TriggerCardPlay();
                return;
            }
            
            // Check for modifier key (Ctrl/Cmd for instant play)
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || 
                Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand))
            {
                TriggerCardPlay();
                return;
            }
            
            // Regular selection
            ToggleSelection();
            lastClickTime = Time.time;
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInteractable) return;
        
        isHovered = true;
        UpdateVisuals();
        OnCardHovered?.Invoke(this);
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isInteractable) return;
        
        isHovered = false;
        isMouseDown = false; // Cancel hold if mouse leaves
        UpdateVisuals();
        OnCardUnhovered?.Invoke(this);
    }
    
    private void TriggerCardPlay()
    {
        if (!isInteractable) return;
        
        OnCardPlayTriggered?.Invoke(this);
    }
    
    public void ToggleSelection()
    {
        if (!isInteractable) return;
        
        if (isSelected)
            Deselect();
        else
            Select();
    }
    
    public void Select()
    {
        if (!isInteractable || isSelected) return;
        
        isSelected = true;
        currentState = CardState.Selected;
        UpdateVisuals();
        OnCardSelected?.Invoke(this);
    }
    
    public void Deselect()
    {
        if (!isSelected) return;
        
        isSelected = false;
        currentState = CardState.Idle;
        UpdateVisuals();
        OnCardDeselected?.Invoke(this);
    }
    
    public void ForceDeselect()
    {
        isSelected = false;
        currentState = CardState.Idle;
        UpdateVisuals();
    }
    
    private void UpdateVisuals()
    {
        if (cardBackground == null) return;
        
        // Determine color based on state priority
        Color targetColor = currentState switch
        {
            CardState.Disabled => disabledColor,
            CardState.Selected => selectedColor,
            _ when isHovered => hoverColor,
            _ => normalColor
        };
        
        cardBackground.color = targetColor;
        
        // Update canvas group alpha for disabled state
        if (canvasGroup != null)
        {
            canvasGroup.alpha = currentState == CardState.Disabled ? 0.5f : 1f;
        }
    }
    
    public void SetInteractable(bool interactable)
    {
        if (isInteractable == interactable) return;
        
        isInteractable = interactable;
        currentState = interactable ? CardState.Idle : CardState.Disabled;
        UpdateVisuals();
    }
    
    // Pool system support
    public void ClearEventSubscriptions()
    {
        OnCardSelected = null;
        OnCardDeselected = null;
        OnCardHovered = null;
        OnCardUnhovered = null;
        OnCardPlayTriggered = null;
    }
    
    public void ResetCardState()
    {
        isSelected = false;
        isInteractable = true;
        isHovered = false;
        isMouseDown = false;
        currentState = CardState.Idle;
        cardData = null;
        lastClickTime = 0f;
        mouseDownTime = 0f;
        
        // Clear UI elements
        if (cardNameText != null) cardNameText.text = "";
        if (descriptionText != null) descriptionText.text = "";
        if (letterValuesText != null) letterValuesText.text = "";
        if (tierText != null) tierText.text = "";
        if (cardImage != null) cardImage.gameObject.SetActive(false);
        
        // Reset canvas group
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
        
        // Reset transform
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
        }
        
        UpdateVisuals();
    }
    
    // Utility methods
    public bool IsInState(CardState state) => currentState == state;
    public bool IsInAnyState(params CardState[] states)
    {
        foreach (var state in states)
        {
            if (currentState == state) return true;
        }
        return false;
    }
    
    // Debug methods
#if UNITY_EDITOR
    [ContextMenu("Select Card")]
    public void DebugSelect() => Select();
    
    [ContextMenu("Deselect Card")]
    public void DebugDeselect() => Deselect();
    
    [ContextMenu("Update Display")]
    public void DebugUpdateDisplay() => UpdateCardDisplay();
    
    [ContextMenu("Toggle Interactable")]
    public void DebugToggleInteractable() => SetInteractable(!isInteractable);
    
    [ContextMenu("Trigger Play")]
    public void DebugTriggerPlay() => TriggerCardPlay();
    
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        
        // Validate component references in editor
        if (cardBackground == null)
            cardBackground = GetComponent<Image>();
    }
#endif
}