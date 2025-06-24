// SharedEnums.cs - NEUE DATEI
// KRITISCHER FIX: Ersetzt alle Enum-Duplikate aus bestehenden Extensions
// LOCATION: Assets/Scripts/Extensions/SharedEnums.cs

namespace GameCore.Enums
{
    // === ENTITY SYSTEM (aus EntityExtensions.cs + EntityAsset.cs) ===
    public enum EntityType
    {
        Enemy,
        Unit,
        Neutral,
        Environmental
    }

    public enum EntityCategory
    {
        Standard,
        Elite,
        Boss,
        Minion,
        Summon,
        Special
    }

    public enum EntityHealthStatus
    {
        Invalid,
        Dead,
        Critical,
        Low,
        Moderate,
        High,
        Full
    }

    public enum EntitySortCriteria
    {
        Health,
        HealthPercentage,
        MaxHealth,
        TargetPriority,
        Name
    }

    // === COMBAT SYSTEM (aus CombatExtensions.cs + CombatManager.cs) ===
    public enum DamageType
    {
        Normal,
        Fire,
        Ice,
        Lightning,
        True
    }

    public enum TurnPhase
    {
        PlayerTurn,
        EnemyTurn,
        Setup,
        Cleanup,
        TurnTransition,
        CombatEnd
    }

    public enum CombatDifficulty
    {
        None,
        Easy,
        Moderate,
        Hard,
        Desperate
    }

    public enum ThreatLevel
    {
        None,
        Low,
        Medium,
        High,
        Extreme
    }

    public enum TargetingStrategy
    {
        Optimal,
        Weakest,
        Strongest,
        Nearest,
        Priority,
        Random
    }

    public enum HealthComparison
    {
        Below,
        Above,
        Equal,
        BelowOrEqual,
        AboveOrEqual
    }

    // === RESOURCE SYSTEM (aus ResourceExtensions.cs + CombatManager.cs) ===
    public enum ResourceType
    {
        Life,
        Creativity,
        Energy,
        Mana
    }

    public enum ResourceHealth
    {
        Dead = 0,
        Dying = 1,
        Critical = 2,
        Low = 3,
        Moderate = 4,
        Good = 5,
        Excellent = 6
    }

    public enum ResourceUrgency
    {
        None,
        Low,
        Medium,
        High,
        Immediate
    }

    public enum ResourcePriority
    {
        VeryLow = 1,
        Low = 2,
        Medium = 3,
        High = 4,
        Critical = 5
    }

    public enum ResourceAction
    {
        Maintain,
        RecoverImmediately,
        RecoverBeforeSpending,
        ReduceSpending,
        ConsiderIncreaseSpending,
        Optimize
    }

    public enum BudgetStatus
    {
        Balanced,
        Overbudget,
        Underbudget
    }

    // === STATUS ENUMS (konsolidiert aus CombatExtensions.cs) ===
    public enum HealthStatus
    {
        Dying,
        Critical,
        Low,
        Moderate,
        Good,
        Excellent
    }

    public enum ResourceStatus
    {
        Critical,
        Low,
        Medium,
        High
    }

    // === CARD SYSTEM (aus Card.cs + CardData.cs) ===
    public enum CardType
    {
        Vowel,
        Consonant,
        Special
    }

    public enum CardSubType
    {
        Basic,
        Element,
        School,
        Ender
    }

    public enum CardState
    {
        Idle,
        Selected,
        Disabled
    }

    public enum CardSortCriteria
    {
        Name,
        Tier,
        Type,
        LetterCount,
        State
    }

    // === SPELL SYSTEM (aus SpellAsset.cs + SpellcastManager.cs) ===
    public enum SpellType
    {
        Basic,
        Element,
        School
    }

    public enum SpellSubtype
    {
        Basic,
        Fire,
        Light,
        Nature,
        Dark,
        Time,
        Attack,
        Defense,
        Support,
        Disrupt
    }

    public enum SpellEffectType
    {
        Damage,
        Heal,
        Buff,
        Debuff,
        Summon,
        Teleport,
        Shield,
        Custom
    }

    public enum ComboState
    {
        Empty,
        Building,
        Ready,
        Invalid
    }

    // === ACTION SYSTEM (aus ManagerExtensions.cs + CombatExtensions.cs) ===
    public enum PlayerActionType
    {
        General,
        PlayCards,
        DrawCard,
        EndTurn,
        SpendCreativity,
        CastSpell,
        SelectCard,
        DiscardCard
    }

    public enum ActionType
    {
        Attack,
        Defend,
        Heal,
        DrawCard,
        EndTurn,
        CastSpell
    }

    public enum ActionPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum UrgencyLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    // === SPECIALIZED ENUMS (aus verschiedenen Extensions) ===
    public enum HealingMode
    {
        Self,
        MostDamaged,
        All,
        Critical
    }

    public enum FormationType
    {
        Line,
        Circle,
        Grid,
        Custom
    }

    public enum UnitGroupStatus
    {
        None,
        Healthy,
        Moderate,
        Damaged,
        Critical
    }

    // === MANAGER SYSTEM (aus GameManager.cs) ===
    public enum ManagerType
    {
        Card,
        Deck,
        HandLayout,
        Spellcast,
        Combat,
        Enemy,
        Unit
    }
}

// === SHARED DATA CLASSES ===
// Klassen die von mehreren Extensions verwendet werden

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