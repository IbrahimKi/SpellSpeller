using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GameCore.Enums;
using GameCore.Data;

public class CardSlotManager : SingletonBehaviour<CardSlotManager>, IGameManager
{
    [Header("Slot System Configuration")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private List<CardSlotAsset> slotAssets = new List<CardSlotAsset>();
    [SerializeField] private bool autoCreateSlots = true;
    [SerializeField] private int defaultSlotCount = 5;
    
    [Header("Layout Settings")]
    [SerializeField] private float slotSpacing = 10f;
    [SerializeField] private bool useHorizontalLayout = true;
    
    [Header("Gameplay Settings")]
    [SerializeField] private bool enableSlotSystem = true;
    [SerializeField] private bool clearSlotsOnTurnEnd = false;
    [SerializeField] private bool playAllSlotsSequentially = true;
    
    // Runtime data
    private List<CardSlotBehaviour> _slots = new List<CardSlotBehaviour>();
    private bool _isInitialized = false;
    
    // Events
    public static event System.Action<List<Card>> OnSlotSequenceChanged;
    public static event System.Action<CardSlotBehaviour, Card> OnCardPlacedInAnySlot;
    public static event System.Action<CardSlotBehaviour, Card> OnCardRemovedFromAnySlot;
    public static event System.Action<List<Card>> OnAllSlotsPlayed;
    
    // Properties
    public bool IsEnabled => enableSlotSystem;
    public bool IsReady => _isInitialized;
    public bool IsInitialized => _isInitialized;
    public List<CardSlotBehaviour> Slots => _slots;
    public int SlotCount => _slots.Count;
    public int FilledSlotCount => _slots.Count(s => s.IsFilled);
    public int EmptySlotCount => _slots.Count(s => s.IsEmpty);
    public bool HasEmptySlots => EmptySlotCount > 0;
    public bool AreAllSlotsFilled => FilledSlotCount == SlotCount && SlotCount > 0;
    
    protected override void OnAwakeInitialize()
    {
        InitializeSlotContainer();
    }
    
    private void Start()
    {
        if (autoCreateSlots)
        {
            InitializeSlots();
        }
        
        SetupEventListeners();
    }
    
    private void OnDestroy()
    {
        CleanupEventListeners();
    }
    
    // === INITIALIZATION ===
    
    private void InitializeSlotContainer()
    {
        if (slotContainer == null)
        {
            GameObject container = new GameObject("Slot Container");
            container.transform.SetParent(transform, false);
            slotContainer = container.transform;
            
            // Add layout component
            if (useHorizontalLayout)
            {
                var layoutGroup = container.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                layoutGroup.spacing = slotSpacing;
                layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
            }
        }
    }
    
    public void InitializeSlots()
    {
        if (_isInitialized) return;
        
        ClearExistingSlots();
        
        int slotsToCreate = Mathf.Max(slotAssets.Count, defaultSlotCount);
        
        for (int i = 0; i < slotsToCreate; i++)
        {
            CreateSlot(i);
        }
        
        _isInitialized = true;
        Debug.Log($"[CardSlotManager] Initialized {_slots.Count} slots");
    }
    
    private void CreateSlot(int index)
    {
        if (slotPrefab == null)
        {
            Debug.LogError("[CardSlotManager] No slot prefab assigned!");
            return;
        }
        
        GameObject slotObject = Instantiate(slotPrefab, slotContainer);
        slotObject.name = $"Card Slot {index + 1}";
        
        CardSlotBehaviour slotBehaviour = slotObject.GetComponent<CardSlotBehaviour>();
        if (slotBehaviour == null)
        {
            slotBehaviour = slotObject.AddComponent<CardSlotBehaviour>();
        }
        
        // Configure slot
        slotBehaviour.SetSlotIndex(index);
        
        // Assign asset if available
        if (index < slotAssets.Count && slotAssets[index] != null)
        {
            slotBehaviour.SetSlotAsset(slotAssets[index]);
        }
        
        _slots.Add(slotBehaviour);
        
        Debug.Log($"[CardSlotManager] Created slot {index + 1}");
    }
    
    private void ClearExistingSlots()
    {
        foreach (var slot in _slots)
        {
            if (slot != null && slot.gameObject != null)
            {
                DestroyImmediate(slot.gameObject);
            }
        }
        _slots.Clear();
    }
    
    // === EVENT MANAGEMENT ===
    
    private void SetupEventListeners()
    {
        CardSlotBehaviour.OnCardPlacedInSlot += HandleCardPlacedInSlot;
        CardSlotBehaviour.OnCardRemovedFromSlot += HandleCardRemovedFromSlot;
        CardSlotBehaviour.OnCardPlayedFromSlot += HandleCardPlayedFromSlot;
        
        // Combat events for turn management using Extensions
        this.TryWithManager<CombatManager>(cm => 
        {
            CombatManager.OnPlayerTurnEnded += HandleTurnEnd;
            CombatManager.OnPlayerTurnStarted += HandleTurnStart;
        });
    }
    
    private void CleanupEventListeners()
    {
        CardSlotBehaviour.OnCardPlacedInSlot -= HandleCardPlacedInSlot;
        CardSlotBehaviour.OnCardRemovedFromSlot -= HandleCardRemovedFromSlot;
        CardSlotBehaviour.OnCardPlayedFromSlot -= HandleCardPlayedFromSlot;
        
        // Cleanup combat events safely
        CombatManager.OnPlayerTurnEnded -= HandleTurnEnd;
        CombatManager.OnPlayerTurnStarted -= HandleTurnStart;
    }
    
    // === EVENT HANDLERS ===
    
    private void HandleCardPlacedInSlot(CardSlotBehaviour slot, Card card)
    {
        OnCardPlacedInAnySlot?.Invoke(slot, card);
        NotifySlotSequenceChanged();
        
        Debug.Log($"[CardSlotManager] Card {card.GetCardName()} placed in slot {slot.SlotIndex + 1}");
    }
    
    private void HandleCardRemovedFromSlot(CardSlotBehaviour slot, Card card)
    {
        OnCardRemovedFromAnySlot?.Invoke(slot, card);
        NotifySlotSequenceChanged();
        
        Debug.Log($"[CardSlotManager] Card {card.GetCardName()} removed from slot {slot.SlotIndex + 1}");
    }
    
    private void HandleCardPlayedFromSlot(CardSlotBehaviour slot, Card card)
    {
        Debug.Log($"[CardSlotManager] Card {card.GetCardName()} played from slot {slot.SlotIndex + 1}");
        NotifySlotSequenceChanged();
    }
    
    private void HandleTurnEnd(int turn)
    {
        if (clearSlotsOnTurnEnd)
        {
            ClearAllSlots();
        }
        
        // Process turn-end effects for all slots
        foreach (var slot in _slots)
        {
            slot.OnTurnEnd();
        }
    }
    
    private void HandleTurnStart(int turn)
    {
        // Process turn-start effects for all slots
        foreach (var slot in _slots)
        {
            slot.OnTurnStart();
        }
    }
    
    // === SLOT MANAGEMENT ===
    
    public bool TryPlaceCardInSlot(Card card, int slotIndex = -1)
    {
        if (!enableSlotSystem || card == null) return false;
        
        // Find target slot
        CardSlotBehaviour targetSlot = null;
        
        if (slotIndex >= 0 && slotIndex < _slots.Count)
        {
            targetSlot = _slots[slotIndex];
        }
        else
        {
            // Find first empty slot that can accept the card
            targetSlot = _slots.FirstOrDefault(s => s.IsEmpty && s.CanAcceptCard(card));
        }
        
        if (targetSlot == null) return false;
        
        return targetSlot.TryPlaceCard(card);
    }
    
    public Card RemoveCardFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return null;
        
        return _slots[slotIndex].RemoveCard();
    }
    
