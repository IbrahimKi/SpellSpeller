using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Complete Extension Methods für Card-Klasse
/// Funktioniert mit jeder Card-Klasse, auch ohne Modifikationen
/// Automatic fallbacks und kompatibel mit bestehenden Extensions
/// </summary>
public static class CardExtensions
{
    // === HAND INDEX MANAGEMENT ===
    
    /// <summary>
    /// Setzt den Hand-Index der Karte
    /// Kompatibel mit direkten Properties und Extension Methods
    /// </summary>
    public static void SetHandIndex(this Card card, int index)
    {
        if (card == null) return;
        
        // Try direct property first
        var handIndexProperty = card.GetType().GetProperty("HandIndex");
        if (handIndexProperty != null && handIndexProperty.CanWrite)
        {
            handIndexProperty.SetValue(card, index);
            return;
        }
        
        // Try direct method
        var setHandIndexMethod = card.GetType().GetMethod("SetHandIndex");
        if (setHandIndexMethod != null)
        {
            setHandIndexMethod.Invoke(card, new object[] { index });
            return;
        }
        
        // Fallback: Use component
        var tracker = card.GetComponent<CardHandIndexTracker>();
        if (tracker == null)
            tracker = card.gameObject.AddComponent<CardHandIndexTracker>();
        tracker.HandIndex = index;
    }
    
    /// <summary>
    /// Holt den Hand-Index der Karte
    /// </summary>
    public static int HandIndex(this Card card)
    {
        if (card == null) return -1;
        
        // Try direct property first
        var handIndexProperty = card.GetType().GetProperty("HandIndex");
        if (handIndexProperty != null && handIndexProperty.CanRead)
        {
            var value = handIndexProperty.GetValue(card);
            return value is int intValue ? intValue : -1;
        }
        
        // Try component tracker
        var tracker = card.GetComponent<CardHandIndexTracker>();
        if (tracker != null)
            return tracker.HandIndex;
        
        // Fallback: Use transform sibling index if in hand
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        if (handLayoutManager != null && card.transform.parent == handLayoutManager.transform)
        {
            return card.transform.GetSiblingIndex();
        }
        
        // Last fallback: Find in hand cards list
        var cardManager = CoreExtensions.GetManager<CardManager>();
        var handCards = cardManager?.GetHandCards();
        if (handCards != null)
        {
            for (int i = 0; i < handCards.Count; i++)
            {
                if (handCards[i] == card)
                    return i;
            }
        }
        
        return -1;
    }
    
    // === HIGHLIGHT MANAGEMENT ===
    
    /// <summary>
    /// Markiert Karte als hervorgehoben
    /// </summary>
    public static void Highlight(this Card card)
    {
        if (card == null) return;
        
        // Try direct method first
        var highlightMethod = card.GetType().GetMethod("Highlight");
        if (highlightMethod != null)
        {
            highlightMethod.Invoke(card, null);
            return;
        }
        
        // Fallback: Use component and apply visual effects
        var tracker = card.GetComponent<CardHighlightTracker>();
        if (tracker == null)
            tracker = card.gameObject.AddComponent<CardHighlightTracker>();
        
        if (!tracker.IsHighlighted)
        {
            tracker.IsHighlighted = true;
            ApplyHighlightVisuals(card, true);
            
            // Fire Event if available
            TryFireHighlightEvent(card, true);
        }
    }
    
    /// <summary>
    /// Entfernt Hervorhebung von Karte
    /// </summary>
    public static void Unhighlight(this Card card)
    {
        if (card == null) return;
        
        // Try direct method first
        var unhighlightMethod = card.GetType().GetMethod("Unhighlight");
        if (unhighlightMethod != null)
        {
            unhighlightMethod.Invoke(card, null);
            return;
        }
        
        // Fallback: Use component
        var tracker = card.GetComponent<CardHighlightTracker>();
        if (tracker != null && tracker.IsHighlighted)
        {
            tracker.IsHighlighted = false;
            ApplyHighlightVisuals(card, false);
            
            // Fire Event if available
            TryFireHighlightEvent(card, false);
        }
    }
    
