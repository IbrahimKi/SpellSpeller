using UnityEngine;
using System.Collections.Generic;
using GameCore.Enums;
using GameCore.Data;

[CreateAssetMenu(fileName = "New Card Slot", menuName = "Card System/Card Slot")]
public class CardSlotAsset : ScriptableObject
{
    [Header("Slot Configuration")]
    public string slotName = "Card Slot";
    public int slotIndex = 0;
    public bool isActive = true;
    public bool canAcceptCards = true;
    
    [Header("Visual Settings")]
    public Color emptySlotColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);
    public Color filledSlotColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
    public Color highlightColor = new Color(0f, 1f, 0f, 0.3f);
    public Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
    
    [Header("Slot Effects")]
    public List<SlotEffect> slotEffects = new List<SlotEffect>();
    
    [Header("Card Restrictions")]
    public List<CardType> allowedCardTypes = new List<CardType>();
    public List<CardSubType> allowedSubTypes = new List<CardSubType>();
    public int minTier = 0;
    public int maxTier = 99;
    
    [Header("Special Properties")]
    public bool autoPlayWhenFilled = false;
    public float autoPlayDelay = 0.5f;
    public bool persistCardBetweenTurns = false;
    public bool consumeOnPlay = true;
    
    // Validation
    public bool CanAcceptCard(Card card)
    {
        if (!isActive || !canAcceptCards || card == null) return false;
        
        // Type restrictions
        if (allowedCardTypes.Count > 0 && !allowedCardTypes.Contains(card.GetCardType()))
            return false;
            
        if (allowedSubTypes.Count > 0 && !allowedSubTypes.Contains(card.GetCardSubType()))
            return false;
            
        // Tier restrictions
        int cardTier = card.GetTier();
        if (cardTier < minTier || cardTier > maxTier)
            return false;
        
        return true;
    }
    
    // Effect Processing
    public void ProcessEffectsOnCardPlaced(Card card, CardSlotBehaviour slotBehaviour)
    {
        foreach (var effect in slotEffects)
        {
            if (effect.triggerEvent == SlotTriggerEvent.OnCardPlaced)
            {
                effect.ApplyEffect(card, slotBehaviour);
            }
        }
    }
    
    public void ProcessEffectsOnCardRemoved(Card card, CardSlotBehaviour slotBehaviour)
    {
        foreach (var effect in slotEffects)
        {
            if (effect.triggerEvent == SlotTriggerEvent.OnCardRemoved)
            {
                effect.ApplyEffect(card, slotBehaviour);
            }
        }
    }
    
    public void ProcessEffectsOnPlay(Card card, CardSlotBehaviour slotBehaviour)
    {
        foreach (var effect in slotEffects)
        {
            if (effect.triggerEvent == SlotTriggerEvent.OnPlay)
            {
                effect.ApplyEffect(card, slotBehaviour);
            }
        }
    }
}

[System.Serializable]
public class SlotEffect
{
    [Header("Effect Configuration")]
    public string effectName = "Slot Effect";
    public SlotTriggerEvent triggerEvent = SlotTriggerEvent.OnCardPlaced;
    public SlotEffectType effectType = SlotEffectType.Buff;
    
    [Header("Effect Values")]
    public int effectValue = 1;
    public float effectDuration = 0f; // 0 = permanent/instant
    public bool stackable = false;
    
    [Header("Target")]
    public SlotEffectTarget target = SlotEffectTarget.PlacedCard;
    
    [Header("Conditions")]
    public List<SlotEffectCondition> conditions = new List<SlotEffectCondition>();
    
    public bool CanApplyEffect(Card card, CardSlotBehaviour slotBehaviour)
    {
        foreach (var condition in conditions)
        {
            if (!condition.CheckCondition(card, slotBehaviour))
                return false;
        }
        return true;
    }
    
    public void ApplyEffect(Card card, CardSlotBehaviour slotBehaviour)
    {
        if (!CanApplyEffect(card, slotBehaviour)) return;
        
        Debug.Log($"[SlotEffect] Applying {effectName} to {card?.GetCardName() ?? "null"}");
        
        switch (effectType)
        {
            case SlotEffectType.Buff:
                ApplyBuffEffect(card, slotBehaviour);
                break;
            case SlotEffectType.Debuff:
                ApplyDebuffEffect(card, slotBehaviour);
                break;
            case SlotEffectType.Damage:
                ApplyDamageEffect(card, slotBehaviour);
                break;
            case SlotEffectType.Heal:
                ApplyHealEffect(card, slotBehaviour);
                break;
            case SlotEffectType.ResourceModify:
                ApplyResourceEffect(card, slotBehaviour);
                break;
            case SlotEffectType.Custom:
                ApplyCustomEffect(card, slotBehaviour);
                break;
        }
    }
    
