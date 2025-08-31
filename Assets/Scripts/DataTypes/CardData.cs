using UnityEngine;

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
    public CardType cardType = CardType.Consonant;
    public CardSubType CardSubType = CardSubType.Basic;
    
    [Header("Letter Values")]
    public string letterValues = "";
    
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(letterValues))
            letterValues = letterValues.ToUpper();
        
        if (string.IsNullOrEmpty(cardName))
            cardName = "Unnamed Card";
    }
    
    public bool HasLetter(char letter)
    {
        return letterValues.Contains(letter.ToString().ToUpper());
    }
    
    public char[] GetLetters()
    {
        return letterValues.ToCharArray();
    }
}
