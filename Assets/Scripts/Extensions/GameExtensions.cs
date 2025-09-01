using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityRandom = UnityEngine.Random;

/// <summary>
/// GameExtensions - GAME LOGIC Extensions
/// Keine Duplikationen mit Core/Entity Extensions
/// </summary>
public static class GameExtensions
{
    // === GENERAL OBJECT VALIDATION (andere Signatur als Core) ===
    public static bool IsValid(this object obj) 
        => obj != null && (!(obj is UnityEngine.Object uObj) || uObj != null);
    
    // === MANAGER UTILITIES - NUR für GameExtensions ===
    public static bool TryGameManager<T>(System.Action<T> action) where T : SingletonBehaviour<T>
    {
        var manager = CoreExtensions.GetManager<T>();
        if (manager != null && manager is IGameManager gm && gm.IsReady)
        {
            action(manager);
            return true;
        }
        return false;
    }

    // === MANAGER SYSTEM CHECKS ===
    public static bool AreAllCriticalManagersReady()
        => CoreExtensions.IsManagerReady<CardManager>() &&
           CoreExtensions.IsManagerReady<DeckManager>() &&
           CoreExtensions.IsManagerReady<CombatManager>() &&
           CoreExtensions.IsManagerReady<SpellcastManager>();

    public static void TryStartCombat()
    {
        CoreExtensions.TryWithManagerStatic<CombatManager>(cm => cm.StartCombat());
    }

    // === CARD SYSTEM EXTENSIONS ===
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
    
    public static bool IsPlayable(this Card card) 
        => card.IsValid() && card.IsInteractable && card.CardData != null;

    public static string GetCardName(this Card card)
        => card?.CardData?.cardName ?? "Unknown";
    
    public static CardType GetCardType(this Card card)
        => card?.CardData?.cardType ?? CardType.Special;
        
    public static int GetTier(this Card card)
        => card?.CardData?.tier ?? 0;

    // === CARD COLLECTION EXTENSIONS ===
    public static IEnumerable<Card> GetValidCards(this IEnumerable<Card> cards)
        => cards?.Where(c => c.IsValid()) ?? Enumerable.Empty<Card>();

    public static int GetValidCardCount(this IEnumerable<Card> cards)
        => cards?.Count(c => c.IsValid()) ?? 0;

    public static bool HasValidCards(this IEnumerable<Card> cards)
        => cards?.Any(c => c.IsValid()) ?? false;

    public static bool HasPlayableCards(this IEnumerable<Card> cards)
        => cards?.Any(c => c.IsPlayable()) ?? false;

    public static IEnumerable<Card> FilterByType(this IEnumerable<Card> cards, CardType cardType)
        => cards.GetValidCards().Where(c => c.GetCardType() == cardType);

    public static IEnumerable<Card> FilterByTier(this IEnumerable<Card> cards, int tier)
        => cards.GetValidCards().Where(c => c.GetTier() == tier);

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

    // === SPELL BUILDING ANALYSIS ===
    public static SpellBuildingPotential GetSpellBuildingPotential(this IEnumerable<Card> cards)
    {
        var potential = new SpellBuildingPotential();
        var validCards = cards.GetValidCards().ToList();
        
        if (validCards.Count == 0) return potential;

        var letterCounts = new Dictionary<char, int>();
        foreach (var card in validCards)
        {
            foreach (char letter in card.GetLetters())
                letterCounts[letter] = letterCounts.GetValueOrDefault(letter, 0) + 1;
        }

        potential.UniqueLetters = letterCounts.Keys.Count;
        potential.TotalLetters = letterCounts.Values.Sum();
        potential.VowelCount = letterCounts.Where(kvp => "AEIOU".Contains(kvp.Key)).Sum(kvp => kvp.Value);
        potential.ConsonantCount = potential.TotalLetters - potential.VowelCount;
        
        float vowelRatio = potential.TotalLetters > 0 ? (float)potential.VowelCount / potential.TotalLetters : 0f;
        float diversityScore = potential.TotalLetters > 0 ? (float)potential.UniqueLetters / potential.TotalLetters : 0f;
        potential.OverallScore = (vowelRatio * 0.3f + diversityScore * 0.7f) * Mathf.Min(1f, potential.TotalLetters / 10f);
        
        return potential;
    }

    public static IEnumerable<Card> FindCardsForSpell(this IEnumerable<Card> cards, string spellCode)
    {
        if (string.IsNullOrEmpty(spellCode)) return Enumerable.Empty<Card>();
        
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

    // === COMBAT EXTENSIONS ===
    public static bool IsPlayerTurn(this CombatManager cm) 
        => cm.IsValid() && cm.CurrentPhase == TurnPhase.PlayerTurn;
    
    public static bool CanAct(this CombatManager cm) 
        => cm.IsPlayerTurn() && !cm.IsProcessingTurn;
        
    // === ENTITY COMBAT HELPERS ===
    public static void DealDamageToTarget(this EntityBehaviour entity, int damage)
    {
        if (entity.IsValid() && entity.IsAlive())
            entity.TakeDamage(damage, DamageType.Normal);
    }
    
    public static void HealTarget(this EntityBehaviour entity, int amount)
    {
        if (entity.IsValid() && entity.IsAlive() && entity.CurrentHealth < entity.MaxHealth)
            entity.Heal(Mathf.Min(amount, entity.MaxHealth - entity.CurrentHealth));
    }
    
    // === TARGETING HELPERS ===
    public static void DamageAllTargets(int damage)
    {
        CoreExtensions.TryWithManagerStatic<EnemyManager>(em => 
        {
            foreach (var enemy in em.AliveEnemies)
            {
                enemy?.DealDamageToTarget(damage);
            }
        });
    }

    // === RESOURCE EXTENSIONS ===
    public static bool CanAfford(this Resource res, int amount) 
        => res != null && res.CurrentValue >= amount;
    
    public static bool IsLow(this Resource res, float threshold = 0.3f) 
        => res != null && res.Percentage <= threshold;
    
    public static void Spend(this Resource res, int amount)
    {
        if (res.CanAfford(amount))
            res.ModifyBy(-amount);
    }

    // === COLLECTION HELPERS ===
    public static T GetValidFirst<T>(this IEnumerable<T> collection) where T : class
        => collection?.FirstOrDefault(item => item.IsValid());
    
    public static int CountValid<T>(this IEnumerable<T> collection) where T : class
        => collection?.Count(item => item.IsValid()) ?? 0;

    // === DEBUG UTILITIES ===
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
        bool isReady = CoreExtensions.IsManagerReady<T>();
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