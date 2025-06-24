using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameCore.Enums;

/// <summary>
/// CardExtensions - Spezialisierte Extensions für Card-System
/// OPTIMIZED: Redundante Funktionen entfernt, Performance verbessert
/// DEPENDENCIES: CoreExtensions, SharedEnums
/// </summary>
public static class CardExtensions
{
    // === CARD VALIDATION (Basis für alle Card-Operations) ===
    
    /// <summary>
    /// Card ist gültig und verwendbar
    /// PERFORMANCE: O(1) - cached properties
    /// </summary>
    public static bool IsValid(this Card card)
        => card.IsValidReference() && card.CardData != null;
    
    /// <summary>
    /// Card kann gespielt werden
    /// </summary>
    public static bool IsPlayable(this Card card)
        => card.IsValid() && card.IsInteractable && card.IsActiveAndValid();

    /// <summary>
    /// Card ist aktuell ausgewählt
    /// </summary>
    public static bool IsSelected(this Card card)
        => card.IsValid() && card.IsSelected;

    /// <summary>
    /// Card ist in spezifischem State
    /// </summary>
    public static bool IsInState(this Card card, CardState state)
        => card.IsValid() && card.CurrentState == state;

    // === CARD DATA ACCESS (Sichere Zugriffe mit Fallbacks) ===
    
    /// <summary>
    /// Letter Values mit Safety
    /// </summary>
    public static string GetLetterValues(this Card card)
        => card.IsValid() ? (card.CardData.letterValues ?? "") : "";

    /// <summary>
    /// Card Name mit Fallback
    /// </summary>
    public static string GetCardName(this Card card)
        => card.IsValid() ? (card.CardData.cardName ?? "Unknown") : "Invalid Card";

    /// <summary>
    /// Card Description mit Safety
    /// </summary>
    public static string GetDescription(this Card card)
        => card.IsValid() ? (card.CardData.description ?? "") : "";
    
    /// <summary>
    /// Card Tier mit Safety
    /// </summary>
    public static int GetTier(this Card card)
        => card.IsValid() ? card.CardData.tier : 0;
    
    /// <summary>
    /// Card Type mit Safety
    /// </summary>
    public static CardType GetCardType(this Card card)
        => card.IsValid() ? card.CardData.cardType : CardType.Consonant;

    /// <summary>
    /// Card SubType mit Safety
    /// </summary>
    public static CardSubType GetCardSubType(this Card card)
        => card.IsValid() ? card.CardData.CardSubType : CardSubType.Basic;

    // === LETTER ANALYSIS (Optimierte Letter-Operationen) ===
    
    /// <summary>
    /// Card hat spezifischen Buchstaben
    /// </summary>
    public static bool HasLetter(this Card card, char letter)
        => card.IsValid() && card.CardData.HasLetter(letter);
    
    /// <summary>
    /// Card hat einen der Buchstaben
    /// PERFORMANCE: Short-circuit evaluation
    /// </summary>
    public static bool HasAnyLetter(this Card card, params char[] letters)
        => card.IsValid() && letters.Any(letter => card.CardData.HasLetter(letter));
    
    /// <summary>
    /// Card hat alle Buchstaben
    /// </summary>
    public static bool HasAllLetters(this Card card, params char[] letters)
        => card.IsValid() && letters.All(letter => card.CardData.HasLetter(letter));

    /// <summary>
    /// Alle Buchstaben der Card
    /// </summary>
    public static char[] GetLetters(this Card card)
        => card.IsValid() ? card.CardData.GetLetters() : new char[0];
    
    /// <summary>
    /// Anzahl Buchstaben
    /// </summary>
    public static int GetLetterCount(this Card card)
        => card.GetLetterValues().Length;

    /// <summary>
    /// Card hat Vokale
    /// PERFORMANCE: Konstanten-String statt Array
    /// </summary>
    public static bool HasVowel(this Card card)
        => card.IsValid() && card.GetLetters().Any(c => "AEIOU".Contains(c));
    
