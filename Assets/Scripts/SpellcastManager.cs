using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Hauptmanager für Letter Tracking und Spell System
/// </summary>
public class SpellcastManager : MonoBehaviour
{
    public static SpellcastManager Instance { get; private set; }
    
    [Header("Letter Tracking")]
    [SerializeField] private List<string> letterSequenceHistory = new List<string>();
    [SerializeField] private int maxHistoryLength = 10;
    [SerializeField] private bool showDebugInfo = true;
    
    [Header("Spell System")]
    [SerializeField] private List<SpellData> availableSpells = new List<SpellData>(); // Wird später durch SpellDatabase ersetzt
    [SerializeField] private bool allowPartialMatches = false;
    [SerializeField] private bool caseSensitive = false;
    
    // Performance Caching
    private Dictionary<string, SpellData> _spellCache = new Dictionary<string, SpellData>();
    private string _currentLetterSequence = "";
    private bool _spellCacheDirty = true;
    
    // Events für andere Systeme
    public static event Action<List<Card>, string> OnCardsPlayed; // Cards, Letter Sequence
    public static event Action<string> OnLetterSequenceUpdated; // Current Letter Sequence
    public static event Action<string, string> OnSpellFound; // Spell Name, Used Letters
    public static event Action<string> OnSpellNotFound; // Attempted Letters
    public static event Action OnLetterSequenceCleared;
    
    // Spell-spezifische Events (erweiterbar für andere Systeme)
    public static event Action<SpellData, List<Card>> OnSpellCast; // Spell Data, Source Cards
    public static event Action<string, int> OnResourceEvent; // Resource Type, Amount
    public static event Action<Card, string> OnSpecialCardEffect; // Card, Effect Type
    
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
    
    private void Start()
    {
        // Initialize spell cache
        RebuildSpellCache();
        
        if (showDebugInfo)
        {
            Debug.Log($"[GameManager] Initialized with {availableSpells.Count} available spells");
        }
    }
    
    #region Public API
    
    /// <summary>
    /// Hauptmethode: Verarbeitet gespielten Karten und deren Letter Values
    /// </summary>
    public void ProcessCardPlay(List<Card> playedCards, string letterSequence)
    {
        if (playedCards == null || playedCards.Count == 0 || string.IsNullOrEmpty(letterSequence))
        {
            if (showDebugInfo)
                Debug.LogWarning("[GameManager] Invalid card play - no cards or letters provided");
            return;
        }
        
        // Event für andere Systeme
        OnCardsPlayed?.Invoke(playedCards, letterSequence);
        
        // Letter Sequence verarbeiten
        ProcessLetterSequence(letterSequence, playedCards);
        
        if (showDebugInfo)
        {
            Debug.Log($"[GameManager] Processed {playedCards.Count} cards with letters: {letterSequence}");
        }
    }
    
    /// <summary>
    /// Aktuelle Letter Sequence abrufen
    /// </summary>
    public string GetCurrentLetterSequence() => _currentLetterSequence;
    
    /// <summary>
    /// Letter Sequence Cache manuell löschen
    /// </summary>
    public void ClearLetterSequence()
    {
        _currentLetterSequence = "";
        OnLetterSequenceCleared?.Invoke();
        
        if (showDebugInfo)
        {
            Debug.Log("[GameManager] Letter sequence cleared");
        }
    }
    
    /// <summary>
    /// Neue Spells zur Laufzeit hinzufügen (für SpellDatabase Integration)
    /// </summary>
    public void UpdateSpellDatabase(List<SpellData> newSpells)
    {
        availableSpells.Clear();
        availableSpells.AddRange(newSpells);
        _spellCacheDirty = true;
        RebuildSpellCache();
        
        if (showDebugInfo)
        {
            Debug.Log($"[GameManager] Spell database updated with {newSpells.Count} spells");
        }
    }
    
    #endregion
    
    #region Letter Sequence Processing
    
