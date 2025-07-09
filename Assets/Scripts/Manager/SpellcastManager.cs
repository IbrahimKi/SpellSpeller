using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class SpellcastManager : SingletonBehaviour<SpellcastManager>, IGameManager
{
    [Header("Spells")]
    [SerializeField] private List<SpellAsset> availableSpells = new List<SpellAsset>();
    [SerializeField] private bool caseSensitive = false;
    
    private string _currentCombo = "";
    private Dictionary<string, SpellAsset> _spellCache = new Dictionary<string, SpellAsset>();
    private List<CardData> _comboCardData = new List<CardData>();
    
    public bool IsReady { get; private set; }
    
    // Events
    public static event Action<string, ComboState> OnComboStateChanged;
    public static event Action<SpellAsset, List<CardData>> OnSpellCast;
    public static event Action<SpellAsset, int> OnSpellDamageDealt;
    public static event Action OnComboCleared;
    
    // Properties
    public string CurrentCombo => _currentCombo;
    public ComboState CurrentComboState { get; private set; } = ComboState.Empty;
    
    protected override void OnAwakeInitialize()
    {
        // Build spell cache
        foreach (var spell in availableSpells.Where(s => s && s.IsValid))
        {
            string key = caseSensitive ? spell.LetterCode : spell.LetterCode.ToUpper();
            _spellCache[key] = spell;
        }
        IsReady = true;
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && CurrentComboState == ComboState.Ready)
            CastCurrentCombo();
    }
    
    public void ProcessCardPlay(List<Card> cards)
    {
        if (cards == null || cards.Count == 0) return;
        
        // Get letters and destroy cards
        string letters = cards.GetLetterSequence();
        if (string.IsNullOrEmpty(letters)) return;
        
        var cardManager = GameExtensions.GetManager<CardManager>();
        foreach (var card in cards.Where(c => c.IsPlayable()))
        {
            _comboCardData.Add(card.CardData);
            cardManager?.RemoveCardFromHand(card);
            cardManager?.DestroyCard(card);
        }
        
        // Update combo
        _currentCombo += caseSensitive ? letters : letters.ToUpper();
        UpdateComboState();
    }
    
    void UpdateComboState()
    {
        if (string.IsNullOrEmpty(_currentCombo))
        {
            CurrentComboState = ComboState.Empty;
        }
        else if (_spellCache.ContainsKey(_currentCombo))
        {
            CurrentComboState = ComboState.Ready;
        }
        else if (_spellCache.Keys.Any(k => k.StartsWith(_currentCombo)))
        {
            CurrentComboState = ComboState.Building;
        }
        else
        {
            CurrentComboState = ComboState.Invalid;
            Invoke(nameof(ClearCombo), 0.5f);
        }
        
        OnComboStateChanged?.Invoke(_currentCombo, CurrentComboState);
    }
    
    void CastCurrentCombo()
    {
        if (CurrentComboState != ComboState.Ready) return;
        
        if (_spellCache.TryGetValue(_currentCombo, out SpellAsset spell))
        {
            ExecuteSpell(spell);
            ClearCombo();
        }
    }
    
    void ExecuteSpell(SpellAsset spell)
    {
        OnSpellCast?.Invoke(spell, _comboCardData);
        
        int totalDamage = 0;
        
        // Simple effect execution
        foreach (var effect in spell.Effects)
        {
            switch (effect.effectType)
            {
                case SpellEffectType.Damage:
                    int damage = (int)effect.value;
                    GameExtensions.TryManager<EnemyManager>(em => 
                    {
                        var target = em.AliveEnemies.GetWeakest();
                        if (target != null)
                        {
                            target.DamageTarget(damage);
                            totalDamage += damage;
                        }
                    });
                    break;
                    
                case SpellEffectType.Heal:
                    GameExtensions.TryManager<CombatManager>(cm => 
                        cm.ModifyLife((int)effect.value));
                    break;
            }
        }
        
        if (totalDamage > 0)
            OnSpellDamageDealt?.Invoke(spell, totalDamage);
        
        // Return cards to deck
        GameExtensions.TryManager<DeckManager>(dm => 
        {
            foreach (var card in _comboCardData)
                dm.AddCardToBottom(card);
        });
    }
    
    public void ClearCombo()
    {
        _currentCombo = "";
        _comboCardData.Clear();
        CurrentComboState = ComboState.Empty;
        OnComboCleared?.Invoke();
        OnComboStateChanged?.Invoke("", ComboState.Empty);
    }
    
    // UI Support
    public void PlaySelectedCards()
    {
        var cardManager = GameExtensions.GetManager<CardManager>();
        if (cardManager?.SelectedCards?.Count > 0)
            ProcessCardPlay(cardManager.SelectedCards);
    }
    
    public void ClearSelection()
    {
        GameExtensions.GetManager<CardManager>()?.ClearSelection();
    }
    
    public void DrawCard()
    {
        GameExtensions.TryManager<DeckManager>(dm => dm.TryDrawCard());
    }
    
    // Static helpers for UI
    public static bool CheckCanPlayCards(List<Card> cards = null)
    {
        if (!HasInstance) return false;
        var combat = GameExtensions.GetManager<CombatManager>();
        return combat != null && combat.CanAct();
    }
    
    public static bool CheckCanDiscardCard(Card card = null)
    {
        if (!HasInstance) return false;
        var combat = GameExtensions.GetManager<CombatManager>();
        return combat != null && combat.CanAct() && combat.Creativity.CanAfford(1);
    }
}