    /// <summary>
    /// Card hat Konsonanten
    /// </summary>
    public static bool HasConsonant(this Card card)
        => card.IsValid() && card.GetLetters().Any(c => !"AEIOU".Contains(c));

    // === COLLECTION OPERATIONS (Performance-optimiert für große Sammlungen) ===
    
    /// <summary>
    /// Filtert gültige Cards aus Collection
    /// PERFORMANCE: Lazy evaluation mit LINQ
    /// </summary>
    public static IEnumerable<Card> GetValidCards(this IEnumerable<Card> cards)
        => cards?.Where(c => c.IsValid()) ?? Enumerable.Empty<Card>();
    
    /// <summary>
    /// Filtert spielbare Cards
    /// </summary>
    public static IEnumerable<Card> GetPlayableCards(this IEnumerable<Card> cards)
        => cards.GetValidCards().Where(c => c.IsPlayable());
  
    /// <summary>
    /// Filtert ausgewählte Cards
    /// </summary>
    public static IEnumerable<Card> GetSelectedCards(this IEnumerable<Card> cards)
        => cards.GetValidCards().Where(c => c.IsSelected);
   
    /// <summary>
    /// Cards in spezifischem State
    /// </summary>
    public static IEnumerable<Card> GetCardsInState(this IEnumerable<Card> cards, CardState state)
        => cards.GetValidCards().Where(c => c.IsInState(state));
    
    /// <summary>
    /// Collection hat gültige Cards
    /// PERFORMANCE: Any() stoppt bei erstem Match
    /// </summary>
    public static bool HasValidCards(this IEnumerable<Card> cards)
        => cards?.Any(c => c.IsValid()) ?? false;
    
    /// <summary>
    /// Collection hat spielbare Cards
    /// </summary>
    public static bool HasPlayableCards(this IEnumerable<Card> cards)
        => cards.GetPlayableCards().Any();
    
    /// <summary>
    /// Anzahl gültiger Cards
    /// </summary>
    public static int GetValidCardCount(this IEnumerable<Card> cards)
        => cards?.Count(c => c.IsValid()) ?? 0;
    
    /// <summary>
    /// Anzahl spielbarer Cards
    /// </summary>
    public static int GetPlayableCardCount(this IEnumerable<Card> cards)
        => cards.GetPlayableCards().Count();

    // === LETTER SEQUENCE OPERATIONS (Für Spell-System) ===
    
    /// <summary>
    /// Letter-Sequenz aus Card-Collection erstellen
    /// PERFORMANCE: StringBuilder statt String-Concatenation
    /// </summary>
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
    
    /// <summary>
    /// Letter-Sequenz nur von spielbaren Cards
    /// </summary>
    public static string GetPlayableLetterSequence(this IEnumerable<Card> cards)
        => cards.GetPlayableCards().GetLetterSequence();

    // === SPELL BUILDING (Optimiert für Spellcast-System) ===
    
    /// <summary>
    /// Cards finden die für Spell benötigt werden
    /// PERFORMANCE: Greedy-Algorithmus, stoppt bei erstem Match
    /// </summary>
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
    
    /// <summary>
    /// Prüft ob Spell mit verfügbaren Cards baubar ist
    /// PERFORMANCE: Dictionary für Letter-Counting
    /// </summary>
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

    // === FILTERING & SORTING (Performance-optimiert) ===
    
    /// <summary>
    /// Filter nach Card Type
    /// </summary>
    public static IEnumerable<Card> FilterByType(this IEnumerable<Card> cards, CardType cardType)
        => cards.GetValidCards().Where(c => c.GetCardType() == cardType);
    
    /// <summary>
    /// Filter nach SubType
    /// </summary>
    public static IEnumerable<Card> FilterBySubType(this IEnumerable<Card> cards, CardSubType subType)
        => cards.GetValidCards().Where(c => c.GetCardSubType() == subType);
    
