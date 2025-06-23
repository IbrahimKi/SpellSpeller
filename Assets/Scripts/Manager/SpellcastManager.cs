using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
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
            TryCastCurrentCombo();
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
    
    public void ProcessCardPlay(List<Card> playedCards)
    {
        if (!playedCards.HasValidCards()) return;
        
        string letterSequence = playedCards.GetLetterSequence();
        if (string.IsNullOrEmpty(letterSequence)) return;
        
        string normalizedLetters = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        foreach (var card in playedCards.GetValidCards())
        {
            this.TryWithManager<CardManager>(cm => 
            {
                cm.RemoveCardFromHand(card);
                cm.DestroyCard(card);
            });
            _comboCardData.Add(card.CardData);
        }
        
        _currentCombo += normalizedLetters;
        UpdateComboState();
        
        OnCardsPlayed?.Invoke(playedCards.GetValidCards().ToList(), normalizedLetters);
        Debug.Log($"[SpellcastManager] Added '{normalizedLetters}' to combo. Current: '{_currentCombo}'");
    }
    
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
            TriggerSpellEffect(effect);
        
        this.TryWithManager<DeckManager>(dm => 
        {
            foreach (var cardData in sourceCardData)
                dm.AddCardToBottom(cardData);
        });
        
        this.TryWithManager<CardManager>(cm => cm.ClearSelection());
    }
    
    public void TriggerSpellEffect(SpellEffect effect)
    {
        OnSpellEffectTriggered?.Invoke(effect);
        
        this.TryWithManager<CombatManager>(cm => 
        {
            switch (effect.effectType)
            {
                case SpellEffectType.Damage:
                    ApplyDamageEffect(effect, cm);
                    break;
                case SpellEffectType.Heal:
                    ApplyHealEffect(effect, cm);
                    break;
                case SpellEffectType.Buff:
                    ApplyBuffEffect(effect, cm);
                    break;
            }
        });
    }
    
    private void ApplyDamageEffect(SpellEffect effect, CombatManager combat)
    {
        if (combat.CurrentTargets.Count == 0)
            combat.AddSmartTarget(TargetingStrategy.Optimal);
        
        if (combat.CurrentTargets.Count > 0)
        {
            int damage = Mathf.RoundToInt(effect.value);
            combat.DealDamageToTargets(damage, DamageType.Normal);
        }
    }
    
    private void ApplyHealEffect(SpellEffect effect, CombatManager combat)
    {
        int healAmount = Mathf.RoundToInt(effect.value);
        
        var lifeHealth = combat.Life.GetResourceHealth();
        if (lifeHealth <= ResourceHealth.Critical)
        {
            combat.ModifyLife(healAmount);
            Debug.Log($"[SpellcastManager] Emergency heal: +{healAmount} life");
        }
        else
        {
            int optimalHeal = combat.Life.GetOptimalRecovery(healAmount);
            if (optimalHeal > 0)
            {
                combat.ModifyLife(optimalHeal);
                Debug.Log($"[SpellcastManager] Optimal heal: +{optimalHeal} life");
            }
        }
    }
    
    private void ApplyBuffEffect(SpellEffect effect, CombatManager combat)
    {
        if (effect.effectName.ToLower().Contains("creativity"))
        {
            int creativityGain = Mathf.RoundToInt(effect.value);
            int optimalGain = combat.Creativity.GetOptimalRecovery(creativityGain);
            
            if (optimalGain > 0)
            {
                combat.ModifyCreativity(optimalGain);
                Debug.Log($"[SpellcastManager] Creativity gain: +{optimalGain}");
            }
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
        this.TryWithManager<CardManager>(cm => 
        {
            var selectedCards = cm.SelectedCards;
            if (selectedCards?.Count > 0)
                ProcessCardPlay(selectedCards);
        });
    }
    
    public void DrawCard()
    {
        if (!CanDraw()) return;
        
        this.TryWithManager<DeckManager>(dm => dm.TryDrawCard());
    }
    
    public void ClearSelection()
    {
        this.TryWithManager<CardManager>(cm => cm.ClearSelection());
    }
    
    private bool CanDraw()
    {
        return this.TryWithManager<CardManager, bool>(cm => !cm.IsHandFull) && 
               this.TryWithManager<DeckManager, bool>(dm => !dm.IsDeckEmpty);
    }
    
    // Drop area support
    public static bool CheckCanPlayCards(List<Card> cards = null)
    {
        if (!HasInstance) return false;
        
        return Instance.TryWithManager<CombatManager, bool>(cm => 
        {
            var cardsToCheck = cards ?? Instance.TryWithManager<CardManager, List<Card>>(
                cardManager => cardManager.SelectedCards) ?? new List<Card>();
            
            return cardsToCheck.Count > 0 && 
                   cm.CanPerformPlayerAction(PlayerActionType.PlayCards);
        });
    }
    
    public static bool CheckCanDiscardCard(Card card = null)
    {
        if (!HasInstance) return false;
        
        return Instance.TryWithManager<CombatManager, bool>(cm => 
        {
            return cm.CanPerformPlayerAction(PlayerActionType.DiscardCard) && 
                   cm.CanSpendCreativity(1) &&
                   Instance.TryWithManager<DeckManager, bool>(dm => 
                       dm.GetTotalAvailableCards() > 0);
        });
    }
    
    // Spell recommendations using CardExtensions
    public List<SpellRecommendation> GetSpellRecommendations()
    {
        var recommendations = new List<SpellRecommendation>();
        
        this.TryWithManager<CardManager>(cm => 
        {
            foreach (var spell in availableSpells.Where(s => s?.IsValid == true))
            {
                if (cm.CanBuildSpell(spell.LetterCode))
                {
                    var rec = new SpellRecommendation
                    {
                        Spell = spell,
                        CanCast = true,
                        RequiredCards = cm.FindCardsForSpell(spell.LetterCode),
                        Effectiveness = CalculateSpellEffectiveness(spell)
                    };
                    recommendations.Add(rec);
                }
            }
            
            recommendations = recommendations.OrderByDescending(r => r.Effectiveness).ToList();
        });
        
        return recommendations;
    }
    
    private float CalculateSpellEffectiveness(SpellAsset spell)
    {
        float effectiveness = 1f;
        
        this.TryWithManager<CombatManager>(cm => 
        {
            var situation = cm.GetCombatSituation();
            
            foreach (var effect in spell.Effects)
            {
                switch (effect.effectType)
                {
                    case SpellEffectType.Heal:
                        if (situation.HealthStatus <= HealthStatus.Low)
                            effectiveness *= 2f;
                        break;
                        
                    case SpellEffectType.Damage:
                        if (situation.EnemyThreat >= ThreatLevel.High)
                            effectiveness *= 1.5f;
                        break;
                        
                    case SpellEffectType.Buff:
                        if (effect.effectName.ToLower().Contains("creativity") && 
                            situation.CreativityStatus <= ResourceStatus.Low)
                            effectiveness *= 1.8f;
                        break;
                }
            }
        });
        
        return effectiveness;
    }
    
    public void AutoSelectOptimalSpell()
    {
        var recommendations = GetSpellRecommendations();
        if (recommendations.Count == 0) return;
        
        var bestSpell = recommendations.First();
        
        this.TryWithManager<CardManager>(cm => 
        {
            cm.TrySelectCards(bestSpell.RequiredCards);
            Debug.Log($"[SpellcastManager] Auto-selected cards for spell: {bestSpell.Spell.SpellName}");
        });
    }

#if UNITY_EDITOR
    [ContextMenu("Log Available Spells")]
    public void LogAvailableSpells()
    {
        Debug.Log($"[SpellcastManager] Available Spells ({availableSpells.Count}):");
        foreach (var spell in availableSpells.Where(s => s != null))
            Debug.Log($"  - {spell.SpellName}: '{spell.LetterCode}'");
    }
    
    [ContextMenu("Get Spell Recommendations")]
    public void DebugSpellRecommendations()
    {
        var recommendations = GetSpellRecommendations();
        Debug.Log($"[SpellcastManager] Spell Recommendations:");
        foreach (var rec in recommendations)
            Debug.Log($"  - {rec.Spell.SpellName}: Effectiveness {rec.Effectiveness:F2}");
    }
#endif
}

[System.Serializable]
public class SpellRecommendation
{
    public SpellAsset Spell;
    public bool CanCast;
    public List<Card> RequiredCards;
    public float Effectiveness;
}