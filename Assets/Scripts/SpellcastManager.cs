using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using System.Collections;

public enum ComboState
{
    Empty,          // Keine Combo
    Building,       // Combo wird aufgebaut (gelb)
    Potential,      // Hat Potential für Spells (grün)
    Invalid,        // Keine Spells möglich (rot)
    Completed       // Spell gefunden und ausgeführt (blau)
}

public class SpellcastManager : SingletonBehaviour<SpellcastManager>, IGameManager
{
    [Header("Spell Configuration")]
    [SerializeField] private List<SpellAsset> availableSpells = new List<SpellAsset>();
    [SerializeField] private bool caseSensitive = false;
    [SerializeField] private bool consumeCardsOnCast = false;
    
    private string _currentCombo = "";
    private Dictionary<string, SpellAsset> _spellCache = new Dictionary<string, SpellAsset>();
    
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    // Cached Manager References
    private CardManager _cardManager;
    private DeckManager _deckManager;
    private CombatManager _combatManager;
    
    // Events - ERWEITERT für bessere UI Integration
    public static event Action<List<Card>, string> OnCardsPlayed; // NEU: Für UI Status
    public static event Action<string> OnComboUpdated;
    public static event Action<string, ComboState> OnComboStateChanged; // NEUER Event für Farbkodierung
    public static event Action<SpellAsset, string> OnSpellFound;
    public static event Action<string> OnSpellNotFound;
    public static event Action OnComboCleared;
    public static event Action<SpellAsset, List<Card>> OnSpellCast;
    public static event Action<SpellEffect> OnSpellEffectTriggered;
    public static event Action<List<Card>> OnCardsDiscarded;
    
    // Properties
    public string CurrentCombo => _currentCombo;
    public ComboState CurrentComboState { get; private set; } = ComboState.Empty;
    public string LastPlayedSequence { get; private set; } = "";
    public IReadOnlyList<SpellAsset> AvailableSpells => availableSpells.AsReadOnly();
    
    protected override void OnAwakeInitialize()
    {
        // Cached references für bessere Performance
        _cardManager = CardManager.Instance;
        _deckManager = DeckManager.Instance;
        _combatManager = CombatManager.Instance;
        InitializeSpellCache();
        _isReady = true;
    }
    
    private void OnEnable()
    {
        CardManager.OnSelectionChanged += OnCardSelectionChanged;
    }
    
    private void OnDisable()
    {
        CardManager.OnSelectionChanged -= OnCardSelectionChanged;
    }
    
    private void InitializeSpellCache()
    {
        _spellCache.Clear();
        
        foreach (var spell in availableSpells)
        {
            if (spell?.IsValid == true)
            {
                string key = caseSensitive ? spell.LetterCode : spell.LetterCode.ToUpper();
                if (!_spellCache.ContainsKey(key))
                    _spellCache[key] = spell;
            }
        }
        
        Debug.Log($"[SpellcastManager] Initialized {_spellCache.Count} spells");
    }
    
    public void PlaySelectedCards()
    {
        if (!CardManager.HasInstance) return;
        var selectedCards = CardManager.Instance.SelectedCards;
        if (selectedCards.Count > 0)
        {
            TryPlayCards(selectedCards);
        }
    }

    public void DiscardSelectedCards()
    {
        if (!CanDiscard()) return;
        
        var selectedCards = CardManager.Instance.SelectedCards;
        if (selectedCards.Count == 0) return;
        
        if (_combatManager.SpendCreativity(1))
        {
            foreach (var card in selectedCards.ToList())
            {
                if (card != null)
                {
                    _deckManager.DiscardCard(card.CardData);
                    _cardManager.RemoveCardFromHand(card);
                    _cardManager.DestroyCard(card);
                }
            }
            
            OnCardsDiscarded?.Invoke(selectedCards);
            DrawCard();
            _cardManager.ClearSelection();
        }
    }

    public void DrawCard()
    {
        if (!CanDraw()) return;
        var drawnCard = DeckManager.Instance.DrawCard();
        if (drawnCard != null)
        {
            CardManager.Instance.SpawnCard(drawnCard, null, true);
        }
    }

    public void ClearSelection()
    {
        if (CardManager.HasInstance)
        {
            CardManager.Instance.ClearSelection();
        }
    }

    private bool CanDraw()
    {
        return CardManager.HasInstance && !CardManager.Instance.IsHandFull && 
               DeckManager.HasInstance && !DeckManager.Instance.IsDeckEmpty;
    }
    
    private bool CanDiscard()
    {
        return CardManager.HasInstance && 
               CombatManager.HasInstance && CombatManager.Instance.CanSpendCreativity(1) &&
               CardManager.Instance.SelectedCards.Count > 0;
    }
    
