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
    
    // Offset zwischen Mausposition und Kartenmitte beim Start des Drags
    private Vector2 dragOffset;
    
    // Events für Drag-Aktionen
    public static UnityEvent<GameObject> OnCardDragStart = new UnityEvent<GameObject>();
    public static UnityEvent<GameObject> OnCardDragEnd = new UnityEvent<GameObject>();

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Speichere Original-Position und Parent
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        
        // Berechne den Offset zwischen Mausposition und Kartenmitte
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 mousePositionInCanvas
        );
        
        // Konvertiere die aktuelle Position der Karte in Canvas-Space
        Vector2 cardPositionInCanvas = canvas.transform.InverseTransformPoint(transform.position);
        
        // Berechne und speichere den Offset
        dragOffset = cardPositionInCanvas - mousePositionInCanvas;
        
        // Setze die Karte als letztes Child des Canvas für korrektes Rendering
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();
        
        // Mache die Karte während des Drags nicht blockierend
        canvasGroup.alpha = 0.8f;
        canvasGroup.blocksRaycasts = false;
        
        // Feuere das Drag-Start Event
        OnCardDragStart?.Invoke(gameObject);
        
        Debug.Log($"[CardDragHandler] Begin Drag - Mouse: {eventData.position}, Offset: {dragOffset}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Konvertiere Mouse-Position in Canvas-Local-Space
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );
        
        // Wende den gespeicherten Offset an
        rectTransform.anchoredPosition = localPoint + dragOffset;
        
        Debug.Log($"[CardDragHandler] Dragging - Screen: {eventData.position}, Local: {localPoint}, Final: {rectTransform.anchoredPosition}");
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"[CardDragHandler] End Drag at position: {rectTransform.anchoredPosition}");
        
        // Prüfe ob über einem gültigen Drop-Target
        GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
        
        if (dropTarget != null && dropTarget.CompareTag("CardSlot"))
        {
            // Karte wurde auf einem gültigen Slot abgelegt
            CardSlot slot = dropTarget.GetComponent<CardSlot>();
            if (slot != null && slot.CanAcceptCard(gameObject))
            {
                slot.PlaceCard(gameObject);
            }
            else
            {
                // Slot kann Karte nicht akzeptieren, zurück zum Ursprung
                ReturnToOriginalPosition();
            }
        }
        else
        {
            // Kein gültiger Drop-Target, zurück zum Ursprung
            ReturnToOriginalPosition();
        }
        
        // Stelle normale Eigenschaften wieder her
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        
        // Feuere das Drag-End Event
        OnCardDragEnd?.Invoke(gameObject);
    }

    private void ReturnToOriginalPosition()
    {
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.anchoredPosition = originalPosition;
    }
}

// Beispiel CardSlot Script
public class CardSlot : MonoBehaviour
{
    private GameObject currentCard;
    
    public bool CanAcceptCard(GameObject card)
    {
        // Prüfe ob Slot leer ist
        return currentCard == null;
    }
    
    public void PlaceCard(GameObject card)
    {
        currentCard = card;
        
        // Setze die Karte als Child des Slots
        card.transform.SetParent(transform);
        
        // Zentriere die Karte im Slot
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.localScale = Vector3.one;
    }
    
    public void RemoveCard()
    {
        currentCard = null;
    }
}