using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using GameCore.Enums;
using GameCore.Data;

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
            var containerGO = new GameObject("Card Container");
            cardContainer = containerGO.AddComponent<RectTransform>();
            cardContainer.SetParent(transform, false);
            cardContainer.anchorMin = Vector2.zero;
            cardContainer.anchorMax = Vector2.one;
            cardContainer.offsetMin = Vector2.zero;
            cardContainer.offsetMax = Vector2.zero;
        }
        
        if (slotNumberText == null)
        {
            var numberObj = cardContainer.Find("Slot Number");
            if (numberObj != null)
                slotNumberText = numberObj.GetComponent<TextMeshProUGUI>();
        }
    }
    
    private void SetupSlot()
    {
        if (slotNumberText != null)
            slotNumberText.text = (slotIndex + 1).ToString();
    }
    
    // === SLOT MANAGEMENT ===
    
    public void SetSlotIndex(int index)
    {
        slotIndex = index;
        if (slotNumberText != null)
            slotNumberText.text = (index + 1).ToString();
    }
    
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        UpdateVisuals();
    }
    
    public bool TryPlaceCard(Card card)
    {
        if (!CanAcceptCard(card)) return false;
        
        PlaceCard(card);
        return true;
    }
    
    public bool CanAcceptCard(Card card)
    {
        return _isEnabled && IsEmpty && card != null && card.IsPlayable();
    }
    
    private void PlaceCard(Card card)
    {
        if (card == null) return;
        
        // Remove from previous location
        RemoveCard(false);
        
        // Set new card
        _occupyingCard = card;
        
        // Position card in slot
        card.transform.SetParent(cardContainer, false);
        var cardRect = card.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localScale = Vector3.one * 0.85f; // Slightly smaller to fit in slot
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
        }
        
        UpdateVisuals();
        
        OnCardPlaced?.Invoke(this, card);
        OnSlotStateChanged?.Invoke(this);
        
        Debug.Log($"[CardSlotBehaviour] Card {card.GetCardName()} placed in slot {slotIndex + 1}");
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
    
    // === VISUAL UPDATES ===
    
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
    }
    
    // === UI EVENT HANDLERS ===
    
    public void OnDrop(PointerEventData eventData)
    {
        if (!_isEnabled) return;
        
        var draggedCard = eventData.pointerDrag?.GetComponent<Card>();
        if (draggedCard != null)
        {
            TryPlaceCard(draggedCard);
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
    
    // === UTILITY ===
    
    public string GetSlotInfo()
    {
        return $"Slot {slotIndex + 1}: {(IsFilled ? _occupyingCard.GetCardName() : "Empty")} (Enabled: {_isEnabled})";
    }

#if UNITY_EDITOR
    [ContextMenu("Debug Slot Info")]
    private void DebugSlotInfo()
    {
        Debug.Log($"[CardSlotBehaviour] {GetSlotInfo()}");
    }
#endif
}