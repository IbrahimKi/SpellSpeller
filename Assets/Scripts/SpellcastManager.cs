using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using System.Collections;

public enum ComboState
{
    Empty,          // Keine Combo
    Building,       // Combo wird aufgebaut
    Ready,          // Combo kann gecastet werden
    Invalid         // Keine weiteren Spells möglich
}

public class SpellcastManager : SingletonBehaviour<SpellcastManager>, IGameManager
{
    [Header("Spell Configuration")]
    [SerializeField] private List<SpellAsset> availableSpells = new List<SpellAsset>();
    [SerializeField] private bool caseSensitive = false;
    
    [Header("Combo Settings")]
    [SerializeField] private bool autoCastOnInvalid = true;
    [SerializeField] private float comboClearDelay = 0.5f;
    
    private string _currentCombo = "";
    private Dictionary<string, SpellAsset> _spellCache = new Dictionary<string, SpellAsset>();
    private List<Card> _comboCards = new List<Card>(); // Track cards in combo
    
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    // Events
    public static event Action<List<Card>, string> OnCardsPlayed;
    public static event Action<string, ComboState> OnComboStateChanged;
    public static event Action<SpellAsset, string> OnSpellFound;
    public static event Action<string> OnSpellNotFound;
    public static event Action OnComboCleared;
    public static event Action<SpellAsset, List<Card>> OnSpellCast;
    public static event Action<SpellEffect> OnSpellEffectTriggered;
    
    // Properties
    public string CurrentCombo => _currentCombo;
    public ComboState CurrentComboState { get; private set; } = ComboState.Empty;
    public bool CanCastCombo => CurrentComboState == ComboState.Ready && _comboCards.Count > 0;
    
