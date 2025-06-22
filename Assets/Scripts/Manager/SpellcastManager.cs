using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using System.Collections;

public enum ComboState
{
    Empty,
    Building,
    Ready,
    Invalid
}

public class SpellcastManager : SingletonBehaviour<SpellcastManager>, IGameManager
{
    [Header("Spell Configuration")]
    [SerializeField] private List<SpellAsset> availableSpells = new List<SpellAsset>();
    [SerializeField] private bool caseSensitive = false;
    
    [Header("Combo Settings")]
    [SerializeField] private float comboClearDelay = 0.5f;
    
    private string _currentCombo = "";
    private Dictionary<string, SpellAsset> _spellCache = new Dictionary<string, SpellAsset>();
    private List<CardData> _comboCardData = new List<CardData>();
    
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
    
    // SIMPLIFIED: ProcessCardPlay ohne komplexe Auto-Cast Logik
    public void ProcessCardPlay(List<Card> playedCards)
    {
        if (playedCards == null || playedCards.Count == 0) return;
        
        string letterSequence = CardManager.GetLetterSequenceFromCards(playedCards);
        if (string.IsNullOrEmpty(letterSequence)) return;
        
        string normalizedLetters = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        // Karten aus Hand entfernen und zur Combo hinzufügen
        foreach (var card in playedCards)
        {
            if (card?.CardData != null)
            {
                CardManager.Instance?.RemoveCardFromHand(card);
                _comboCardData.Add(card.CardData);
                CardManager.Instance?.DestroyCard(card);
            }
        }
        
        _currentCombo += normalizedLetters;
        UpdateComboState();
        
        OnCardsPlayed?.Invoke(playedCards, normalizedLetters);
        Debug.Log($"[SpellcastManager] Added '{normalizedLetters}' to combo. Current: '{_currentCombo}'");
    }
    
    // SIMPLIFIED: Combo State Update ohne Auto-Cast Komplexität
    private void UpdateComboState()
    {
        if (string.IsNullOrEmpty(_currentCombo))
        {
            SetComboState(ComboState.Empty);
            return;
        }
        
        if (HasExactSpell(_currentCombo))
        {
            SetComboState(ComboState.Ready);
            Debug.Log($"[SpellcastManager] Combo ready: {_currentCombo} - Press SPACE to cast!");
            return;
        }
        
        if (HasPotentialMatch(_currentCombo))
        {
            SetComboState(ComboState.Building);
            Debug.Log($"[SpellcastManager] Building combo: {_currentCombo}");
            return;
        }
        
        SetComboState(ComboState.Invalid);
        Debug.Log($"[SpellcastManager] Invalid combo: {_currentCombo}");
        OnSpellNotFound?.Invoke(_currentCombo);
        StartCoroutine(DelayedComboClear(comboClearDelay));
    }
    
    public void TryCastCurrentCombo()
    {
        if (!CanCastCombo) return;
        
        if (_spellCache.TryGetValue(_currentCombo, out SpellAsset spell))
        {
            ExecuteSpell(spell, _comboCardData, _currentCombo);
            ClearCombo();
        }
    }
    
    private void ExecuteSpell(SpellAsset spell, List<CardData> sourceCardData, string usedLetters)
    {
        Debug.Log($"[SpellcastManager] Casting '{spell.SpellName}' with sequence: {usedLetters}");
        
        OnSpellFound?.Invoke(spell, usedLetters);
        OnSpellCast?.Invoke(spell, sourceCardData);
        
        foreach (var effect in spell.Effects)
        {
            TriggerSpellEffect(effect);
        }
        
        if (DeckManager.HasInstance)
        {
            foreach (var cardData in sourceCardData)
            {
                DeckManager.Instance.AddCardToBottom(cardData);
            }
        }
        
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