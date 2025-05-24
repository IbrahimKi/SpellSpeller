using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Card Database")]
    [SerializeField] private List<CardData> allCardData = new List<CardData>();
    
    [Header("Runtime Management")]
    [SerializeField] private List<Card> activeCards = new List<Card>();
    [SerializeField] private List<Card> selectedCards = new List<Card>();
    
    [Header("Game Settings")]
    [SerializeField] private int maxSelectedCards = 1;
    [SerializeField] private bool allowMultiSelect = false;
    
    // Events für das Spielsystem
    public static event Action<Card> OnCardActivated;
    public static event Action<Card> OnCardDeactivated;
    public static event Action<List<Card>> OnSelectionChanged;
    public static event Action<string, List<Card>> OnLetterCombination; // Letters, Affected Cards
    public static event Action<Card, BonusEffect> OnBonusEffectTriggered;
    
    // Scoring und Combos
    [Header("Scoring System")]
    [SerializeField] private int baseLetterScore = 10;
    [SerializeField] private float comboMultiplier = 1.5f;
    private int currentScore = 0;
    private int currentCombo = 0;
    
    // Performance Caching
    private Dictionary<string, List<Card>> _letterToCardsCache = new Dictionary<string, List<Card>>();
    private Dictionary<CardType, List<Card>> _typeToCardsCache = new Dictionary<CardType, List<Card>>();
    
    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void OnEnable()
    {
        // Subscribe to card events
        Card.OnCardPlayed += HandleCardPlayed;
        Card.OnCardSelected += HandleCardSelected;
        Card.OnCardDeselected += HandleCardDeselected;
        Card.OnCardLetterTriggered += HandleCardLetterTriggered;
        
        // Subscribe to drag events will be handled by individual DragObject instances
    }
    
    private void OnDisable()
    {
        // Unsubscribe from events
        Card.OnCardPlayed -= HandleCardPlayed;
        Card.OnCardSelected -= HandleCardSelected;
        Card.OnCardDeselected -= HandleCardDeselected;
        Card.OnCardLetterTriggered -= HandleCardLetterTriggered;
        
        // Unsubscribe from drag events will be handled by individual DragObject instances
    }
    
    private void Start()
    {
        // Initialize card database
        InitializeCardDatabase();
        
        // Find all existing cards in scene
        RefreshActiveCards();
    }
    
    #region Card Database Management
    
    private void InitializeCardDatabase()
    {
        if (allCardData.Count == 0)
        {
            // Auto-load all CardData assets
            CardData[] foundCards = Resources.LoadAll<CardData>("");
            allCardData.AddRange(foundCards);
            Debug.Log($"[CardManager] Auto-loaded {foundCards.Length} card data assets");
        }
        
        Debug.Log($"[CardManager] Initialized with {allCardData.Count} card types");
    }
    
    public CardData GetCardDataByName(string cardName)
    {
        return allCardData.FirstOrDefault(card => card.cardName.Equals(cardName, StringComparison.OrdinalIgnoreCase));
    }
    
    public List<CardData> GetCardDataByType(CardType cardType)
    {
        return allCardData.Where(card => card.cardType == cardType).ToList();
    }
    
    public List<CardData> GetCardDataWithLetter(string letter)
    {
        return allCardData.Where(card => card.HasLetter(letter[0])).ToList();
    }
    
    #endregion
    
    #region Active Card Management
    
   
    public void RefreshActiveCards()
    {
        activeCards.Clear();
        Card[] foundCards = FindObjectsOfType<Card>();
        activeCards.AddRange(foundCards);
        
        // Rebuild caches
        RebuildCaches();
        
        Debug.Log($"[CardManager] Found {activeCards.Count} active cards in scene");
    }
    
    private void RebuildCaches()
    {
        _letterToCardsCache.Clear();
        _typeToCardsCache.Clear();
        
        foreach (var card in activeCards)
        {
            if (card == null || card.Data == null) continue;
            
            // Cache by letters
            foreach (char letter in card.Data.GetLetters())
            {
                string letterKey = letter.ToString();
                if (!_letterToCardsCache.ContainsKey(letterKey))
                    _letterToCardsCache[letterKey] = new List<Card>();
                
                _letterToCardsCache[letterKey].Add(card);
            }
            
            // Cache by type
            if (!_typeToCardsCache.ContainsKey(card.Data.cardType))
                _typeToCardsCache[card.Data.cardType] = new List<Card>();
            
            _typeToCardsCache[card.Data.cardType].Add(card);
        }
    }
    
    public void RegisterCard(Card card)
    {
        if (card != null && !activeCards.Contains(card))
        {
            activeCards.Add(card);
            RebuildCaches(); // TODO: Optimize to only update relevant caches
            OnCardActivated?.Invoke(card);
        }
    }
    
    public void UnregisterCard(Card card)
    {
        if (activeCards.Contains(card))
        {
            activeCards.Remove(card);
            selectedCards.Remove(card);
            RebuildCaches();
            OnCardDeactivated?.Invoke(card);
            OnSelectionChanged?.Invoke(selectedCards);
        }
    }
    
    #endregion
    
    #region Selection Management
    
    private void HandleCardSelected(Card card)
    {
        if (selectedCards.Contains(card)) return;
        
        // Check selection limits
        if (!allowMultiSelect)
        {
            // Deselect all other cards
            foreach (var selectedCard in selectedCards.ToList())
            {
                selectedCard.DeselectCard();
            }
            selectedCards.Clear();
        }
        else if (selectedCards.Count >= maxSelectedCards)
        {
            // Remove oldest selection
            Card oldestCard = selectedCards[0];
            oldestCard.DeselectCard();
            selectedCards.RemoveAt(0);
        }
        
        selectedCards.Add(card);
        OnSelectionChanged?.Invoke(selectedCards);
        
        Debug.Log($"[CardManager] Card selected: {card.Data.cardName}. Total selected: {selectedCards.Count}");
    }
    
    private void HandleCardDeselected(Card card)
    {
        if (selectedCards.Remove(card))
        {
            OnSelectionChanged?.Invoke(selectedCards);
        }
    }
    
    public void ClearSelection()
    {
        foreach (var card in selectedCards.ToList())
        {
            card.DeselectCard();
        }
        selectedCards.Clear();
        OnSelectionChanged?.Invoke(selectedCards);
    }
    
    public List<Card> GetSelectedCards()
    {
        return new List<Card>(selectedCards);
    }
    
    #endregion
    
    #region Letter System & Combos
    
    private void HandleCardLetterTriggered(Card card, string letter)
    {
        Debug.Log($"[CardManager] Letter '{letter}' triggered by {card.Data.cardName}");
        
        // Find all cards with this letter
        if (_letterToCardsCache.TryGetValue(letter, out List<Card> cardsWithLetter))
        {
            // Filter out the triggering card to avoid self-triggering
            var affectedCards = cardsWithLetter.Where(c => c != card && c != null).ToList();
            
            if (affectedCards.Count > 0)
            {
                OnLetterCombination?.Invoke(letter, affectedCards);
                ProcessLetterCombo(letter, affectedCards);
            }
        }
    }
    
    private void ProcessLetterCombo(string letter, List<Card> affectedCards)
    {
        currentCombo++;
        int comboScore = Mathf.RoundToInt(baseLetterScore * Mathf.Pow(comboMultiplier, currentCombo - 1));
        currentScore += comboScore * affectedCards.Count;
        
        Debug.Log($"[CardManager] Letter combo '{letter}' affects {affectedCards.Count} cards. Combo: {currentCombo}, Score: +{comboScore * affectedCards.Count}");
        
        // Trigger bonus effects on affected cards
        foreach (var card in affectedCards)
        {
            card.TriggerBonusEffects(BonusEffectType.Triggered);
        }
        
        // TODO: Hier können Sie weitere Combo-Effekte hinzufügen:
        // - Visuelle Effekte für Combos
        // - Sound-Effekte
        // - Screen-Shake bei großen Combos
        // - Bonus-Multiplier für aufeinanderfolgende Combos
    }
    
    public void TriggerLetterSequence(string letterSequence)
    {
        foreach (char letter in letterSequence)
        {
            if (_letterToCardsCache.TryGetValue(letter.ToString(), out List<Card> cards))
            {
                foreach (var card in cards)
                {
                    if (card != null)
                    {
                        card.TriggerLetterEvent(letter.ToString());
                    }
                }
            }
        }
    }
    
    public void ResetCombo()
    {
        currentCombo = 0;
        Debug.Log("[CardManager] Combo reset");
    }
    
    #endregion
    
    #region Bonus Effect System
    
    private void HandleCardPlayed(Card card)
    {
        Debug.Log($"[CardManager] Card played: {card.Data.cardName}");
        
        // Trigger OnPlay bonus effects
        foreach (var effect in card.Data.bonusEffects)
        {
            if (effect.effectType == BonusEffectType.OnPlay)
            {
                ProcessBonusEffect(card, effect);
            }
        }
        
        // TODO: Hier können Sie weitere Spiel-Logik hinzufügen:
        // - Karte zu gespielten Karten hinzufügen
        // - Mana/Ressourcen verbrauchen
        // - Gegner-Reaktionen auslösen
        // - Spielfeld-Effekte aktivieren
    }
    
    private void ProcessBonusEffect(Card sourceCard, BonusEffect effect)
    {
        OnBonusEffectTriggered?.Invoke(sourceCard, effect);
        
        switch (effect.effectName.ToLower())
        {
            case "heal":
                // TODO: Implement healing logic
                Debug.Log($"[CardManager] Healing for {effect.effectValue} points");
                break;
                
            case "damage":
                // TODO: Implement damage logic
                Debug.Log($"[CardManager] Dealing {effect.effectValue} damage");
                break;
                
            case "draw":
                // TODO: Implement card draw logic
                Debug.Log($"[CardManager] Drawing {effect.effectValue} cards");
                break;
                
            case "buff":
                // TODO: Implement buff logic
                Debug.Log($"[CardManager] Applying buff with value {effect.effectValue}");
                break;
                
            default:
                Debug.LogWarning($"[CardManager] Unknown bonus effect: {effect.effectName}");
                break;
        }
    }
    
    #endregion
    
    #region Drag System Integration
    
    private void HandleDragStarted(DragObject dragObject)
    {
        Debug.Log($"[CardManager] Drag started on {dragObject.name} with {dragObject.AttachedCards.Count} cards");
        
        // TODO: Hier können Sie Drag-Start-Logik hinzufügen:
        // - Highlight valid drop zones
        // - Show card information
        // - Pause other game elements
    }
    
    private void HandleDragEnded(DragObject dragObject)
    {
        Debug.Log($"[CardManager] Drag ended on {dragObject.name}");
        
        // TODO: Hier können Sie Drag-End-Logik hinzufügen:
        // - Check for valid drop zones
        // - Process card interactions
        // - Update game state
    }
    
    #endregion
    
    #region Query Methods
    
    public List<Card> GetCardsWithLetter(string letter)
    {
        return _letterToCardsCache.TryGetValue(letter, out List<Card> cards) ? 
               new List<Card>(cards) : new List<Card>();
    }
    
    public List<Card> GetCardsOfType(CardType cardType)
    {
        return _typeToCardsCache.TryGetValue(cardType, out List<Card> cards) ? 
               new List<Card>(cards) : new List<Card>();
    }
    
    public List<Card> GetCardsWithBonusEffect(string effectName)
    {
        return activeCards.Where(card => card != null && card.HasBonusEffect(effectName)).ToList();
    }
    
    public int GetTotalCardsOfTier(int tier)
    {
        return activeCards.Count(card => card != null && card.Data != null && card.Data.tier == tier);
    }
    
    public float GetAverageTierLevel()
    {
        var validCards = activeCards.Where(card => card != null && card.Data != null).ToList();
        return validCards.Count > 0 ? (float)validCards.Average(card => card.Data.tier) : 0f;
    }
    
    #endregion
    
    #region Utility Methods
    
    public void LogGameState()
    {
        Debug.Log($"=== CARD MANAGER STATE ===");
        Debug.Log($"Active Cards: {activeCards.Count}");
        Debug.Log($"Selected Cards: {selectedCards.Count}");
        Debug.Log($"Current Score: {currentScore}");
        Debug.Log($"Current Combo: {currentCombo}");
        Debug.Log($"Cards by Type:");
        
        foreach (var kvp in _typeToCardsCache)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value.Count} cards");
        }
        
        Debug.Log($"Cards by Letter:");
        foreach (var kvp in _letterToCardsCache)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value.Count} cards");
        }
    }
    
    [ContextMenu("Refresh Active Cards")]
    private void EditorRefreshActiveCards()
    {
        RefreshActiveCards();
    }
    
    [ContextMenu("Log Game State")]
    private void EditorLogGameState()
    {
        LogGameState();
    }
    
    [ContextMenu("Clear Selection")]
    private void EditorClearSelection()
    {
        ClearSelection();
    }
    
    #endregion
    
    // Properties for external access
    public int CurrentScore => currentScore;
    public int CurrentCombo => currentCombo;
    public int ActiveCardCount => activeCards.Count;
    public int SelectedCardCount => selectedCards.Count;
}