using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GameCore.Enums;
using GameCore.Data;

/// <summary>
/// ManagerExtensions - Zentrale Manager-Integration ohne Duplikate
/// CRITICAL FIX: Alle redundanten TryWithManager Implementierungen entfernt
/// DEPENDENCIES: CoreExtensions (verwendet dessen TryWithManager)
/// </summary>
public static class ManagerExtensions
{
    // === MANAGER STATUS UTILITIES ===
    
    /// <summary>
    /// Singleton Manager abrufen (null-safe)
    /// USAGE: Ersetzt direkte Singleton.Instance Zugriffe
    /// </summary>
    public static T TryGetManager<T>() where T : SingletonBehaviour<T>
        => SingletonBehaviour<T>.HasInstance ? SingletonBehaviour<T>.Instance : null;

    // === COMBAT MANAGER INTEGRATION ===
    
    /// <summary>
    /// Player kann Aktion ausführen
    /// PERFORMANCE: Batch-Check aller Bedingungen
    /// </summary>
    public static bool CanPerformPlayerAction(this CombatManager combat, PlayerActionType actionType = PlayerActionType.General)
    {
        if (!combat.IsManagerReady()) return false;
        
        bool baseConditions = combat.IsInCombat && 
                             combat.IsPlayerTurn && 
                             !combat.IsProcessingTurn;
        
        if (!baseConditions) return false;
        
        return actionType switch
        {
            PlayerActionType.PlayCards => true,
            PlayerActionType.DrawCard => CheckCanDraw(),
            PlayerActionType.EndTurn => combat.CanEndTurn,
            PlayerActionType.SpendCreativity => combat.Creativity.CurrentValue > 0,
            PlayerActionType.CastSpell => CheckCanCastSpell(),
            _ => true
        };
        
        // Local helper functions
        bool CheckCanDraw() => 
            TryGetManager<CardManager>()?.IsManagerReady() == true &&
            !TryGetManager<CardManager>().IsHandFull &&
            TryGetManager<DeckManager>()?.IsManagerReady() == true &&
            !TryGetManager<DeckManager>().IsDeckEmpty;
            
        bool CheckCanCastSpell() =>
            TryGetManager<SpellcastManager>()?.IsManagerReady() == true &&
            TryGetManager<SpellcastManager>().CanCastCombo;
    }
    
    /// <summary>
    /// Combat ist in gültigem State
    /// </summary>
    public static bool IsInValidCombatState(this CombatManager combat)
        => combat.IsManagerReady() && combat.IsInCombat && combat.Life.CurrentValue > 0;
    
    /// <summary>
    /// Resource kann ausgegeben werden
    /// INTEGRATION: Verwendet CoreExtensions.IsValidResource
    /// </summary>
    public static bool CanSpendResource(this CombatManager combat, ResourceType type, int amount)
    {
        if (!combat.IsManagerReady()) return false;
        
        var resource = combat.GetResourceByType(type);
        return resource.IsValidResource() && resource.HasAvailable(amount);
    }
    
    /// <summary>
    /// Resource sicher modifizieren
    /// </summary>
    public static bool TryModifyResource(this CombatManager combat, ResourceType type, int delta)
    {
        if (!combat.IsManagerReady()) return false;
        
        try
        {
            switch (type)
            {
                case ResourceType.Life:
                    combat.ModifyLife(delta);
                    return true;
                case ResourceType.Creativity:
                    combat.ModifyCreativity(delta);
                    return true;
                default:
                    return false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManagerExtensions] Resource modification failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Resource-Referenz abrufen
    /// </summary>
    public static Resource GetResourceByType(this CombatManager combat, ResourceType type)
    {
        if (!combat.IsManagerReady()) return null;
        
        return type switch
        {
            ResourceType.Life => combat.Life,
            ResourceType.Creativity => combat.Creativity,
            _ => null
        };
    }

    // === CARD MANAGER INTEGRATION ===
    
    /// <summary>
    /// Kann Karte ziehen
    /// PERFORMANCE: Kombiniert alle Checks in einem Aufruf
    /// </summary>
    public static bool CanDrawCard(this CardManager cardManager)
    {
        if (!cardManager.IsManagerReady()) return false;
        
        return !cardManager.IsHandFull && 
               TryGetManager<DeckManager>()?.IsManagerReady() == true &&
               !TryGetManager<DeckManager>().IsDeckEmpty &&
               TryGetManager<CombatManager>()?.IsPlayerTurn == true;
    }
    
    /// <summary>
    /// Sichere Card-Selection mit Validation
    /// </summary>
    public static bool TrySelectCards(this CardManager cardManager, IEnumerable<Card> cards)
    {
        if (!cardManager.IsManagerReady() || cards == null) return false;
        
        try
        {
            cardManager.ClearSelection();
            foreach (var card in cards.Where(c => c.IsValid()))
            {
                if (!card.TrySelect()) return false;
            }
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManagerExtensions] Card selection failed: {ex.Message}");
            return false;
        }
    }

    // === DECK MANAGER INTEGRATION ===
    
    /// <summary>
    /// Sichere Karte ziehen mit Manager-Integration
    /// INTEGRATION: Verwendet CoreExtensions.TryWithManager Pattern
    /// </summary>
    public static bool TryDrawCard(this DeckManager deck)
    {
        if (!deck.IsManagerReady() || deck.IsDeckEmpty) return false;
        
        return TryGetManager<CardManager>()?.TryWithManager<CardManager>(cm => 
        {
            if (!cm.IsHandFull)
            {
                var cardData = deck.DrawCard();
                if (cardData != null)
                {
                    cm.SpawnCard(cardData, null, true);
                }
            }
        }) == true;
    }

    // === SPELLCAST MANAGER INTEGRATION ===
    
    /// <summary>
    /// Kann Spells casten
    /// </summary>
    public static bool CanCastSpells(this SpellcastManager spellcast)
    {
        if (!spellcast.IsManagerReady()) return false;
        
        return TryGetManager<CombatManager>()?.CanPerformPlayerAction(PlayerActionType.CastSpell) == true;
    }
    
    /// <summary>
    /// Sichere Card-Processing für Spells
    /// </summary>
    public static bool TryProcessCards(this SpellcastManager spellcast, IEnumerable<Card> cards)
    {
        if (!spellcast.IsManagerReady() || cards == null) return false;
        
        var cardList = cards.Where(c => c.IsValid()).ToList();
        if (cardList.Count == 0) return false;
        
        try
        {
            spellcast.ProcessCardPlay(cardList);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManagerExtensions] Spell processing failed: {ex.Message}");
            return false;
        }
    }

