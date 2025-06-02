using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class SpellcastManager : SingletonBehaviour<SpellcastManager>
{
    [Header("Spell Configuration")]
    [SerializeField] private List<SpellAsset> availableSpells = new List<SpellAsset>();
    [SerializeField] private bool caseSensitive = false;
    
    private string _currentCombo = "";
    private Dictionary<string, SpellAsset> _spellCache = new Dictionary<string, SpellAsset>();
    
    // Events
    public static event Action<List<Card>, string> OnCardsPlayed;
    public static event Action<string> OnComboUpdated;
    public static event Action<SpellAsset, string> OnSpellFound;
    public static event Action<string> OnSpellNotFound;
    public static event Action OnComboCleared;
    public static event Action<SpellAsset, List<Card>> OnSpellCast;
    public static event Action<SpellEffect> OnSpellEffectTriggered;
    
    // Properties
    public string CurrentCombo => _currentCombo;
    public IReadOnlyList<SpellAsset> AvailableSpells => availableSpells.AsReadOnly();
    
    protected override void OnAwakeInitialize()
    {
        InitializeSpellCache();
    }
    
    private void InitializeSpellCache()
    {
        _spellCache.Clear();
        
        foreach (var spell in availableSpells)
        {
            if (spell != null && spell.IsValid)
            {
                string key = caseSensitive ? spell.LetterCode : spell.LetterCode.ToUpper();
                if (!_spellCache.ContainsKey(key))
                    _spellCache[key] = spell;
            }
        }
        
        Debug.Log($"[SpellcastManager] Initialized {_spellCache.Count} spells.");
    }
    
    public void ProcessCardPlay(List<Card> playedCards, string letterSequence)
    {
        if (playedCards == null || playedCards.Count == 0 || string.IsNullOrEmpty(letterSequence))
        {
            Debug.LogWarning("[SpellcastManager] Invalid cards or letter sequence.");
            return;
        }
        
        OnCardsPlayed?.Invoke(playedCards, letterSequence);
        ProcessLetterSequence(letterSequence, playedCards);
    }
    
    private void ProcessLetterSequence(string newLetters, List<Card> sourceCards)
    {
        string normalizedLetters = caseSensitive ? newLetters : newLetters.ToUpper();
        
        // Check if new letters can extend current combo
        string testCombo = _currentCombo + normalizedLetters;
        
        if (HasPotentialMatches(testCombo))
        {
            // Extend combo
            _currentCombo = testCombo;
            OnComboUpdated?.Invoke(_currentCombo);
            
            // Check for exact matches
            if (TryMatchSpell(_currentCombo, sourceCards))
                return;
        }
        else
        {
            // Current combo + new letters don't work, try just new letters
            if (HasPotentialMatches(normalizedLetters))
            {
                _currentCombo = normalizedLetters;
                OnComboUpdated?.Invoke(_currentCombo);
                
                if (TryMatchSpell(_currentCombo, sourceCards))
                    return;
            }
            else
            {
                // No potential matches at all
                OnSpellNotFound?.Invoke(normalizedLetters);
                ClearCombo();
            }
        }
    }
    
    private bool TryMatchSpell(string letterSequence, List<Card> sourceCards)
    {
        string key = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        // Check for exact match
        if (_spellCache.TryGetValue(key, out SpellAsset exactMatch))
        {
            ExecuteSpell(exactMatch, sourceCards, letterSequence);
            return true;
        }
        
        // Check for partial matches (spell contained in sequence)
        var partialMatches = FindPartialMatches(letterSequence);
        if (partialMatches.Count > 0)
        {
            // Get longest match
            var bestMatch = partialMatches.OrderByDescending(s => s.LetterCode.Length).First();
            string spellCode = caseSensitive ? bestMatch.LetterCode : bestMatch.LetterCode.ToUpper();
            
            ExecuteSpell(bestMatch, sourceCards, spellCode);
            
            // Update combo by removing used part
            int index = letterSequence.IndexOf(spellCode, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                _currentCombo = letterSequence.Remove(index, spellCode.Length);
                OnComboUpdated?.Invoke(_currentCombo);
                
                // Clear combo if no potential matches remain
                if (!HasPotentialMatches(_currentCombo))
                    ClearCombo();
            }
            
            return true;
        }
        
        return false;
    }
    
    private List<SpellAsset> FindPartialMatches(string letterSequence)
    {
        var matches = new List<SpellAsset>();
        string searchSequence = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        foreach (var spell in availableSpells)
        {
            if (spell?.IsValid == true)
            {
                string spellCode = caseSensitive ? spell.LetterCode : spell.LetterCode.ToUpper();
                if (searchSequence.Contains(spellCode))
                    matches.Add(spell);
            }
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
        
        OnSpellFound?.Invoke(spell, usedLetters);
        OnSpellCast?.Invoke(spell, sourceCards);
        
        spell.ExecuteEffects();
    }
    
    private bool HasPotentialMatches(string sequence)
    {
        if (string.IsNullOrEmpty(sequence)) return false;
        
        string searchSequence = caseSensitive ? sequence : sequence.ToUpper();
        
        // Check if any spell starts with this sequence or contains it
        foreach (var key in _spellCache.Keys)
        {
            if (key.StartsWith(searchSequence, StringComparison.OrdinalIgnoreCase) || 
                searchSequence.Contains(key))
                return true;
        }
        
        return false;
    }
    
    public void TriggerSpellEffect(SpellEffect effect)
    {
        OnSpellEffectTriggered?.Invoke(effect);
        
        switch (effect.effectType)
        {
            case SpellEffectType.Damage:
                HandleDamageEffect(effect);
                break;
            case SpellEffectType.Heal:
                HandleHealEffect(effect);
                break;
            case SpellEffectType.Buff:
                HandleBuffEffect(effect);
                break;
            case SpellEffectType.Debuff:
                HandleDebuffEffect(effect);
                break;
            default:
                HandleCustomEffect(effect);
                break;
        }
    }
    
    private void HandleDamageEffect(SpellEffect effect)
    {
        Debug.Log($"[SpellcastManager] Damage: {effect.value}");
    }
    
    private void HandleHealEffect(SpellEffect effect)
    {
        Debug.Log($"[SpellcastManager] Heal: {effect.value}");
    }
    
    private void HandleBuffEffect(SpellEffect effect)
    {
        Debug.Log($"[SpellcastManager] Buff: {effect.effectName}");
    }
    
    private void HandleDebuffEffect(SpellEffect effect)
    {
        Debug.Log($"[SpellcastManager] Debuff: {effect.effectName}");
    }
    
    private void HandleCustomEffect(SpellEffect effect)
    {
        Debug.Log($"[SpellcastManager] Custom: {effect.effectName}");
    }
    
    public void ClearCombo()
    {
        _currentCombo = "";
        OnComboCleared?.Invoke();
        OnComboUpdated?.Invoke(_currentCombo);
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
#endif
}