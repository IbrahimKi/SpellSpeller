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
    [SerializeField] private bool consumeCardsOnCast = false; // CHANGED: Cards are NOT consumed on cast
    
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
        
        // Spend creativity for discard
        if (_combatManager.SpendCreativity(1))
        {
            // Discard cards
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
            
            // Draw new card
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
            ClearCombo();
        }
    }
    
    private void UpdateComboFromSelection(List<Card> selectedCards)
    {
        string letterSequence = ExtractLettersFromCards(selectedCards);
        if (string.IsNullOrEmpty(letterSequence))
        {
            ClearCombo();
            return;
        }
        
        string normalizedLetters = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        if (_currentCombo != normalizedLetters)
        {
            _currentCombo = normalizedLetters;
            FireEvent(OnComboUpdated, _currentCombo);
        }
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
    
        // Try exact match first
        if (TryMatchSpell(normalizedLetters, playedCards))
        {
            return;
        }
    
        // Try partial matches
        var partialMatches = FindPartialMatches(normalizedLetters);
        Debug.Log($"[SpellcastManager] Gefundener Value: {normalizedLetters}");
        if (partialMatches.Count > 0)
        {
            var bestMatch = partialMatches.OrderByDescending(s => s.LetterCode.Length).First();
            ExecuteSpell(bestMatch, playedCards, bestMatch.LetterCode);
            return;
        }
    
        
        FireEvent(OnSpellNotFound, normalizedLetters);
        Debug.Log($"[SpellcastManager] No spell found for: {normalizedLetters}");
        
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
        
        // Clear selection after spell cast (cards remain in hand)
        if (_cardManager != null)
        {
            _cardManager.ClearSelection();
        }
        ClearCombo();
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
        if (_combatManager != null)
        {
            // Use combat manager's targeting system
            _combatManager.DealDamageToTargets(Mathf.RoundToInt(effect.value));
        }
        Debug.Log($"[SpellcastManager] Applied {effect.value} damage to targets");
    }

    private void ApplyDebuffEffect(SpellEffect effect)
    {
        if (_combatManager != null)
        {
            // Apply debuff to current targets
            foreach (var target in _combatManager.CurrentTargets)
            {
                if (target != null && target.IsAlive)
                {
                    Debug.Log($"[SpellcastManager] Applied debuff {effect.effectName} to {target.EntityName}");
                    // TODO: Implement actual debuff system
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
            FireEvent(OnComboUpdated, _currentCombo);
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