    /// <summary>
    /// Filter nach Tier
    /// </summary>
    public static IEnumerable<Card> FilterByTier(this IEnumerable<Card> cards, int tier)
        => cards.GetValidCards().Where(c => c.GetTier() == tier);
    
    /// <summary>
    /// Filter nach Tier-Range
    /// </summary>
    public static IEnumerable<Card> FilterByTierRange(this IEnumerable<Card> cards, int minTier, int maxTier)
        => cards.GetValidCards().Where(c => c.GetTier() >= minTier && c.GetTier() <= maxTier);
    
    /// <summary>
    /// Sortierung nach verschiedenen Kriterien
    /// PERFORMANCE: Einmaliges Filtern, dann Sortieren
    /// </summary>
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

    // === SAFE OPERATIONS (Fehlerresistente Card-Operationen) ===
    
    /// <summary>
    /// Sichere Card-Selection
    /// </summary>
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
            card.LogError("Failed to select card", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Sichere Card-Deselection
    /// </summary>
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
            card.LogError("Failed to deselect card", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Sichere Interactable-State Änderung
    /// </summary>
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
            card.LogError("Failed to set interactable state", ex);
            return false;
        }
    }

    // === ANALYSIS CLASSES (Für Spell-Building & AI) ===
    
    /// <summary>
    /// Detaillierte Letter-Analyse einer Card
    /// </summary>
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
    
    /// <summary>
    /// Collection-weite Letter-Analyse
    /// PERFORMANCE: Einmaliges LINQ statt mehrfache Iterationen
    /// </summary>
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
    
    /// <summary>
    /// Spell-Building Potenzial berechnen
    /// PERFORMANCE: Cached calculation basierend auf Letter-Analysis
    /// </summary>
    public static SpellBuildingPotential GetSpellBuildingPotential(this IEnumerable<Card> cards)
    {
        var potential = new SpellBuildingPotential();
        var playableCards = cards.GetPlayableCards().ToList();
        
        if (!playableCards.Any())
            return potential;
        
        var analysis = playableCards.GetCollectionLetterAnalysis();
        
        potential.LetterDiversity = (float)analysis.UniqueLetters / 26f;
        potential.VowelConsonantBalance = analysis.Vowels > 0 && analysis.Consonants > 0 
            ? (float)System.Math.Min(analysis.Vowels, analysis.Consonants) / System.Math.Max(analysis.Vowels, analysis.Consonants)
            : 0f;
        
        potential.ShortSpellPotential = analysis.UniqueLetters >= 3 ? 1f : analysis.UniqueLetters / 3f;
        potential.MediumSpellPotential = analysis.UniqueLetters >= 5 ? 1f : analysis.UniqueLetters / 5f;
        potential.LongSpellPotential = analysis.UniqueLetters >= 8 ? 1f : analysis.UniqueLetters / 8f;
        
        potential.OverallScore = (potential.LetterDiversity + potential.VowelConsonantBalance + 
                                potential.MediumSpellPotential) / 3f;
        
        return potential;
    }
}

// === SUPPORTING DATA CLASSES ===

[System.Serializable]
public class LetterAnalysis
{
    public int TotalLetters { get; set; }
    public int Vowels { get; set; }
    public int Consonants { get; set; }
    public int UniqueLetters { get; set; }
    public Dictionary<char, int> LetterFrequency { get; set; } = new Dictionary<char, int>();
}

[System.Serializable]
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

[System.Serializable]
public class SpellBuildingPotential
{
    public float LetterDiversity { get; set; }
    public float VowelConsonantBalance { get; set; }
    public float ShortSpellPotential { get; set; }  // 3-4 Buchstaben
    public float MediumSpellPotential { get; set; } // 5-7 Buchstaben
    public float LongSpellPotential { get; set; }   // 8+ Buchstaben
    public float OverallScore { get; set; }
}