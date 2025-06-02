using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

public class CardPlayHandler : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button drawButton;
    [SerializeField] private TMPro.TextMeshProUGUI statusDisplay;
    [SerializeField] private TMPro.TextMeshProUGUI comboDisplay;
    [SerializeField] private TMPro.TextMeshProUGUI historyDisplay;
    
    [Header("History Settings")]
    [SerializeField] private int maxHistoryEntries = 10;
    
    [Header("Draw Settings")]
    [SerializeField] private List<CardData> drawPool = new List<CardData>();
    [SerializeField] private int cardsPerDraw = 1;
    
    private List<Card> _selectedCards = new List<Card>();
    private string _cachedLetterSequence = "";
    private bool _isDirty = true;
    private bool _isDestroyed = false;
    
    // History tracking
    private Queue<string> _playHistory = new Queue<string>();
    private Queue<string> _spellHistory = new Queue<string>();
    
    private void Awake()
    {
        if (playButton != null)
            playButton.onClick.AddListener(PlaySelectedCards);
        if (clearButton != null)
            clearButton.onClick.AddListener(ClearSelection);
        if (drawButton != null)
            drawButton.onClick.AddListener(DrawCards);
    }
    
    private void OnEnable()
    {
        if (_isDestroyed) return;
        
        CardManager.OnSelectionChanged += OnSelectionChanged;
        CardManager.OnHandUpdated += OnHandUpdated;
        SpellcastManager.OnSpellFound += OnSpellFound;
        SpellcastManager.OnSpellNotFound += OnSpellNotFound;
        SpellcastManager.OnSpellCast += OnSpellCast;
        SpellcastManager.OnSpellEffectTriggered += OnSpellEffectTriggered;
        SpellcastManager.OnComboUpdated += OnComboUpdated;
        
        // NEW: Subscribe to direct card play events
        Card.OnCardPlayTriggered += OnCardPlayTriggered;
    }
    
    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }
    
    private void UnsubscribeFromEvents()
    {
        CardManager.OnSelectionChanged -= OnSelectionChanged;
        CardManager.OnHandUpdated -= OnHandUpdated;
        SpellcastManager.OnSpellFound -= OnSpellFound;
        SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
        SpellcastManager.OnSpellCast -= OnSpellCast;
        SpellcastManager.OnSpellEffectTriggered -= OnSpellEffectTriggered;
        SpellcastManager.OnComboUpdated -= OnComboUpdated;
        Card.OnCardPlayTriggered -= OnCardPlayTriggered;
    }
    
    private void OnDestroy()
    {
        _isDestroyed = true;
        UnsubscribeFromEvents();
        
        if (playButton != null)
            playButton.onClick.RemoveListener(PlaySelectedCards);
        if (clearButton != null)
            clearButton.onClick.RemoveListener(ClearSelection);
        if (drawButton != null)
            drawButton.onClick.RemoveListener(DrawCards);
    }
    
    // NEW: Handle direct card play (double-click, hold, modifier key)
    private void OnCardPlayTriggered(Card card)
    {
        if (_isDestroyed || card == null) return;
        
        var singleCardList = new List<Card> { card };
        PlayCards(singleCardList);
    }
    
    public void DrawCards()
    {
        if (_isDestroyed || CardManager.Instance == null || drawPool.Count == 0) return;
        if (CardManager.Instance.IsHandFull) return;
        
        for (int i = 0; i < cardsPerDraw && !CardManager.Instance.IsHandFull; i++)
        {
            CardData randomCard = drawPool[Random.Range(0, drawPool.Count)];
            CardManager.Instance.SpawnCard(randomCard, null, true);
        }
    }
    
    private void OnHandUpdated(List<Card> handCards)
    {
        if (_isDestroyed || drawButton == null) return;
        drawButton.interactable = !CardManager.Instance.IsHandFull && drawPool.Count > 0;
    }
    
    private void OnSelectionChanged(List<Card> selectedCards)
    {
        if (_isDestroyed) return;
        
        _selectedCards.Clear();
        if (selectedCards != null)
            _selectedCards.AddRange(selectedCards);
        _isDirty = true;
        UpdateUI();
    }
    
    public void PlaySelectedCards()
    {
        if (_isDestroyed || _selectedCards.Count == 0) return;
        PlayCards(_selectedCards);
    }
    
    // REFACTORED: Common play logic for both selected cards and direct play
    private void PlayCards(List<Card> cardsToPlay)
    {
        if (_isDestroyed || cardsToPlay.Count == 0) return;
        
        string letterSequence = ExtractLetterSequence(cardsToPlay);
        if (string.IsNullOrEmpty(letterSequence)) return;
        
        // Process spell first
        if (SpellcastManager.Instance != null)
            SpellcastManager.Instance.ProcessCardPlay(cardsToPlay, letterSequence);
        
        // Add to play history
        AddToPlayHistory(letterSequence);
        
        // Destroy card objects immediately
        foreach (var card in cardsToPlay)
        {
            if (card != null)
            {
                // Remove from hand first
                CardManager.Instance?.RemoveCardFromHand(card);
                
                // Clean up layout references
                HandLayoutManager.Instance?.CleanupCardReference(card);
                
                // Destroy the GameObject
                Destroy(card.gameObject);
            }
        }
        
        // Clear selection if playing selected cards
        if (cardsToPlay == _selectedCards)
        {
            _selectedCards.Clear();
            _cachedLetterSequence = "";
            _isDirty = true;
            UpdateUI();
        }
    }
    
    public void ClearSelection()
    {
        if (_isDestroyed) return;
        
        if (CardManager.Instance != null)
            CardManager.Instance.ClearSelection();
        _selectedCards.Clear();
        _cachedLetterSequence = "";
        _isDirty = true;
        UpdateUI();
    }
    
    private string ExtractLetterSequence(List<Card> cards)
    {
        if (cards == null || cards.Count == 0) return "";
        
        var letterBuilder = new StringBuilder();
        foreach (var card in cards)
        {
            if (card?.CardData?.letterValues != null)
                letterBuilder.Append(card.CardData.letterValues);
        }
        return letterBuilder.ToString();
    }
    
    private void UpdateUI()
    {
        if (_isDestroyed) return;
        
        bool hasCards = _selectedCards.Count > 0;
        
        if (playButton != null)
            playButton.interactable = hasCards;
        
        if (clearButton != null)
            clearButton.interactable = hasCards;
        
        if (drawButton != null)
            drawButton.interactable = !CardManager.Instance.IsHandFull && drawPool.Count > 0;
        
        if (statusDisplay != null)
        {
            if (hasCards)
            {
                if (_isDirty)
                {
                    _cachedLetterSequence = ExtractLetterSequence(_selectedCards);
                    _isDirty = false;
                }
                statusDisplay.text = $"Letters: {_cachedLetterSequence}";
            }
            else
            {
                statusDisplay.text = "Select cards to play\nTip: Double-click, hold, or Ctrl+click to play instantly";
            }
        }
    }
    
    private void OnComboUpdated(string currentCombo)
    {
        if (_isDestroyed || comboDisplay == null) return;
        
        if (string.IsNullOrEmpty(currentCombo))
        {
            comboDisplay.text = "Combo: -";
            comboDisplay.color = Color.gray;
        }
        else
        {
            comboDisplay.text = $"Combo: {currentCombo}";
            comboDisplay.color = Color.yellow;
        }
    }
    
    private void OnSpellFound(SpellAsset spell, string usedLetters)
    {
        if (_isDestroyed || statusDisplay == null) return;
        
        statusDisplay.text = $"Spell: {spell.SpellName}!";
        statusDisplay.color = Color.green;
        
        // Add to spell history
        AddToSpellHistory(spell.SpellName, usedLetters);
        
        CancelInvoke(nameof(ResetStatusDisplay));
        Invoke(nameof(ResetStatusDisplay), 2f);
    }
    
    private void OnSpellCast(SpellAsset spell, List<Card> sourceCards)
    {
        if (_isDestroyed) return;
        
        Debug.Log($"[CardPlayHandler] Spell '{spell.SpellName}' cast with {sourceCards.Count} cards");
    }
    
    private void OnSpellEffectTriggered(SpellEffect effect)
    {
        if (_isDestroyed) return;
        
        Debug.Log($"[CardPlayHandler] Spell effect triggered: {effect.effectName} ({effect.effectType})");
        
        if (statusDisplay != null)
        {
            statusDisplay.text = $"Effect: {effect.effectName}";
            statusDisplay.color = GetEffectColor(effect.effectType);
            
            CancelInvoke(nameof(ResetStatusDisplay));
            Invoke(nameof(ResetStatusDisplay), 1f);
        }
    }
    
    private Color GetEffectColor(SpellEffectType effectType)
    {
        return effectType switch
        {
            SpellEffectType.Damage => Color.red,
            SpellEffectType.Heal => Color.green,
            SpellEffectType.Buff => Color.blue,
            SpellEffectType.Debuff => Color.magenta,
            SpellEffectType.Shield => Color.cyan,
            _ => Color.white
        };
    }
    
    private void OnSpellNotFound(string attemptedLetters)
    {
        if (_isDestroyed || statusDisplay == null) return;
        
        statusDisplay.text = $"No spell found";
        statusDisplay.color = Color.red;
        
        CancelInvoke(nameof(ResetStatusDisplay));
        Invoke(nameof(ResetStatusDisplay), 1.5f);
    }
    
    private void AddToPlayHistory(string letters)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string entry = $"[{timestamp}] Played: {letters}";
        
        _playHistory.Enqueue(entry);
        if (_playHistory.Count > maxHistoryEntries)
            _playHistory.Dequeue();
            
        UpdateHistoryDisplay();
    }
    
    private void AddToSpellHistory(string spellName, string letters)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string entry = $"[{timestamp}] Cast: {spellName} ({letters})";
        
        _spellHistory.Enqueue(entry);
        if (_spellHistory.Count > maxHistoryEntries)
            _spellHistory.Dequeue();
            
        UpdateHistoryDisplay();
    }
    
    private void UpdateHistoryDisplay()
    {
        if (_isDestroyed || historyDisplay == null) return;
        
        var history = new System.Text.StringBuilder();
        
        // Add spell history (most recent first)
        if (_spellHistory.Count > 0)
        {
            history.AppendLine("SPELLS CAST:");
            var spells = _spellHistory.ToArray();
            for (int i = spells.Length - 1; i >= 0; i--)
            {
                history.AppendLine(spells[i]);
            }
            history.AppendLine();
        }
        
        // Add play history (most recent first)
        if (_playHistory.Count > 0)
        {
            history.AppendLine("CARDS PLAYED:");
            var plays = _playHistory.ToArray();
            for (int i = plays.Length - 1; i >= 0; i--)
            {
                history.AppendLine(plays[i]);
            }
        }
        
        historyDisplay.text = history.Length > 0 ? history.ToString().TrimEnd() : "No history yet";
    }
    
    private void ResetStatusDisplay()
    {
        if (_isDestroyed || statusDisplay == null) return;
        
        statusDisplay.color = Color.white;
        UpdateUI();
    }
    
    public bool HasSelectedCards => _selectedCards.Count > 0;
    public int SelectedCardCount => _selectedCards.Count;
    
    // History access
    public void ClearHistory()
    {
        _playHistory.Clear();
        _spellHistory.Clear();
        UpdateHistoryDisplay();
    }
}