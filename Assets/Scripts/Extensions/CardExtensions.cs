using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

public static class CardExtensions
{
    public static bool IsValid(this Card card)
        => card != null && !card.Equals(null) && card.CardData != null;
    
    public static bool IsPlayable(this Card card)
        => card.IsValid() && card.IsInteractable;

    public static bool IsSelected(this Card card)
        => card.IsValid() && card.IsSelected;

    public static bool IsInState(this Card card, CardState state)
        => card.IsValid() && card.CurrentState == state;

    public static bool IsActiveCard(this Card card)
        => card.IsValid() && card.gameObject.activeInHierarchy;
    
    public static string GetLetterValues(this Card card)
        => card.IsValid() ? (card.CardData.letterValues ?? "") : "";

    public static string GetCardName(this Card card)
        => card.IsValid() ? (card.CardData.cardName ?? "Unknown") : "Invalid Card";

    public static string GetDescription(this Card card)
        => card.IsValid() ? (card.CardData.description ?? "") : "";
    
    public static int GetTier(this Card card)
        => card.IsValid() ? card.CardData.tier : 0;
    
    public static CardType GetCardType(this Card card)
        => card.IsValid() ? card.CardData.cardType : CardType.Consonant;

    public static CardSubType GetCardSubType(this Card card)
        => card.IsValid() ? card.CardData.CardSubType : CardSubType.Basic;
    
    public static bool HasLetter(this Card card, char letter)
        => card.IsValid() && card.CardData.HasLetter(letter);
    
    public static bool HasAnyLetter(this Card card, params char[] letters)
        => card.IsValid() && letters.Any(letter => card.CardData.HasLetter(letter));
    
    public static bool HasAllLetters(this Card card, params char[] letters)
        => card.IsValid() && letters.All(letter => card.CardData.HasLetter(letter));

    public static char[] GetLetters(this Card card)
        => card.IsValid() ? card.CardData.GetLetters() : new char[0];
    
    public static int GetLetterCount(this Card card)
        => card.GetLetterValues().Length;

    public static bool HasVowel(this Card card)
        => card.HasAnyLetter('A', 'E', 'I', 'O', 'U');
    
    public static bool HasConsonant(this Card card)
        => card.IsValid() && card.GetLetters().Any(c => !"AEIOU".Contains(c));
    
    public static LetterAnalysis GetLetterAnalysis(this Card card)
    {
        var analysis = new LetterAnalysis();
        
        if (!card.IsValid())
            return analysis;
        
        var letters = card.GetLetters();
        analysis.TotalLetters = letters.Length;
        analysis.Vowels = letters.Count(c => "AEIOU".Contains(c));
        analysis.Consonants = letters.Count(c => !"AEIOU".Contains(c));
        analysis.UniqueLetters = letters.Distinct().Count();
        analysis.LetterFrequency = letters.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        
        return analysis;
    }
    
    public static IEnumerable<Card> GetValidCards(this IEnumerable<Card> cards)
        => cards?.Where(c => c.IsValid()) ?? Enumerable.Empty<Card>();
    
    public static IEnumerable<Card> GetPlayableCards(this IEnumerable<Card> cards)
        => cards.GetValidCards().Where(c => c.IsPlayable());
  
    public static IEnumerable<Card> GetSelectedCards(this IEnumerable<Card> cards)
        => cards.GetValidCards().Where(c => c.IsSelected);
   
    public static IEnumerable<Card> GetCardsInState(this IEnumerable<Card> cards, CardState state)
        => cards.GetValidCards().Where(c => c.IsInState(state));
    
    public static bool HasValidCards(this IEnumerable<Card> cards)
        => cards?.Any(c => c.IsValid()) ?? false;
    
    public static bool HasPlayableCards(this IEnumerable<Card> cards)
        => cards.GetPlayableCards().Any();
    
    public static int GetValidCardCount(this IEnumerable<Card> cards)
        => cards?.Count(c => c.IsValid()) ?? 0;
    
    public static int GetPlayableCardCount(this IEnumerable<Card> cards)
        => cards.GetPlayableCards().Count();
    
