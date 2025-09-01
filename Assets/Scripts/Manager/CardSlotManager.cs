using UnityEngine;
using System.Collections.Generic;
using System.Linq;


public class CardSlotManager : SingletonBehaviour<CardSlotManager>, IGameManager
{
    [Header("Slot Configuration")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private int maxSlots = 5;
    [SerializeField] private bool enableSlotSystem = true;
    
    [Header("Layout")]
    [SerializeField] private float slotSpacing = 10f;
    [SerializeField] private Vector2 slotSize = new Vector2(120f, 180f);
    
    private List<CardSlotBehaviour> _slots = new List<CardSlotBehaviour>();
    private bool _isInitialized = false;
    
    public static event System.Action<List<Card>> OnSlotSequenceChanged;
    public static event System.Action<CardSlotBehaviour, Card> OnCardPlacedInSlot;
    public static event System.Action<CardSlotBehaviour, Card> OnCardRemovedFromSlot;
    
    public bool IsEnabled => enableSlotSystem;
    public bool IsReady => _isInitialized;
    public List<CardSlotBehaviour> Slots => _slots;
    public int SlotCount => _slots.Count;
    public int FilledSlotCount => _slots.Count(s => s != null && s.IsFilled);
    public int EmptySlotCount => _slots.Count(s => s != null && s.IsEmpty);
    public bool HasEmptySlots => EmptySlotCount > 0;
    
    protected override void OnAwakeInitialize()
    {
        SetupSlotContainer();
        InitializeSlots();
    }
    
    private void Start()
    {
        SetupEventListeners();
    }
    
    private void OnDestroy()
    {
        CleanupEventListeners();
    }
    
    private void SetupSlotContainer()
    {
        if (slotContainer == null)
        {
            slotContainer = FindSlotContainerInScene();
            if (slotContainer == null)
                CreateNewSlotContainer();
        }
        
        Debug.Log($"[CardSlotManager] Using slot container: {slotContainer.name}");
    }
    
    private Transform FindSlotContainerInScene()
    {
        var possibleNames = new[] { "CardSlotArea", "SlotContainer", "Card Slot Container", "Slots" };
        
        foreach (var name in possibleNames)
        {
            var found = GameObject.Find(name);
            if (found != null)
            {
                Debug.Log($"[CardSlotManager] Found existing container: {name}");
                return found.transform;
            }
        }
        
        var allRectTransforms = FindObjectsOfType<RectTransform>();
        foreach (var rect in allRectTransforms)
        {
            if (rect.GetComponentsInChildren<CardSlotBehaviour>().Length > 0)
            {
                Debug.Log($"[CardSlotManager] Found container with slots: {rect.name}");
                return rect;
            }
        }
        
        return null;
    }
    
    private void CreateNewSlotContainer()
    {
        GameObject container = new GameObject("CardSlotContainer");
        container.transform.SetParent(transform, false);
        slotContainer = container.transform;
        
        var layoutGroup = container.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layoutGroup.spacing = slotSpacing;
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        
        Debug.Log("[CardSlotManager] Created new slot container");
    }
    
    private void InitializeSlots()
    {
        if (_isInitialized) return;
        
        Debug.Log("[CardSlotManager] Initializing slots...");
        
        ClearExistingSlots();
        
        var existingSlots = slotContainer.GetComponentsInChildren<CardSlotBehaviour>();
        
        if (existingSlots.Length > 0)
        {
            Debug.Log($"[CardSlotManager] Found {existingSlots.Length} existing slots, registering them");
            RegisterExistingSlots(existingSlots);
        }
        else
        {
            Debug.Log($"[CardSlotManager] Creating {maxSlots} new slots");
            CreateNewSlots();
        }
        
        ValidateAllSlots();
        
        _isInitialized = true;
        Debug.Log($"[CardSlotManager] Initialized with {_slots.Count} valid slots");
    }
    
    private void ValidateAllSlots()
    {
        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            if (_slots[i] == null || _slots[i].gameObject == null)
            {
                Debug.LogWarning($"[CardSlotManager] Removing null slot at index {i}");
                _slots.RemoveAt(i);
            }
        }
        
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].SetSlotIndex(i);
        }
    }
    
    private void RegisterExistingSlots(CardSlotBehaviour[] existingSlots)
    {
        for (int i = 0; i < existingSlots.Length && i < maxSlots; i++)
        {
            var slot = existingSlots[i];
            if (slot != null)
            {
                slot.SetSlotIndex(i);
                _slots.Add(slot);
                Debug.Log($"[CardSlotManager] Registered existing slot {i + 1}");
            }
        }
        
        for (int i = maxSlots; i < existingSlots.Length; i++)
        {
            if (existingSlots[i] != null)
            {
                DestroyImmediate(existingSlots[i].gameObject);
                Debug.Log($"[CardSlotManager] Removed excess slot {i + 1}");
            }
        }
    }
    
    private void CreateNewSlots()
    {
        for (int i = 0; i < maxSlots; i++)
        {
            CreateSlot(i);
        }
    }
    
    private void CreateSlot(int index)
    {
        GameObject slotObject = new GameObject($"Card Slot {index + 1}");
        slotObject.transform.SetParent(slotContainer, false);
        
        var rectTransform = slotObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = slotSize;
        
        var image = slotObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.8f, 0.8f, 0.8f, 0.3f);
        
        CreateSlotNumberText(slotObject, index);
        
        var slotBehaviour = slotObject.AddComponent<CardSlotBehaviour>();
        slotBehaviour.SetSlotIndex(index);
        slotBehaviour.SetEnabled(enableSlotSystem);
        
        _slots.Add(slotBehaviour);
        Debug.Log($"[CardSlotManager] Created slot {index + 1}");
    }
    
    private void CreateSlotNumberText(GameObject slotObject, int index)
    {
        GameObject textObject = new GameObject("Slot Number");
        textObject.transform.SetParent(slotObject.transform, false);
        
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        var textComponent = textObject.AddComponent<TMPro.TextMeshProUGUI>();
        textComponent.text = (index + 1).ToString();
        textComponent.fontSize = 24;
        textComponent.color = Color.white;
        textComponent.alignment = TMPro.TextAlignmentOptions.Center;
        textComponent.raycastTarget = false;
    }
    
    private void ClearExistingSlots()
    {
        foreach (var slot in _slots)
        {
            if (slot != null && slot.gameObject != null)
                DestroyImmediate(slot.gameObject);
        }
        _slots.Clear();
    }
    
    private void SetupEventListeners()
    {
        CardSlotBehaviour.OnCardPlaced += HandleCardPlaced;
        CardSlotBehaviour.OnCardRemoved += HandleCardRemoved;
        CardSlotBehaviour.OnSlotStateChanged += HandleSlotStateChanged;
    }
    
    private void CleanupEventListeners()
    {
        CardSlotBehaviour.OnCardPlaced -= HandleCardPlaced;
        CardSlotBehaviour.OnCardRemoved -= HandleCardRemoved;
        CardSlotBehaviour.OnSlotStateChanged -= HandleSlotStateChanged;
    }
    
    private void HandleCardPlaced(CardSlotBehaviour slot, Card card)
    {
        Debug.Log($"[CardSlotManager] Card {card.GetCardName()} placed in slot {slot.SlotIndex + 1}");
        OnCardPlacedInSlot?.Invoke(slot, card);
        NotifySlotSequenceChanged();
    }
    
    private void HandleCardRemoved(CardSlotBehaviour slot, Card card)
    {
        Debug.Log($"[CardSlotManager] Card {card.GetCardName()} removed from slot {slot.SlotIndex + 1}");
        OnCardRemovedFromSlot?.Invoke(slot, card);
        NotifySlotSequenceChanged();
    }
    
    private void HandleSlotStateChanged(CardSlotBehaviour slot)
    {
        // Optional: Additional state change handling
    }
    
    public bool TryPlaceCardInSlot(Card card, int slotIndex = -1)
    {
        if (!enableSlotSystem || card == null || !card.IsPlayable()) 
        {
            Debug.LogWarning($"[CardSlotManager] Cannot place card: system={enableSlotSystem}, card={card?.GetCardName()}, playable={card?.IsPlayable()}");
            return false;
        }
        
        CardSlotBehaviour targetSlot = null;
        
        if (slotIndex >= 0 && slotIndex < _slots.Count)
        {
            targetSlot = _slots[slotIndex];
            Debug.Log($"[CardSlotManager] Trying specific slot {slotIndex + 1}");
        }
        else
        {
            targetSlot = _slots.FirstOrDefault(s => s != null && s.IsEmpty && s.CanAcceptCard(card));
            if (targetSlot != null)
                Debug.Log($"[CardSlotManager] Found available slot {targetSlot.SlotIndex + 1}");
        }
        
        if (targetSlot == null)
        {
            Debug.LogWarning("[CardSlotManager] No available slot found");
            return false;
        }
        
        bool success = targetSlot.TryPlaceCard(card);
        Debug.Log($"[CardSlotManager] Place card result: {success}");
        return success;
    }
    
    public Card RemoveCardFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count || _slots[slotIndex] == null) 
        {
            Debug.LogWarning($"[CardSlotManager] Invalid slot index: {slotIndex}");
            return null;
        }
        
        return _slots[slotIndex].RemoveCard();
    }
    
    public void ClearAllSlots()
    {
        Debug.Log("[CardSlotManager] Clearing all slots...");
        
        int clearedCount = 0;
        foreach (var slot in _slots)
        {
            if (slot != null && slot.IsFilled)
            {
                slot.RemoveCard();
                clearedCount++;
            }
        }
        
        Debug.Log($"[CardSlotManager] Cleared {clearedCount} slots");
    }
    
    public List<Card> GetSlotSequence()
    {
        return _slots
            .Where(s => s != null && s.IsFilled && s.OccupyingCard != null)
            .OrderBy(s => s.SlotIndex)
            .Select(s => s.OccupyingCard)
            .ToList();
    }
    
    public bool PlayAllSlots()
    {
        var sequence = GetSlotSequence();
        if (sequence.Count == 0) 
        {
            Debug.LogWarning("[CardSlotManager] No cards in slots to play");
            return false;
        }
    
        Debug.Log($"[CardSlotManager] Playing {sequence.Count} cards from slots");
    
        bool success = CoreExtensions.TryWithManagerStatic<SpellcastManager>( sm => 
        {
            sm.ProcessCardPlay(sequence);
        });
    
        if (success)
        {
            ClearAllSlots();
            Debug.Log("[CardSlotManager] Successfully played and cleared slots");
        }
        else
        {
            Debug.LogError("[CardSlotManager] Failed to process card play");
        }
    
        return success;
    }
    
    public string GetSlotLetterSequence()
    {
        var sequence = GetSlotSequence();
        if (sequence.Count == 0) return "";
        
        return sequence.GetLetterSequence();
    }
    
    public bool CanPlaySlotSequence()
    {
        var sequence = GetSlotSequence();
        if (sequence.Count == 0) return false;
    
        bool isPlayerTurn = CoreExtensions.TryWithManagerStatic<CombatManager, bool>(null, cm => cm.IsPlayerTurn);
        bool canPlayCards = SpellcastManager.CheckCanPlayCards(sequence);
    
        Debug.Log($"[CardSlotManager] Can play check: playerTurn={isPlayerTurn}, canPlay={canPlayCards}, cards={sequence.Count}");
    
        return isPlayerTurn && canPlayCards;
    }

    
    private void NotifySlotSequenceChanged()
    {
        var sequence = GetSlotSequence();
        OnSlotSequenceChanged?.Invoke(sequence);
        
        Debug.Log($"[CardSlotManager] Sequence changed: {sequence.Count} cards, letters='{GetSlotLetterSequence()}'");
    }
    
    public void SetEnabled(bool enabled)
    {
        enableSlotSystem = enabled;
        
        foreach (var slot in _slots)
        {
            if (slot != null)
                slot.SetEnabled(enabled);
        }
        
        if (slotContainer != null)
            slotContainer.gameObject.SetActive(enabled);
            
        Debug.Log($"[CardSlotManager] System {(enabled ? "enabled" : "disabled")}");
    }
    
    public void ValidateSystem()
    {
        Debug.Log($"[CardSlotManager] System Validation:");
        Debug.Log($"  Initialized: {_isInitialized}");
        Debug.Log($"  Enabled: {enableSlotSystem}");
        Debug.Log($"  Container: {slotContainer?.name ?? "null"}");
        Debug.Log($"  Total Slots: {_slots.Count}");
        Debug.Log($"  Filled Slots: {FilledSlotCount}");
        Debug.Log($"  Empty Slots: {EmptySlotCount}");
        Debug.Log($"  Current Sequence: '{GetSlotLetterSequence()}'");
        Debug.Log($"  Can Play: {CanPlaySlotSequence()}");
        
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot != null)
                Debug.Log($"    Slot {i + 1}: {slot.GetSlotInfo()}");
            else
                Debug.LogWarning($"    Slot {i + 1}: NULL");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Force Reinitialize")]
    private void ForceReinitialize()
    {
        _isInitialized = false;
        InitializeSlots();
    }
    
    [ContextMenu("Clear All Slots")]
    private void EditorClearAllSlots()
    {
        ClearAllSlots();
    }
    
    [ContextMenu("Validate System")]
    private void EditorValidateSystem()
    {
        ValidateSystem();
    }
    
    [ContextMenu("Test Slot Placement")]
    private void TestSlotPlacement()
    {
        Debug.Log("[CardSlotManager] Testing slot placement system...");
        
        if (!CardManager.HasInstance)
        {
            Debug.LogError("CardManager not available for testing");
            return;
        }
        
        var handCards = CardManager.Instance.GetHandCards();
        if (handCards.Count == 0)
        {
            Debug.LogWarning("No cards in hand to test with");
            return;
        }
        
        var testCard = handCards.First();
        bool placed = TryPlaceCardInSlot(testCard);
        Debug.Log($"Test placement result: {placed}");
    }
#endif
}