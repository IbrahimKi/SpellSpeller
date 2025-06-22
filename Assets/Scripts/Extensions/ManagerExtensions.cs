using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manager Extensions für sichere Singleton-Operations und häufige Manager-Checks
/// Eliminiert 90% der HasInstance checks und bietet intelligente Manager-Validation
/// 
/// USAGE:
/// - manager.IsManagerReady() statt HasInstance && Instance.IsReady
/// - CombatManager.Instance.CanPerformPlayerAction() statt komplexe turn checks
/// - manager.TryGetManager<T>() für sichere Manager-Access
/// </summary>
public static class ManagerExtensions
{
    // ===========================================
    // SINGLETON SAFETY EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Sichere Prüfung ob Manager bereit ist (ersetzt HasInstance && IsReady)
    /// </summary>
    public static bool IsManagerReady<T>(this T manager) where T : MonoBehaviour, IGameManager
        => manager != null && !manager.Equals(null) && manager.IsReady;
    
    /// <summary>
    /// Sichere Prüfung für SingletonBehaviour Manager
    /// </summary>
    public static bool IsManagerReady<T>(this SingletonBehaviour<T> manager) where T : MonoBehaviour
    {
        if (manager == null || manager.Equals(null)) return false;
        return manager is IGameManager gameManager ? gameManager.IsReady : true;
    }
    
    /// <summary>
    /// Versucht Manager zu holen, falls verfügbar
    /// </summary>
    public static T TryGetManager<T>() where T : SingletonBehaviour<T>
        => SingletonBehaviour<T>.HasInstance ? SingletonBehaviour<T>.Instance : null;
    
