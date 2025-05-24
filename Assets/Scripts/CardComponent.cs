using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class Card : MonoBehaviour
{
    [Header("Card Data")]
    [SerializeField] private CardData cardData;
    
    [Header("UI References")]
    [SerializeField] private Image cardImageDisplay;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI tierText;
    [SerializeField] private TextMeshProUGUI letterValuesText;
    [SerializeField] private Transform bonusEffectsContainer; // Container für Bonus-Icons
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject selectionHighlight;
    [SerializeField] private CanvasGroup canvasGroup;
    
    // Events für das Kartensystem
    public static event Action<Card> OnCardPlayed;
    public static event Action<Card> OnCardSelected;
    public static event Action<Card> OnCardDeselected;
    public static event Action<Card, string> OnCardLetterTriggered; // Card, Letter
    
    // Properties
    public CardData Data => cardData;
    public bool IsSelected { get; private set; }
    
    // Cached components
    private DragObject _dragObject;
    
    private void Awake()
    {
        // Cache components
        _dragObject = GetComponent<DragObject>();
        
        // Initialize canvas group if not assigned
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }
    
    private void Start()
    {
        // Initialize card display
        UpdateCardDisplay();
        
        // Hide selection highlight initially
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(false);
        }
    }
    
    // Method to set card data (useful for runtime card creation)
    public void SetCardData(CardData newCardData)
    {
        if (newCardData == null)
        {
            Debug.LogWarning($"[Card] Trying to set null CardData on {gameObject.name}");
            return;
        }
        
        cardData = newCardData;
        UpdateCardDisplay();
    }
    
    // Update all UI elements with card data
    private void UpdateCardDisplay()
    {
        if (cardData == null)
        {
            Debug.LogWarning($"[Card] No CardData assigned to {gameObject.name}");
            return;
        }
        
        // Update UI elements
        if (cardImageDisplay != null)
            cardImageDisplay.sprite = cardData.cardImage;
            
        if (nameText != null)
            nameText.text = cardData.cardName;
            
        if (descriptionText != null)
            descriptionText.text = cardData.description;
            
        if (tierText != null)
            tierText.text = $"Tier {cardData.tier}";
            
        if (letterValuesText != null)
            letterValuesText.text = cardData.letterValues;
        
        // Update bonus effects display
        UpdateBonusEffectsDisplay();
        
        // TODO: Hier können Sie weitere UI-Updates hinzufügen:
        // - Kartenrahmen je nach Typ färben
        // - Spezielle Effekte für Legendary-Karten
        // - Animationen beim Update
    }
    
    private void UpdateBonusEffectsDisplay()
    {
        if (bonusEffectsContainer == null || cardData == null) return;
        
        // Clear existing bonus effect displays
        foreach (Transform child in bonusEffectsContainer)
        {
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
        
        // TODO: Hier können Sie Bonus-Effekt Icons erstellen
        // Beispiel für jeden Bonus-Effekt ein kleines Icon anzeigen
        /*
        foreach (var effect in cardData.bonusEffects)
        {
            GameObject effectIcon = new GameObject($"Effect_{effect.effectName}");
            effectIcon.transform.SetParent(bonusEffectsContainer);
            // Fügen Sie hier Image-Komponente und Icon hinzu
        }
        */
    }
    
    // Card interaction methods
    public void SelectCard()
    {
        if (IsSelected) return;
        
        IsSelected = true;
        
        if (selectionHighlight != null)
            selectionHighlight.SetActive(true);
        
        OnCardSelected?.Invoke(this);
        
        // TODO: Hier können Sie weitere Selektions-Effekte hinzufügen:
        // - Sound abspielen
        // - Skalierungs-Animation
        // - Glow-Effekt
    }
    
    public void DeselectCard()
    {
        if (!IsSelected) return;
        
        IsSelected = false;
        
        if (selectionHighlight != null)
            selectionHighlight.SetActive(false);
        
        OnCardDeselected?.Invoke(this);
    }
    
    public void PlayCard()
    {
        OnCardPlayed?.Invoke(this);
        
        // Trigger bonus effects
        TriggerBonusEffects(BonusEffectType.OnPlay);
        
        // TODO: Hier können Sie Karten-Spiel-Logik hinzufügen:
        // - Animation zum Spielfeld
        // - Sound-Effekte
        // - Partikel-Effekte
        // - Karte aus Hand entfernen
    }
    
    public void TriggerLetterEvent(string letters)
    {
        if (cardData == null) return;
        
        foreach (char letter in letters)
        {
            if (cardData.HasLetter(letter))
            {
                OnCardLetterTriggered?.Invoke(this, letter.ToString());
                
                // TODO: Hier können Sie spezielle Letter-Effekte hinzufügen:
                // - Visuelle Hervorhebung des Buchstabens
                // - Sound für Letter-Match
                // - Bonus-Punkte vergeben
            }
        }
    }
    
    public void TriggerBonusEffects(BonusEffectType effectType)
    {
        if (cardData == null) return;
        
        foreach (var effect in cardData.bonusEffects)
        {
            if (effect.effectType == effectType)
            {
                ExecuteBonusEffect(effect);
            }
        }
    }
    
    private void ExecuteBonusEffect(BonusEffect effect)
    {
        // TODO: Hier implementieren Sie die Bonus-Effekt-Logik basierend auf effect.effectType
        Debug.Log($"[Card] Executing bonus effect: {effect.effectName} with value {effect.effectValue}");
        
        switch (effect.effectType)
        {
            case BonusEffectType.Passive:
                // Dauerhafte Effekte - meist über einen Manager verwaltet
                break;
            case BonusEffectType.OnPlay:
                // Effekte beim Ausspielen
                break;
            case BonusEffectType.OnDiscard:
                // Effekte beim Abwerfen
                break;
            case BonusEffectType.Triggered:
                // Bedingte Effekte
                break;
            case BonusEffectType.Instant:
                // Sofortige Effekte
                break;
        }
    }
    
    // Visual feedback methods
    public void SetInteractable(bool interactable)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = interactable ? 1f : 0.5f;
            canvasGroup.interactable = interactable;
        }
        
        if (_dragObject != null)
        {
            // Hier könnten Sie DragObject aktivieren/deaktivieren
            // _dragObject.enabled = interactable;
        }
    }
    
    // Utility methods
    public bool HasBonusEffect(string effectName)
    {
        if (cardData == null) return false;
        
        foreach (var effect in cardData.bonusEffects)
        {
            if (effect.effectName.Equals(effectName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
    
    public bool IsOfType(CardType type)
    {
        return cardData != null && cardData.cardType == type;
    }
    
    // Event cleanup
    private void OnDestroy()
    {
        // Deselect if selected to trigger cleanup
        if (IsSelected)
        {
            DeselectCard();
        }
    }
}