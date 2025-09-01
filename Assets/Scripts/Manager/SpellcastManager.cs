using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SpellcastManager : SingletonBehaviour<SpellcastManager>, IGameManager
{
    [Header("Spells")]
    [SerializeField] private List<SpellAsset> availableSpells = new List<SpellAsset>();
    [SerializeField] private bool caseSensitive = false;
    
    private string _currentCombo = "";
    private Dictionary<string, SpellAsset> _spellCache = new Dictionary<string, SpellAsset>();
    private List<CardData> _comboCardData = new List<CardData>();
    
    public bool IsReady { get; private set; }
    
    // Events - FIXED: Korrekte System.Action Syntax
    public static event System.Action<string, ComboState> OnComboStateChanged;
    public static event System.Action<SpellAsset, List<CardData>> OnSpellCast;
    public static event System.Action<SpellAsset, int> OnSpellDamageDealt;
    public static event System.Action OnComboCleared;
    public static event System.Action<SpellAsset, string> OnSpellFound;
    public static event System.Action<string> OnSpellNotFound;
    
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
            TryCastCurrentCombo();
    }
    
    public void ProcessCardPlay(List<Card> cards)
    {
        if (cards == null || cards.Count == 0) return;
        
        // Get letters and destroy cards
        string letters = cards.GetLetterSequence();
        if (string.IsNullOrEmpty(letters)) return;
        
        var cardManager = CoreExtensions.GetManager<CardManager>();
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
            // Fire spell found event
            OnSpellFound?.Invoke(_spellCache[_currentCombo], _currentCombo);
        }
        else if (_spellCache.Keys.Any(k => k.StartsWith(_currentCombo)))
        {
            CurrentComboState = ComboState.Building;
        }
        else
        {
            CurrentComboState = ComboState.Invalid;
            OnSpellNotFound?.Invoke(_currentCombo);
            Invoke(nameof(ClearCombo), 0.5f);
        }
        
        OnComboStateChanged?.Invoke(_currentCombo, CurrentComboState);
    }
    
    public void TryCastCurrentCombo()
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
        OnSpellCast?.Invoke(spell, new List<CardData>(_comboCardData));
    
        int totalDamage = 0;
    
        // Simple effect execution
        foreach (var effect in spell.Effects)
        {
            switch (effect.effectType)
            {
                case SpellEffectType.Damage:
                    int damage = (int)effect.value;
                    CoreExtensions.TryWithManagerStatic<EnemyManager>( em => 
                    {
                        var target = em.AliveEnemies.GetWeakest();
                        if (target != null)
                        {
                            target.TakeDamage(damage, DamageType.Normal);
                            totalDamage += damage;
                        }
                    });
                    break;
                
                case SpellEffectType.Heal:
                    CoreExtensions.TryWithManagerStatic<CombatManager>( cm => 
                        cm.ModifyLife((int)effect.value));
                    break;
            }
        }
    
        if (totalDamage > 0)
            OnSpellDamageDealt?.Invoke(spell, totalDamage);
    
        // Return cards to deck
        CoreExtensions.TryWithManagerStatic<DeckManager>( dm => 
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
    
    public void ClearSelection()
    {
        CoreExtensions.GetManager<CardManager>()?.ClearSelection();
    }
    
    // UI Support
    public void PlaySelectedCards()
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        if (cardManager?.SelectedCards?.Count > 0)
            ProcessCardPlay(cardManager.SelectedCards);
    }
    
    public void DrawCard()
    {
        CoreExtensions.TryWithManagerStatic<DeckManager>( dm => dm.TryDrawCard());
    }
    
    // Static helpers for UI
    public static bool CheckCanPlayCards(List<Card> cards = null)
    {
        if (!HasInstance) return false;
        return CoreExtensions.TryWithManagerStatic<CombatManager, bool>(null, combat => combat.CanAct());
    }

    public static bool CheckCanDiscardCard(Card card = null)
    {
        if (!HasInstance) return false;
        return CoreExtensions.TryWithManagerStatic<CombatManager, bool>(null, combat => 
            combat.CanAct() && combat.Creativity.CanAfford(1));
    }
}