    private void ProcessLetterSequence(string newLetters, List<Card> sourceCards)
    {
        // Normalisierung der Eingabe
        string normalizedLetters = caseSensitive ? newLetters : newLetters.ToUpper();
        
        // Neue Letters zur aktuellen Sequence hinzufügen
        _currentLetterSequence += normalizedLetters;
        
        // History aktualisieren
        UpdateLetterHistory(normalizedLetters);
        
        // Event für Letter Sequence Update
        OnLetterSequenceUpdated?.Invoke(_currentLetterSequence);
        
        // Spell-Matching versuchen
        bool spellFound = TryMatchSpell(_currentLetterSequence, sourceCards);
        
        if (!spellFound)
        {
            // Kein Spell gefunden - prüfen ob wir weitermachen oder Cache löschen sollen
            if (ShouldClearCache(_currentLetterSequence))
            {
                OnSpellNotFound?.Invoke(_currentLetterSequence);
                ClearLetterSequence();
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[GameManager] Letter sequence now: '{_currentLetterSequence}' (Spell found: {spellFound})");
        }
    }
    
    private void UpdateLetterHistory(string letters)
    {
        letterSequenceHistory.Add(letters);
        
        // History-Länge begrenzen für Performance
        while (letterSequenceHistory.Count > maxHistoryLength)
        {
            letterSequenceHistory.RemoveAt(0);
        }
    }
    
    private bool ShouldClearCache(string currentSequence)
    {
        // Cache löschen wenn:
        // 1. Keine möglichen Matches mehr existieren
        // 2. Sequence zu lang wird
        // 3. Kein Spell gefunden und keine Partial Matches erlaubt
        
        if (!allowPartialMatches)
            return true;
        
        // Prüfen ob noch mögliche Matches existieren
        return !HasPotentialMatches(currentSequence);
    }
    
    private bool HasPotentialMatches(string sequence)
    {
        if (_spellCacheDirty)
            RebuildSpellCache();
        
        foreach (var spell in _spellCache.Values)
        {
            if (spell.letterSequence.StartsWith(sequence, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
    
    #endregion
    
    #region Spell Matching System
    
    private bool TryMatchSpell(string letterSequence, List<Card> sourceCards)
    {
        if (_spellCacheDirty)
            RebuildSpellCache();
        
        // Exakte Matches zuerst
        if (_spellCache.TryGetValue(letterSequence, out SpellData exactMatch))
        {
            ExecuteSpell(exactMatch, sourceCards, letterSequence);
            return true;
        }
        
        // Partial Matches wenn erlaubt
        if (allowPartialMatches)
        {
            var partialMatches = FindPartialMatches(letterSequence);
            if (partialMatches.Count > 0)
            {
                // Besten Match auswählen (längster Match)
                var bestMatch = partialMatches.OrderByDescending(s => s.letterSequence.Length).First();
                ExecuteSpell(bestMatch, sourceCards, bestMatch.letterSequence);
                
                // Verwendete Letters von aktueller Sequence entfernen
                _currentLetterSequence = _currentLetterSequence.Substring(bestMatch.letterSequence.Length);
                return true;
            }
        }
        
        return false;
    }
    
    private List<SpellData> FindPartialMatches(string letterSequence)
    {
        var matches = new List<SpellData>();
        
        foreach (var spell in _spellCache.Values)
        {
            if (letterSequence.Contains(spell.letterSequence))
            {
                matches.Add(spell);
            }
        }
        
        return matches;
    }
    
    private void ExecuteSpell(SpellData spell, List<Card> sourceCards, string usedLetters)
    {
        // Events auslösen
        OnSpellFound?.Invoke(spell.spellName, usedLetters);
        OnSpellCast?.Invoke(spell, sourceCards);
        
        // Spell-spezifische Effekte verarbeiten
        ProcessSpellEffects(spell, sourceCards);
        
        // Letter Sequence nach erfolgreichem Spell zurücksetzen
        ClearLetterSequence();
        
        if (showDebugInfo)
        {
            Debug.Log($"[GameManager] Spell executed: {spell.spellName} using letters: {usedLetters}");
        }
    }
    
    private void ProcessSpellEffects(SpellData spell, List<Card> sourceCards)
    {
        // TODO: Hier werden später die verschiedenen Spell-Effekte implementiert
        // Für jetzt nur Events für andere Systeme auslösen
        
        switch (spell.effectType.ToLower())
        {
            case "damage":
                TriggerResourceEvent("damage", spell.effectValue);
                break;
                
            case "heal":
                TriggerResourceEvent("health", spell.effectValue);
                break;
                
            case "mana":
                TriggerResourceEvent("mana", spell.effectValue);
                break;
                
            case "special":
                // Spezialeffekte auf Karten anwenden
                foreach (var card in sourceCards)
                {
                    TriggerSpecialCardEffect(card, spell.effectType);
                }
                break;
                
            default:
                if (showDebugInfo)
                {
                    Debug.Log($"[GameManager] Unknown spell effect type: {spell.effectType}");
                }
                break;
        }
    }
    
    #endregion
    
    #region Event Triggers für andere Systeme
    
    private void TriggerResourceEvent(string resourceType, int amount)
    {
        OnResourceEvent?.Invoke(resourceType, amount);
        
        if (showDebugInfo)
        {
            Debug.Log($"[GameManager] Resource event: {resourceType} {amount:+#;-#;0}");
        }
    }
    
    private void TriggerSpecialCardEffect(Card card, string effectType)
    {
        if (card != null)
        {
            OnSpecialCardEffect?.Invoke(card, effectType);
            
            if (showDebugInfo)
            {
                Debug.Log($"[GameManager] Special effect '{effectType}' triggered on card: {card.Data?.cardName}");
            }
        }
    }
    
    #endregion
    
    #region Spell Cache Management
    
    private void RebuildSpellCache()
    {
        _spellCache.Clear();
        
        foreach (var spell in availableSpells)
        {
            if (spell != null && !string.IsNullOrEmpty(spell.letterSequence))
            {
                string key = caseSensitive ? spell.letterSequence : spell.letterSequence.ToUpper();
                
                if (!_spellCache.ContainsKey(key))
                {
                    _spellCache[key] = spell;
                }
                else if (showDebugInfo)
                {
                    Debug.LogWarning($"[GameManager] Duplicate spell letter sequence: {key}");
                }
            }
        }
        
        _spellCacheDirty = false;
        
        if (showDebugInfo)
        {
            Debug.Log($"[GameManager] Spell cache rebuilt with {_spellCache.Count} spells");
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Debug-Informationen über aktuellen Zustand ausgeben
    /// </summary>
    [ContextMenu("Log Game State")]
    public void LogGameState()
    {
        Debug.Log("=== GAME MANAGER STATE ===");
        Debug.Log($"Current Letter Sequence: '{_currentLetterSequence}'");
        Debug.Log($"Available Spells: {availableSpells.Count}");
        Debug.Log($"Cached Spells: {_spellCache.Count}");
        Debug.Log($"Letter History: [{string.Join(", ", letterSequenceHistory)}]");
        
        if (_spellCache.Count > 0)
        {
            Debug.Log("Available Spell Sequences:");
            foreach (var kvp in _spellCache)
            {
                Debug.Log($"  '{kvp.Key}' -> {kvp.Value.spellName}");
            }
        }
    }
    
    /// <summary>
    /// Prüfen ob ein bestimmter Spell verfügbar ist
    /// </summary>
    public bool HasSpell(string letterSequence)
    {
        if (_spellCacheDirty)
            RebuildSpellCache();
        
        string key = caseSensitive ? letterSequence : letterSequence.ToUpper();
        return _spellCache.ContainsKey(key);
    }
    
    /// <summary>
    /// Alle verfügbaren Spells abrufen
    /// </summary>
    public List<SpellData> GetAvailableSpells()
    {
        return new List<SpellData>(availableSpells);
    }
    
    #endregion
    
    #region Editor Helpers
    
    #if UNITY_EDITOR
    [ContextMenu("Clear Letter Sequence")]
    private void EditorClearLetterSequence()
    {
        ClearLetterSequence();
    }
    
    [ContextMenu("Rebuild Spell Cache")]
    private void EditorRebuildSpellCache()
    {
        RebuildSpellCache();
    }
    
    [ContextMenu("Test Spell 'FIRE'")]
    private void EditorTestSpellFire()
    {
        ProcessLetterSequence("FIRE", new List<Card>());
    }
    #endif
    
    #endregion
}

/// <summary>
/// Temporäre SpellData Klasse - wird später durch ScriptableObject ersetzt
/// </summary>
[System.Serializable]
public class SpellData
{
    public string spellName = "New Spell";
    public string letterSequence = "";
    public string effectType = "damage";
    public int effectValue = 1;
    [TextArea(2, 3)]
    public string description = "Spell description...";
    
    // TODO: Später erweitern für komplexere Spell-Systeme:
    // public Sprite spellIcon;
    // public AudioClip spellSound;
    // public GameObject spellEffect;
    // public float cooldown;
    // public int manaCost;
}