    public static string GetLetterSequence(this IEnumerable<Card> cards)
    {
        if (cards == null) return "";
        
        var letterBuilder = new StringBuilder();
        foreach (var card in cards.GetValidCards())
        {
            letterBuilder.Append(card.GetLetterValues());
        }
        return letterBuilder.ToString();
    }
    
    public static string GetPlayableLetterSequence(this IEnumerable<Card> cards)
        => cards.GetPlayableCards().GetLetterSequence();
    
    public static CollectionLetterAnalysis GetCollectionLetterAnalysis(this IEnumerable<Card> cards)
    {
        var analysis = new CollectionLetterAnalysis();
        var validCards = cards.GetValidCards().ToList();
        
        if (!validCards.Any())
            return analysis;
        
        analysis.TotalCards = validCards.Count;
        analysis.TotalLetters = validCards.Sum(c => c.GetLetterCount());
        
        var allLetters = validCards.SelectMany(c => c.GetLetters()).ToList();
        analysis.LetterFrequency = allLetters.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        analysis.UniqueLetters = allLetters.Distinct().Count();
        analysis.Vowels = allLetters.Count(c => "AEIOU".Contains(c));
        analysis.Consonants = allLetters.Count(c => !"AEIOU".Contains(c));
        
        analysis.VowelCards = validCards.Count(c => c.GetCardType() == CardType.Vowel);
        analysis.ConsonantCards = validCards.Count(c => c.GetCardType() == CardType.Consonant);
        analysis.SpecialCards = validCards.Count(c => c.GetCardType() == CardType.Special);
        
        return analysis;
    }
    
    public static IEnumerable<Card> FindCardsForSpell(this IEnumerable<Card> cards, string spellCode)
    {
        if (string.IsNullOrEmpty(spellCode))
            return Enumerable.Empty<Card>();
        
        var playableCards = cards.GetPlayableCards().ToList();
        var neededLetters = spellCode.ToCharArray();
        var selectedCards = new List<Card>();
        
        foreach (var letter in neededLetters)
        {
            var cardWithLetter = playableCards.FirstOrDefault(c => 
                c.HasLetter(letter) && !selectedCards.Contains(c));
            
            if (cardWithLetter != null)
                selectedCards.Add(cardWithLetter);
        }
        
        return selectedCards;
    }
    
    public static bool CanBuildSpell(this IEnumerable<Card> cards, string spellCode)
    {
        if (string.IsNullOrEmpty(spellCode))
            return false;
        
        var availableLetters = cards.GetPlayableCards()
            .SelectMany(c => c.GetLetters())
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());
        
