using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CardSlotBehaviour : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Slot Configuration")]
    [SerializeField] private int slotIndex = 0;
    
    [Header("UI Components")]
    [SerializeField] private Image slotImage;
    [SerializeField] private TextMeshProUGUI slotNumberText;
    [SerializeField] private RectTransform cardContainer;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color emptyColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);
    [SerializeField] private Color filledColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
    [SerializeField] private Color highlightColor = new Color(0f, 1f, 0f, 0.7f);
    [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
    
    // State
    private Card _occupyingCard;
    private bool _isHighlighted = false;
    private bool _isEnabled = true;
    
    // Properties
    public int SlotIndex => slotIndex;
    public bool IsEmpty => _occupyingCard == null;
    public bool IsFilled => _occupyingCard != null;
    public Card OccupyingCard => _occupyingCard;
    public bool IsEnabled => _isEnabled;
    
    // Events
    public static event System.Action<CardSlotBehaviour, Card> OnCardPlaced;
    public static event System.Action<CardSlotBehaviour, Card> OnCardRemoved;
    public static event System.Action<CardSlotBehaviour> OnSlotStateChanged;
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void Start()
    {
        SetupSlot();
        UpdateVisuals();
    }
    
    private void InitializeComponents()
    {
        if (slotImage == null)
            slotImage = GetComponent<Image>();
            
        if (cardContainer == null)
        {
            cardContainer = transform as RectTransform;
            var existingContainer = transform.Find("Card Container");
            if (existingContainer != null)
                cardContainer = existingContainer as RectTransform;
        }
        
        if (slotNumberText == null)
        {
            var numberObj = GetComponentInChildren<TextMeshProUGUI>();
            if (numberObj != null)
                slotNumberText = numberObj;
        }
    }
    
    private void SetupSlot()
    {
        if (slotNumberText != null)
            slotNumberText.text = (slotIndex + 1).ToString();
    }
    
    public void SetSlotIndex(int index)
    {
        slotIndex = index;
        if (slotNumberText != null)
            slotNumberText.text = (index + 1).ToString();
        gameObject.name = $"Card Slot {index + 1}";
    }
    
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        UpdateVisuals();
    }
    
    public bool TryPlaceCard(Card card)
    {
        if (!CanAcceptCard(card)) 
        {
            Debug.LogWarning($"[CardSlotBehaviour] Cannot accept card {card?.GetCardName()} in slot {slotIndex + 1}");
            return false;
        }
        
        PlaceCard(card);
        return true;
    }
    
    public bool CanAcceptCard(Card card)
    {
        if (!_isEnabled || !IsEmpty || card == null) 
            return false;
        
        // Simplified validation
        return card.IsInteractable && card.CardData != null;
    }
    
    private void PlaceCard(Card card)
    {
        if (card == null) return;
        
        RemoveCard(false);
        _occupyingCard = card;
        SetupCardInSlot(card);
        UpdateVisuals();
        
        OnCardPlaced?.Invoke(this, card);
        OnSlotStateChanged?.Invoke(this);
        
        Debug.Log($"[CardSlotBehaviour] Card {card.GetCardName()} placed in slot {slotIndex + 1}");
    }
    
    private void SetupCardInSlot(Card card)
    {
        var cardTransform = card.transform;
        var cardRect = card.GetComponent<RectTransform>();
        
        if (cardRect == null)
        {
            Debug.LogError($"[CardSlotBehaviour] Card {card.GetCardName()} has no RectTransform!");
            return;
        }
        
        // Erst Parent setzen
        cardTransform.SetParent(cardContainer, false);
        
        // Dann Anchors/Pivot RICHTIG setzen für centered positioning
        cardRect.anchorMin = Vector2.one * 0.5f;
        cardRect.anchorMax = Vector2.one * 0.5f;
        cardRect.pivot = Vector2.one * 0.5f;
        cardRect.anchoredPosition = Vector2.zero;
        
        // LocalPosition explizit auf zero
        cardRect.localPosition = Vector3.zero;
        
        // Calculate scale
        Vector2 slotSize = (transform as RectTransform).sizeDelta;
        Vector2 cardSize = cardRect.sizeDelta;
        
        float scaleX = (slotSize.x * 0.9f) / cardSize.x;
        float scaleY = (slotSize.y * 0.9f) / cardSize.y;
        float uniformScale = Mathf.Min(scaleX, scaleY, 1f);
        
        cardRect.localScale = Vector3.one * uniformScale;
        
        // Force Layout Update
        cardTransform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect);
        
        Debug.Log($"[CardSlotBehaviour] Card positioned - Pos: {cardRect.anchoredPosition}, Scale: {uniformScale:F2}");
    }
    
    public Card RemoveCard(bool triggerEvents = true)
    {
        if (IsEmpty) return null;
        
        Card removedCard = _occupyingCard;
        _occupyingCard = null;
        
        UpdateVisuals();
        
        if (triggerEvents)
        {
            OnCardRemoved?.Invoke(this, removedCard);
            OnSlotStateChanged?.Invoke(this);
        }
        
        Debug.Log($"[CardSlotBehaviour] Card {removedCard.GetCardName()} removed from slot {slotIndex + 1}");
        return removedCard;
    }
    
    private void UpdateVisuals()
    {
        if (slotImage == null) return;
        
        Color targetColor;
        
        if (!_isEnabled)
            targetColor = disabledColor;
        else if (_isHighlighted)
            targetColor = highlightColor;
        else if (IsFilled)
            targetColor = filledColor;
        else
            targetColor = emptyColor;
        
        slotImage.color = targetColor;
        
        if (slotNumberText != null)
            slotNumberText.gameObject.SetActive(IsEmpty);
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        if (!_isEnabled) 
        {
            Debug.LogWarning($"[CardSlotBehaviour] Slot {slotIndex + 1} is disabled");
            return;
        }
        
        var draggedCard = eventData.pointerDrag?.GetComponent<Card>();
        if (draggedCard != null)
        {
            bool success = TryPlaceCard(draggedCard);
            Debug.Log($"[CardSlotBehaviour] Drop attempt: {(success ? "Success" : "Failed")}");
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isEnabled) return;
        _isHighlighted = true;
        UpdateVisuals();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHighlighted = false;
        UpdateVisuals();
    }
    
    public string GetSlotInfo()
    {
        return $"Slot {slotIndex + 1}: {(IsFilled ? _occupyingCard.GetCardName() : "Empty")} (Enabled: {_isEnabled})";
    }
    
    public void ForceRefreshVisuals()
    {
        UpdateVisuals();
    }
    
    public bool ValidateCard()
    {
        if (_occupyingCard == null) return true;
        
        bool isCardValid = _occupyingCard.IsValid();
        if (!isCardValid)
        {
            Debug.LogWarning($"[CardSlotBehaviour] Invalid card detected in slot {slotIndex + 1}, removing...");
            RemoveCard();
            return false;
        }
        
        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug Slot Info")]
    private void DebugSlotInfo()
    {
        Debug.Log($"[CardSlotBehaviour] {GetSlotInfo()}");
        Debug.Log($"  CardContainer: {cardContainer?.name}");
        Debug.Log($"  SlotImage: {slotImage?.name}");
        Debug.Log($"  NumberText: {slotNumberText?.text}");
        Debug.Log($"  Enabled: {_isEnabled}, Highlighted: {_isHighlighted}");
        
        if (_occupyingCard != null)
        {
            var cardRect = _occupyingCard.GetComponent<RectTransform>();
            Debug.Log($"  Card Position: {cardRect.anchoredPosition}");
            Debug.Log($"  Card Scale: {cardRect.localScale}");
        }
    }
    
    [ContextMenu("Force Remove Card")]
    private void ForceRemoveCard()
    {
        RemoveCard();
    }
    
    [ContextMenu("Test Card Placement")]
    private void TestCardPlacement()
    {
        Debug.Log($"[CardSlotBehaviour] Testing slot {slotIndex + 1}:");
        Debug.Log($"  IsEmpty: {IsEmpty}");
        Debug.Log($"  IsEnabled: {IsEnabled}");
        Debug.Log($"  CanAcceptCard: {CanAcceptCard(null)}");
    }
#endif
}