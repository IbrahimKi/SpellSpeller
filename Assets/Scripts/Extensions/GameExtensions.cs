using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// FIXED: Using Aliases für ambiguous references
using UnityRandom = UnityEngine.Random;

/// <summary>
/// GameExtensions - ALLE anderen Extensions (Card, Entity, Manager Utils)
/// Keine Überschneidungen mit CoreExtensions
/// </summary>
public static class GameExtensions
{
    // === UNIVERSAL NULL SAFETY ===
    public static bool IsValid(this object obj) 
        => obj != null && (!(obj is UnityEngine.Object uObj) || uObj != null);
    
    public static bool IsActive(this GameObject obj) 
        => obj.IsValid() && obj.activeInHierarchy;
    
    public static bool IsActive(this Component comp) 
        => comp.IsValid() && comp.gameObject.IsActive();

    // === MANAGER ACCESS UTILS ===
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

    // === MANAGER VALIDATION (GameManager specific) ===
    public static bool AreAllCriticalManagersReady()
    {
        return CardManager.HasInstance && CardManager.Instance.IsReady &&
               DeckManager.HasInstance && DeckManager.Instance.IsReady &&
               CombatManager.HasInstance && CombatManager.Instance.IsReady &&
               SpellcastManager.HasInstance && SpellcastManager.Instance.IsReady;
    }

    public static void TryStartCombat()
    {
        TryManager<CombatManager>(cm => cm.StartCombat());
    }

    public static void LogManagerPerformance()
    {
        Debug.Log("[GameExtensions] Manager Performance Status:");
        LogManagerStatus<CardManager>("CardManager");
        LogManagerStatus<DeckManager>("DeckManager");
        LogManagerStatus<CombatManager>("CombatManager");
        LogManagerStatus<SpellcastManager>("SpellcastManager");
        LogManagerStatus<EnemyManager>("EnemyManager");
        LogManagerStatus<UnitManager>("UnitManager");
        LogManagerStatus<HandLayoutManager>("HandLayoutManager");
        LogManagerStatus<CardSlotManager>("CardSlotManager");
    }

    private static void LogManagerStatus<T>(string name) where T : SingletonBehaviour<T>
    {
        bool hasInstance = SingletonBehaviour<T>.HasInstance;
        bool isReady = hasInstance && SingletonBehaviour<T>.Instance is IGameManager gm && gm.IsReady;
        Debug.Log($"  {name}: Instance={hasInstance}, Ready={isReady}");
    }

    // === CARD ESSENTIALS (30 Zeilen statt 400) ===
    
    public static string GetLetters(this Card card) 
        => card?.CardData?.letterValues ?? "";
    
    public static string GetLetterSequence(this IEnumerable<Card> cards) 
        => string.Concat(cards?.Where(c => c.IsPlayable()).Select(c => c.GetLetters()) ?? System.Linq.Enumerable.Empty<string>());
    
    public static bool CanBuildSpell(this IEnumerable<Card> cards, string spellCode)
    {
        if (string.IsNullOrEmpty(spellCode)) return false;
        var available = cards.GetLetterSequence();
        return spellCode.All(letter => available.Count(c => c == letter) >= spellCode.Count(sc => sc == letter));
    }
    
    public static bool IsPlayable(this Card card) 
        => card.IsValid() && card.IsInteractable && card.CardData != null;

    public static string GetCardName(this Card card)
        => card?.CardData?.cardName ?? "Unknown";

    // === FEHLENDE CARD EXTENSIONS ===
    
    // Collection Extensions
    public static IEnumerable<Card> GetValidCards(this IEnumerable<Card> cards)
        => cards?.Where(c => c.IsValid()) ?? System.Linq.Enumerable.Empty<Card>();

    public static int GetValidCardCount(this IEnumerable<Card> cards)
        => cards?.Count(c => c.IsValid()) ?? 0;

    public static bool HasValidCards(this IEnumerable<Card> cards)
        => cards?.Any(c => c.IsValid()) ?? false;

    public static bool HasPlayableCards(this IEnumerable<Card> cards)
        => cards?.Any(c => c.IsPlayable()) ?? false;

    // FEHLENDE METHODEN FÜR CARDMANAGER
    public static SpellBuildingPotential GetSpellBuildingPotential(this IEnumerable<Card> cards)
    {
        var potential = new SpellBuildingPotential();
        var validCards = cards.GetValidCards().ToList();
        
        if (validCards.Count == 0)
        {
            potential.OverallScore = 0f;
            return potential;
        }

        var letterCounts = new Dictionary<char, int>();
        foreach (var card in validCards)
        {
            foreach (char letter in card.GetLetters())
            {
                letterCounts[letter] = letterCounts.GetValueOrDefault(letter, 0) + 1;
            }
        }

        // Basic scoring
        potential.UniqueLetters = letterCounts.Keys.Count;
        potential.TotalLetters = letterCounts.Values.Sum();
        potential.VowelCount = letterCounts.Where(kvp => "AEIOU".Contains(kvp.Key)).Sum(kvp => kvp.Value);
        potential.ConsonantCount = potential.TotalLetters - potential.VowelCount;
        
        // Calculate spell building potential (simplified)
        float vowelRatio = potential.TotalLetters > 0 ? (float)potential.VowelCount / potential.TotalLetters : 0f;
        float diversityScore = potential.TotalLetters > 0 ? (float)potential.UniqueLetters / potential.TotalLetters : 0f;
        
        potential.OverallScore = (vowelRatio * 0.3f + diversityScore * 0.7f) * Mathf.Min(1f, potential.TotalLetters / 10f);
        
        return potential;
    }