        var requiredLetters = spellCode.ToCharArray()
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());
        
        return requiredLetters.All(req => 
            availableLetters.ContainsKey(req.Key) && 
            availableLetters[req.Key] >= req.Value);
    }
    
    public static IEnumerable<string> FindPossibleSpells(this IEnumerable<Card> cards, 
        IEnumerable<string> knownSpells)
    {
        var playableCards = cards.GetPlayableCards().ToList();
        if (!playableCards.Any() || knownSpells == null)
            return Enumerable.Empty<string>();
        
        return knownSpells.Where(spell => playableCards.CanBuildSpell(spell));
    }
    
    public static SpellBuildingPotential GetSpellBuildingPotential(this IEnumerable<Card> cards)
    {
        var potential = new SpellBuildingPotential();
        var playableCards = cards.GetPlayableCards().ToList();
        
        if (!playableCards.Any())
            return potential;
        
        var analysis = playableCards.GetCollectionLetterAnalysis();
        
        potential.LetterDiversity = (float)analysis.UniqueLetters / 26f;
        potential.VowelConsonantBalance = Math.Min(analysis.Vowels, analysis.Consonants) / 
                                        (float)Math.Max(analysis.Vowels, analysis.Consonants);
        
        potential.ShortSpellPotential = analysis.UniqueLetters >= 3 ? 1f : analysis.UniqueLetters / 3f;
        potential.MediumSpellPotential = analysis.UniqueLetters >= 5 ? 1f : analysis.UniqueLetters / 5f;
        potential.LongSpellPotential = analysis.UniqueLetters >= 8 ? 1f : analysis.UniqueLetters / 8f;
        
        potential.OverallScore = (potential.LetterDiversity + potential.VowelConsonantBalance + 
                                potential.MediumSpellPotential) / 3f;
        
        return potential;
    }
    
    public static IEnumerable<Card> FilterByType(this IEnumerable<Card> cards, CardType cardType)
        => cards.GetValidCards().Where(c => c.GetCardType() == cardType);
    
    public static IEnumerable<Card> FilterBySubType(this IEnumerable<Card> cards, CardSubType subType)
        => cards.GetValidCards().Where(c => c.GetCardSubType() == subType);
    
    public static IEnumerable<Card> FilterByTier(this IEnumerable<Card> cards, int tier)
        => cards.GetValidCards().Where(c => c.GetTier() == tier);
    
    public static IEnumerable<Card> FilterByTierRange(this IEnumerable<Card> cards, int minTier, int maxTier)
        => cards.GetValidCards().Where(c => c.GetTier() >= minTier && c.GetTier() <= maxTier);
    
    public static IEnumerable<Card> SortBy(this IEnumerable<Card> cards, CardSortCriteria criteria, 
        bool ascending = true)
    {
        var validCards = cards.GetValidCards();
        
        var sorted = criteria switch
        {
            CardSortCriteria.Name => validCards.OrderBy(c => c.GetCardName()),
            CardSortCriteria.Tier => validCards.OrderBy(c => c.GetTier()),
            CardSortCriteria.Type => validCards.OrderBy(c => c.GetCardType()),
            CardSortCriteria.LetterCount => validCards.OrderBy(c => c.GetLetterCount()),
            CardSortCriteria.State => validCards.OrderBy(c => c.CurrentState),
            _ => validCards
        };
        
        return ascending ? sorted : sorted.Reverse();
    }
    
    public static Card GetRandomCard(this IEnumerable<Card> cards)
    {
        var validCards = cards.GetValidCards().ToList();
        return validCards.Any() ? validCards[UnityEngine.Random.Range(0, validCards.Count)] : null;
    }
    
    public static IEnumerable<Card> GetRandomCards(this IEnumerable<Card> cards, int count)
    {
        var validCards = cards.GetValidCards().ToList();
        if (!validCards.Any() || count <= 0)
            return Enumerable.Empty<Card>();
        
        return validCards.OrderBy(x => Guid.NewGuid()).Take(count);
    }
    
    public static bool TrySelect(this Card card)
    {
        if (!card.IsPlayable()) return false;
        
        try
        {
            card.Select();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CardExtensions] Failed to select card: {ex.Message}");
            return false;
        }
    }
    
    public static bool TryDeselect(this Card card)
    {
        if (!card.IsValid()) return false;
        
        try
        {
            card.Deselect();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CardExtensions] Failed to deselect card: {ex.Message}");
            return false;
        }
    }
    
    public static bool TrySetInteractable(this Card card, bool interactable)
    {
        if (!card.IsValid()) return false;
        
        try
        {
            card.SetInteractable(interactable);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CardExtensions] Failed to set interactable: {ex.Message}");
            return false;
        }
    }
    
    public static CardData CloneCardData(this Card card)
    {
        if (!card.IsValid()) return null;
        return card.CardData;
    }
}

public enum CardSortCriteria
{
    Name,
    Tier,
    Type,
    LetterCount,
    State
}

public class LetterAnalysis
{
    public int TotalLetters { get; set; }
    public int Vowels { get; set; }
    public int Consonants { get; set; }
    public int UniqueLetters { get; set; }
    public Dictionary<char, int> LetterFrequency { get; set; } = new Dictionary<char, int>();
}

public class CollectionLetterAnalysis
{
    public int TotalCards { get; set; }
    public int TotalLetters { get; set; }
    public int UniqueLetters { get; set; }
    public int Vowels { get; set; }
    public int Consonants { get; set; }
    public int VowelCards { get; set; }
    public int ConsonantCards { get; set; }
    public int SpecialCards { get; set; }
    public Dictionary<char, int> LetterFrequency { get; set; } = new Dictionary<char, int>();
}

public class SpellBuildingPotential
{
    public float LetterDiversity { get; set; }
    public float VowelConsonantBalance { get; set; }
    public float ShortSpellPotential { get; set; }
    public float MediumSpellPotential { get; set; }
    public float LongSpellPotential { get; set; }
    public float OverallScore { get; set; }
}