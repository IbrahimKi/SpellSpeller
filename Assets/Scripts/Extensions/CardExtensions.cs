using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

/// <summary>
/// Card Extensions für erweiterte Card-Operations und Collection Management
/// Bietet sichere Validation, intelligente Queries, Letter Analysis und Card Utilities
/// 
/// USAGE:
/// - card.IsValid() statt null checks
/// - cards.GetPlayableCards() für sichere Filtering
/// - cards.GetLetterAnalysis() für Spell-Building
/// - cards.FindCardsForSpell("FIRE") für intelligente Card-Suche
/// </summary>
public static class CardExtensions
{
    // ===========================================
    // BASIC VALIDATION EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Prüft ob Card und CardData valid sind
    /// </summary>
    public static bool IsValid(this Card card)
        => card != null && !card.Equals(null) && card.CardData != null;
    
    /// <summary>
    /// Prüft ob Card spielbar ist
    /// </summary>
    public static bool IsPlayable(this Card card)
        => card.IsValid() && card.IsInteractable;
    
    /// <summary>
    /// Prüft ob Card ausgewählt ist
    /// </summary>
    public static bool IsSelected(this Card card)
        => card.IsValid() && card.IsSelected;
    
    /// <summary>
    /// Prüft ob Card in bestimmtem Zustand ist
    /// </summary>
    public static bool IsInState(this Card card, CardState state)
        => card.IsValid() && card.CurrentState == state;
    
    /// <summary>
    /// Erweiterte Unity-sichere Validation
    /// </summary>
    public static bool IsActiveCard(this Card card)
        => card.IsValid() && card.gameObject.activeInHierarchy;
    
    // ===========================================
    // SAFE DATA ACCESS EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Sichere Letter Values mit Fallback
    /// </summary>
    public static string GetLetterValues(this Card card)
        => card.IsValid() ? (card.CardData.letterValues ?? "") : "";
    
    /// <summary>
    /// Sicherer Card Name mit Fallback
    /// </summary>
    public static string GetCardName(this Card card)
        => card.IsValid() ? (card.CardData.cardName ?? "Unknown") : "Invalid Card";
    
    /// <summary>
    /// Sichere Description mit Fallback
    /// </summary>
    public static string GetDescription(this Card card)
        => card.IsValid() ? (card.CardData.description ?? "") : "";
    
    /// <summary>
    /// Sicherer Tier Access
    /// </summary>
    public static int GetTier(this Card card)
        => card.IsValid() ? card.CardData.tier : 0;
    
    /// <summary>
    /// Sicherer Type Access
    /// </summary>
    public static CardType GetCardType(this Card card)
        => card.IsValid() ? card.CardData.cardType : CardType.Consonant;
    
    /// <summary>
    /// Sicherer SubType Access
    /// </summary>
    public static CardSubType GetCardSubType(this Card card)
        => card.IsValid() ? card.CardData.CardSubType : CardSubType.Basic;
    
    // ===========================================
    // LETTER ANALYSIS EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Prüft ob Card bestimmten Buchstaben enthält
    /// </summary>
    public static bool HasLetter(this Card card, char letter)
        => card.IsValid() && card.CardData.HasLetter(letter);
    
    /// <summary>
    /// Prüft ob Card einen der Buchstaben enthält
    /// </summary>
    public static bool HasAnyLetter(this Card card, params char[] letters)
        => card.IsValid() && letters.Any(letter => card.CardData.HasLetter(letter));
    
    /// <summary>
    /// Prüft ob Card alle Buchstaben enthält
    /// </summary>
    public static bool HasAllLetters(this Card card, params char[] letters)
        => card.IsValid() && letters.All(letter => card.CardData.HasLetter(letter));
    
    /// <summary>
    /// Holt alle Buchstaben der Card
    /// </summary>
    public static char[] GetLetters(this Card card)
        => card.IsValid() ? card.CardData.GetLetters() : new char[0];
    
    /// <summary>
    /// Zählt Buchstaben in Card
    /// </summary>
    public static int GetLetterCount(this Card card)
        => card.GetLetterValues().Length;
    
