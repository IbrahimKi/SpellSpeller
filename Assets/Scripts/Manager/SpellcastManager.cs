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
    
    // INTEGRATION: ProcessCardPlay with CardExtensions + ManagerExtensions
    public void ProcessCardPlay(List<Card> playedCards)
    {
        // INTEGRATION: Use CardExtensions for validation
        if (!playedCards.HasValidCards()) return;
        
        // INTEGRATION: Use CardExtensions for letter sequence
        string letterSequence = playedCards.GetLetterSequence();
        if (string.IsNullOrEmpty(letterSequence)) return;
        
        string normalizedLetters = caseSensitive ? letterSequence : letterSequence.ToUpper();
        
        // INTEGRATION: Safe card processing using CardExtensions + ManagerExtensions
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
        {
            TriggerSpellEffect(effect);
        }
        
        // INTEGRATION: Safe deck operations using ManagerExtensions
        this.TryWithManager<DeckManager>(dm => 
        {
            foreach (var cardData in sourceCardData)
            {
                dm.AddCardToBottom(cardData);
            }
        });
        
        this.TryWithManager<CardManager>(cm => cm.ClearSelection());
    }
    
    // INTEGRATION: Enhanced spell effect triggering with ResourceExtensions
    public void TriggerSpellEffect(SpellEffect effect)
    {
        OnSpellEffectTriggered?.Invoke(effect);
        
        // INTEGRATION: Safe combat operations using ManagerExtensions + ResourceExtensions
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
    
    // INTEGRATION: Enhanced damage effect with EntityExtensions
    private void ApplyDamageEffect(SpellEffect effect, CombatManager combat)
    {
        // INTEGRATION: Safe enemy targeting using ManagerExtensions + EntityExtensions
        if (combat.CurrentTargets.Count == 0)
        {
            this.TryWithManager<EnemyManager>(em => 
            {
                // INTEGRATION: Use EntityExtensions for smart target selection
                var optimalTarget = em.AliveEnemies.GetWeakest();
                if (optimalTarget != null)
                {
                    combat.AddTarget(optimalTarget);
                    Debug.Log($"[SpellcastManager] Auto-targeted weakest enemy: {optimalTarget.EntityName}");
                }
            });
        }
        
        if (combat.CurrentTargets.Count > 0)
        {
            int damage = Mathf.RoundToInt(effect.value);
            
            // INTEGRATION: Enhanced damage application using EntityExtensions
            foreach (var target in combat.CurrentTargets.ToList())
            {
                if (target.IsValidTarget())
                {
                    var result = target.TryDamageWithEffects(damage, DamageType.Normal, true);
                    if (result.Success)
                    {
                        Debug.Log($"[SpellcastManager] Spell damage: {result.DamageDealt} to {target.EntityName}");
                        
                        // Check for elimination
                        if (result.WasKilled)
                        {
                            Debug.Log($"[SpellcastManager] {target.EntityName} eliminated by spell!");
                        }
                        else if (target.IsCriticalHealth())
                        {
                            Debug.Log($"[SpellcastManager] {target.EntityName} is in critical condition!");
                        }
                    }
                }
            }
        }
    }
    
    private void ApplyHealEffect(SpellEffect effect, CombatManager combat)
    {
        int healAmount = Mathf.RoundToInt(effect.value);
        
        // INTEGRATION: Use ResourceExtensions for smart healing
        var lifeHealth = combat.Life.GetResourceHealth();
        
        if (lifeHealth <= ResourceHealth.Critical)
        {
            // Emergency healing - apply full amount
            combat.ModifyLife(healAmount);
            Debug.Log($"[SpellcastManager] Emergency heal: +{healAmount} life");
        }
        else if (lifeHealth <= ResourceHealth.Low)
        {
            // Standard healing
            combat.ModifyLife(healAmount);
            Debug.Log($"[SpellcastManager] Heal: +{healAmount} life");
        }
        else
        {
            // Overheal protection - reduce healing if nearly full
            int optimalHeal = combat.Life.GetOptimalRecovery(healAmount);
            if (optimalHeal > 0)
            {
                combat.ModifyLife(optimalHeal);
                Debug.Log($"[SpellcastManager] Optimal heal: +{optimalHeal} life (reduced from {healAmount})");
            }
        }
    }
    
    private void ApplyBuffEffect(SpellEffect effect, CombatManager combat)
    {
        if (effect.effectName.ToLower().Contains("creativity"))
        {
            int creativityGain = Mathf.RoundToInt(effect.value);
            
            // INTEGRATION: Use ResourceExtensions for smart creativity management
            var creativityHealth = combat.Creativity.GetResourceHealth();
            
            if (creativityHealth <= ResourceHealth.Critical)
            {
                // Emergency creativity boost
                combat.ModifyCreativity(creativityGain);
                Debug.Log($"[SpellcastManager] Emergency creativity boost: +{creativityGain}");
            }
            else
            {
                // Normal creativity gain with overflow protection
                int optimalGain = combat.Creativity.GetOptimalRecovery(creativityGain);
                if (optimalGain > 0)
                {
                    combat.ModifyCreativity(optimalGain);
                    Debug.Log($"[SpellcastManager] Creativity gain: +{optimalGain}");
                }
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
    
    // UI Support Methods - INTEGRATION with ManagerExtensions
    public void PlaySelectedCards()
    {
        this.TryWithManager<CardManager>(cm => 
        {
            var selectedCards = cm.SelectedCards;
            if (selectedCards?.Count > 0)
            {
                ProcessCardPlay(selectedCards);
            }
        });
    }
    
    public void DrawCard()
    {
        if (!CanDraw()) return;
        
        this.TryWithManager<DeckManager>(dm => 
        {
            var drawnCard = dm.DrawCard();
            if (drawnCard != null)
            {
                this.TryWithManager<CardManager>(cm => 
                    cm.SpawnCard(drawnCard, null, true));
            }
        });
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
    
    // Drop area support - INTEGRATION with CombatExtensions
    public static bool CheckCanPlayCards(List<Card> cards = null)
    {
        if (!HasInstance) return false;
        
        return Instance.TryWithManager<CombatManager, bool>(cm => 
        {
            var cardsToCheck = cards ?? Instance.TryWithManager<CardManager, List<Card>>(
                cardManager => cardManager.SelectedCards) ?? new List<Card>();
            
            return cardsToCheck.Count > 0 && 
                   cm.IsInCombat && 
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
    
    // INTEGRATION: New spell recommendation methods using CardExtensions
    
    /// <summary>
    /// Get spell recommendations based on current hand
    /// </summary>
    public List<SpellRecommendation> GetSpellRecommendations()
    {
        var recommendations = new List<SpellRecommendation>();
        
        this.TryWithManager<CardManager>(cm => 
        {
            var handAnalysis = cm.GetHandAnalysis();
            var spellPotential = cm.GetHandSpellPotential();
            
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
            
            // Sort by effectiveness
            recommendations = recommendations.OrderByDescending(r => r.Effectiveness).ToList();
        });
        
        return recommendations;
    }
    
    private float CalculateSpellEffectiveness(SpellAsset spell)
    {
        float effectiveness = 1f;
        
        // Analyze based on current situation using CombatExtensions
        this.TryWithManager<CombatManager>(cm => 
        {
            var situation = cm.GetCombatSituation();
            
            foreach (var effect in spell.Effects)
            {
                switch (effect.effectType)
                {
                    case SpellEffectType.Heal:
                        if (situation.HealthStatus <= HealthStatus.Low)
                            effectiveness *= 2f; // Double effectiveness when health is low
                        break;
                        
                    case SpellEffectType.Damage:
                        if (situation.EnemyThreat >= ThreatLevel.High)
                            effectiveness *= 1.5f; // Higher priority when enemies are threatening
                        break;
                        
                    case SpellEffectType.Buff:
                        if (effect.effectName.ToLower().Contains("creativity") && 
                            situation.CreativityStatus <= ResourceStatus.Low)
                            effectiveness *= 1.8f; // High priority when creativity is low
                        break;
                }
            }
        });
        
        return effectiveness;
    }
    
    /// <summary>
    /// Auto-select optimal spell based on situation
    /// </summary>
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
    
    [ContextMenu("Get Spell Recommendations")]
    public void DebugSpellRecommendations()
    {
        var recommendations = GetSpellRecommendations();
        Debug.Log($"[SpellcastManager] Spell Recommendations:");
        foreach (var rec in recommendations)
        {
            Debug.Log($"  - {rec.Spell.SpellName}: Effectiveness {rec.Effectiveness:F2}");
        }
    }
    
    [ContextMenu("Auto Select Optimal Spell")]
    public void DebugAutoSelect()
    {
        AutoSelectOptimalSpell();
    }
#endif
}

// INTEGRATION: Supporting class for spell recommendations
[System.Serializable]
public class SpellRecommendation
{
    public SpellAsset Spell;
    public bool CanCast;
    public List<Card> RequiredCards;
    public float Effectiveness;
}