    public void ClearAllSlots()
    {
        foreach (var slot in _slots)
        {
            slot.RemoveCard();
        }
        
        Debug.Log("[CardSlotManager] All slots cleared");
    }
    
    public List<Card> GetSlotSequence()
    {
        return _slots
            .Where(s => s.IsFilled)
            .OrderBy(s => s.SlotIndex)
            .Select(s => s.OccupyingCard)
            .ToList();
    }
    
    public List<Card> GetFilledSlots()
    {
        return _slots
            .Where(s => s.IsFilled)
            .Select(s => s.OccupyingCard)
            .ToList();
    }
    
    // === PLAY FUNCTIONALITY ===
    
    public bool PlayAllSlots()
    {
        if (!enableSlotSystem) return false;
        
        var filledSlots = _slots.Where(s => s.IsFilled).OrderBy(s => s.SlotIndex).ToList();
        
        if (filledSlots.Count == 0)
        {
            Debug.LogWarning("[CardSlotManager] No cards in slots to play");
            return false;
        }
        
        if (playAllSlotsSequentially)
        {
            return PlaySlotsSequentially(filledSlots);
        }
        else
        {
            return PlaySlotsSimultaneously(filledSlots);
        }
    }
    
    private bool PlaySlotsSequentially(List<CardSlotBehaviour> slotsToPlay)
    {
        var cardsToPlay = new List<Card>();
        
        foreach (var slot in slotsToPlay)
        {
            if (slot.IsFilled)
            {
                cardsToPlay.Add(slot.OccupyingCard);
            }
        }
        
        if (cardsToPlay.Count == 0) return false;
        
        // Play cards via SpellcastManager using Extensions
        bool success = this.TryWithManager<SpellcastManager>(sm =>
        {
            sm.ProcessCardPlay(cardsToPlay);
        });
        
        if (success)
        {
            // Clear slots after playing (respecting persistence settings)
            foreach (var slot in slotsToPlay)
            {
                if (slot.SlotAsset?.consumeOnPlay != false)
                {
                    slot.RemoveCard(false);
                }
            }
            
            OnAllSlotsPlayed?.Invoke(cardsToPlay);
            this.LogDebug($"Played {cardsToPlay.Count} cards sequentially from slots");
        }
        
        return success;
    }
    
