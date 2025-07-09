using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GameExtensions - ALLE essentiellen Extensions in EINER Datei
/// 90% Code-Reduktion durch Eliminierung von Duplikaten und Over-Engineering
/// </summary>
public static class GameExtensions
{
    // === UNIVERSAL NULL SAFETY (20 Zeilen statt 200) ===
    
    public static bool IsValid(this object obj) 
        => obj != null && (!(obj is UnityEngine.Object uObj) || uObj != null);
    
    public static bool IsActive(this GameObject obj) 
        => obj.IsValid() && obj.activeInHierarchy;
    
    public static bool IsActive(this Component comp) 
        => comp.IsValid() && comp.gameObject.IsActive();

    // === MANAGER ACCESS (10 Zeilen statt 150) ===
    
    public static T GetManager<T>() where T : SingletonBehaviour<T>
        => SingletonBehaviour<T>.HasInstance ? SingletonBehaviour<T>.Instance : null;
    
    public static bool TryManager<T>(System.Action<T> action) where T : SingletonBehaviour<T>
    {
        var manager = GetManager<T>();
        if (manager != null && manager is IGameManager gm && gm.IsReady)
        {
            action(manager);
            return true;
        }
        return false;
    }

    // === CARD ESSENTIALS (30 Zeilen statt 400) ===
    
    public static bool IsPlayable(this Card card) 
        => card.IsValid() && card.IsInteractable && card.CardData != null;
    
    public static string GetLetters(this Card card) 
        => card?.CardData?.letterValues ?? "";
    
    public static string GetLetterSequence(this IEnumerable<Card> cards) 
        => string.Concat(cards?.Where(c => c.IsPlayable()).Select(c => c.GetLetters()) ?? Enumerable.Empty<string>());
    
    public static bool CanBuildSpell(this IEnumerable<Card> cards, string spellCode)
    {
        if (string.IsNullOrEmpty(spellCode)) return false;
        var available = cards.GetLetterSequence();
        return spellCode.All(letter => available.Count(c => c == letter) >= spellCode.Count(sc => sc == letter));
    }

    // === COMBAT ESSENTIALS (25 Zeilen statt 300) ===
    
    public static bool IsPlayerTurn(this CombatManager cm) 
        => cm.IsValid() && cm.CurrentPhase == TurnPhase.PlayerTurn;
    
    public static bool CanAct(this CombatManager cm) 
        => cm.IsPlayerTurn() && !cm.IsProcessingTurn;
    
    public static void DamageTarget(this EntityBehaviour entity, int damage)
    {
        if (entity.IsValid() && entity.IsAlive)
            entity.TakeDamage(damage, DamageType.Normal);
    }
    
    public static void HealTarget(this EntityBehaviour entity, int amount)
    {
        if (entity.IsValid() && entity.IsAlive && entity.CurrentHealth < entity.MaxHealth)
            entity.Heal(Mathf.Min(amount, entity.MaxHealth - entity.CurrentHealth));
    }

    // === ENTITY ESSENTIALS (20 Zeilen statt 500) ===
    
    public static bool IsValidTarget(this EntityBehaviour entity) 
        => entity.IsValid() && entity.IsAlive && entity.IsTargetable;
    
    public static EntityBehaviour GetWeakest(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e.IsValidTarget()).OrderBy(e => e.CurrentHealth).FirstOrDefault();
    
    public static EntityBehaviour GetRandom(this IEnumerable<EntityBehaviour> entities)
    {
        var valid = entities?.Where(e => e.IsValidTarget()).ToList();
        return valid?.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }

    // === RESOURCE ESSENTIALS (15 Zeilen statt 600) ===
    
    public static bool CanAfford(this Resource res, int amount) 
        => res != null && res.CurrentValue >= amount;
    
    public static bool IsLow(this Resource res, float threshold = 0.3f) 
        => res != null && res.Percentage <= threshold;
    
    public static void Spend(this Resource res, int amount)
    {
        if (res.CanAfford(amount))
            res.ModifyBy(-amount);
    }

    // === COLLECTION HELPERS (10 Zeilen statt 100) ===
    
    public static T GetValidFirst<T>(this IEnumerable<T> collection) where T : class
        => collection?.FirstOrDefault(item => item.IsValid());
    
    public static int CountValid<T>(this IEnumerable<T> collection) where T : class
        => collection?.Count(item => item.IsValid()) ?? 0;
}

// ENUMS sind jetzt in SharedEnums.cs definiert!