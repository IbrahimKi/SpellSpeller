using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class ManagerExtensions
{
    public static bool IsManagerReady<T>(this T manager) where T : MonoBehaviour, IGameManager
        => manager != null && !manager.Equals(null) && manager.IsReady;
    
    public static bool IsManagerReady<T>(this SingletonBehaviour<T> manager) where T : MonoBehaviour
    {
        if (manager == null || manager.Equals(null)) return false;
        return manager is IGameManager gameManager ? gameManager.IsReady : true;
    }
    
    public static T TryGetManager<T>() where T : SingletonBehaviour<T>
        => SingletonBehaviour<T>.HasInstance ? SingletonBehaviour<T>.Instance : null;
    
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
    
    public static bool IsInValidCombatState(this CombatManager combat)
        => combat.IsManagerReady() && combat.IsInCombat && combat.Life.CurrentValue > 0;
    
    public static bool CanSpendResource(this CombatManager combat, ResourceType type, int amount)
    {
        if (!combat.IsManagerReady()) return false;
        
        var resource = combat.GetResourceByType(type);
        if (resource == null) return false;
        
        return resource.HasAvailable(amount) && !resource.IsInCriticalState();
    }
    
    public static bool TryModifyResource(this CombatManager combat, ResourceType type, int delta)
    {
        if (!combat.IsManagerReady()) return false;
        
        var resource = combat.GetResourceByType(type);
        if (resource == null) return false;
        
        try
        {
            if (delta < 0)
            {
                var cost = new ResourceCost { ResourceType = type, Amount = -delta };
                return resource.TryApplyCost(cost);
            }
            else
            {
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
    
    public static ResourcePortfolio GetResourcePortfolio(this CombatManager combat, IEnumerable<ResourceCost> plannedCosts = null)
    {
        if (!combat.IsManagerReady()) return new ResourcePortfolio();
        
        var resources = new[] { combat.Life, combat.Creativity }.Where(r => r != null);
        return resources.OptimizePortfolio(plannedCosts ?? Enumerable.Empty<ResourceCost>());
    }
    
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
    
    public static IEnumerable<Resource> GetAllResources(this CombatManager combat)
    {
        if (!combat.IsManagerReady()) yield break;
        
        if (combat.Life != null) yield return combat.Life;
        if (combat.Creativity != null) yield return combat.Creativity;
    }
    
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
    
    public static bool CanDrawCard(this CardManager cardManager)
    {
        if (!cardManager.IsManagerReady()) return false;
        
        return !cardManager.IsHandFull && 
               TryWithManager<DeckManager, bool>(dm => !dm.IsDeckEmpty) &&
               TryWithManager<CombatManager, bool>(cm => cm.IsPlayerTurn);
    }
    
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
    
    public static bool CanCastSpells(this SpellcastManager spellcast)
    {
        if (!spellcast.IsManagerReady()) return false;
        
        return TryWithManager<CombatManager, bool>(cm => cm.CanPerformPlayerAction(PlayerActionType.CastSpell));
    }
    
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
    
    public static bool TryStartCombat(this GameManager gameManager)
    {
        if (!gameManager.AreAllCriticalManagersReady()) return false;
        
        return TryWithManager<CombatManager>(cm => cm.StartCombat());
    }
    
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

public enum EntityType
{
    Unit,
    Enemy,
    Neutral
}

public enum TurnPhase
{
    PlayerTurn,
    EnemyTurn,
    Setup,
    Cleanup
}

public enum ComboState
{
    None,
    Building,
    Ready,
    Processing
}