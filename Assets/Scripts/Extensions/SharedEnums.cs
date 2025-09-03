// MINIMAL ENUMS - Nur die WIRKLICH genutzten
// KEINE NAMESPACES! Alles global

// Card System
public enum CardState { Idle, Selected, Highlighted, Dragging, Disabled }
public enum CardType { Vowel, Consonant, Special }
public enum CardSubType { Basic, Element, School, Ender }
public enum HandPosition 
{ 
    Left, 
    Right, 
    Center,
    Custom 
}

public enum CardMoveDirection
{
    Left = -1,
    None = 0,
    Right = 1
}

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
public enum ManagerType { Card, Deck, HandLayout, Selection, Spellcast, Combat, Enemy, Unit }
public enum PlayerActionType { PlayCards, DiscardCard, DrawCard, EndTurn }
public enum FormationType { Line, Circle, Grid }
public enum TargetingStrategy { Optimal, Weakest, Strongest, Nearest, Priority, Random }
public enum HealthComparison { Below, Above, Equal, BelowOrEqual, AboveOrEqual }
public enum HealthStatus { Dead, Critical, Low, Moderate, Healthy, Full }
public enum ThreatLevel { None, Low, Medium, High, Extreme }
public enum UnitGroupStatus { None, Critical, Damaged, Moderate, Healthy }
// Interface für alle Manager
public interface IGameManager
{
    bool IsReady { get; }
}