    /// <summary>
    /// Prüft ob Karte hervorgehoben ist
    /// </summary>
    public static bool IsHighlighted(this Card card)
    {
        if (card == null) return false;
        
        // Try direct property first
        var isHighlightedProperty = card.GetType().GetProperty("IsHighlighted");
        if (isHighlightedProperty != null && isHighlightedProperty.CanRead)
        {
            var value = isHighlightedProperty.GetValue(card);
            return value is bool boolValue && boolValue;
        }
        
        // Fallback: Use component
        var tracker = card.GetComponent<CardHighlightTracker>();
        return tracker?.IsHighlighted ?? false;
    }
    
    // === DRAG VISUAL FEEDBACK ===
    
    /// <summary>
    /// Startet visuelles Feedback für Drag-Operation
    /// </summary>
    public static void StartDrag(this Card card)
    {
        if (card == null) return;
        
        // Try direct method first
        var startDragMethod = card.GetType().GetMethod("StartDrag");
        if (startDragMethod != null)
        {
            startDragMethod.Invoke(card, null);
            return;
        }
        
        // Fallback: Apply visual effect directly
        ApplyDragVisuals(card, true);
    }
    
    /// <summary>
    /// Beendet visuelles Feedback für Drag-Operation
    /// </summary>
    public static void EndDrag(this Card card)
    {
        if (card == null) return;
        
        // Try direct method first
        var endDragMethod = card.GetType().GetMethod("EndDrag");
        if (endDragMethod != null)
        {
            endDragMethod.Invoke(card, null);
            return;
        }
        
        // Fallback: Reset visual effect
        ApplyDragVisuals(card, false);
    }
    
    // === SELECTION HELPERS ===
    