    /// <summary>
    /// Prüft ob Card Vokal enthält
    /// </summary>
    public static bool HasVowel(this Card card)
        => card.HasAnyLetter('A', 'E', 'I', 'O', 'U');
    
    /// <summary>
    /// Prüft ob Card Konsonant enthält
    /// </summary>
    public static bool HasConsonant(this Card card)
        => card.IsValid() && card.GetLetters().Any(c => !"AEIOU".Contains(c));
    
    /// <summary>
    /// Analysiert Letter-Zusammensetzung
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
    
    // ===========================================
    // COLLECTION VALIDATION EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Filtert nur gültige Cards
    /// </summary>
    public static IEnumerable<Card> GetValidCards(this IEnumerable<Card> cards)
        => cards?.Where(c => c.IsValid()) ?? Enumerable.Empty<Card>();
    
    /// <summary>
    /// Filtert nur spielbare Cards
    /// </summary>
    public static IEnumerable<Card> GetPlayableCards(this IEnumerable<Card> cards)
        => cards.GetValidCards().Where(c => c.IsPlayable());
    
    /// <summary>
    /// Filtert nur ausgewählte Cards
    /// </summary>
    public static IEnumerable<Card> GetSelectedCards(this IEnumerable<Card> cards)
        => cards.GetValidCards().Where(c => c.IsSelected);
    
    /// <summary>
    /// Filtert Cards nach State
    /// </summary>
    public static IEnumerable<Card> GetCardsInState(this IEnumerable<Card> cards, CardState state)
        => cards.GetValidCards().Where(c => c.IsInState(state));
    
    /// <summary>
    /// Prüft ob Collection gültige Cards hat
    /// </summary>
    public static bool HasValidCards(this IEnumerable<Card> cards)
        => cards?.Any(c => c.IsValid()) ?? false;
    
    /// <summary>
    /// Prüft ob Collection spielbare Cards hat
    /// </summary>
    public static bool HasPlayableCards(this IEnumerable<Card> cards)
        => cards.GetPlayableCards().Any();
    
    /// <summary>
    /// Zählt gültige Cards
    /// </summary>
    public static int GetValidCardCount(this IEnumerable<Card> cards)
        => cards?.Count(c => c.IsValid()) ?? 0;
    
    /// <summary>
    /// Zählt spielbare Cards
    /// </summary>
    public static int GetPlayableCardCount(this IEnumerable<Card> cards)
        => cards.GetPlayableCards().Count();
    
    // ===========================================
    // LETTER SEQUENCE EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Holt Letter Sequence aus Card Collection
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
    /// Holt Letter Sequence nur von spielbaren Cards
    /// </summary>
    public static string GetPlayableLetterSequence(this IEnumerable<Card> cards)
        => cards.GetPlayableCards().GetLetterSequence();
    
    /// <summary>
    /// Analysiert Letter-Verteilung in Collection
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
        
        // Card Type Analysis
        analysis.VowelCards = validCards.Count(c => c.GetCardType() == CardType.Vowel);
        analysis.ConsonantCards = validCards.Count(c => c.GetCardType() == CardType.Consonant);
        analysis.SpecialCards = validCards.Count(c => c.GetCardType() == CardType.Special);
        