    private bool PlaySlotsSimultaneously(List<CardSlotBehaviour> slotsToPlay)
    {
        int successfulPlays = 0;
        
        foreach (var slot in slotsToPlay)
        {
            if (slot.PlayCard())
            {
                successfulPlays++;
            }
        }
        
        if (successfulPlays > 0)
        {
            var playedCards = slotsToPlay.Where(s => s.IsFilled).Select(s => s.OccupyingCard).ToList();
            OnAllSlotsPlayed?.Invoke(playedCards);
            Debug.Log($"[CardSlotManager] Played {successfulPlays} cards simultaneously");
        }
        
        return successfulPlays > 0;
    }
    
    public bool PlaySpecificSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return false;
        
        return _slots[slotIndex].PlayCard();
    }
    
    // === UTILITY METHODS ===
    
    public string GetSlotLetterSequence()
    {
        return GetSlotSequence().GetLetterSequence();
    }
    
    public bool CanPlaySlotSequence()
    {
        var sequence = GetSlotSequence();
        if (sequence.Count == 0) return false;
        
        // Check using Extensions and SpellcastManager
        return sequence.HasPlayableCards() && 
               this.TryWithManager<SpellcastManager, bool>(sm => 
                   SpellcastManager.CheckCanPlayCards(sequence)
               ) &&
               this.TryWithManager<CombatManager, bool>(cm => 
                   cm.CanPerformPlayerAction(PlayerActionType.PlayCards)
               );
    }
    
    public CardSlotBehaviour GetSlot(int index)
    {
        return index >= 0 && index < _slots.Count ? _slots[index] : null;
    }
    
    public CardSlotBehaviour FindSlotWithCard(Card card)
    {
        return _slots.FirstOrDefault(s => s.OccupyingCard == card);
    }
    
    public List<CardSlotBehaviour> GetSlotsWithEffect(SlotEffectType effectType)
    {
        return _slots.Where(s => s.HasEffectOfType(effectType)).ToList();
    }
    
    private void NotifySlotSequenceChanged()
    {
        OnSlotSequenceChanged?.Invoke(GetSlotSequence());
    }
    
    // === CONFIGURATION ===
    
    public void SetEnabled(bool enabled)
    {
        enableSlotSystem = enabled;
        
        foreach (var slot in _slots)
        {
            slot.SetEnabled(enabled);
        }
        
        if (slotContainer != null)
        {
            slotContainer.gameObject.SetActive(enabled);
        }
        
        Debug.Log($"[CardSlotManager] Slot system {(enabled ? "enabled" : "disabled")}");
    }
    
    public void AddSlotAsset(CardSlotAsset asset)
    {
        if (asset != null && !slotAssets.Contains(asset))
        {
            slotAssets.Add(asset);
        }
    }
    
    public void SetSlotAsset(int slotIndex, CardSlotAsset asset)
    {
        if (slotIndex >= 0 && slotIndex < _slots.Count && asset != null)
        {
            _slots[slotIndex].SetSlotAsset(asset);
        }
    }
    
    // === STATUS AND DEBUGGING ===
    
    public SlotSystemStatus GetStatus()
    {
        return new SlotSystemStatus
        {
            IsEnabled = enableSlotSystem,
            IsInitialized = _isInitialized,
            TotalSlots = SlotCount,
            FilledSlots = FilledSlotCount,
            EmptySlots = EmptySlotCount,
            SlotSequence = GetSlotLetterSequence(),
            CanPlay = CanPlaySlotSequence()
        };
    }
    
    // === MANAGER INTEGRATION WITH EXTENSIONS ===
    
    public void ValidateManagerDependencies()
    {
        var requiredManagers = new System.Type[]
        {
            typeof(SpellcastManager),
            typeof(CombatManager),
            typeof(CardManager)
        };
        
        bool allReady = ManagerExtensions.ValidateManagerChain(requiredManagers);
        
        if (!allReady)
        {
            this.LogError("Not all required managers are ready for CardSlotManager");
        }
        else
        {
            this.LogDebug("All manager dependencies validated successfully");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Initialize Slots")]
    private void EditorInitializeSlots()
    {
        _isInitialized = false;
        InitializeSlots();
    }
    
    [ContextMenu("Clear All Slots")]
    private void EditorClearAllSlots()
    {
        ClearAllSlots();
    }
    
    [ContextMenu("Play All Slots")]
    private void EditorPlayAllSlots()
    {
        PlayAllSlots();
    }
    
    [ContextMenu("Log Slot Status")]
    private void LogSlotStatus()
    {
        var status = GetStatus();
        Debug.Log($"[CardSlotManager] Status:");
        Debug.Log($"  Enabled: {status.IsEnabled}");
        Debug.Log($"  Initialized: {status.IsInitialized}");
        Debug.Log($"  Slots: {status.FilledSlots}/{status.TotalSlots}");
        Debug.Log($"  Sequence: '{status.SlotSequence}'");
        Debug.Log($"  Can Play: {status.CanPlay}");
        
        for (int i = 0; i < _slots.Count; i++)
        {
            Debug.Log($"    Slot {i + 1}: {_slots[i].GetSlotInfo()}");
        }
    }
    
    [ContextMenu("Test Fill Random Slots")]
    private void TestFillRandomSlots()
    {
        CoreExtensions.TryWithManager<CardManager>(this, cm =>
        {
            var handCards = cm.GetHandCards();
            int cardsToPlace = Mathf.Min(handCards.Count, EmptySlotCount, 3);
            
            for (int i = 0; i < cardsToPlace; i++)
            {
                if (handCards.Count > 0)
                {
                    var randomCard = handCards[Random.Range(0, handCards.Count)];
                    if (TryPlaceCardInSlot(randomCard))
                    {
                        handCards.Remove(randomCard);
                    }
                }
            }
        });
    }
#endif
}

// === SUPPORTING DATA CLASSES ===

[System.Serializable]
public class SlotSystemStatus
{
    public bool IsEnabled;
    public bool IsInitialized;
    public int TotalSlots;
    public int FilledSlots;
    public int EmptySlots;
    public string SlotSequence;
    public bool CanPlay;
}