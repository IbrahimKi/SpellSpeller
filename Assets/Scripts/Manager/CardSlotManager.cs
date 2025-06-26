using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GameCore.Enums;
using GameCore.Data;

public class CardSlotManager : SingletonBehaviour<CardSlotManager>, IGameManager
{
    [Header("Slot Configuration")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private int maxSlots = 5;
    [SerializeField] private bool enableSlotSystem = true;
    
    [Header("Layout")]
    [SerializeField] private float slotSpacing = 10f;
    [SerializeField] private Vector2 slotSize = new Vector2(120f, 180f);
    
    // Runtime data
    private List<CardSlotBehaviour> _slots = new List<CardSlotBehaviour>();
    private bool _isInitialized = false;
    
    // Events
    public static event System.Action<List<Card>> OnSlotSequenceChanged;
    public static event System.Action<CardSlotBehaviour, Card> OnCardPlacedInSlot;
    public static event System.Action<CardSlotBehaviour, Card> OnCardRemovedFromSlot;
    
    // Properties
    public bool IsEnabled => enableSlotSystem;
    public bool IsReady => _isInitialized;
    public List<CardSlotBehaviour> Slots => _slots;
    public int SlotCount => _slots.Count;
    public int FilledSlotCount => _slots.Count(s => s.IsFilled);
    public int EmptySlotCount => _slots.Count(s => s.IsEmpty);
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
    
    // === INITIALIZATION ===
    
    private void SetupSlotContainer()
    {
        if (slotContainer == null)
        {
            // Suche nach existierendem Container
            var existingContainer = transform.Find("CardSlotContainer") ?? 
                                  FindObjectOfType<Transform>().Find("CardSlotContainer");
            
            if (existingContainer != null)
            {
                slotContainer = existingContainer;
                Debug.Log("[CardSlotManager] Found existing slot container");
                return;
            }
            
            // Erstelle neuen Container
            GameObject container = new GameObject("CardSlotContainer");
            container.transform.SetParent(transform, false);
            slotContainer = container.transform;
            
            // Layout hinzufügen
            var layoutGroup = container.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layoutGroup.spacing = slotSpacing;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            
            Debug.Log("[CardSlotManager] Created new slot container");
        }
    }
    
    private void InitializeSlots()
    {
        if (_isInitialized) return;
        
        // Bereinige existierende Slots falls vorhanden
        ClearExistingSlots();
        
        // Überprüfe ob bereits Slots im Container existieren
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
        
        _isInitialized = true;
        Debug.Log($"[CardSlotManager] Initialized with {_slots.Count} slots");
    }
    
    private void RegisterExistingSlots(CardSlotBehaviour[] existingSlots)
    {
        for (int i = 0; i < existingSlots.Length && i < maxSlots; i++)
        {
            var slot = existingSlots[i];
            slot.SetSlotIndex(i);
            _slots.Add(slot);
        }
        
        // Entferne überschüssige Slots
        for (int i = maxSlots; i < existingSlots.Length; i++)
        {
            DestroyImmediate(existingSlots[i].gameObject);
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
        
        // RectTransform Setup
        var rectTransform = slotObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = slotSize;
        
        // Image Component
        var image = slotObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.8f, 0.8f, 0.8f, 0.3f);
        
        // Slot Behaviour
        var slotBehaviour = slotObject.AddComponent<CardSlotBehaviour>();
        slotBehaviour.SetSlotIndex(index);
        
        _slots.Add(slotBehaviour);
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
    
    // === EVENT MANAGEMENT ===
    
    private void SetupEventListeners()
    {
        CardSlotBehaviour.OnCardPlaced += HandleCardPlaced;
        CardSlotBehaviour.OnCardRemoved += HandleCardRemoved;
    }
    
    private void CleanupEventListeners()
    {
        CardSlotBehaviour.OnCardPlaced -= HandleCardPlaced;
        CardSlotBehaviour.OnCardRemoved -= HandleCardRemoved;
    }
    
    private void HandleCardPlaced(CardSlotBehaviour slot, Card card)
    {
        OnCardPlacedInSlot?.Invoke(slot, card);
        NotifySlotSequenceChanged();
    }
    
    private void HandleCardRemoved(CardSlotBehaviour slot, Card card)
    {
        OnCardRemovedFromSlot?.Invoke(slot, card);
        NotifySlotSequenceChanged();
    }
    
    // === SLOT MANAGEMENT ===
    
    public bool TryPlaceCardInSlot(Card card, int slotIndex = -1)
    {
        if (!enableSlotSystem || card == null) return false;
        
        CardSlotBehaviour targetSlot = null;
        
        if (slotIndex >= 0 && slotIndex < _slots.Count)
        {
            targetSlot = _slots[slotIndex];
        }
        else
        {
            targetSlot = _slots.FirstOrDefault(s => s.IsEmpty && s.CanAcceptCard(card));
        }
        
        return targetSlot?.TryPlaceCard(card) ?? false;
    }
    
    public Card RemoveCardFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return null;
        return _slots[slotIndex].RemoveCard();
    }
    
    public void ClearAllSlots()
    {
        foreach (var slot in _slots)
            slot.RemoveCard();
    }
    
    public List<Card> GetSlotSequence()
    {
        return _slots
            .Where(s => s.IsFilled)
            .OrderBy(s => s.SlotIndex)
            .Select(s => s.OccupyingCard)
            .ToList();
    }
    
    public bool PlayAllSlots()
    {
        var sequence = GetSlotSequence();
        if (sequence.Count == 0) return false;
        
        return CoreExtensions.TryWithManager<SpellcastManager>(this, sm => 
        {
            sm.ProcessCardPlay(sequence);
            ClearAllSlots();
        });
    }
    
    // === UTILITY ===
    
    public string GetSlotLetterSequence()
    {
        return GetSlotSequence().GetLetterSequence();
    }
    
    public bool CanPlaySlotSequence()
    {
        var sequence = GetSlotSequence();
        return sequence.Count > 0 && SpellcastManager.CheckCanPlayCards(sequence);
    }
    
    private void NotifySlotSequenceChanged()
    {
        OnSlotSequenceChanged?.Invoke(GetSlotSequence());
    }
    
    public void SetEnabled(bool enabled)
    {
        enableSlotSystem = enabled;
        
        foreach (var slot in _slots)
            slot.SetEnabled(enabled);
        
        if (slotContainer != null)
            slotContainer.gameObject.SetActive(enabled);
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
    
    [ContextMenu("Log Slot Status")]
    private void LogSlotStatus()
    {
        Debug.Log($"[CardSlotManager] Status: {FilledSlotCount}/{SlotCount} slots filled");
        Debug.Log($"  Sequence: '{GetSlotLetterSequence()}'");
        Debug.Log($"  Can Play: {CanPlaySlotSequence()}");
        
        for (int i = 0; i < _slots.Count; i++)
            Debug.Log($"    Slot {i + 1}: {_slots[i].GetSlotInfo()}");
    }
#endif
}