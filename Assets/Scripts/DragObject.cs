using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DragObject : MonoBehaviour
{
    [SerializeField] private bool isDraggable = true;
    
    // Cached transform component for better performance
    private RectTransform _rectTransform;
    
    // Original sorting order/layer for returning after drag ends
    private int _originalSortingOrder;
    private int _dragSortingOrderBonus = 10; // How much to increase when dragging
    
    // Optional - sorting layer or canvas sorting order component
    private Canvas _canvas;
    
    // Card system integration
    [Header("Card System")]
    [SerializeField] private bool autoDetectCards = true;
    [SerializeField] private List<Card> attachedCards = new List<Card>();
    
    // Events for drag system
    public System.Action<DragObject> OnDragStarted;
    public System.Action<DragObject> OnDragEnded;
    public System.Action<DragObject, List<Card>> OnCardsDetected;
    
    public bool IsDraggable => isDraggable;
    public List<Card> AttachedCards => attachedCards;
    public bool HasCards => attachedCards.Count > 0;
    
    private void Awake()
    {
        // Cache components for performance
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponent<Canvas>();
        
        if (_canvas != null)
        {
            _originalSortingOrder = _canvas.sortingOrder;
        }
        
        // Auto-detect cards if enabled
        if (autoDetectCards)
        {
            DetectAttachedCards();
        }
    }
    
    private void Start()
    {
        // Subscribe to card events if we have cards
        SubscribeToCardEvents();
    }
    
    public void OnDragStart()
    {
        // Bring the object to front
        if (_canvas != null)
        {
            _canvas.sortingOrder = _originalSortingOrder + _dragSortingOrderBonus;
        }
        
        // Notify attached cards about drag start
        foreach (var card in attachedCards)
        {
            if (card != null)
            {
                // TODO: Hier können Sie Karten-spezifische Drag-Start-Effekte hinzufügen:
                // card.OnDragStarted();
                // card.SetHighlight(true);
            }
        }
        
        // Trigger event
        OnDragStarted?.Invoke(this);
        
        // You could add more effects here:
        // - Scale up slightly
        // - Play sound
        // - Show glow effect
        // - Highlight valid drop zones
    }
    
    public void OnDragEnd()
    {
        // Return to original sorting order
        if (_canvas != null)
        {
            _canvas.sortingOrder = _originalSortingOrder;
        }
        
        // Notify attached cards about drag end
        foreach (var card in attachedCards)
        {
            if (card != null)
            {
                // TODO: Hier können Sie Karten-spezifische Drag-End-Effekte hinzufügen:
                // card.OnDragEnded();
                // card.SetHighlight(false);
            }
        }
        
        // Trigger event
        OnDragEnded?.Invoke(this);
        
        // Other possible actions:
        // - Snap to grid
        // - Validate move
        // - Play drop sound
        // - Check for valid drop zones
        // - Trigger card interactions
    }
    
    /// <summary>
    /// Automatisch alle Card-Komponenten an diesem GameObject und seinen Kindern erkennen
    /// </summary>
    public void DetectAttachedCards()
    {
        attachedCards.Clear();
        
        // Suche nach Cards am aktuellen GameObject
        Card mainCard = GetComponent<Card>();
        if (mainCard != null)
        {
            attachedCards.Add(mainCard);
        }
        
        // Suche nach Cards in Kindobjekten
        Card[] childCards = GetComponentsInChildren<Card>();
        foreach (var card in childCards)
        {
            if (card != mainCard && !attachedCards.Contains(card))
            {
                attachedCards.Add(card);
            }
        }
        
        // Remove null references
        attachedCards = attachedCards.Where(card => card != null).ToList();
        
        // Trigger event wenn Karten gefunden wurden
        if (attachedCards.Count > 0)
        {
            OnCardsDetected?.Invoke(this, attachedCards);
        }
        
        Debug.Log($"[DragObject] Detected {attachedCards.Count} cards on {gameObject.name}");
    }
    
    /// <summary>
    /// Manuell eine Karte hinzufügen
    /// </summary>
    public void AddCard(Card card)
    {
        if (card != null && !attachedCards.Contains(card))
        {
            attachedCards.Add(card);
            SubscribeToCardEvents();
        }
    }
    
    /// <summary>
    /// Manuell eine Karte entfernen
    /// </summary>
    public void RemoveCard(Card card)
    {
        if (attachedCards.Contains(card))
        {
            attachedCards.Remove(card);
            UnsubscribeFromCardEvents(card);
        }
    }
    
    /// <summary>
    /// Alle Karten entfernen
    /// </summary>
    public void ClearCards()
    {
        UnsubscribeFromAllCardEvents();
        attachedCards.Clear();
    }
    
    /// <summary>
    /// Prüfen ob eine bestimmte Karte angehängt ist
    /// </summary>
    public bool HasCard(Card card)
    {
        return attachedCards.Contains(card);
    }
    
    /// <summary>
    /// Prüfen ob Karten eines bestimmten Typs angehängt sind
    /// </summary>
    public bool HasCardOfType(CardType cardType)
    {
        return attachedCards.Any(card => card != null && card.IsOfType(cardType));
    }
    
    /// <summary>
    /// Alle Karten eines bestimmten Typs erhalten
    /// </summary>
    public List<Card> GetCardsOfType(CardType cardType)
    {
        return attachedCards.Where(card => card != null && card.IsOfType(cardType)).ToList();
    }
    
    /// <summary>
    /// Buchstaben-Event an alle angehängten Karten senden
    /// </summary>
    public void TriggerLetterEvent(string letters)
    {
        foreach (var card in attachedCards)
        {
            if (card != null)
            {
                card.TriggerLetterEvent(letters);
            }
        }
    }
    
    /// <summary>
    /// Alle angehängten Karten ausspielen
    /// </summary>
    public void PlayAllCards()
    {
        foreach (var card in attachedCards)
        {
            if (card != null)
            {
                card.PlayCard();
            }
        }
    }
    
    /// <summary>
    /// Bonus-Effekte aller Karten auslösen
    /// </summary>
    public void TriggerBonusEffects(BonusEffectType effectType)
    {
        foreach (var card in attachedCards)
        {
            if (card != null)
            {
                card.TriggerBonusEffects(effectType);
            }
        }
    }
    
    private void SubscribeToCardEvents()
    {
        // TODO: Hier können Sie sich für Card-Events registrieren:
        // Card.OnCardPlayed += OnCardPlayed;
        // Card.OnCardSelected += OnCardSelected;
    }
    
    private void UnsubscribeFromCardEvents(Card card)
    {
        // TODO: Spezifische Card-Event-Abmeldung implementieren
    }
    
    private void UnsubscribeFromAllCardEvents()
    {
        // TODO: Von allen Card-Events abmelden:
        // Card.OnCardPlayed -= OnCardPlayed;
        // Card.OnCardSelected -= OnCardSelected;
    }
    
    // Event handlers (TODO: Implementieren Sie diese nach Bedarf)
    private void OnCardPlayed(Card card)
    {
        // TODO: Reagieren auf gespielte Karten
        Debug.Log($"[DragObject] Card {card.Data.cardName} was played from {gameObject.name}");
    }
    
    private void OnCardSelected(Card card)
    {
        // TODO: Reagieren auf Kartenauswahl
        Debug.Log($"[DragObject] Card {card.Data.cardName} was selected on {gameObject.name}");
    }
    
    // Editor helper methods
    #if UNITY_EDITOR
    [ContextMenu("Detect Cards")]
    private void EditorDetectCards()
    {
        DetectAttachedCards();
    }
    
    [ContextMenu("Log Card Info")]
    private void EditorLogCardInfo()
    {
        Debug.Log($"[DragObject] {gameObject.name} has {attachedCards.Count} cards:");
        foreach (var card in attachedCards)
        {
            if (card != null && card.Data != null)
            {
                Debug.Log($"- {card.Data.cardName} (Tier {card.Data.tier}, Letters: {card.Data.letterValues})");
            }
        }
    }
    #endif
    
    private void OnDestroy()
    {
        UnsubscribeFromAllCardEvents();
    }
}