    public static IEnumerable<Card> FindCardsForSpell(this IEnumerable<Card> cards, string spellCode)
    {
        if (string.IsNullOrEmpty(spellCode)) return System.Linq.Enumerable.Empty<Card>();
        
        var neededLetters = spellCode.ToCharArray().ToList();
        var result = new List<Card>();
        var availableCards = cards.GetValidCards().ToList();

        foreach (char letter in neededLetters)
        {
            var cardWithLetter = availableCards.FirstOrDefault(c => c.GetLetters().Contains(letter));
            if (cardWithLetter != null)
            {
                result.Add(cardWithLetter);
                availableCards.Remove(cardWithLetter);
            }
        }

        return result;
    }

    public static CollectionLetterAnalysis GetCollectionLetterAnalysis(this IEnumerable<Card> cards)
    {
        var analysis = new CollectionLetterAnalysis();
        var validCards = cards.GetValidCards().ToList();
        
        analysis.TotalCards = validCards.Count;
        
        if (validCards.Count == 0) return analysis;

        var allLetters = validCards.SelectMany(c => c.GetLetters()).ToList();
        analysis.TotalLetters = allLetters.Count;
        analysis.UniqueLetters = allLetters.Distinct().Count();
        analysis.Vowels = allLetters.Count(c => "AEIOU".Contains(c));
        analysis.Consonants = analysis.TotalLetters - analysis.Vowels;

        return analysis;
    }

    public static IEnumerable<Card> SortBy(this IEnumerable<Card> cards, CardSortCriteria criteria)
    {
        return criteria switch
        {
            CardSortCriteria.Name => cards.OrderBy(c => c.GetCardName()),
            CardSortCriteria.Tier => cards.OrderBy(c => c.GetTier()),
            CardSortCriteria.Type => cards.OrderBy(c => c.GetCardType()),
            CardSortCriteria.LetterCount => cards.OrderByDescending(c => c.GetLetters().Length),
            _ => cards
        };
    }

    public static IEnumerable<Card> FilterByType(this IEnumerable<Card> cards, CardType cardType)
        => cards.GetValidCards().Where(c => c.GetCardType() == cardType);

    public static IEnumerable<Card> FilterByTier(this IEnumerable<Card> cards, int tier)
        => cards.GetValidCards().Where(c => c.GetTier() == tier);

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
    
    // FIXED: GetWeakest, GetRandom mit safer implementation
    public static EntityBehaviour GetWeakest(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e != null && e.IsValidTarget()).OrderBy(e => e.CurrentHealth).FirstOrDefault();
    
    public static EntityBehaviour GetStrongest(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e != null && e.IsValidTarget()).OrderByDescending(e => e.CurrentHealth).FirstOrDefault();
    
    public static EntityBehaviour GetRandom(this IEnumerable<EntityBehaviour> entities)
    {
        var valid = entities?.Where(e => e != null && e.IsValidTarget()).ToList();
        // FIXED: UnityRandom instead of System.Random
        return valid?.Count > 0 ? valid[UnityRandom.Range(0, valid.Count)] : null;
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

    // === MANAGER EXTENSIONS ===
    public static bool AreAllCriticalManagersReady(this GameManager gameManager)
    {
        return CardManager.HasInstance && CardManager.Instance.IsReady &&
               DeckManager.HasInstance && DeckManager.Instance.IsReady &&
               CombatManager.HasInstance && CombatManager.Instance.IsReady &&
               SpellcastManager.HasInstance && SpellcastManager.Instance.IsReady;
    }

    public static void TryStartCombat(this GameManager gameManager)
    {
        TryManager<CombatManager>(cm => cm.StartCombat());
    }

    public static T TryGetManager<T>() where T : SingletonBehaviour<T>
        => SingletonBehaviour<T>.HasInstance ? SingletonBehaviour<T>.Instance : null;

    public static void LogManagerPerformance()
    {
        Debug.Log("[GameExtensions] Manager Performance Status:");
        LogManagerStatus<CardManager>("CardManager");
        LogManagerStatus<DeckManager>("DeckManager");
        LogManagerStatus<CombatManager>("CombatManager");
        LogManagerStatus<SpellcastManager>("SpellcastManager");
        LogManagerStatus<EnemyManager>("EnemyManager");
        LogManagerStatus<UnitManager>("UnitManager");
        LogManagerStatus<HandLayoutManager>("HandLayoutManager");
        LogManagerStatus<CardSlotManager>("CardSlotManager");
    }

    private static void LogManagerStatus<T>(string name) where T : SingletonBehaviour<T>
    {
        bool hasInstance = SingletonBehaviour<T>.HasInstance;
        bool isReady = hasInstance && SingletonBehaviour<T>.Instance is IGameManager gm && gm.IsReady;
        Debug.Log($"  {name}: Instance={hasInstance}, Ready={isReady}");
    }
}

// === SUPPORT CLASSES ===
[System.Serializable]
public class SpellBuildingPotential
{
    public int TotalLetters;
    public int UniqueLetters;
    public int VowelCount;
    public int ConsonantCount;
    public float OverallScore;
}

[System.Serializable]
public class CollectionLetterAnalysis
{
    public int TotalCards;
    public int TotalLetters;
    public int UniqueLetters;
    public int Vowels;
    public int Consonants;
}

public enum CardSortCriteria
{
    Name,
    Tier,
    Type,
    LetterCount
}