    /// <summary>
    /// Versucht Karte zu selektieren
    /// </summary>
    public static bool TrySelect(this Card card)
    {
        if (card == null || !card.IsPlayable()) return false;
        
        // Try direct method first
        var trySelectMethod = card.GetType().GetMethod("TrySelect");
        if (trySelectMethod != null)
        {
            var result = trySelectMethod.Invoke(card, null);
            return result is bool boolResult && boolResult;
        }
        
        // Fallback: Use existing Select method
        var selectMethod = card.GetType().GetMethod("Select");
        if (selectMethod != null)
        {
            selectMethod.Invoke(card, null);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Reset card state (für Object Pooling)
    /// </summary>
    public static void ResetCardState(this Card card)
    {
        if (card == null) return;
        
        // Try direct method first
        var resetMethod = card.GetType().GetMethod("ResetCardState");
        if (resetMethod != null)
        {
            resetMethod.Invoke(card, null);
            return;
        }
        
        // Fallback: Manual reset
        card.SetHandIndex(-1);
        card.Unhighlight();
        card.EndDrag();
        
        // Reset transform
        card.transform.localScale = Vector3.one;
        
        var canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }
    
    // === UTILITY METHODS ===
    
    /// <summary>
    /// Prüft ob Karte zur Hand gehört
    /// </summary>
    public static bool IsInHand(this Card card)
    {
        if (card == null) return false;
        
        var cardManager = CoreExtensions.GetManager<CardManager>();
        var handCards = cardManager?.GetHandCards();
        
        if (handCards != null)
        {
            foreach (var handCard in handCards)
            {
                if (handCard == card)
                    return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Findet benachbarte Karten in der Hand
    /// </summary>
    public static (Card left, Card right) GetAdjacentCards(this Card card)
    {
        if (card == null) return (null, null);
        
        var cardManager = CoreExtensions.GetManager<CardManager>();
        var handCards = cardManager?.GetHandCards();
        
        if (handCards == null || handCards.Count <= 1) return (null, null);
        
        int currentIndex = card.HandIndex();
        if (currentIndex < 0) return (null, null);
        
        Card leftCard = null;
        Card rightCard = null;
        
        if (currentIndex > 0)
        {
            foreach (var c in handCards)
            {
                if (c.HandIndex() == currentIndex - 1)
                {
                    leftCard = c;
                    break;
                }
            }
        }
        
        if (currentIndex < handCards.Count - 1)
        {
            foreach (var c in handCards)
            {
                if (c.HandIndex() == currentIndex + 1)
                {
                    rightCard = c;
                    break;
                }
            }
        }
        
        return (leftCard, rightCard);
    }
    
    /// <summary>
    /// Berechnet relative Position in der Hand (0.0 = links, 1.0 = rechts)
    /// </summary>
    public static float GetRelativeHandPosition(this Card card)
    {
        if (card == null) return 0f;
        
        var cardManager = CoreExtensions.GetManager<CardManager>();
        int handSize = cardManager?.HandSize ?? 1;
        int cardIndex = card.HandIndex();
        
        if (handSize <= 1 || cardIndex < 0) return 0.5f;
        
        return (float)cardIndex / (handSize - 1);
    }
    
    /// <summary>
    /// Findet Karten in einem bestimmten Bereich um diese Karte
    /// </summary>
    public static List<Card> GetNearbyCards(this Card card, int range = 1)
    {
        var result = new List<Card>();
        if (card == null) return result;
        
        var cardManager = CoreExtensions.GetManager<CardManager>();
        var handCards = cardManager?.GetHandCards();
        if (handCards == null) return result;
        
        int currentIndex = card.HandIndex();
        if (currentIndex < 0) return result;
        
        int startIndex = Mathf.Max(0, currentIndex - range);
        int endIndex = Mathf.Min(handCards.Count - 1, currentIndex + range);
        
        for (int i = startIndex; i <= endIndex; i++)
        {
            if (i != currentIndex) // Don't include self
            {
                foreach (var c in handCards)
                {
                    if (c.HandIndex() == i)
                    {
                        result.Add(c);
                        break;
                    }
                }
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Swaps positions with another card
    /// </summary>
    public static void SwapPositionWith(this Card card, Card otherCard)
    {
        if (card == null || otherCard == null) return;
        
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        if (handLayoutManager == null) return;
        
        int cardIndex = card.HandIndex();
        int otherIndex = otherCard.HandIndex();
        
        if (cardIndex >= 0 && otherIndex >= 0)
        {
            handLayoutManager.MoveCardToPosition(card, otherIndex);
            handLayoutManager.MoveCardToPosition(otherCard, cardIndex);
        }
    }
    
    // === VISUAL HELPER METHODS ===
    
    private static void ApplyHighlightVisuals(Card card, bool highlight)
    {
        if (card == null) return;
        
        // Outline Effekt
        var outline = card.GetComponent<Outline>();
        if (outline == null && highlight)
        {
            outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.yellow;
            outline.effectDistance = new Vector2(2, 2);
        }
        
        if (outline != null)
            outline.enabled = highlight;
        
        // Leichte Skalierung
        if (highlight)
            card.transform.localScale = Vector3.one * 1.02f;
        else
            card.transform.localScale = Vector3.one;
    }
    
    private static void ApplyDragVisuals(Card card, bool dragging)
    {
        if (card == null) return;
        
        var canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = card.gameObject.AddComponent<CanvasGroup>();
        
        if (dragging)
        {
            canvasGroup.alpha = 0.8f;
            canvasGroup.blocksRaycasts = false;
            card.transform.localScale = Vector3.one * 1.05f;
        }
        else
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            
            // Preserve highlight scale if highlighted
            bool isHighlighted = card.IsHighlighted();
            card.transform.localScale = isHighlighted ? Vector3.one * 1.02f : Vector3.one;
        }
    }
    
    private static void TryFireHighlightEvent(Card card, bool highlighted)
    {
        try
        {
            // Try to fire Card.OnCardHighlighted or Card.OnCardUnhighlighted
            var eventFieldName = highlighted ? "OnCardHighlighted" : "OnCardUnhighlighted";
            var eventField = card.GetType().GetField(eventFieldName, 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            
            if (eventField != null)
            {
                var eventValue = eventField.GetValue(null) as System.Action<Card>;
                eventValue?.Invoke(card);
            }
        }
        catch
        {
            // Ignore if events don't exist
        }
    }
}

/// <summary>
/// Helper Component um Hand-Index zu tracken
/// Wird automatisch hinzugefügt falls die Card-Klasse kein HandIndex Property hat
/// </summary>
[System.Serializable]
public class CardHandIndexTracker : MonoBehaviour
{
    [SerializeField] private int _handIndex = -1;
    
    public int HandIndex 
    { 
        get => _handIndex; 
        set => _handIndex = value; 
    }
    
    // Optional: Visual feedback when index changes
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            // Could update visual ordering here if needed
        }
    }
}

/// <summary>
/// Helper Component um Highlight-Status zu tracken
/// Wird automatisch hinzugefügt falls die Card-Klasse kein IsHighlighted Property hat
/// </summary>
[System.Serializable]
public class CardHighlightTracker : MonoBehaviour
{
    [SerializeField] private bool _isHighlighted = false;
    
    public bool IsHighlighted 
    { 
        get => _isHighlighted; 
        set 
        {
            if (_isHighlighted != value)
            {
                _isHighlighted = value;
                UpdateVisual();
            }
        } 
    }
    
    private void UpdateVisual()
    {
        // Apply highlight visual effect automatically
        var outline = GetComponent<Outline>();
        if (outline == null && _isHighlighted)
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = Color.yellow;
            outline.effectDistance = new Vector2(2, 2);
        }
        
        if (outline != null)
            outline.enabled = _isHighlighted;
        
        // Scale effect
        transform.localScale = _isHighlighted ? Vector3.one * 1.02f : Vector3.one;
    }
}

/// <summary>
/// Integration with existing Extensions
/// Macht CardExtensions kompatibel mit CoreExtensions und GameExtensions
/// </summary>
public static class CardExtensionsIntegration
{
    /// <summary>
    /// Enhanced GetCardName mit Fallbacks
    /// </summary>
    public static string GetCardName(this Card card)
    {
        if (card == null) return "Unknown Card";
        
        // Try CardData first (from GameExtensions)
        if (card.CardData != null)
            return card.CardData.cardName ?? "Unknown";
        
        // Try entityAsset (from EntityExtensions)
        var entityAsset = card.GetType().GetProperty("entityAsset")?.GetValue(card);
        if (entityAsset != null)
        {
            var entityName = entityAsset.GetType().GetProperty("EntityName")?.GetValue(entityAsset);
            if (entityName is string name)
                return name;
        }
        
        // Fallback: GameObject name
        return card.gameObject.name;
    }
    
    /// <summary>
    /// Enhanced IsPlayable mit mehreren Checks
    /// </summary>
    public static bool IsPlayable(this Card card)
    {
        if (card == null) return false;
        
        // Try direct method first
        var isPlayableMethod = card.GetType().GetMethod("IsPlayable");
        if (isPlayableMethod != null)
        {
            var result = isPlayableMethod.Invoke(card, null);
            return result is bool boolResult && boolResult;
        }
        
        // Fallback checks
        bool hasCardData = card.CardData != null;
        bool isInteractable = true;
        
        var isInteractableProperty = card.GetType().GetProperty("IsInteractable");
        if (isInteractableProperty != null && isInteractableProperty.CanRead)
        {
            var value = isInteractableProperty.GetValue(card);
            isInteractable = value is bool boolValue && boolValue;
        }
        
        return hasCardData && isInteractable;
    }
    
    /// <summary>
    /// Integration mit GameExtensions GetLetters
    /// </summary>
    public static string GetLetters(this Card card)
    {
        if (card?.CardData?.letterValues != null)
            return card.CardData.letterValues;
        return "";
    }
    
    /// <summary>
    /// Integration mit GameExtensions GetCardType
    /// </summary>
    public static CardType GetCardType(this Card card)
    {
        if (card?.CardData != null)
            return card.CardData.cardType;
        return CardType.Special;
    }
    
    /// <summary>
    /// Integration mit GameExtensions GetTier
    /// </summary>
    public static int GetTier(this Card card)
    {
        if (card?.CardData != null)
            return card.CardData.tier;
        return 0;
    }
}