    protected override void OnAwakeInitialize()
    {
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
    
    private void Update()
    {
        // Leertaste für Combo-Cast
        if (Input.GetKeyDown(KeyCode.Space) && CanCastCombo)
        {
            TryCastCurrentCombo();
        }
    }
    
    private void InitializeSpellCache()
    {
        _spellCache.Clear();
        
        foreach (var spell in availableSpells.Where(s => s?.IsValid == true))
        {
            string key = caseSensitive ? spell.LetterCode : spell.LetterCode.ToUpper();
            if (!_spellCache.ContainsKey(key))
                _spellCache[key] = spell;
        }
        
        Debug.Log($"[SpellcastManager] Initialized {_spellCache.Count} spells");
    }
    
    private void OnCardSelectionChanged(List<Card> selectedCards)
    {
        if (selectedCards?.Count > 0)
        {
            UpdateComboPreview(selectedCards);
        }
    }
    
    private void UpdateComboPreview(List<Card> selectedCards)
    {
        string letterSequence = ExtractLettersFromCards(selectedCards);
        if (string.IsNullOrEmpty(letterSequence)) return;
        
        string preview = _currentCombo + letterSequence;
        
        // Check potential state
        if (HasExactSpell(preview))
        {
            SetComboState(preview, ComboState.Ready);
        }
        else if (HasPotentialMatch(preview))
        {
            SetComboState(preview, ComboState.Building);
        }
        else
        {
            SetComboState(preview, ComboState.Invalid);
        }
    }
    
    public void PlaySelectedCards()
    {
        if (!CardManager.HasInstance) return;
        var selectedCards = CardManager.Instance.SelectedCards;
        if (selectedCards.Count > 0)
        {
            ProcessCardPlay(selectedCards);
        }
    }
    
    public void ProcessCardPlay(List<Card> playedCards)
    {
        if (playedCards == null || playedCards.Count == 0) return;
        
        string letterSequence = ExtractLettersFromCards(playedCards);
        if (string.IsNullOrEmpty(letterSequence)) return;
        
        string normalizedLetters = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        // Add to combo
        _currentCombo += normalizedLetters;
        _comboCards.AddRange(playedCards);
        
        OnCardsPlayed?.Invoke(playedCards, normalizedLetters);
        
        // Check combo state
        bool hasExactMatch = HasExactSpell(_currentCombo);
        bool hasPotential = HasPotentialMatch(_currentCombo);
        
        if (hasExactMatch)
        {
            // Combo is ready to cast
            SetComboState(_currentCombo, ComboState.Ready);
            Debug.Log($"[SpellcastManager] Combo ready: {_currentCombo} - Press SPACE to cast!");
        }
        else if (hasPotential)
        {
            // Can still build combo
            SetComboState(_currentCombo, ComboState.Building);
            Debug.Log($"[SpellcastManager] Building combo: {_currentCombo}");
        }
        else
        {
            // No valid spell possible
            SetComboState(_currentCombo, ComboState.Invalid);
            
            if (autoCastOnInvalid)
            {
                // Try to cast any partial match before clearing
                TryAutoCastPartialMatch();
            }
            else
            {
                OnSpellNotFound?.Invoke(_currentCombo);
                StartCoroutine(DelayedComboClear(comboClearDelay));
            }
        }
    }
    
    // Manual combo cast (via Space or UI button)
    public void TryCastCurrentCombo()
    {
        if (!CanCastCombo) return;
        
        if (_spellCache.TryGetValue(_currentCombo, out SpellAsset spell))
        {
            ExecuteSpell(spell, _comboCards, _currentCombo);
            ClearCombo();
        }
    }
    
    // Auto-cast when combo becomes invalid
    private void TryAutoCastPartialMatch()
    {
        // Check for partial matches in the combo
        var partialMatches = FindAllPartialMatches(_currentCombo);
        
        if (partialMatches.Count > 0)
        {
            // Cast the longest matching spell
            var bestMatch = partialMatches.OrderByDescending(s => s.LetterCode.Length).First();
            
            // Find which cards were used for this spell
            int spellLength = bestMatch.LetterCode.Length;
            var usedCards = GetCardsForSpellLength(spellLength);
            
            ExecuteSpell(bestMatch, usedCards, bestMatch.LetterCode);
            
            // Keep remaining combo
            string remainingCombo = _currentCombo.Substring(spellLength);
            var remainingCards = _comboCards.Skip(usedCards.Count).ToList();
            
            _currentCombo = remainingCombo;
            _comboCards = remainingCards;
            
            // Re-evaluate remaining combo
            if (string.IsNullOrEmpty(_currentCombo))
            {
                ClearCombo();
            }
            else
            {
                ProcessRemainingCombo();
            }
        }
        else
        {
            // No spells found
            OnSpellNotFound?.Invoke(_currentCombo);
            ClearCombo();
        }
    }
    
    private void ProcessRemainingCombo()
    {
        if (HasExactSpell(_currentCombo))
        {
            SetComboState(_currentCombo, ComboState.Ready);
        }
        else if (HasPotentialMatch(_currentCombo))
        {
            SetComboState(_currentCombo, ComboState.Building);
        }
        else
        {
            SetComboState(_currentCombo, ComboState.Invalid);
            if (autoCastOnInvalid)
            {
                TryAutoCastPartialMatch();
            }
        }
    }
    
    private List<Card> GetCardsForSpellLength(int length)
    {
        var result = new List<Card>();
        int currentLength = 0;
        
        foreach (var card in _comboCards)
        {
            if (currentLength >= length) break;
            result.Add(card);
            currentLength += card.CardData.letterValues.Length;
        }
        
        return result;
    }
    
    private void ExecuteSpell(SpellAsset spell, List<Card> sourceCards, string usedLetters)
    {
        Debug.Log($"[SpellcastManager] Casting '{spell.SpellName}' with sequence: {usedLetters}");
        
        OnSpellFound?.Invoke(spell, usedLetters);
        OnSpellCast?.Invoke(spell, sourceCards);
        
        // Execute effects
        foreach (var effect in spell.Effects)
        {
            TriggerSpellEffect(effect);
        }
        
        // Clear selection
        if (CardManager.HasInstance)
        {
            CardManager.Instance.ClearSelection();
        }
    }
    
    public void TriggerSpellEffect(SpellEffect effect)
    {
        OnSpellEffectTriggered?.Invoke(effect);
        
        if (!CombatManager.HasInstance) return;
        
        switch (effect.effectType)
        {
            case SpellEffectType.Damage:
                ApplyDamageEffect(effect);
                break;
            case SpellEffectType.Heal:
                CombatManager.Instance.ModifyLife(Mathf.RoundToInt(effect.value));
                break;
            case SpellEffectType.Buff:
                if (effect.effectName.ToLower().Contains("creativity"))
                    CombatManager.Instance.ModifyCreativity(Mathf.RoundToInt(effect.value));
                break;
        }
    }
    
    private void ApplyDamageEffect(SpellEffect effect)
    {
        var combat = CombatManager.Instance;
        
        // Auto-target if needed
        if (combat.CurrentTargets.Count == 0 && EnemyManager.HasInstance)
        {
            var firstEnemy = EnemyManager.Instance.AliveEnemies.FirstOrDefault();
            if (firstEnemy != null)
            {
                combat.AddTarget(firstEnemy);
            }
        }
        
        if (combat.CurrentTargets.Count > 0)
        {
            combat.DealDamageToTargets(Mathf.RoundToInt(effect.value));
        }
    }
    
    public void ClearCombo()
    {
        _currentCombo = "";
        _comboCards.Clear();
        CurrentComboState = ComboState.Empty;
        
        OnComboCleared?.Invoke();
        OnComboStateChanged?.Invoke(_currentCombo, CurrentComboState);
    }
    
    // Helper methods
    private string ExtractLettersFromCards(List<Card> cards)
    {
        if (cards == null || cards.Count == 0) return "";
        
        var sb = new StringBuilder(cards.Count * 2);
        foreach (var card in cards.Where(c => c?.CardData != null))
        {
            sb.Append(card.CardData.letterValues);
        }
        return sb.ToString();
    }
    
    private void SetComboState(string combo, ComboState state)
    {
        _currentCombo = combo;
        CurrentComboState = state;
        OnComboStateChanged?.Invoke(combo, state);
    }
    
    private bool HasExactSpell(string sequence)
    {
        string key = caseSensitive ? sequence : sequence.ToUpper();
        return _spellCache.ContainsKey(key);
    }
    
    private bool HasPotentialMatch(string sequence)
    {
        if (string.IsNullOrEmpty(sequence)) return false;
        
        string searchSequence = caseSensitive ? sequence : sequence.ToUpper();
        
        // Check if any spell starts with this sequence
        return _spellCache.Keys.Any(key => key.StartsWith(searchSequence));
    }
    
    private List<SpellAsset> FindAllPartialMatches(string sequence)
    {
        var matches = new List<SpellAsset>();
        string searchSequence = caseSensitive ? sequence : sequence.ToUpper();
        
        foreach (var kvp in _spellCache)
        {
            if (searchSequence.Contains(kvp.Key))
                matches.Add(kvp.Value);
        }
        
        return matches;
    }
    
    private IEnumerator DelayedComboClear(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClearCombo();
    }
    
    // UI Support Methods
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
    
    // Drop area support
    public static bool CheckCanPlayCards(List<Card> cards = null)
    {
        if (!HasInstance || !CombatManager.HasInstance) return false;
        
        var cardsToCheck = cards ?? CardManager.Instance?.SelectedCards ?? new List<Card>();
        
        return cardsToCheck.Count > 0 && 
               CombatManager.Instance.IsInCombat && 
               CombatManager.Instance.IsPlayerTurn;
    }
    
    public static bool CheckCanDiscardCard(Card card = null)
    {
        if (!HasInstance || !CombatManager.HasInstance || !DeckManager.HasInstance) return false;
        
        return CombatManager.Instance.IsPlayerTurn && 
               CombatManager.Instance.CanSpendCreativity(1) &&
               DeckManager.Instance.GetTotalAvailableCards() > 0;
    }

#if UNITY_EDITOR
    [ContextMenu("Log Available Spells")]
    public void LogAvailableSpells()
    {
        Debug.Log($"[SpellcastManager] Available Spells ({availableSpells.Count}):");
        foreach (var spell in availableSpells.Where(s => s != null))
        {
            Debug.Log($"  - {spell.SpellName}: '{spell.LetterCode}'");
        }
    }
    
    [ContextMenu("Cast Current Combo")]
    public void DebugCastCombo()
    {
        TryCastCurrentCombo();
    }
#endif
}