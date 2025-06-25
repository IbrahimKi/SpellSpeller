using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using GameCore.Enums;
using GameCore.Data;

public class DropAreaHandler : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private Color highlightColor = new Color(0f, 1f, 0f, 0.3f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private Color slotOccupiedColor = new Color(0.5f, 0.5f, 1f, 0.4f);
    
    [Header("Card Slot System")]
    [SerializeField] private bool enableCardSlots = false;
    [SerializeField] private int maxSlots = 5;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotsContainer;
    
    // Public Properties für CardDragHandler Access
    public List<CardSlot> cardSlots => cardSlots;
    public Transform SlotsContainer => slotsContainer;
    [SerializeField] private float slotSpacing = 10f;
    [SerializeField] private Vector2 slotSize = new Vector2(120f, 180f);
    
    [Header("Slot Visual Settings")]
    [SerializeField] private Color emptySlotColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);
    [SerializeField] private Color filledSlotColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
    [SerializeField] private bool showSlotNumbers = true;
    [SerializeField] private Color slotNumberColor = Color.white;
    
    private Image dropAreaImage;
    private GameObject currentDraggedCard;
    private bool canAcceptDrop = false;
    
    // Card Slot System
    private List<CardSlot> cardSlots = new List<CardSlot>();
    private bool slotsInitialized = false;
    
    [System.Serializable]
    public class CardSlot
    {
        public int slotIndex;
        public RectTransform slotTransform;
        public Image slotImage;
        public TextMeshProUGUI slotNumberText;
        public Card occupyingCard;
        public bool isEmpty => occupyingCard == null;
        public bool isFilled => occupyingCard != null;
        
        public CardSlot(int index)
        {
            slotIndex = index;
        }
    }
    
    // Events für Slot-System
    public static event System.Action<int, Card> OnCardSlotFilled;
    public static event System.Action<int, Card> OnCardSlotCleared;
    public static event System.Action<List<Card>> OnSlotSequenceChanged;
    
    void Awake()
    {
        dropAreaImage = GetComponent<Image>();
        if (dropAreaImage == null)
        {
            dropAreaImage = gameObject.AddComponent<Image>();
        }
        
        if (dropAreaImage.color.a == 0)
        {
            dropAreaImage.color = normalColor;
        }
        
        if (enableCardSlots)
        {
            InitializeCardSlots();
        }
    }
    
    void OnEnable()
    {
        CardDragHandler.OnCardDragStart.AddListener(OnCardDragStart);
        CardDragHandler.OnCardDragEnd.AddListener(OnCardDragEnd);
    }
    
    void OnDisable()
    {
        CardDragHandler.OnCardDragStart.RemoveListener(OnCardDragStart);
        CardDragHandler.OnCardDragEnd.RemoveListener(OnCardDragEnd);
    }
    
    // === CARD SLOT SYSTEM INITIALIZATION ===
    
    private void InitializeCardSlots()
    {
        if (_slotsInitialized) return;
        
        Debug.Log($"[DropAreaHandler] Initializing {maxSlots} card slots");
        
        // Container erstellen falls nicht vorhanden
        if (slotsContainer == null)
        {
            GameObject container = new GameObject("Card Slots Container");
            container.transform.SetParent(transform, false);
            slotsContainer = container.transform;
            
            // Layout Group für automatische Anordnung
            var layoutGroup = container.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = slotSpacing;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
        }
        
        // Slots erstellen
        for (int i = 0; i < maxSlots; i++)
        {
            CreateCardSlot(i);
        }
        
        _slotsInitialized = true;
        Debug.Log($"[DropAreaHandler] Card slots initialized successfully");
    }
    
    private void CreateCardSlot(int index)
    {
        GameObject slotObject;
        
        if (slotPrefab != null)
        {
            slotObject = Instantiate(slotPrefab, slotsContainer);
        }
        else
        {
            slotObject = CreateDefaultSlot();
        }
        
        slotObject.name = $"Card Slot {index + 1}";
        
        var slot = new CardSlot(index);
        SetupSlotComponents(slot, slotObject);
        
        _cardSlots.Add(slot);
        UpdateSlotVisual(slot);
    }
    
    private GameObject CreateDefaultSlot()
    {
        GameObject slotObject = new GameObject("Card Slot");
        slotObject.transform.SetParent(slotsContainer, false);
        
        // RectTransform Setup
        var rectTransform = slotObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = slotSize;
        
        // Image Component
        var image = slotObject.AddComponent<Image>();
        image.color = emptySlotColor;
        
        // Outline für bessere Sichtbarkeit
        var outline = slotObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, 1);
        
        // Slot Number Text
        if (showSlotNumbers)
        {
            GameObject textObject = new GameObject("Slot Number");
            textObject.transform.SetParent(slotObject.transform, false);
            
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(10f, -10f);
            textRect.sizeDelta = new Vector2(30f, 30f);
            
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = "";
            text.color = slotNumberColor;
            text.fontSize = 14f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
        }
        
        return slotObject;
    }
    
    private void SetupSlotComponents(CardSlot slot, GameObject slotObject)
    {
        slot.slotTransform = slotObject.GetComponent<RectTransform>();
        slot.slotImage = slotObject.GetComponent<Image>();
        
        if (showSlotNumbers)
        {
            slot.slotNumberText = slotObject.GetComponentInChildren<TextMeshProUGUI>();
            if (slot.slotNumberText != null)
            {
                slot.slotNumberText.text = (slot.slotIndex + 1).ToString();
            }
        }
    }
    
    // === SLOT MANAGEMENT ===
    
    public bool TryPlaceCardInSlot(Card card, int preferredSlotIndex = -1)
    {
        if (!enableCardSlots || card == null) return false;
        
        int targetSlot = preferredSlotIndex;
        
        // Wenn kein spezifischer Slot angegeben, finde ersten leeren
        if (targetSlot < 0 || targetSlot >= _cardSlots.Count || !_cardSlots[targetSlot].isEmpty)
        {
            targetSlot = FindFirstEmptySlot();
        }
        
        if (targetSlot < 0) return false;
        
        PlaceCardInSlot(card, targetSlot);
        return true;
    }
    
    private void PlaceCardInSlot(Card card, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _cardSlots.Count) return;
        
        var slot = _cardSlots[slotIndex];
        
        // Vorherige Karte entfernen falls vorhanden
        if (slot.isFilled)
        {
            RemoveCardFromSlot(slotIndex);
        }
        
        // Karte in Slot platzieren
        slot.occupyingCard = card;
        
        // Card visuell positionieren
        card.transform.SetParent(slot.slotTransform, false);
        var cardRect = card.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localScale = Vector3.one;
            
            // Leicht skalieren um in Slot zu passen
            float scaleMultiplier = Mathf.Min(
                (slotSize.x - 10f) / cardRect.sizeDelta.x,
                (slotSize.y - 10f) / cardRect.sizeDelta.y
            );
            cardRect.localScale = Vector3.one * Mathf.Min(scaleMultiplier, 1f);
        }
        
        UpdateSlotVisual(slot);
        
        OnCardSlotFilled?.Invoke(slotIndex, card);
        NotifySlotSequenceChanged();
        
        Debug.Log($"[DropAreaHandler] Card '{card.GetCardName()}' placed in slot {slotIndex + 1}");
    }
    
    public void RemoveCardFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _cardSlots.Count) return;
        
        var slot = _cardSlots[slotIndex];
        if (slot.isEmpty) return;
        
        Card removedCard = slot.occupyingCard;
        slot.occupyingCard = null;
        
        UpdateSlotVisual(slot);
        
        OnCardSlotCleared?.Invoke(slotIndex, removedCard);
        NotifySlotSequenceChanged();
        
        Debug.Log($"[DropAreaHandler] Card '{removedCard.GetCardName()}' removed from slot {slotIndex + 1}");
    }
    
    public void ClearAllSlots()
    {
        for (int i = 0; i < _cardSlots.Count; i++)
        {
            if (_cardSlots[i].isFilled)
            {
                RemoveCardFromSlot(i);
            }
        }
        
        Debug.Log("[DropAreaHandler] All card slots cleared");
    }
    
    // === SLOT QUERIES ===
    
    public List<Card> GetSlotSequence()
    {
        return _cardSlots
            .Where(slot => slot.isFilled)
            .OrderBy(slot => slot.slotIndex)
            .Select(slot => slot.occupyingCard)
            .ToList();
    }
    
    public List<Card> GetFilledSlots()
    {
        return _cardSlots
            .Where(slot => slot.isFilled)
            .Select(slot => slot.occupyingCard)
            .ToList();
    }
    
    public int GetFilledSlotCount()
    {
        return _cardSlots.Count(slot => slot.isFilled);
    }
    
    public int GetEmptySlotCount()
    {
        return _cardSlots.Count(slot => slot.isEmpty);
    }
    
    public bool HasEmptySlots()
    {
        return GetEmptySlotCount() > 0;
    }
    
    public bool AreAllSlotsFilled()
    {
        return GetFilledSlotCount() == _cardSlots.Count;
    }
    
    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < _cardSlots.Count; i++)
        {
            if (_cardSlots[i].isEmpty)
                return i;
        }
        return -1;
    }
    
    private int FindClosestEmptySlot(Vector2 position)
    {
        int closestSlot = -1;
        float closestDistance = float.MaxValue;
        
        for (int i = 0; i < _cardSlots.Count; i++)
        {
            if (_cardSlots[i].isEmpty)
            {
                float distance = Vector2.Distance(position, _cardSlots[i].slotTransform.anchoredPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestSlot = i;
                }
            }
        }
        
        return closestSlot;
    }
    
    // === VISUAL UPDATES ===
    
    private void UpdateSlotVisual(CardSlot slot)
    {
        if (slot.slotImage != null)
        {
            slot.slotImage.color = slot.isFilled ? filledSlotColor : emptySlotColor;
        }
    }
    
    private void NotifySlotSequenceChanged()
    {
        OnSlotSequenceChanged?.Invoke(GetSlotSequence());
    }
    
    // === EXISTING DROP AREA FUNCTIONALITY ===
    
    private void OnCardDragStart(GameObject card)
    {
        currentDraggedCard = card;
        UpdateDropValidity();
    }
    
    private void OnCardDragEnd(GameObject card)
    {
        currentDraggedCard = null;
        canAcceptDrop = false;
        dropAreaImage.color = normalColor;
    }
    
    private void UpdateDropValidity()
    {
        if (currentDraggedCard == null)
        {
            canAcceptDrop = false;
            dropAreaImage.color = normalColor;
            return;
        }
        
        Card cardComponent = currentDraggedCard.GetComponent<Card>();
        if (!cardComponent.IsPlayable())
        {
            canAcceptDrop = false;
            dropAreaImage.color = normalColor;
            return;
        }
        
        // Slot-spezifische Validierung
        if (enableCardSlots)
        {
            canAcceptDrop = HasEmptySlots();
            dropAreaImage.color = canAcceptDrop ? highlightColor : invalidColor;
            return;
        }
        
        // Standard Drop Area Validierung
        if (gameObject.CompareTag("PlayArea"))
        {
            var cardList = new List<Card> { cardComponent };
            canAcceptDrop = SpellcastManager.CheckCanPlayCards(cardList);
            
            if (canAcceptDrop)
            {
                canAcceptDrop = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => 
                    cm.CanPerformPlayerAction(PlayerActionType.PlayCards)
                );
            }
        }
        else if (gameObject.CompareTag("DiscardArea"))
        {
            canAcceptDrop = SpellcastManager.CheckCanDiscardCard(cardComponent) &&
                          CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => 
                              cm.CanSpendResource(ResourceType.Creativity, 1)
                          );
        }
        else
        {
            canAcceptDrop = false;
        }
        
        dropAreaImage.color = canAcceptDrop ? highlightColor : invalidColor;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentDraggedCard != null)
        {
            UpdateDropValidity();
            
            if (canAcceptDrop)
            {
                Color hoverColor = highlightColor;
                hoverColor.a = Mathf.Min(1f, highlightColor.a * 1.5f);
                dropAreaImage.color = hoverColor;
            }
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentDraggedCard != null)
        {
            dropAreaImage.color = canAcceptDrop ? highlightColor : invalidColor;
        }
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null && canAcceptDrop)
        {
            Card cardComponent = eventData.pointerDrag.GetComponent<Card>();
            
            if (enableCardSlots && cardComponent != null)
            {
                // Card in Slot platzieren
                Vector2 localPosition;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    slotsContainer, 
                    eventData.position, 
                    eventData.pressEventCamera, 
                    out localPosition
                );
                
                int targetSlot = FindClosestEmptySlot(localPosition);
                if (targetSlot >= 0)
                {
                    TryPlaceCardInSlot(cardComponent, targetSlot);
                }
            }
            
            dropAreaImage.color = highlightColor;
        }
        
        Invoke(nameof(ResetColor), 0.1f);
    }
    
    private void ResetColor()
    {
        dropAreaImage.color = normalColor;
    }
    
    public void RefreshDropValidity()
    {
        if (currentDraggedCard != null)
        {
            UpdateDropValidity();
        }
    }
    
    // === PUBLIC API FÜR GAMEUI INTEGRATION ===
    
    public bool IsSlotSystemEnabled()
    {
        return enableCardSlots && _slotsInitialized;
    }
    
    public void EnableSlotSystem(bool enable)
    {
        enableCardSlots = enable;
        if (enable && !_slotsInitialized)
        {
            InitializeCardSlots();
        }
        
        if (slotsContainer != null)
        {
            slotsContainer.gameObject.SetActive(enable);
        }
    }
    
    public string GetSlotLetterSequence()
    {
        return GetSlotSequence().GetLetterSequence();
    }
    
    public bool CanPlaySlotSequence()
    {
        var sequence = GetSlotSequence();
        return sequence.Count > 0 && SpellcastManager.CheckCanPlayCards(sequence);
    }

#if UNITY_EDITOR
    [ContextMenu("Test Fill All Slots")]
    private void TestFillAllSlots()
    {
        if (!enableCardSlots) return;
        
        Debug.Log("[DropAreaHandler] Testing slot system...");
        Debug.Log($"Slots initialized: {_slotsInitialized}");
        Debug.Log($"Slot count: {_cardSlots.Count}");
        Debug.Log($"Empty slots: {GetEmptySlotCount()}");
        Debug.Log($"Filled slots: {GetFilledSlotCount()}");
    }
    
    [ContextMenu("Clear All Slots")]
    private void TestClearAllSlots()
    {
        ClearAllSlots();
    }
    
    [ContextMenu("Log Slot Sequence")]
    private void LogSlotSequence()
    {
        var sequence = GetSlotSequence();
        Debug.Log($"[DropAreaHandler] Slot sequence ({sequence.Count} cards):");
        for (int i = 0; i < sequence.Count; i++)
        {
            Debug.Log($"  {i + 1}: {sequence[i].GetCardName()} ({sequence[i].GetLetterValues()})");
        }
        Debug.Log($"Letter sequence: '{GetSlotLetterSequence()}'");
    }
#endif
}