    private void OnCardSelectionChanged(List<Card> selectedCards)
    {
        if (selectedCards?.Count > 0)
        {
            UpdateComboFromSelection(selectedCards);
        }
        else
        {
            UpdateComboDisplay(_currentCombo);
        }
    }
    
    private void UpdateComboFromSelection(List<Card> selectedCards)
    {
        string letterSequence = ExtractLettersFromCards(selectedCards);
        if (string.IsNullOrEmpty(letterSequence)) return;
        
        string normalizedLetters = caseSensitive ? letterSequence : letterSequence.ToUpper();
        UpdateComboDisplay(normalizedLetters);
    }
    
    private void UpdateComboDisplay(string combo)
    {
        FireEvent(OnComboUpdated, combo);
    }
    
    public void TryPlayCards(List<Card> selectedCards)
    {
        if (selectedCards?.Count == 0)
        {
            Debug.LogWarning("[SpellcastManager] No cards selected for play");
            return;
        }
        
        string letterSequence = ExtractLettersFromCards(selectedCards);
        if (string.IsNullOrEmpty(letterSequence))
        {
            Debug.LogWarning("[SpellcastManager] No letters found in selected cards");
            return;
        }
        
        ProcessCardPlay(selectedCards, letterSequence);
    }
    
    // OPTIMIERT: Bessere String-Performance
    private string ExtractLettersFromCards(List<Card> cards)
    {
        if (cards == null || cards.Count == 0) return "";
    
        var letterBuilder = new StringBuilder(cards.Count * 2);
        foreach (var card in cards)
        {
            if (card?.CardData?.letterValues != null)
                letterBuilder.Append(card.CardData.letterValues);
        }
        return letterBuilder.ToString();
    }
    
    // ÜBERARBEITETE ProcessCardPlay Methode mit persistenter Combo-Anzeige
    public void ProcessCardPlay(List<Card> playedCards, string letterSequence)
    {
        string normalizedLetters = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        // Event für UI Handler - sofortiges Feedback
        FireEvent(OnCardsPlayed, playedCards, normalizedLetters);
        
        // Store last played sequence für Anzeige
        LastPlayedSequence = normalizedLetters;

        // Update persistent combo
        string testCombo = _currentCombo + normalizedLetters;
        
        // Try exact match with new combo
        if (TryMatchSpell(testCombo, playedCards))
        {
            // Spell gefunden - zeige kurz als "Completed"
            SetComboState(testCombo, ComboState.Completed);
            
            // Nach kurzer Verzögerung combo clearen
            StartCoroutine(DelayedComboClear(0.5f));
            return;
        }

        // Try partial matches with new combo
        var partialMatches = FindPartialMatches(testCombo);
        if (partialMatches.Count > 0)
        {
            var bestMatch = partialMatches.OrderByDescending(s => s.LetterCode.Length).First();
            
            // Spell gefunden - zeige als "Completed"
            SetComboState(bestMatch.LetterCode, ComboState.Completed);
            ExecuteSpell(bestMatch, playedCards, bestMatch.LetterCode);
            
            // Nach kurzer Verzögerung combo clearen
            StartCoroutine(DelayedComboClear(0.5f));
            return;
        }
        
        // Check if new combo has potential future matches
        if (HasPotentialMatch(testCombo))
        {
            // Store the combo and continue building
            _currentCombo = testCombo;
            SetComboState(_currentCombo, ComboState.Potential);
            Debug.Log($"[SpellcastManager] Building combo: {_currentCombo}");
        }
        else
        {
            // NEUE LOGIK: Combo bleibt sichtbar aber als Invalid markiert
            _currentCombo = testCombo; // Behalte die Combo für Anzeige
            SetComboState(_currentCombo, ComboState.Invalid);
            
            // Fire spell not found event
            FireEvent(OnSpellNotFound, normalizedLetters);
            Debug.Log($"[SpellcastManager] Invalid combo: {_currentCombo}");
            
            // Nach kurzer Zeit zurück zu Empty (aber Combo bleibt sichtbar)
            StartCoroutine(DelayedComboStateReset(2f));
        }
    }
    
    // NEUE Hilfsmethoden für Combo-Zustand
    private void SetComboState(string combo, ComboState state)
    {
        _currentCombo = combo;
        CurrentComboState = state;
        
        // Feuere sowohl alte als auch neue Events für Kompatibilität
        FireEvent(OnComboUpdated, combo);
        FireEvent(OnComboStateChanged, combo, state);
        
        Debug.Log($"[SpellcastManager] Combo state: {combo} -> {state}");
    }
    
    private IEnumerator DelayedComboClear(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClearCombo();
    }
    
