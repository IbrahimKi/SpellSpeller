using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class SpellcastManager : SingletonBehaviour<SpellcastManager>
{
    [Header("Spell System")]
    [SerializeField] private List<SpellData> availableSpells = new List<SpellData>();
    [SerializeField] private bool allowPartialMatches = false;
    [SerializeField] private bool caseSensitive = false;
    [SerializeField] private int maxHistoryLength = 10;
    
    private Dictionary<string, SpellData> _spellCache = new Dictionary<string, SpellData>();
    private List<string> _letterHistory = new List<string>();
    private string _currentSequence = "";
    private bool _cacheDirty = true;
    
    public static event Action<List<Card>, string> OnCardsPlayed;
    public static event Action<string> OnLetterSequenceUpdated;
    public static event Action<string, string> OnSpellFound;
    public static event Action<string> OnSpellNotFound;
    public static event Action OnLetterSequenceCleared;
    public static event Action<SpellData, List<Card>> OnSpellCast;
    
    protected override void OnAwakeInitialize()
    {
        // Initialization code that was previously in Start()
        RebuildSpellCache();
    }
    
    public void ProcessCardPlay(List<Card> playedCards, string letterSequence)
    {
        if (playedCards == null || playedCards.Count == 0 || string.IsNullOrEmpty(letterSequence))
            return;
        
        OnCardsPlayed?.Invoke(playedCards, letterSequence);
        ProcessLetterSequence(letterSequence, playedCards);
    }
    
    private void ProcessLetterSequence(string newLetters, List<Card> sourceCards)
    {
        string normalizedLetters = caseSensitive ? newLetters : newLetters.ToUpper();
        _currentSequence += normalizedLetters;
        
        UpdateHistory(normalizedLetters);
        OnLetterSequenceUpdated?.Invoke(_currentSequence);
        
        bool spellFound = TryMatchSpell(_currentSequence, sourceCards);
        
        if (!spellFound && ShouldClearCache(_currentSequence))
        {
            OnSpellNotFound?.Invoke(_currentSequence);
            ClearLetterSequence();
        }
    }
    
    private void UpdateHistory(string letters)
    {
        _letterHistory.Add(letters);
        while (_letterHistory.Count > maxHistoryLength)
            _letterHistory.RemoveAt(0);
    }
    
    private bool TryMatchSpell(string letterSequence, List<Card> sourceCards)
    {
        if (_cacheDirty)
            RebuildSpellCache();
        
        if (_spellCache.TryGetValue(letterSequence, out SpellData exactMatch))
        {
            ExecuteSpell(exactMatch, sourceCards, letterSequence);
            return true;
        }
        
        if (allowPartialMatches)
        {
            var partialMatches = FindPartialMatches(letterSequence);
            if (partialMatches.Count > 0)
            {
                var bestMatch = partialMatches.OrderByDescending(s => s.letterSequence.Length).First();
                ExecuteSpell(bestMatch, sourceCards, bestMatch.letterSequence);
                _currentSequence = _currentSequence.Substring(bestMatch.letterSequence.Length);
                return true;
            }
        }
        
        return false;
    }
    
    private List<SpellData> FindPartialMatches(string letterSequence)
    {
        return _spellCache.Values.Where(spell => letterSequence.Contains(spell.letterSequence)).ToList();
    }
    
    private void ExecuteSpell(SpellData spell, List<Card> sourceCards, string usedLetters)
    {
        OnSpellFound?.Invoke(spell.spellName, usedLetters);
        OnSpellCast?.Invoke(spell, sourceCards);
        ClearLetterSequence();
    }
    
    private bool ShouldClearCache(string currentSequence)
    {
        return !allowPartialMatches || !HasPotentialMatches(currentSequence);
    }
    
    private bool HasPotentialMatches(string sequence)
    {
        if (_cacheDirty)
            RebuildSpellCache();
        
        return _spellCache.Values.Any(spell => 
            spell.letterSequence.StartsWith(sequence, StringComparison.OrdinalIgnoreCase));
    }
    
    private void RebuildSpellCache()
    {
        _spellCache.Clear();
        
        foreach (var spell in availableSpells)
        {
            if (spell != null && !string.IsNullOrEmpty(spell.letterSequence))
            {
                string key = caseSensitive ? spell.letterSequence : spell.letterSequence.ToUpper();
                if (!_spellCache.ContainsKey(key))
                    _spellCache[key] = spell;
            }
        }
        
        _cacheDirty = false;
    }
    
    public string GetCurrentLetterSequence() => _currentSequence;
    
    public void ClearLetterSequence()
    {
        _currentSequence = "";
        OnLetterSequenceCleared?.Invoke();
    }
    
    public void UpdateSpellDatabase(List<SpellData> newSpells)
    {
        availableSpells.Clear();
        availableSpells.AddRange(newSpells);
        _cacheDirty = true;
        RebuildSpellCache();
    }
    
    public bool HasSpell(string letterSequence)
    {
        if (_cacheDirty)
            RebuildSpellCache();
        
        string key = caseSensitive ? letterSequence : letterSequence.ToUpper();
        return _spellCache.ContainsKey(key);
    }
    
    public List<SpellData> GetAvailableSpells()
    {
        return new List<SpellData>(availableSpells);
    }
}

[System.Serializable]
public class SpellData
{
    public string spellName = "Fireball";
    public string letterSequence = "FIRE";
    public string effectType = "damage";
    public int effectValue = 5;
    public string description = "Deals fire damage";
}