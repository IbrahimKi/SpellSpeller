using UnityEngine;
using System.Collections.Generic;

namespace GameCore.Enums
{
    // === ENTITY SYSTEM (aus EntityExtensions.cs + EntityAsset.cs) ===
    // MINIMAL ENUMS - Nur die WIRKLICH genutzten

// Card System
    public enum CardState { Idle, Selected, Disabled }
    public enum CardType { Vowel, Consonant, Special }
    public enum CardSubType { Basic, Element, School, Ender }

// Combat System  
    public enum TurnPhase { PlayerTurn, EnemyTurn, TurnTransition, CombatEnd }
    public enum DamageType { Normal, Fire, Ice, Lightning, True }
    public enum ResourceType { Life, Creativity }

// Entity System
    public enum EntityType { Enemy, Unit, Neutral, Environmental }
    public enum EntityCategory { Standard, Elite, Boss, Minion, Summon, Special }

// Spell System
    public enum SpellType { Basic, Element, School }
    public enum SpellEffectType { Damage, Heal, Buff, Debuff, Summon, Shield, Custom }
    public enum ComboState { Empty, Building, Ready, Invalid }

// Manager System
    public enum ManagerType { Card, Deck, HandLayout, Spellcast, Combat, Enemy, Unit, CardSlot }
    
}

// === SHARED DATA CLASSES ===
namespace GameCore.Data
{
    using UnityEngine;
    using GameCore.Enums;

    [System.Serializable]
    public class ActionCost
    {
        public ResourceType ResourceType { get; set; }
        public int Amount { get; set; }
        public ResourcePriority Priority { get; set; } = ResourcePriority.Medium;
        public string Description { get; set; } = "";
        
        public static ActionCost Create(ResourceType type, int amount, ResourcePriority priority = ResourcePriority.Medium)
            => new ActionCost { ResourceType = type, Amount = amount, Priority = priority };
            
        public static ActionCost None => new ActionCost();
        public static ActionCost Draw => new ActionCost { ResourceType = ResourceType.Creativity, Amount = 1 };
        public static ActionCost Discard => new ActionCost { ResourceType = ResourceType.Creativity, Amount = 1 };
    }

    [System.Serializable]
    public class ResourceCost
    {
        public ResourceType ResourceType { get; set; }
        public int Amount { get; set; }
        public ResourcePriority Priority { get; set; } = ResourcePriority.Medium;
        public string Description { get; set; } = "";
        
        public static ResourceCost Create(ResourceType type, int amount, ResourcePriority priority = ResourcePriority.Medium)
            => new ResourceCost { ResourceType = type, Amount = amount, Priority = priority };
    }

    [System.Serializable]
    public class TargetingCriteria
    {
        public bool PreferLowHealth { get; set; } = true;
        public bool PreferHighHealth { get; set; } = false;
        public bool PreferClose { get; set; } = true;
        public Vector3? ReferencePosition { get; set; }
        public float MaxDistance { get; set; } = 100f;
        public System.Collections.Generic.List<EntityType> PreferredTypes { get; set; } = new System.Collections.Generic.List<EntityType>();
        
        public float HealthWeight { get; set; } = 1f;
        public float DistanceWeight { get; set; } = 0.5f;
        public float PriorityWeight { get; set; } = 0.3f;
        public float TypeWeight { get; set; } = 0.2f;
    }

    [System.Serializable]
    public class CombatSituation
    {
        public bool IsPlayerTurn { get; set; }
        public bool CanAct { get; set; }
        public HealthStatus HealthStatus { get; set; }
        public ResourceStatus CreativityStatus { get; set; }
        public ThreatLevel EnemyThreat { get; set; }
        public ActionType RecommendedAction { get; set; }
        public UrgencyLevel UrgencyLevel { get; set; }
        public bool IsResourceCrisis { get; set; }
    }

    [System.Serializable]
    public class DamageResult
    {
        public bool Success { get; set; }
        public int DamageDealt { get; set; }
        public int FinalHealth { get; set; }
        public float HealthPercentage { get; set; }
        public bool WasKilled { get; set; }
        public DamageType DamageType { get; set; }
        public string FailureReason { get; set; } = "";
    }

    [System.Serializable]
    public class HealResult
    {
        public bool Success { get; set; }
        public int HealingDone { get; set; }
        public int FinalHealth { get; set; }
        public float HealthPercentage { get; set; }
        public bool WasFullyHealed { get; set; }
        public string FailureReason { get; set; } = "";
    }
}