    // === ENTITY MANAGER INTEGRATION ===
    
    /// <summary>
    /// Sichere Entity-Spawn für beliebige Manager
    /// PERFORMANCE: Generic method vermeidet Code-Duplikation
    /// </summary>
    public static T TrySpawnEntity<T>(this T manager, EntityAsset asset, Vector3 position = default) 
        where T : MonoBehaviour, IGameManager
    {
        if (!manager.IsManagerReady() || asset == null) return manager;
        
        try
        {
            switch (manager)
            {
                case EnemyManager em when asset.Type == EntityType.Enemy:
                    em.SpawnEnemy(asset, position);
                    break;
                case UnitManager um when asset.Type == EntityType.Unit:
                    um.SpawnUnit(asset, position);
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManagerExtensions] Entity spawn failed: {ex.Message}");
        }
        
        return manager;
    }
    
    /// <summary>
    /// Sichere Entity-Targeting
    /// </summary>
    public static bool TrySetTarget(this EnemyManager enemyManager, EntityBehaviour target)
    {
        if (!enemyManager.IsManagerReady() || !target.IsValidTarget()) return false;
        
        try
        {
            enemyManager.TargetEnemy(target);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManagerExtensions] Targeting failed: {ex.Message}");
            return false;
        }
    }

    // === LAYOUT MANAGER INTEGRATION ===
    
    /// <summary>
    /// Sichere Layout-Updates
    /// </summary>
    public static bool TryUpdateLayout(this HandLayoutManager layout)
    {
        if (!layout.IsManagerReady()) return false;
        
        try
        {
            layout.UpdateLayout();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManagerExtensions] Layout update failed: {ex.Message}");
            return false;
        }
    }

    // === GAME MANAGER INTEGRATION ===
    
    /// <summary>
    /// Alle kritischen Manager sind bereit
    /// PERFORMANCE: Batch-Check ohne mehrfache TryGetManager Aufrufe
    /// </summary>
    public static bool AreAllCriticalManagersReady(this GameManager gameManager)
    {
        if (!gameManager.IsManagerReady()) return false;
        
        var criticalManagers = new (System.Type type, bool ready)[]
        {
            (typeof(CardManager), TryGetManager<CardManager>()?.IsManagerReady() == true),
            (typeof(DeckManager), TryGetManager<DeckManager>()?.IsManagerReady() == true),
            (typeof(CombatManager), TryGetManager<CombatManager>()?.IsManagerReady() == true)
        };
        
        return criticalManagers.All(m => m.ready);
    }
    
    /// <summary>
    /// Sichere Combat-Initialisierung
    /// </summary>
    public static bool TryStartCombat(this GameManager gameManager)
    {
        if (!gameManager.AreAllCriticalManagersReady()) return false;
        
        return TryGetManager<CombatManager>()?.TryWithManager<CombatManager>(cm => cm.StartCombat()) == true;
    }

    // === DIAGNOSTICS & MONITORING ===
    
    /// <summary>
    /// Manager Status Dictionary für Debugging
    /// PERFORMANCE: Single pass durch alle Manager
    /// </summary>
    public static Dictionary<string, bool> GetManagerStatus()
    {
        return new Dictionary<string, bool>
        {
            ["CardManager"] = TryGetManager<CardManager>()?.IsManagerReady() == true,
            ["DeckManager"] = TryGetManager<DeckManager>()?.IsManagerReady() == true,
            ["CombatManager"] = TryGetManager<CombatManager>()?.IsManagerReady() == true,
            ["SpellcastManager"] = TryGetManager<SpellcastManager>()?.IsManagerReady() == true,
            ["EnemyManager"] = TryGetManager<EnemyManager>()?.IsManagerReady() == true,
            ["UnitManager"] = TryGetManager<UnitManager>()?.IsManagerReady() == true,
            ["HandLayoutManager"] = TryGetManager<HandLayoutManager>()?.IsManagerReady() == true,
            ["GameManager"] = TryGetManager<GameManager>()?.IsManagerReady() == true
        };
    }
    
    /// <summary>
    /// Detailed Manager Performance Logging
    /// USAGE: Für Debugging und Performance-Monitoring
    /// </summary>
    public static void LogManagerPerformance()
    {
        var status = GetManagerStatus();
        var ready = status.Count(kvp => kvp.Value);
        var total = status.Count;
        
        Debug.Log($"[ManagerExtensions] Manager Status: {ready}/{total} ready");
        
        foreach (var kvp in status.Where(s => !s.Value))
        {
            Debug.LogWarning($"[ManagerExtensions] {kvp.Key} not ready!");
        }
    }

    // === COMBAT ASSESSMENT INTEGRATION ===
    
    /// <summary>
    /// Combat Assessment mit Integration aller relevanten Manager
    /// INTEGRATION: Nutzt CombatExtensions ohne Duplikation
    /// </summary>
    public static CombatAssessment GetCombatAssessment(this CombatManager combat)
    {
        if (!combat.IsManagerReady()) 
            return new CombatAssessment { Difficulty = CombatDifficulty.None };
        
        return new CombatAssessment
        {
            Difficulty = combat.GetCombatDifficulty(),
            Situation = combat.GetCombatSituation()
        };
    }
    
    /// <summary>
    /// Smart Resource Recovery basierend auf Combat Situation
    /// INTEGRATION: Verwendet ResourceExtensions
    /// </summary>
    public static bool TryOptimalRecovery(this CombatManager combat, ResourceType type, int maxRecovery)
    {
        var resource = combat.GetResourceByType(type);
        if (!resource.IsValidResource()) return false;
        
        var resourceHealth = resource.GetResourceHealth();
        
        int optimalAmount = resourceHealth switch
        {
            ResourceHealth.Dead or ResourceHealth.Dying => maxRecovery,
            ResourceHealth.Critical => Mathf.Min(maxRecovery, resource.MaxValue / 2),
            ResourceHealth.Low => Mathf.Min(maxRecovery, resource.MaxValue / 3),
            _ => Mathf.Min(maxRecovery, resource.MaxValue / 4)
        };
        
        if (optimalAmount > 0)
        {
            return combat.TryModifyResource(type, optimalAmount);
        }
        
        return false;
    }

    // === BATCH OPERATIONS (Performance-optimiert für mehrere Operationen) ===
    
    /// <summary>
    /// Batch-Validation mehrerer Manager
    /// PERFORMANCE: Single pass, early exit bei ersten Fehler
    /// </summary>
    public static bool ValidateManagerChain(params System.Type[] managerTypes)
    {
        foreach (var type in managerTypes)
        {
            if (type == typeof(CardManager) && TryGetManager<CardManager>()?.IsManagerReady() != true) return false;
            if (type == typeof(DeckManager) && TryGetManager<DeckManager>()?.IsManagerReady() != true) return false;
            if (type == typeof(CombatManager) && TryGetManager<CombatManager>()?.IsManagerReady() != true) return false;
            if (type == typeof(SpellcastManager) && TryGetManager<SpellcastManager>()?.IsManagerReady() != true) return false;
            if (type == typeof(EnemyManager) && TryGetManager<EnemyManager>()?.IsManagerReady() != true) return false;
            if (type == typeof(UnitManager) && TryGetManager<UnitManager>()?.IsManagerReady() != true) return false;
            if (type == typeof(HandLayoutManager) && TryGetManager<HandLayoutManager>()?.IsManagerReady() != true) return false;
            if (type == typeof(GameManager) && TryGetManager<GameManager>()?.IsManagerReady() != true) return false;
        }
        return true;
    }
}

// === SUPPORTING CLASSES ===

/// <summary>
/// Combat Assessment Data Container
/// INTEGRATION: Kombiniert CombatExtensions Daten ohne Duplikation
/// </summary>
[System.Serializable]
public class CombatAssessment
{
    public CombatDifficulty Difficulty { get; set; }
    public CombatSituation Situation { get; set; }
    
    public bool IsInCrisis => Difficulty >= CombatDifficulty.Hard || Situation.UrgencyLevel >= UrgencyLevel.High;
    public bool RequiresImmediateAction => Situation.UrgencyLevel == UrgencyLevel.Critical;
    public bool CanActSafely => Situation.CanAct && !IsInCrisis;
}