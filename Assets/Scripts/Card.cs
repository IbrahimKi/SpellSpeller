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

public class Card : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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
    
    // Cached components
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    
    // State
    private CardState currentState = CardState.Idle;
    private bool isHovered = false;
    
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
        
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
    private void Start()
    {
        UpdateVisuals();
    }
    
    private void OnDestroy()
    {
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
        
        if (cardNameText != null) cardNameText.text = cardData.cardName;
        if (descriptionText != null) descriptionText.text = cardData.description;
        if (letterValuesText != null) letterValuesText.text = cardData.letterValues;
        if (tierText != null) tierText.text = cardData.tier.ToString();
        
        if (cardImage != null && cardData.cardImage != null)
        {
            cardImage.sprite = cardData.cardImage;
            cardImage.gameObject.SetActive(true);
        }
        
        gameObject.name = $"Card_{cardData.cardName}";
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInteractable || eventData.button != PointerEventData.InputButton.Left) 
            return;
        
        ToggleSelection();
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
        UpdateVisuals();
        OnCardUnhovered?.Invoke(this);
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
        
        Color targetColor = currentState switch
        {
            CardState.Disabled => disabledColor,
            CardState.Selected => selectedColor,
            _ when isHovered => hoverColor,
            _ => normalColor
        };
        
        cardBackground.color = targetColor;
        
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
    
    public void ResetCardState()
    {
        isSelected = false;
        isInteractable = true;
        isHovered = false;
        currentState = CardState.Idle;
        cardData = null;
        
        // Clear UI
        if (cardNameText != null) cardNameText.text = "";
        if (descriptionText != null) descriptionText.text = "";
        if (letterValuesText != null) letterValuesText.text = "";
        if (tierText != null) tierText.text = "";
        if (cardImage != null) cardImage.gameObject.SetActive(false);
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
        
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
        }
        
        UpdateVisuals();
    }
}