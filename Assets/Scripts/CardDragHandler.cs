using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private Transform originalParent;
    private int originalSiblingIndex;
    
    // Drag state
    private Vector2 dragOffset;
    private Camera eventCamera;
    
    // Events für Drag-Aktionen
    public static UnityEvent<GameObject> OnCardDragStart = new UnityEvent<GameObject>();
    public static UnityEvent<GameObject> OnCardDragEnd = new UnityEvent<GameObject>();

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
    
    void Start()
    {
        // Canvas im Start finden, um sicherzugehen dass alles initialisiert ist
        FindCanvas();
    }
    
    private void FindCanvas()
    {
        // Finde den Root Canvas
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Canvas rootCanvas = canvas.rootCanvas;
            if (rootCanvas != null)
                canvas = rootCanvas;
        }
        
        if (canvas == null)
        {
            Debug.LogError($"[CardDragHandler] No Canvas found for {gameObject.name}!");
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (canvas == null) FindCanvas();
        if (canvas == null) return;
        
        // Speichere Original-Zustand
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        
        // Speichere die verwendete Camera
        eventCamera = eventData.pressEventCamera;
        if (eventCamera == null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            eventCamera = null; // Overlay mode doesn't need camera
        
        // Parent wechseln für korrektes Rendering über anderen UI Elementen
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();
        
        // KRITISCH: Berechne Offset NACH dem Parent-Wechsel
        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventCamera,
            out localPointerPosition))
        {
            // Offset = Aktuelle Position - Mausposition (beide in Canvas-Space)
            dragOffset = rectTransform.anchoredPosition - localPointerPosition;
        }
        else
        {
            dragOffset = Vector2.zero;
        }
        
        // Visuelles Feedback
        canvasGroup.alpha = 0.8f;
        canvasGroup.blocksRaycasts = false;
        
        // Event feuern
        OnCardDragStart?.Invoke(gameObject);
        
        Debug.Log($"[CardDragHandler] Begin Drag - Screen: {eventData.position}, Canvas Local: {localPointerPosition}, Card Pos: {rectTransform.anchoredPosition}, Offset: {dragOffset}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;
        
        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventCamera,
            out localPointerPosition))
        {
            // Neue Position = Mausposition + Offset
            rectTransform.anchoredPosition = localPointerPosition + dragOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"[CardDragHandler] End Drag at position: {rectTransform.anchoredPosition}");
        
        // Prüfe Drop-Target
        GameObject dropTarget = null;
        
        // Finde alle Objekte unter der Maus
        var raycastResults = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        
        // Suche nach einem gültigen Drop-Target (ignoriere die Karte selbst)
        foreach (var result in raycastResults)
        {
            if (result.gameObject != gameObject)
            {
                // Prüfe verschiedene Drop-Area Typen
                if (result.gameObject.CompareTag("PlayArea"))
                {
                    dropTarget = result.gameObject;
                    HandlePlayAreaDrop(dropTarget);
                    break;
                }
                else if (result.gameObject.CompareTag("DiscardArea"))
                {
                    dropTarget = result.gameObject;
                    HandleDiscardAreaDrop(dropTarget);
                    break;
                }
                else if (result.gameObject.CompareTag("CardSlot"))
                {
                    dropTarget = result.gameObject;
                    HandleCardSlotDrop(dropTarget);
                    break;
                }
            }
        }
        
        // Wenn kein gültiges Target gefunden wurde, zurück zur ursprünglichen Position
        if (dropTarget == null)
        {
            ReturnToOriginalPosition();
        }
        
        // Visuelles Feedback zurücksetzen
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        
        // Event feuern
        OnCardDragEnd?.Invoke(gameObject);
    }
    
    private void HandlePlayAreaDrop(GameObject playArea)
    {
        Debug.Log($"[CardDragHandler] Card dropped on Play Area");
        
        Card cardComponent = GetComponent<Card>();
        if (cardComponent == null)
        {
            ReturnToOriginalPosition();
            return;
        }
        
        // Check if we can play this card
        var cardList = new List<Card> { cardComponent };
        if (!SpellcastManager.CheckCanPlayCards(cardList))
        {
            Debug.Log("[CardDragHandler] Cannot play card - conditions not met");
            ReturnToOriginalPosition();
            return;
        }
        
        // Return to original position first
        ReturnToOriginalPosition();
        
        // Select and play the card
        if (!cardComponent.IsSelected)
        {
            cardComponent.Select();
        }
        
        // Play the card next frame
        StartCoroutine(PlayCardNextFrame());
    }
    
    private System.Collections.IEnumerator PlayCardNextFrame()
    {
        yield return null; // Warte einen Frame
        
        // Nutze CardPlayHandler's PlaySelectedCards statt direkt SpellcastManager
        if (CardManager.HasInstance && CardManager.Instance.SelectedCards.Count > 0)
        {
            // Finde CardPlayHandler und trigger play
            var cardPlayHandler = FindObjectOfType<CardPlayHandler>();
            if (cardPlayHandler != null)
            {
                cardPlayHandler.PlaySelectedCards();
            }
            else if (SpellcastManager.HasInstance)
            {
                // Fallback: Nutze SpellcastManager direkt
                SpellcastManager.Instance.TryPlayCards(CardManager.Instance.SelectedCards);
            }
        }
    }
    
    private void HandleDiscardAreaDrop(GameObject discardArea)
    {
        Debug.Log($"[CardDragHandler] Card dropped on Discard Area");
        
        Card cardComponent = GetComponent<Card>();
        if (cardComponent == null)
        {
            ReturnToOriginalPosition();
            return;
        }
        
        // Check if we can discard this card
        if (!SpellcastManager.CheckCanDiscardCard(cardComponent))
        {
            Debug.Log("[CardDragHandler] Cannot discard - not enough creativity or no cards to draw");
            ReturnToOriginalPosition();
            return;
        }
        
        // Proceed with discard
        if (CombatManager.HasInstance)
        {
            CombatManager.Instance.SpendCreativity(1);
        }
        
        // Add to discard pile
        if (DeckManager.HasInstance && cardComponent.CardData != null)
        {
            DeckManager.Instance.DiscardCard(cardComponent.CardData);
        }
        
        // Remove from hand and destroy
        if (CardManager.HasInstance)
        {
            CardManager.Instance.RemoveCardFromHand(cardComponent);
            CardManager.Instance.DestroyCard(cardComponent);
        }
        
        // Draw new card
        if (DeckManager.HasInstance)
        {
            var newCardData = DeckManager.Instance.DrawCard();
            if (newCardData != null && CardManager.HasInstance)
            {
                CardManager.Instance.SpawnCard(newCardData, null, true);
            }
        }
        
        Debug.Log("[CardDragHandler] Card discarded successfully");
    }
    
    private void HandleCardSlotDrop(GameObject cardSlot)
    {
        Debug.Log($"[CardDragHandler] Card dropped on Card Slot");
        
        CardSlot slot = cardSlot.GetComponent<CardSlot>();
        if (slot != null && slot.CanAcceptCard(gameObject))
        {
            slot.PlaceCard(gameObject);
        }
        else
        {
            ReturnToOriginalPosition();
        }
    }

    private void ReturnToOriginalPosition()
    {
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.anchoredPosition = originalPosition;
    }
    
    // Debug-Hilfe
    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && canvas != null)
        {
            // Zeige Canvas-Bounds
            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect != null)
            {
                Vector3[] corners = new Vector3[4];
                canvasRect.GetWorldCorners(corners);
                Gizmos.color = Color.green;
                for (int i = 0; i < 4; i++)
                {
                    Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
                }
            }
        }
    }
}

// CardSlot bleibt gleich
public class CardSlot : MonoBehaviour
{
    private GameObject currentCard;
    
    public bool CanAcceptCard(GameObject card)
    {
        return currentCard == null;
    }
    
    public void PlaceCard(GameObject card)
    {
        currentCard = card;
        
        card.transform.SetParent(transform);
        
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.localScale = Vector3.one;
    }
    
    public void RemoveCard()
    {
        currentCard = null;
    }
}