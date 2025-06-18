using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropAreaHandler : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.green;
    [SerializeField] private Color invalidColor = Color.red;
    
    [Header("Drop Settings")]
    [SerializeField] private bool acceptMultipleCards = false;
    [SerializeField] private int maxCards = 1;
    
    private Image dropAreaImage;
    private int currentCardCount = 0;
    private GameObject currentDraggedCard;
    
    void Awake()
    {
        dropAreaImage = GetComponent<Image>();
        if (dropAreaImage == null)
        {
            dropAreaImage = gameObject.AddComponent<Image>();
        }
        dropAreaImage.color = normalColor;
    }
    
    void OnEnable()
    {
        // Abonniere die Drag-Events
        CardDragHandler.OnCardDragStart.AddListener(OnCardDragStart);
        CardDragHandler.OnCardDragEnd.AddListener(OnCardDragEnd);
    }
    
    void OnDisable()
    {
        // Deabonniere die Events
        CardDragHandler.OnCardDragStart.RemoveListener(OnCardDragStart);
        CardDragHandler.OnCardDragEnd.RemoveListener(OnCardDragEnd);
    }
    
    private void OnCardDragStart(GameObject card)
    {
        currentDraggedCard = card;
        Debug.Log($"[DropAreaHandler] Card drag started: {card.name}");
        
        // Visuelles Feedback wenn Area verfügbar ist
        if (CanAcceptCard())
        {
            dropAreaImage.color = highlightColor;
        }
    }
    
    private void OnCardDragEnd(GameObject card)
    {
        currentDraggedCard = null;
        dropAreaImage.color = normalColor;
        Debug.Log($"[DropAreaHandler] Card drag ended: {card.name}");
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentDraggedCard != null)
        {
            // Zeige ob Drop möglich ist
            dropAreaImage.color = CanAcceptCard() ? highlightColor : invalidColor;
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentDraggedCard != null)
        {
            dropAreaImage.color = normalColor;
        }
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedCard = eventData.pointerDrag;
        
        if (droppedCard != null && CanAcceptCard())
        {
            PlaceCard(droppedCard);
            Debug.Log($"[DropAreaHandler] Card dropped: {droppedCard.name}");
        }
        else
        {
            Debug.Log("[DropAreaHandler] Drop rejected - area full or invalid card");
        }
        
        dropAreaImage.color = normalColor;
    }
    
    private bool CanAcceptCard()
    {
        if (!acceptMultipleCards)
        {
            return currentCardCount == 0;
        }
        return currentCardCount < maxCards;
    }
    
    private void PlaceCard(GameObject card)
    {
        // Setze die Karte als Child dieser Drop Area
        card.transform.SetParent(transform);
        
        // Positioniere die Karte
        RectTransform cardRect = card.GetComponent<RectTransform>();
        
        if (!acceptMultipleCards)
        {
            // Einzelne Karte - zentrieren
            cardRect.anchoredPosition = Vector2.zero;
        }
        else
        {
            // Multiple Karten - arrangiere sie
            ArrangeCards();
        }
        
        currentCardCount++;
        
        // Optional: Deaktiviere weiteres Dragging wenn gewünscht
        // card.GetComponent<CardDragHandler>().enabled = false;
    }
    
    private void ArrangeCards()
    {
        // Arrangiere alle Karten-Children gleichmäßig
        int childCount = transform.childCount;
        float spacing = 100f; // Abstand zwischen Karten
        float totalWidth = (childCount - 1) * spacing;
        float startX = -totalWidth / 2f;
        
        for (int i = 0; i < childCount; i++)
        {
            RectTransform child = transform.GetChild(i).GetComponent<RectTransform>();
            if (child != null)
            {
                float xPos = startX + (i * spacing);
                child.anchoredPosition = new Vector2(xPos, 0);
            }
        }
    }
    
    public void RemoveCard(GameObject card)
    {
        if (card.transform.parent == transform)
        {
            currentCardCount--;
            
            if (acceptMultipleCards)
            {
                ArrangeCards();
            }
        }
    }
    
    public void ClearArea()
    {
        // Entferne alle Karten aus dieser Area
        while (transform.childCount > 0)
        {
            Transform child = transform.GetChild(0);
            child.SetParent(null);
        }
        currentCardCount = 0;
    }
}