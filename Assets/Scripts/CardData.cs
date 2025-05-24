using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCard", menuName = "Card System/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Basic Card Information")]
    public string cardName = "New Card";
    public Sprite cardImage;
    [TextArea(3, 5)]
    public string description = "Card description...";
    
    [Header("Card Properties")]
    [Range(1, 10)]
    public int tier = 1;
    
    [Header("Letter Values")]
    [Tooltip("Buchstabenwerte für Events (z.B. 'FI' für F und I)")]
    public string letterValues = "";
    
    [Header("Bonus Effects")]
    [Tooltip("Liste der Bonuseffekte - kann leer sein")]
    public List<BonusEffect> bonusEffects = new List<BonusEffect>();
    
    [Header("Card Type Settings")]
    public CardType cardType = CardType.Basic;
    
    // Validation method
    private void OnValidate()
    {
        // Ensure letter values are uppercase
        if (!string.IsNullOrEmpty(letterValues))
        {
            letterValues = letterValues.ToUpper();
        }
        
        // Ensure card name is not empty
        if (string.IsNullOrEmpty(cardName))
        {
            cardName = "Unnamed Card";
        }
    }
    
    // Helper method to check if card has specific letter
    public bool HasLetter(char letter)
    {
        return letterValues.Contains(letter.ToString().ToUpper());
    }
    
    // Helper method to get all letters as array
    public char[] GetLetters()
    {
        return letterValues.ToCharArray();
    }
}

[System.Serializable]
public class BonusEffect
{
    public string effectName = "New Effect";
    public BonusEffectType effectType = BonusEffectType.Passive;
    [TextArea(2, 3)]
    public string effectDescription = "Effect description...";
    public int effectValue = 0;
    
    // TODO: Hier können Sie weitere Eigenschaften hinzufügen:
    // public float duration; // Für zeitbasierte Effekte
    // public Sprite effectIcon; // Icon für UI-Darstellung
    // public AudioClip effectSound; // Sound beim Auslösen
}

public enum CardType
{
    Basic,
    Special,
    Event,
    Bonus,
    Legendary
}

public enum BonusEffectType
{
    Passive,        // Dauerhafter Effekt
    OnPlay,         // Beim Ausspielen
    OnDiscard,      // Beim Abwerfen
    Triggered,      // Durch bestimmte Bedingungen
    Instant         // Sofortige Einmalwirkung
}