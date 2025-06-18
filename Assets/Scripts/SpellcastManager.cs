using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;

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
    
    // Events
    public static event Action<List<Card>, string> OnCardsPlayed;
    public static event Action<string> OnComboUpdated;
    public static event Action<SpellAsset, string> OnSpellFound;
    public static event Action<string> OnSpellNotFound;
    public static event Action OnComboCleared;
    public static event Action<SpellAsset, List<Card>> OnSpellCast;
    public static event Action<SpellEffect> OnSpellEffectTriggered;
    public static event Action<List<Card>> OnCardsDiscarded;
    
    // Properties
    public string CurrentCombo => _currentCombo;
    public IReadOnlyList<SpellAsset> AvailableSpells => availableSpells.AsReadOnly();
    
    protected override void OnAwakeInitialize()
    {
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
    
    public void ProcessCardPlay(List<Card> playedCards, string letterSequence)
    {
        string normalizedLetters = caseSensitive ? letterSequence : letterSequence.ToUpper();
        FireEvent(OnCardsPlayed, playedCards, normalizedLetters);
    
        // Update persistent combo
        string testCombo = _currentCombo + normalizedLetters;
        
        // Try exact match with new combo
        if (TryMatchSpell(testCombo, playedCards))
        {
            ClearCombo();
            return;
        }
    
        // Try partial matches with new combo
        var partialMatches = FindPartialMatches(testCombo);
        if (partialMatches.Count > 0)
        {
            var bestMatch = partialMatches.OrderByDescending(s => s.LetterCode.Length).First();
            ExecuteSpell(bestMatch, playedCards, bestMatch.LetterCode);
            ClearCombo();
            return;
        }
        
        // Check if new combo has potential future matches
        if (HasPotentialMatch(testCombo))
        {
            // Store the combo and continue building
            _currentCombo = testCombo;
            UpdateComboDisplay(_currentCombo);
            Debug.Log($"[SpellcastManager] Building combo: {_currentCombo}");
        }
        else
        {
            // No potential - clear combo and start fresh with current letters
            ClearCombo();
            
            // Check if just the current letters have potential
            if (HasPotentialMatch(normalizedLetters))
            {
                _currentCombo = normalizedLetters;
                UpdateComboDisplay(_currentCombo);
                Debug.Log($"[SpellcastManager] Started new combo: {_currentCombo}");
            }
            else
            {
                FireEvent(OnSpellNotFound, normalizedLetters);
                Debug.Log($"[SpellcastManager] No spell found for: {normalizedLetters}");
            }
        }
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
    
    public void ClearCombo()
    {
        if (!string.IsNullOrEmpty(_currentCombo))
        {
            _currentCombo = "";
            FireEvent(OnComboCleared);
            UpdateComboDisplay(_currentCombo);
        }
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
    
    private void FireEvent<T>(System.Action<T> eventAction, T parameter)
    {
        eventAction?.Invoke(parameter);
    }
    
    private void FireEvent<T1, T2>(System.Action<T1, T2> eventAction, T1 param1, T2 param2)
    {
        eventAction?.Invoke(param1, param2);
    }
    
    private void FireEvent(System.Action eventAction)
    {
        eventAction?.Invoke();
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
            
        // Could add more checks here (e.g. mana cost, special conditions)
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