    private IEnumerator DelayedComboStateReset(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Nur State zurücksetzen, Combo bleibt für Anzeige
        CurrentComboState = ComboState.Empty;
        FireEvent(OnComboStateChanged, _currentCombo, CurrentComboState);
    }
    
    private bool TryMatchSpell(string letterSequence, List<Card> sourceCards)
    {
        string key = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        if (_spellCache.TryGetValue(key, out SpellAsset spell))
        {
            ExecuteSpell(spell, sourceCards, letterSequence);
            return true;
        }
        
        return false;
    }
    
    private List<SpellAsset> FindPartialMatches(string letterSequence)
    {
        var matches = new List<SpellAsset>();
        string searchSequence = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        foreach (var kvp in _spellCache)
        {
            if (searchSequence.Contains(kvp.Key))
                matches.Add(kvp.Value);
        }
        
        return matches;
    }
    
    private void ExecuteSpell(SpellAsset spell, List<Card> sourceCards, string usedLetters)
    {
        if (spell?.IsValid != true)
        {
            Debug.LogError($"[SpellcastManager] Invalid spell: {spell?.SpellName ?? "null"}");
            return;
        }
        
        Debug.Log($"[SpellcastManager] Casting '{spell.SpellName}' with sequence: {usedLetters}");
        
        // Events in korrekter Reihenfolge für UI
        FireEvent(OnSpellFound, spell, usedLetters);
        FireEvent(OnSpellCast, spell, sourceCards);
        
        ExecuteSpellEffects(spell);
        
        if (_cardManager != null)
        {
            _cardManager.ClearSelection();
        }
    }
    
    private void ExecuteSpellEffects(SpellAsset spell)
    {
        foreach (var effect in spell.Effects)
        {
            if (effect != null)
            {
                TriggerSpellEffect(effect);
            }
        }
    }
    
    public void TriggerSpellEffect(SpellEffect effect)
    {
        FireEvent(OnSpellEffectTriggered, effect);
        
        if (_combatManager == null)
        {
            Debug.LogWarning("[SpellcastManager] CombatManager not available for effect execution");
            return;
        }
        
        switch (effect.effectType)
        {
            case SpellEffectType.Damage:
                ApplyDamageEffect(effect);
                break;
            case SpellEffectType.Heal:
                ApplyHealEffect(effect);
                break;
            case SpellEffectType.Buff:
                ApplyBuffEffect(effect);
                break;
            case SpellEffectType.Debuff:
                ApplyDebuffEffect(effect);
                break;
            default:
                Debug.Log($"[SpellcastManager] Custom effect: {effect.effectName}");
                break;
        }
    }
    
    private void ApplyDamageEffect(SpellEffect effect)
    {
        // Auto-target falls keine Targets gesetzt sind
        if (_combatManager?.CurrentTargets?.Count == 0)
        {
            AutoTargetFirstEnemy();
        }
    
        if (_combatManager?.CurrentTargets?.Count > 0)
        {
            _combatManager.DealDamageToTargets(Mathf.RoundToInt(effect.value));
            Debug.Log($"[SpellcastManager] Applied {effect.value} damage to {_combatManager.CurrentTargets.Count} target(s)");
        }
        else
        {
            Debug.LogWarning("[SpellcastManager] No enemies available for damage spell");
        }
    }

    private void ApplyDebuffEffect(SpellEffect effect)
    {
        if (_combatManager != null)
        {
            foreach (var target in _combatManager.CurrentTargets)
            {
                if (target != null && target.IsAlive)
                {
                    Debug.Log($"[SpellcastManager] Applied debuff {effect.effectName} to {target.EntityName}");
                }
            }
        }
    }
    
    private void ApplyHealEffect(SpellEffect effect)
    {
        _combatManager.ModifyLife(Mathf.RoundToInt(effect.value));
        Debug.Log($"[SpellcastManager] Healed player for {effect.value}");
    }
    
    private void ApplyBuffEffect(SpellEffect effect)
    {
        if (effect.effectName.ToLower().Contains("creativity"))
        {
            _combatManager.ModifyCreativity(Mathf.RoundToInt(effect.value));
        }
        
        Debug.Log($"[SpellcastManager] Applied buff: {effect.effectName}");
    }
    
    // ÜBERARBEITETE ClearCombo Methode
    public void ClearCombo()
    {
        if (!string.IsNullOrEmpty(_currentCombo))
        {
            string oldCombo = _currentCombo;
            _currentCombo = "";
            CurrentComboState = ComboState.Empty;
            
            // Events feuern
            FireEvent(OnComboCleared);
            FireEvent(OnComboUpdated, _currentCombo);
            FireEvent(OnComboStateChanged, _currentCombo, CurrentComboState);
            
            Debug.Log($"[SpellcastManager] Combo cleared from: {oldCombo}");
        }
    }
    