        return analysis;
    }
    
    // ===========================================
    // SPELL BUILDING EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Findet Cards die für Spell verwendet werden können
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
    /// Prüft ob Collection genug Cards für Spell hat
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
    
    /// <summary>
    /// Findet mögliche Spell-Kombinationen
    /// </summary>
    public static IEnumerable<string> FindPossibleSpells(this IEnumerable<Card> cards, 
        IEnumerable<string> knownSpells)
    {
        var playableCards = cards.GetPlayableCards().ToList();
        if (!playableCards.Any() || knownSpells == null)
            return Enumerable.Empty<string>();
        
        return knownSpells.Where(spell => playableCards.CanBuildSpell(spell));
    }
    
    /// <summary>
    /// Bewertet Spell-Building Potential
    /// </summary>
    public static SpellBuildingPotential GetSpellBuildingPotential(this IEnumerable<Card> cards)
    {
        var potential = new SpellBuildingPotential();
        var playableCards = cards.GetPlayableCards().ToList();
        
        if (!playableCards.Any())
            return potential;
        
        var analysis = playableCards.GetCollectionLetterAnalysis();
        
        // Bewerte basierend auf Letter-Diversität
        potential.LetterDiversity = (float)analysis.UniqueLetters / 26f; // Percentage of alphabet
        potential.VowelConsonantBalance = Math.Min(analysis.Vowels, analysis.Consonants) / 
                                        (float)Math.Max(analysis.Vowels, analysis.Consonants);
        
        // Bewerte Spell-Length Potential
        potential.ShortSpellPotential = analysis.UniqueLetters >= 3 ? 1f : analysis.UniqueLetters / 3f;
        potential.MediumSpellPotential = analysis.UniqueLetters >= 5 ? 1f : analysis.UniqueLetters / 5f;
        potential.LongSpellPotential = analysis.UniqueLetters >= 8 ? 1f : analysis.UniqueLetters / 8f;
        
        // Overall Score
        potential.OverallScore = (potential.LetterDiversity + potential.VowelConsonantBalance + 
                                potential.MediumSpellPotential) / 3f;
        
        return potential;
    }
    
    // ===========================================
    // CARD FILTERING & SORTING EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Filtert Cards nach Type
    /// </summary>
    public static IEnumerable<Card> FilterByType(this IEnumerable<Card> cards, CardType cardType)
        => cards.GetValidCards().Where(c => c.GetCardType() == cardType);
    
    /// <summary>
    /// Filtert Cards nach SubType
    /// </summary>
    public static IEnumerable<Card> FilterBySubType(this IEnumerable<Card> cards, CardSubType subType)
        => cards.GetValidCards().Where(c => c.GetCardSubType() == subType);
    
    /// <summary>
    /// Filtert Cards nach Tier
    /// </summary>
    public static IEnumerable<Card> FilterByTier(this IEnumerable<Card> cards, int tier)
        => cards.GetValidCards().Where(c => c.GetTier() == tier);
    
    /// <summary>
    /// Filtert Cards nach Tier Range
    /// </summary>
    public static IEnumerable<Card> FilterByTierRange(this IEnumerable<Card> cards, int minTier, int maxTier)
        => cards.GetValidCards().Where(c => c.GetTier() >= minTier && c.GetTier() <= maxTier);
    
    /// <summary>
    /// Sortiert Cards nach verschiedenen Kriterien
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
    
    /// <summary>
    /// Holt Random Card aus Collection
    /// </summary>
    public static Card GetRandomCard(this IEnumerable<Card> cards)
    {
        var validCards = cards.GetValidCards().ToList();
        return validCards.Any() ? validCards[UnityEngine.Random.Range(0, validCards.Count)] : null;
    }
    
    /// <summary>
    /// Holt Random Cards aus Collection
    /// </summary>
    public static IEnumerable<Card> GetRandomCards(this IEnumerable<Card> cards, int count)
    {
        var validCards = cards.GetValidCards().ToList();
        if (!validCards.Any() || count <= 0)
            return Enumerable.Empty<Card>();
        
        return validCards.OrderBy(x => Guid.NewGuid()).Take(count);
    }
    
    // ===========================================
    // CARD UTILITY EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Versucht Card sicher zu selektieren
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
            Debug.LogError($"[CardExtensions] Failed to select card: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Versucht Card sicher zu deselektieren
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
            Debug.LogError($"[CardExtensions] Failed to deselect card: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Setzt Card State sicher
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
            Debug.LogError($"[CardExtensions] Failed to set interactable: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Klont Card Data (für Backup/Undo Operations)
    /// </summary>
    public static CardData CloneCardData(this Card card)
    {
        if (!card.IsValid()) return null;
        
        // Note: This would require CardData to implement ICloneable or custom cloning
        // For now, return reference (would need proper implementation)
        return card.CardData;
    }
}

// ===========================================
// SUPPORTING ENUMS & CLASSES
// ===========================================

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
    public float LetterDiversity { get; set; } // 0-1
    public float VowelConsonantBalance { get; set; } // 0-1
    public float ShortSpellPotential { get; set; } // 0-1 (3+ letters)
    public float MediumSpellPotential { get; set; } // 0-1 (5+ letters)
    public float LongSpellPotential { get; set; } // 0-1 (8+ letters)
    public float OverallScore { get; set; } // 0-1
}

// ===========================================
// VERWENDUNGS-BEISPIELE
// ===========================================

/*
// BASIC VALIDATION USAGE:
public void ProcessCards(List<Card> cards)
{
    // Statt: cards?.Where(c => c != null && c.CardData != null)
    var validCards = cards.GetValidCards();
    
    // Statt: komplexe manual checks
    var playableCards = cards.GetPlayableCards();
    
    if (playableCards.Any())
    {
        Debug.Log($"Found {playableCards.Count()} playable cards");
    }
}

// SPELL BUILDING USAGE:
public void FindBestSpellOptions()
{
    var handCards = CardManager.Instance.GetHandCards();
    
    // Check spell building potential
    var potential = handCards.GetSpellBuildingPotential();
    Debug.Log($"Spell building score: {potential.OverallScore:P0}");
    
    // Find cards for specific spell
    var fireCards = handCards.FindCardsForSpell("FIRE");
    if (fireCards.Any())
    {
        Debug.Log("Can cast FIRE spell!");
    }
    
    // Check multiple spell options
    var possibleSpells = handCards.FindPossibleSpells(new[] { "FIRE", "HEAL", "BOLT" });
    foreach (var spell in possibleSpells)
    {
        Debug.Log($"Can cast: {spell}");
    }
}

// LETTER ANALYSIS USAGE:
public void AnalyzeHand()
{
    var handCards = CardManager.Instance.GetHandCards();
    var analysis = handCards.GetCollectionLetterAnalysis();
    
    Debug.Log($"Hand Analysis:");
    Debug.Log($"- Total Cards: {analysis.TotalCards}");
    Debug.Log($"- Total Letters: {analysis.TotalLetters}");
    Debug.Log($"- Unique Letters: {analysis.UniqueLetters}");
    Debug.Log($"- Vowel/Consonant: {analysis.Vowels}/{analysis.Consonants}");
    
    // Show most frequent letters
    var topLetters = analysis.LetterFrequency
        .OrderByDescending(kvp => kvp.Value)
        .Take(3);
    
    foreach (var letter in topLetters)
    {
        Debug.Log($"- {letter.Key}: {letter.Value}x");
    }
}

// FILTERING & SORTING USAGE:
public void OrganizeCards()
{
    var handCards = CardManager.Instance.GetHandCards();
    
    // Filter by type
    var vowelCards = handCards.FilterByType(CardType.Vowel);
    var highTierCards = handCards.FilterByTierRange(3, 5);
    
    // Sort cards
    var sortedByTier = handCards.SortBy(CardSortCriteria.Tier, ascending: false);
    var sortedByLetters = handCards.SortBy(CardSortCriteria.LetterCount);
    
    // Get specific cards
    var randomCard = handCards.GetRandomCard();
    var randomThree = handCards.GetRandomCards(3);
}

// UI INTEGRATION:
public void UpdateCardUI()
{
    var selectedCards = CardManager.Instance.SelectedCards;
    
    // Show letter sequence
    var sequence = selectedCards.GetLetterSequence();
    sequenceText.text = $"Selected: {sequence}";
    
    // Show spell potential
    var potential = selectedCards.GetSpellBuildingPotential();
    potentialBar.fillAmount = potential.OverallScore;
    
    // Color code based on potential
    potentialBar.color = potential.OverallScore switch
    {
        >= 0.8f => Color.green,
        >= 0.6f => Color.yellow,
        >= 0.4f => Color.orange,
        _ => Color.red
    };
}

// SAFE OPERATIONS:
public void SafeCardOperations(Card card)
{
    // Safe selection
    if (card.TrySelect())
    {
        Debug.Log("Card selected successfully");
    }
    
    // Safe state changes
    if (card.TrySetInteractable(false))
    {
        Debug.Log("Card disabled successfully");
    }
    
    // Safe data access
    var cardName = card.GetCardName(); // Never throws null exception
    var letters = card.GetLetterValues(); // Always returns string
    var tier = card.GetTier(); // Returns 0 if invalid
}
*/