    /// <summary>
    /// Sichere Manager-Operation mit Callback
    /// </summary>
    public static bool TryWithManager<T>(System.Action<T> action) where T : SingletonBehaviour<T>
    {
        var manager = TryGetManager<T>();
        if (manager != null && manager.IsManagerReady())
        {
            try
            {
                action(manager);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ManagerExtensions] Error in {typeof(T).Name}: {ex.Message}");
                return false;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Sichere Manager-Operation mit Return Value
    /// </summary>
    public static TResult TryWithManager<T, TResult>(System.Func<T, TResult> func, TResult defaultValue = default) where T : SingletonBehaviour<T>
    {
        var manager = TryGetManager<T>();
        if (manager != null && manager.IsManagerReady())
        {
            try
            {
                return func(manager);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ManagerExtensions] Error in {typeof(T).Name}: {ex.Message}");
                return defaultValue;
            }
        }
        return defaultValue;
    }
    
    // ===========================================
    // COMBAT MANAGER EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Intelligente Combat Action Validation
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
            PlayerActionType.DrawCard => TryWithManager<CardManager, bool>(cm => !cm.IsHandFull) &&
                                        TryWithManager<DeckManager, bool>(dm => !dm.IsDeckEmpty),
            PlayerActionType.EndTurn => combat.CanEndTurn,
            PlayerActionType.SpendCreativity => combat.Creativity.CurrentValue > 0,
            PlayerActionType.CastSpell => TryWithManager<SpellcastManager, bool>(sm => sm.CanCastCombo),
            _ => true
        };
    }
    
    /// <summary>
    /// Erweiterte Combat State Checks
    /// </summary>
    public static bool IsInValidCombatState(this CombatManager combat)
        => combat.IsManagerReady() && combat.IsInCombat && combat.Life.CurrentValue > 0;
    
    /// <summary>
    /// ENHANCED: Kann Ressource ausgeben? (now uses ResourceExtensions)
    /// </summary>
    public static bool CanSpendResource(this CombatManager combat, ResourceType type, int amount)
    {
        if (!combat.IsManagerReady()) return false;
        
        var resource = combat.GetResourceByType(type);
        if (resource == null) return false;
        
        // INTEGRATION: Use ResourceExtensions for advanced validation
        return resource.HasAvailable(amount) && !resource.IsInCriticalState();
    }
    
    /// <summary>
    /// ENHANCED: Sichere Resource Modification mit ResourceExtensions
    /// </summary>
    public static bool TryModifyResource(this CombatManager combat, ResourceType type, int delta)
    {
        if (!combat.IsManagerReady()) return false;
        
        var resource = combat.GetResourceByType(type);
        if (resource == null) return false;
        
        try
        {
            // INTEGRATION: Use ResourceExtensions for cost validation if spending
            if (delta < 0)
            {
                var cost = new ResourceCost { ResourceType = type, Amount = -delta };
                return resource.TryApplyCost(cost);
            }
            else
            {
                // Direct modification for gains
                resource.ModifyBy(delta);
                return true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManagerExtensions] Resource modification failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// INTEGRATION: Get Resource by Type helper
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
    
    /// <summary>
    /// INTEGRATION: Enhanced Resource Planning with ResourceExtensions
    /// </summary>
    public static ResourcePortfolio GetResourcePortfolio(this CombatManager combat, IEnumerable<ResourceCost> plannedCosts = null)
    {
        if (!combat.IsManagerReady()) return new ResourcePortfolio();
        
        var resources = new[] { combat.Life, combat.Creativity }.Where(r => r != null);
        return resources.OptimizePortfolio(plannedCosts ?? Enumerable.Empty<ResourceCost>());
    }
    
    /// <summary>
    /// INTEGRATION: Smart Resource Recovery using ResourceExtensions
    /// </summary>
    public static bool TryOptimalRecovery(this CombatManager combat, ResourceType type, int maxRecovery)
    {
        var resource = combat.GetResourceByType(type);
        if (resource == null) return false;
        
        int optimalAmount = resource.GetOptimalRecovery(maxRecovery);
        if (optimalAmount > 0)
        {
            resource.ModifyBy(optimalAmount);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// INTEGRATION: Get all resources for portfolio analysis
    /// </summary>
    public static IEnumerable<Resource> GetAllResources(this CombatManager combat)
    {
        if (!combat.IsManagerReady()) yield break;
        
        if (combat.Life != null) yield return combat.Life;
        if (combat.Creativity != null) yield return combat.Creativity;
    }
    
    // ===========================================
    // CARD MANAGER EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Sichere Hand-Operations
    /// </summary>
    public static bool TryDrawCard(this DeckManager deck)
    {
        if (!deck.IsManagerReady() || deck.IsDeckEmpty) return false;
        
        return TryWithManager<CardManager>(cm => 
        {
            if (!cm.IsHandFull)
            {
                var cardData = deck.DrawCard();
                if (cardData != null)
                {
                    cm.SpawnCard(cardData, null, true);
                }
            }
        });
    }
    
    /// <summary>
    /// Kann Card draw performed werden?
    /// </summary>
    public static bool CanDrawCard(this CardManager cardManager)
    {
        if (!cardManager.IsManagerReady()) return false;
        
        return !cardManager.IsHandFull && 
               TryWithManager<DeckManager, bool>(dm => !dm.IsDeckEmpty) &&
               TryWithManager<CombatManager, bool>(cm => cm.IsPlayerTurn);
    }
    
    /// <summary>
    /// Sichere Card Selection Operations
    /// </summary>
    public static bool TrySelectCards(this CardManager cardManager, IEnumerable<Card> cards)
    {
        if (!cardManager.IsManagerReady() || cards == null) return false;
        
        try
        {
            cardManager.ClearSelection();
            foreach (var card in cards.Where(c => c != null && c.IsValid()))
            {
                card.Select();
            }
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManagerExtensions] Card selection failed: {ex.Message}");
            return false;
        }
    }
    
    // ===========================================
    // SPELLCAST MANAGER EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Kann Spell gecastet werden?
    /// </summary>
    public static bool CanCastSpells(this SpellcastManager spellcast)
    {
        if (!spellcast.IsManagerReady()) return false;
        
        return TryWithManager<CombatManager, bool>(cm => cm.CanPerformPlayerAction(PlayerActionType.CastSpell));
    }
    
    /// <summary>
    /// Sichere Spell Execution
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
    
    // ===========================================
    // ENEMY/UNIT MANAGER EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Sichere Entity Spawning
    /// </summary>
    public static T TrySpawnEntity<T>(this T manager, EntityAsset asset, Vector3 position = default) 
        where T : MonoBehaviour, IGameManager
    {
        if (!manager.IsManagerReady() || asset == null) return manager;
        
        try
        {
            if (manager is EnemyManager em && asset.Type == EntityType.Enemy)
                em.SpawnEnemy(asset, position);
            else if (manager is UnitManager um && asset.Type == EntityType.Unit)
                um.SpawnUnit(asset, position);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManagerExtensions] Entity spawn failed: {ex.Message}");
        }
        
        return manager;
    }
    
    /// <summary>
    /// Sichere Target Operations
    /// </summary>
    public static bool TrySetTarget(this EnemyManager enemyManager, EntityBehaviour target)
    {
        if (!enemyManager.IsManagerReady() || target == null) return false;
        
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
    
    // ===========================================
    // LAYOUT MANAGER EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Sichere Layout Updates
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
    
    // ===========================================
    // GAME MANAGER EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Prüft ob alle kritischen Manager bereit sind
    /// </summary>
    public static bool AreAllCriticalManagersReady(this GameManager gameManager)
    {
        if (!gameManager.IsManagerReady()) return false;
        
        var critical = new[]
        {
            TryGetManager<CardManager>()?.IsManagerReady() ?? false,
            TryGetManager<DeckManager>()?.IsManagerReady() ?? false,
            TryGetManager<CombatManager>()?.IsManagerReady() ?? false
        };
        
        return critical.All(ready => ready);
    }
    
    /// <summary>
    /// Sichere Game State Operations
    /// </summary>
    public static bool TryStartCombat(this GameManager gameManager)
    {
        if (!gameManager.AreAllCriticalManagersReady()) return false;
        
        return TryWithManager<CombatManager>(cm => cm.StartCombat());
    }
    
    // ===========================================
    // UTILITY EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Batch Manager Readiness Check
    /// </summary>
    public static Dictionary<string, bool> GetManagerStatus()
    {
        return new Dictionary<string, bool>
        {
            ["CardManager"] = TryGetManager<CardManager>()?.IsManagerReady() ?? false,
            ["DeckManager"] = TryGetManager<DeckManager>()?.IsManagerReady() ?? false,
            ["CombatManager"] = TryGetManager<CombatManager>()?.IsManagerReady() ?? false,
            ["SpellcastManager"] = TryGetManager<SpellcastManager>()?.IsManagerReady() ?? false,
            ["EnemyManager"] = TryGetManager<EnemyManager>()?.IsManagerReady() ?? false,
            ["UnitManager"] = TryGetManager<UnitManager>()?.IsManagerReady() ?? false,
            ["HandLayoutManager"] = TryGetManager<HandLayoutManager>()?.IsManagerReady() ?? false,
            ["GameManager"] = TryGetManager<GameManager>()?.IsManagerReady() ?? false
        };
    }
    
    /// <summary>
    /// Performance Logging für Manager Status
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
}

// ===========================================
// SUPPORTING ENUMS
// ===========================================

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

public enum ResourceType
{
    Life,
    Creativity,
    Energy,
    Mana
}

// ===========================================
// VERWENDUNGS-BEISPIELE
// ===========================================

/*
// VORHER-NACHHER VERGLEICHE:

// VORHER (12 Zeilen):
bool isPlayerTurn = CombatManager.HasInstance && 
                   CombatManager.Instance.IsReady &&
                   CombatManager.Instance.IsInCombat && 
                   CombatManager.Instance.IsPlayerTurn &&
                   !CombatManager.Instance.IsProcessingTurn;
                   
bool hasSelectedCards = CardManager.HasInstance && 
                       CardManager.Instance.IsReady &&
                       CardManager.Instance.SelectedCards?.Count > 0;

if (playButton != null) 
    playButton.interactable = hasSelectedCards && isPlayerTurn;

// NACHHER (3 Zeilen):
bool canPlay = CombatManager.Instance.CanPerformPlayerAction(PlayerActionType.PlayCards);
bool hasCards = CardManager.Instance.SelectedCards.HasValidCards();
if (playButton != null) playButton.interactable = canPlay && hasCards;

// ===================================

// MANAGER OPERATIONS:

// VORHER:
if (CardManager.HasInstance && CardManager.Instance.IsReady)
{
    if (DeckManager.HasInstance && !DeckManager.Instance.IsDeckEmpty)
    {
        var card = DeckManager.Instance.DrawCard();
        if (card != null)
            CardManager.Instance.SpawnCard(card, null, true);
    }
}

// NACHHER:
DeckManager.Instance.TryDrawCard();

// ===================================

// UI BUTTON STATES:

// VORHER:
public void UpdateButtons()
{
    bool canDraw = CardManager.HasInstance && 
                   CardManager.Instance.IsReady &&
                   !CardManager.Instance.IsHandFull &&
                   DeckManager.HasInstance &&
                   DeckManager.Instance.IsReady &&
                   !DeckManager.Instance.IsDeckEmpty &&
                   CombatManager.HasInstance &&
                   CombatManager.Instance.IsPlayerTurn;
    
    drawButton.interactable = canDraw;
}

// NACHHER:
public void UpdateButtons()
{
    drawButton.interactable = CardManager.Instance.CanDrawCard();
}

// ===================================

// SAFE OPERATIONS:

// VORHER:
try
{
    if (SpellcastManager.HasInstance && SpellcastManager.Instance.IsReady)
    {
        SpellcastManager.Instance.ProcessCardPlay(selectedCards);
    }
}
catch (Exception ex) { Debug.LogError(ex); }

// NACHHER:
SpellcastManager.Instance.TryProcessCards(selectedCards);

// ===================================

// BATCH CHECKS:

// VORHER:
bool allReady = CardManager.HasInstance && CardManager.Instance.IsReady &&
                DeckManager.HasInstance && DeckManager.Instance.IsReady &&
                CombatManager.HasInstance && CombatManager.Instance.IsReady;

// NACHHER:
bool allReady = GameManager.Instance.AreAllCriticalManagersReady();

// ===================================

// RESOURCE OPERATIONS:

// VORHER:
if (CombatManager.HasInstance && 
    CombatManager.Instance.Creativity.CurrentValue >= cost)
{
    CombatManager.Instance.ModifyCreativity(-cost);
}

// NACHHER:
CombatManager.Instance.TryModifyResource(ResourceType.Creativity, -cost);
*/