    private void ApplyBuffEffect(Card card, CardSlotBehaviour slotBehaviour)
    {
        // Buff implementation using Extensions
        slotBehaviour.TryWithManager<CombatManager>(cm =>
        {
            switch (target)
            {
                case SlotEffectTarget.Player:
                    cm.ModifyCreativity(effectValue);
                    break;
                case SlotEffectTarget.PlacedCard:
                    // Could add temporary card enhancement
                    slotBehaviour.LogDebug($"Buffing card {card.GetCardName()} by {effectValue}");
                    break;
            }
        });
    }
    
    private void ApplyDebuffEffect(Card card, CardSlotBehaviour slotBehaviour)
    {
        // Debuff implementation
        slotBehaviour.LogDebug($"Applying debuff {effectName} with value {effectValue}");
    }
    
    private void ApplyDamageEffect(Card card, CardSlotBehaviour slotBehaviour)
    {
        slotBehaviour.TryWithManager<EnemyManager>(em =>
        {
            switch (target)
            {
                case SlotEffectTarget.AllEnemies:
                    em.DamageAllEnemies(effectValue);
                    break;
                case SlotEffectTarget.RandomEnemy:
                    em.DamageRandomEnemy(effectValue);
                    break;
                case SlotEffectTarget.TargetedEnemies:
                    em.DamageTargetedEnemies(effectValue);
                    break;
            }
        });
    }
    
    private void ApplyHealEffect(Card card, CardSlotBehaviour slotBehaviour)
    {
        slotBehaviour.TryWithManager<CombatManager>(cm =>
        {
            switch (target)
            {
                case SlotEffectTarget.Player:
                    cm.ModifyLife(effectValue);
                    break;
                case SlotEffectTarget.AllUnits:
                    slotBehaviour.TryWithManager<UnitManager>(um =>
                        um.HealAllUnits(effectValue)
                    );
                    break;
            }
        });
    }
    
    private void ApplyResourceEffect(Card card, CardSlotBehaviour slotBehaviour)
    {
        slotBehaviour.TryWithManager<CombatManager>(cm =>
        {
            // Default to creativity modification
            cm.ModifyCreativity(effectValue);
        });
    }
    
    private void ApplyCustomEffect(Card card, CardSlotBehaviour slotBehaviour)
    {
        // Custom effect hook - can be extended via events
        SlotEffectEvent?.Invoke(this, card, slotBehaviour);
    }
    
    // Custom effect event for extensibility
    public static event System.Action<SlotEffect, Card, CardSlotBehaviour> SlotEffectEvent;
}

[System.Serializable]
public class SlotEffectCondition
{
    public SlotConditionType conditionType = SlotConditionType.Always;
    public CardType requiredCardType = CardType.Consonant;
    public int requiredValue = 1;
    public string requiredTag = "";
    
    public bool CheckCondition(Card card, CardSlotBehaviour slotBehaviour)
    {
        switch (conditionType)
        {
            case SlotConditionType.Always:
                return true;
                
            case SlotConditionType.CardType:
                return card != null && card.GetCardType() == requiredCardType;
                
            case SlotConditionType.CardTier:
                return card != null && card.GetTier() >= requiredValue;
                
            case SlotConditionType.PlayerHealthBelow:
                return slotBehaviour.TryWithManager<CombatManager, bool>(cm =>
                    cm.Life.Percentage < (requiredValue / 100f)
                );
                
            case SlotConditionType.EnemyCount:
                return slotBehaviour.TryWithManager<EnemyManager, bool>(em =>
                    em.AliveEnemyCount >= requiredValue
                );
                
            case SlotConditionType.HasTag:
                return card != null && card.HasTag(requiredTag);
                
            default:
                return true;
        }
    }
}

// Supporting Enums
public enum SlotTriggerEvent
{
    OnCardPlaced,
    OnCardRemoved,
    OnPlay,
    OnTurnStart,
    OnTurnEnd
}

public enum SlotEffectType
{
    Buff,
    Debuff,
    Damage,
    Heal,
    ResourceModify,
    Custom
}

public enum SlotEffectTarget
{
    PlacedCard,
    Player,
    AllEnemies,
    TargetedEnemies,
    RandomEnemy,
    AllUnits,
    SelectedUnit
}

public enum SlotConditionType
{
    Always,
    CardType,
    CardTier,
    PlayerHealthBelow,
    EnemyCount,
    HasTag
}