    // NEUE Methode für manuelles Combo-Reset (z.B. durch Button)
    public void ResetComboDisplay()
    {
        ClearCombo();
        LastPlayedSequence = "";
    }
    
    public bool HasSpell(string letterCode)
    {
        string key = caseSensitive ? letterCode : letterCode.ToUpper();
        return _spellCache.ContainsKey(key);
    }
    
    public SpellAsset FindSpell(string letterCode)
    {
        string key = caseSensitive ? letterCode : letterCode.ToUpper();
        return _spellCache.TryGetValue(key, out SpellAsset spell) ? spell : null;
    }
    
    public bool HasPotentialMatch(string sequence)
    {
        if (string.IsNullOrEmpty(sequence)) return false;
        
        string searchSequence = caseSensitive ? sequence : sequence.ToUpper();
        
        foreach (var key in _spellCache.Keys)
        {
            if (key.StartsWith(searchSequence) || searchSequence.Contains(key))
                return true;
        }
        
        return false;
    }
    
    private void AutoTargetFirstEnemy()
    {
        if (EnemyManager.HasInstance && _combatManager != null)
        {
            var firstEnemy = EnemyManager.Instance.AliveEnemies.FirstOrDefault();
            if (firstEnemy != null)
            {
                _combatManager.AddTarget(firstEnemy);
                Debug.Log($"[SpellcastManager] Auto-targeted: {firstEnemy.EntityName}");
            }
        }
    }
    
    // OPTIMIERT: Bessere Event-Performance mit Error Handling
    private void FireEvent<T>(System.Action<T> eventAction, T parameter)
    {
        try
        {
            eventAction?.Invoke(parameter);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpellcastManager] Event error: {e.Message}");
        }
    }
    
    private void FireEvent<T1, T2>(System.Action<T1, T2> eventAction, T1 param1, T2 param2)
    {
        try
        {
            eventAction?.Invoke(param1, param2);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpellcastManager] Event error: {e.Message}");
        }
    }
    
    private void FireEvent(System.Action eventAction)
    {
        try
        {
            eventAction?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpellcastManager] Event error: {e.Message}");
        }
    }
    
    // Public Check Methods for Drop Areas
    public bool CanPlayCards(List<Card> cards = null)
    {
        // Use provided cards or current selection
        var cardsToCheck = cards ?? (_cardManager?.SelectedCards ?? new List<Card>());
        
        if (cardsToCheck.Count == 0)
            return false;
            
        // Check if it's player's turn
        if (_combatManager != null && !_combatManager.IsPlayerTurn)
            return false;
            
        // Check if we're in combat
        if (_combatManager != null && !_combatManager.IsInCombat)
            return false;
            
        return true;
    }
    
    public bool CanDiscardCard(Card card = null)
    {
        // Check if we have a card to discard
        if (card == null && (_cardManager?.SelectedCards?.Count ?? 0) == 0)
            return false;
            
        // Check if it's player's turn
        if (_combatManager != null && !_combatManager.IsPlayerTurn)
            return false;
            
        // Check if we have enough creativity
        if (_combatManager != null && !_combatManager.CanSpendCreativity(1))
            return false;
            
        // Check if deck has cards to draw
        if (_deckManager != null && _deckManager.GetTotalAvailableCards() == 0)
            return false;
            
        return true;
    }
    
    // Static helper methods for easy access
    public static bool CheckCanPlayCards(List<Card> cards = null)
    {
        return HasInstance && Instance.CanPlayCards(cards);
    }
    
    public static bool CheckCanDiscardCard(Card card = null)
    {
        return HasInstance && Instance.CanDiscardCard(card);
    }
    
#if UNITY_EDITOR
    [ContextMenu("Log Available Spells")]
    public void LogAvailableSpells()
    {
        Debug.Log($"[SpellcastManager] Available Spells ({availableSpells.Count}):");
        
        foreach (var spell in availableSpells)
        {
            if (spell != null)
            {
                Debug.Log($"  - {spell.SpellName}: '{spell.LetterCode}' ({spell.Type}, {spell.Effects.Count} effects)");
            }
        }
    }
    
    [ContextMenu("Clear Combo")]
    public void DebugClearCombo()
    {
        ClearCombo();
    }
    
    [ContextMenu("Reset Combo Display")]
    public void DebugResetComboDisplay()
    {
        ResetComboDisplay();
    }
    
    [ContextMenu("Test Spell Cast")]
    public void DebugTestSpellCast()
    {
        if (_cardManager != null)
        {
            var selectedCards = _cardManager.SelectedCards;
            if (selectedCards.Count > 0)
            {
                TryPlayCards(selectedCards);
            }
            else
            {
                Debug.Log("[SpellcastManager] No cards selected for test cast");
            }
        }
    }
#endif
}