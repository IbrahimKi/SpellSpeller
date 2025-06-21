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
    private List<CardData> _comboCardData = new List<CardData>(); // Nur CardData speichern
    
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    // Events
    public static event Action<List<Card>, string> OnCardsPlayed;
    public static event Action<string, ComboState> OnComboStateChanged;
    public static event Action<SpellAsset, string> OnSpellFound;
    public static event Action<string> OnSpellNotFound;
    public static event Action OnComboCleared;
    public static event Action<SpellAsset, List<CardData>> OnSpellCast;
    public static event Action<SpellEffect> OnSpellEffectTriggered;
    
    // Properties
    public string CurrentCombo => _currentCombo;
    public ComboState CurrentComboState { get; private set; } = ComboState.Empty;
    public bool CanCastCombo => CurrentComboState == ComboState.Ready && _comboCardData.Count > 0;
    
    protected override void OnAwakeInitialize()
    {
        InitializeSpellCache();
        _isReady = true;
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
    
    // HAUPTMETHODE: Karten spielen (von UI oder Drag&Drop)
    public void ProcessCardPlay(List<Card> playedCards)
    {
        if (playedCards == null || playedCards.Count == 0) return;
        
        string letterSequence = ExtractLettersFromCards(playedCards);
        if (string.IsNullOrEmpty(letterSequence)) return;
        
        // Normalisiere Buchstaben
        string normalizedLetters = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        // Karten sofort aus der Hand entfernen und zur Combo hinzufügen
        foreach (var card in playedCards)
        {
            if (card?.CardData != null)
            {
                CardManager.Instance?.RemoveCardFromHand(card);
                _comboCardData.Add(card.CardData);
                CardManager.Instance?.DestroyCard(card);
            }
        }
        
        // Combo erweitern
        _currentCombo += normalizedLetters;
        
        // Combo-Status prüfen
        UpdateComboState();
        
        OnCardsPlayed?.Invoke(playedCards, normalizedLetters);
        
        Debug.Log($"[SpellcastManager] Added '{normalizedLetters}' to combo. Current: '{_currentCombo}'");
    }
    
    private void UpdateComboState()
    {
        if (string.IsNullOrEmpty(_currentCombo))
        {
            SetComboState(ComboState.Empty);
            return;
        }
        
        // Prüfe auf exakte Übereinstimmung
        if (HasExactSpell(_currentCombo))
        {
            SetComboState(ComboState.Ready);
            Debug.Log($"[SpellcastManager] Combo ready: {_currentCombo} - Press SPACE to cast!");
            return;
        }
        
        // Prüfe auf potentielle Übereinstimmung
        if (HasPotentialMatch(_currentCombo))
        {
            SetComboState(ComboState.Building);
            Debug.Log($"[SpellcastManager] Building combo: {_currentCombo}");
            return;
        }
        
        // Keine Übereinstimmung möglich
        SetComboState(ComboState.Invalid);
        Debug.Log($"[SpellcastManager] Invalid combo: {_currentCombo}");
        
        if (autoCastOnInvalid)
        {
            TryAutoCastBestMatch();
        }
        else
        {
            OnSpellNotFound?.Invoke(_currentCombo);
            StartCoroutine(DelayedComboClear(comboClearDelay));
        }
    }
    
    // Manual combo cast (via Space or UI button)
    public void TryCastCurrentCombo()
    {
        if (!CanCastCombo) return;
        
        if (_spellCache.TryGetValue(_currentCombo, out SpellAsset spell))
        {
            ExecuteSpell(spell, _comboCardData, _currentCombo);
            ClearCombo();
        }
    }
    
    // Auto-cast bei invalider Combo - finde besten Match
    private void TryAutoCastBestMatch()
    {
        string bestMatch = "";
        List<CardData> bestMatchCards = new List<CardData>();
        
        // Suche längsten gültigen Spell von links nach rechts
        for (int length = _currentCombo.Length; length > 0; length--)
        {
            string testCombo = _currentCombo.Substring(0, length);
            if (HasExactSpell(testCombo))
            {
                bestMatch = testCombo;
                bestMatchCards = GetCardDataForLength(length);
                break;
            }
        }
        
        if (!string.IsNullOrEmpty(bestMatch) && _spellCache.TryGetValue(bestMatch, out SpellAsset spell))
        {
            // Cast besten Match
            ExecuteSpell(spell, bestMatchCards, bestMatch);
            
            // Entferne verwendete Buchstaben und Karten
            string remainingCombo = _currentCombo.Substring(bestMatch.Length);
            var remainingCards = _comboCardData.Skip(bestMatchCards.Count).ToList();
            
            // Setze Combo mit verbleibenden Elementen fort
            _currentCombo = remainingCombo;
            _comboCardData = remainingCards;
            
            if (!string.IsNullOrEmpty(_currentCombo))
            {
                UpdateComboState(); // Prüfe verbleibende Combo
            }
            else
            {
                ClearCombo();
            }
        }
        else
        {
            // Kein gültiger Spell gefunden
            OnSpellNotFound?.Invoke(_currentCombo);
            StartCoroutine(DelayedComboClear(comboClearDelay));
        }
    }
    
    private List<CardData> GetCardDataForLength(int targetLength)
    {
        var result = new List<CardData>();
        int currentLength = 0;
        
        foreach (var cardData in _comboCardData)
        {
            if (currentLength >= targetLength) break;
            result.Add(cardData);
            currentLength += cardData.letterValues.Length;
        }
        
        return result;
    }
    
    private void ExecuteSpell(SpellAsset spell, List<CardData> sourceCardData, string usedLetters)
    {
        Debug.Log($"[SpellcastManager] Casting '{spell.SpellName}' with sequence: {usedLetters}");
        
        OnSpellFound?.Invoke(spell, usedLetters);
        OnSpellCast?.Invoke(spell, sourceCardData);
        
        // Execute spell effects
        foreach (var effect in spell.Effects)
        {
            TriggerSpellEffect(effect);
        }
        
        // Karten unter das Deck legen
        if (DeckManager.HasInstance)
        {
            foreach (var cardData in sourceCardData)
            {
                DeckManager.Instance.AddCardToBottom(cardData);
            }
        }
        
        // Clear selection falls noch vorhanden
        CardManager.Instance?.ClearSelection();
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
        _comboCardData.Clear();
        SetComboState(ComboState.Empty);
        
        OnComboCleared?.Invoke();
        Debug.Log("[SpellcastManager] Combo cleared");
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
    
    private void SetComboState(ComboState state)
    {
        CurrentComboState = state;
        OnComboStateChanged?.Invoke(_currentCombo, state);
    }
    
    private bool HasExactSpell(string sequence)
    {
        if (string.IsNullOrEmpty(sequence)) return false;
        string key = caseSensitive ? sequence : sequence.ToUpper();
        return _spellCache.ContainsKey(key);
    }
    
    private bool HasPotentialMatch(string sequence)
    {
        if (string.IsNullOrEmpty(sequence)) return false;
        
        string searchSequence = caseSensitive ? sequence : sequence.ToUpper();
        return _spellCache.Keys.Any(key => key.StartsWith(searchSequence));
    }
    
    private IEnumerator DelayedComboClear(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClearCombo();
    }
    
    // UI Support Methods
    public void PlaySelectedCards()
    {
        if (!CardManager.HasInstance) return;
        var selectedCards = CardManager.Instance.SelectedCards;
        if (selectedCards?.Count > 0)
        {
            ProcessCardPlay(selectedCards);
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
        CardManager.Instance?.ClearSelection();
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
    
    [ContextMenu("Clear Combo")]
    public void DebugClearCombo()
    {
        ClearCombo();
    }
#endif
}