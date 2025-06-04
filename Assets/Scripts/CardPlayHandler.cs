using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class CardPlayHandler : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button drawButton;
    
    [Header("Settings")]
    [SerializeField] private int maxHistoryEntries = 10;
    [SerializeField] private int cardsPerDraw = 1;
    [SerializeField] private bool autoUpdateLayout = true;
    [SerializeField] private int initialHandSize = 5;
    [SerializeField] private float managerTimeout = 5f;
    
    private List<Card> _selectedCards = new List<Card>();
    
    // History tracking
    private Queue<string> _playHistory = new Queue<string>();
    private Queue<string> _spellHistory = new Queue<string>();
    
    // Manager state tracking
    private bool _managersInitialized = false;
    
    // Events
    public static event System.Action<List<Card>> OnCardsPlayed;
    public static event System.Action<int> OnCardsDrawn;
    public static event System.Action<Queue<string>, Queue<string>> OnHistoryUpdated;
    public static event System.Action OnManagersReady;
    
    // Properties for safe manager access
    private CardManager CardManagerInstance => CardManager.HasInstance ? CardManager.Instance : null;
    private DeckManager DeckManagerInstance => DeckManager.HasInstance ? DeckManager.Instance : null;
    private HandLayoutManager HandLayoutManagerInstance => HandLayoutManager.HasInstance ? HandLayoutManager.Instance : null;
    private SpellcastManager SpellcastManagerInstance => SpellcastManager.HasInstance ? SpellcastManager.Instance : null;
    
    private void Awake()
    {
        SetupButtons();
    }
    
    private void Start()
    {
        StartCoroutine(WaitForManagerInitialization());
    }
    
    private void SetupButtons()
    {
        playButton?.onClick.AddListener(PlaySelectedCards);
        clearButton?.onClick.AddListener(ClearSelection);
        drawButton?.onClick.AddListener(DrawCardsFromDeck);
        
        // Initially disable buttons until managers are ready
        SetButtonsInteractable(false);
    }
    
    private IEnumerator WaitForManagerInitialization()
    {
        yield return new WaitForSeconds(0.1f);
        
        float elapsed = 0f;
        while (elapsed < managerTimeout)
        {
            // Prüfe zuerst SimpleGameManager falls vorhanden
            if (SimpleGameManager.AllManagersReady)
            {
                OnManagersInitialized();
                yield break;
            }
            
            // Fallback: Direkter Manager-Check
            if (CheckManagersReady())
            {
                OnManagersInitialized();
                yield break;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        Debug.LogError("[CardPlayHandler] Managers not ready after timeout! Forcing activation.");
        _managersInitialized = true;
        SetButtonsInteractable(true);
    }

    private bool CheckManagersReady()
    {
        bool cardManagerReady = CardManager.HasInstance && CardManager.Instance.IsInitialized;
        bool deckManagerReady = DeckManager.HasInstance && DeckManager.Instance.IsInitialized;
        bool combatManagerReady = CombatManager.HasInstance; // CombatManager ist immer ready
        
        if (!cardManagerReady)
            Debug.LogWarning("[CardPlayHandler] CardManager not ready");
        if (!deckManagerReady)
            Debug.LogWarning("[CardPlayHandler] DeckManager not ready");
        if (!combatManagerReady)
            Debug.LogWarning("[CardPlayHandler] CombatManager not ready");
        
        return cardManagerReady && deckManagerReady && combatManagerReady;
    }
    
    private void OnManagersInitialized()
    {
        Debug.Log("[CardPlayHandler] All managers ready, enabling functionality");
        
        _managersInitialized = true;
        SetButtonsInteractable(true);
        SubscribeToEvents();
        OnManagersReady?.Invoke();
    }
    
    private void SetButtonsInteractable(bool interactable)
    {
        if (playButton) playButton.interactable = interactable && HasSelectedCards;
        if (clearButton) clearButton.interactable = interactable && HasSelectedCards;
        if (drawButton) drawButton.interactable = interactable && CanDraw();
    }
    
    private void SubscribeToEvents()
    {
        if (CardManagerInstance != null)
        {
            CardManager.OnSelectionChanged += OnSelectionChanged;
            CardManager.OnHandUpdated += OnHandUpdated;
        }
        
        if (SpellcastManagerInstance != null)
        {
            SpellcastManager.OnSpellFound += OnSpellFound;
        }
        
        Card.OnCardPlayTriggered += OnCardPlayTriggered;
        
        if (CombatManager.HasInstance)
        {
            CombatManager.OnCombatStarted += OnCombatStarted;
        }
    }
    
    private void OnDestroy()
    {
        playButton?.onClick.RemoveAllListeners();
        clearButton?.onClick.RemoveAllListeners();
        drawButton?.onClick.RemoveAllListeners();
        
        UnsubscribeFromEvents();
    }
    
    private void UnsubscribeFromEvents()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnSelectionChanged -= OnSelectionChanged;
            CardManager.OnHandUpdated -= OnHandUpdated;
        }
        
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnSpellFound -= OnSpellFound;
        }
        
        Card.OnCardPlayTriggered -= OnCardPlayTriggered;
        
        if (CombatManager.HasInstance)
        {
            CombatManager.OnCombatStarted -= OnCombatStarted;
        }
    }
    
    // Event Handlers
    private void OnCombatStarted() => ClearSelection();
    
    private void OnCardPlayTriggered(Card card)
    {
        if (card != null && _managersInitialized) 
            PlayCards(new List<Card> { card });
    }
    
    private void OnSelectionChanged(List<Card> selectedCards)
    {
        _selectedCards.Clear();
        if (selectedCards != null) _selectedCards.AddRange(selectedCards);
        
        // Update button states
        if (playButton) playButton.interactable = _managersInitialized && HasSelectedCards;
        if (clearButton) clearButton.interactable = _managersInitialized && HasSelectedCards;
    }
    
    private void OnHandUpdated(List<Card> handCards) 
    {
        if (autoUpdateLayout && _managersInitialized) 
            HandLayoutManagerInstance?.UpdateLayout();
            
        // Update draw button state
        if (drawButton) drawButton.interactable = _managersInitialized && CanDraw();
    }
    
    // Core Actions
    public void PlaySelectedCards()
    {
        if (!CanPerformAction() || _selectedCards.Count == 0) return;
        PlayCards(_selectedCards);
    }
    
    private void PlayCards(List<Card> cardsToPlay)
    {
        if (!CanPerformAction() || cardsToPlay.Count == 0) return;
    
        string letterSequence = CardManager.GetLetterSequenceFromCards(cardsToPlay);
        if (string.IsNullOrEmpty(letterSequence)) return;
    
        OnCardsPlayed?.Invoke(cardsToPlay);
        SpellcastManagerInstance?.ProcessCardPlay(cardsToPlay, letterSequence);
        AddToPlayHistory(letterSequence);
    
        // Remove cards efficiently
        var cardsToRemove = new List<Card>(cardsToPlay);
        foreach (var card in cardsToRemove)
        {
            if (card?.CardData != null)
            {
                DeckManagerInstance?.DiscardCard(card.CardData);
                CardManagerInstance?.RemoveCardFromHand(card);
                HandLayoutManagerInstance?.CleanupCardReference(card);
                
                if (card.gameObject != null) 
                {
                    CardManagerInstance?.DestroyCard(card);
                }
            }
        }
    
        if (cardsToPlay == _selectedCards) _selectedCards.Clear();
        if (autoUpdateLayout) HandLayoutManagerInstance?.UpdateLayout();
    }
    
    public void DrawCardsFromDeck()
    {
        if (!CanPerformAction() || !CanDraw()) 
        {
            Debug.LogWarning("[CardPlayHandler] Cannot draw cards - conditions not met");
            return;
        }
        
        StartCoroutine(DrawCardsCoroutine());
    }
    
    private IEnumerator DrawCardsCoroutine()
    {
        int cardsDrawn = 0;
        for (int i = 0; i < cardsPerDraw && CanDraw(); i++)
        {
            var drawnCard = DeckManagerInstance.DrawCard();
            if (drawnCard != null)
            {
                CardManagerInstance.SpawnCard(drawnCard, null, true);
                cardsDrawn++;
                yield return null; // Spread drawing over frames for better performance
            }
            else 
            {
                Debug.LogWarning($"[CardPlayHandler] Failed to draw card {i+1}/{cardsPerDraw}");
                break;
            }
        }
        
        if (cardsDrawn > 0)
        {
            OnCardsDrawn?.Invoke(cardsDrawn);
            if (autoUpdateLayout) HandLayoutManagerInstance?.UpdateLayout();
            Debug.Log($"[CardPlayHandler] Drew {cardsDrawn} cards");
        }
    }
    
    public void ClearSelection()
    {
        if (!CanPerformAction()) return;
        
        CardManagerInstance?.ClearSelection();
        _selectedCards.Clear();
    }
    
    // Spell Event Handlers
    private void OnSpellFound(SpellAsset spell, string usedLetters)
    {
        AddToSpellHistory(spell.SpellName, usedLetters);
    }
    
    // History Management
    private void AddToPlayHistory(string letters)
    {
        var entry = $"[{System.DateTime.Now:HH:mm:ss}] Played: {letters}";
        _playHistory.Enqueue(entry);
        if (_playHistory.Count > maxHistoryEntries) _playHistory.Dequeue();
        OnHistoryUpdated?.Invoke(_playHistory, _spellHistory);
    }
    
    private void AddToSpellHistory(string spellName, string letters)
    {
        var entry = $"[{System.DateTime.Now:HH:mm:ss}] Cast: {spellName} ({letters})";
        _spellHistory.Enqueue(entry);
        if (_spellHistory.Count > maxHistoryEntries) _spellHistory.Dequeue();
        OnHistoryUpdated?.Invoke(_playHistory, _spellHistory);
    }
    
    private bool CanPerformAction()
    {
        return _managersInitialized && CardManagerInstance != null && DeckManagerInstance != null;
    }
    
    private bool CanDraw()
    {
        return CanPerformAction() && 
               !CardManagerInstance.IsHandFull && 
               !DeckManagerInstance.IsDeckEmpty;
    }
    
    // Properties
    public bool HasSelectedCards => _selectedCards.Count > 0;
    public int SelectedCardCount => _selectedCards.Count;
    public IReadOnlyCollection<string> PlayHistory => _playHistory;
    public IReadOnlyCollection<string> SpellHistory => _spellHistory;
    public bool ManagersInitialized => _managersInitialized;
    
    // Public Methods
    public void ClearHistory()
    {
        _playHistory.Clear();
        _spellHistory.Clear();
        OnHistoryUpdated?.Invoke(_playHistory, _spellHistory);
    }
    
    public void ForceDrawCards(int count)
    {
        if (!CanPerformAction()) return;
        
        int originalCardsPerDraw = cardsPerDraw;
        cardsPerDraw = count;
        DrawCardsFromDeck();
        cardsPerDraw = originalCardsPerDraw;
    }
    
    // Debug Methods
    [ContextMenu("Force Manager Check")]
    public void DebugCheckManagers()
    {
        Debug.Log($"[CardPlayHandler] Manager Status:");
        Debug.Log($"  CardManager: {(CardManager.HasInstance ? "Available" : "Missing")} " +
                  $"{(CardManager.HasInstance && CardManager.Instance.IsInitialized ? "(Initialized)" : "(Not Initialized)")}");
        Debug.Log($"  DeckManager: {(DeckManager.HasInstance ? "Available" : "Missing")} " +
                  $"{(DeckManager.HasInstance && DeckManager.Instance.IsInitialized ? "(Initialized)" : "(Not Initialized)")}");
        Debug.Log($"  CombatManager: {(CombatManager.HasInstance ? "Available" : "Missing")} " +
                  $"{(CombatManager.HasInstance && CombatManager.Instance.ManagersReady ? "(Ready)" : "(Not Ready)")}");
        Debug.Log($"  Managers Ready: {_managersInitialized}");
        Debug.Log($"  Can Draw: {CanDraw()}");
    }
}