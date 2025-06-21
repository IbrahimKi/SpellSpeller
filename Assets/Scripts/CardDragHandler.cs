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
    
    // Events
    public static UnityEvent<GameObject> OnCardDragStart = new UnityEvent<GameObject>();
    public static UnityEvent<GameObject> OnCardDragEnd = new UnityEvent<GameObject>();

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
    void Start()
    {
        FindCanvas();
    }
    
    private void FindCanvas()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Canvas rootCanvas = canvas.rootCanvas;
            if (rootCanvas != null)
                canvas = rootCanvas;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (canvas == null) FindCanvas();
        if (canvas == null) return;
        
        // Save original state
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        
        eventCamera = eventData.pressEventCamera;
        
        // Change parent for proper rendering
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();
        
        // Calculate offset AFTER parent change
        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventCamera,
            out localPointerPosition))
        {
            dragOffset = rectTransform.anchoredPosition - localPointerPosition;
        }
        else
        {
            dragOffset = Vector2.zero;
        }
        
        // Visual feedback
        canvasGroup.alpha = 0.8f;
        canvasGroup.blocksRaycasts = false;
        
        OnCardDragStart?.Invoke(gameObject);
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
            rectTransform.anchoredPosition = localPointerPosition + dragOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        GameObject dropTarget = null;
        
        // Find drop target
        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        
        foreach (var result in raycastResults)
        {
            if (result.gameObject != gameObject)
            {
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
            }
        }
        
        // Return to original if no valid target
        if (dropTarget == null)
        {
            ReturnToOriginalPosition();
        }
        
        // Reset visual feedback
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        
        OnCardDragEnd?.Invoke(gameObject);
    }
    
    private void HandlePlayAreaDrop(GameObject playArea)
    {
        Card cardComponent = GetComponent<Card>();
        if (cardComponent == null)
        {
            ReturnToOriginalPosition();
            return;
        }
        
        // Check if we can play
        var cardList = new List<Card> { cardComponent };
        if (!SpellcastManager.CheckCanPlayCards(cardList))
        {
            ReturnToOriginalPosition();
            return;
        }
        
        // Return to position first
        ReturnToOriginalPosition();
        
        // Select if not selected
        if (!cardComponent.IsSelected)
        {
            cardComponent.Select();
        }
        
        // Play the card
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.Instance.ProcessCardPlay(cardList);
        }
    }
    
    private void HandleDiscardAreaDrop(GameObject discardArea)
    {
        Card cardComponent = GetComponent<Card>();
        if (cardComponent == null)
        {
            ReturnToOriginalPosition();
            return;
        }
        
        // Check if we can discard
        if (!SpellcastManager.CheckCanDiscardCard(cardComponent))
        {
            ReturnToOriginalPosition();
            return;
        }
        
        // Process discard
        if (CombatManager.HasInstance)
        {
            CombatManager.Instance.SpendCreativity(1);
        }
        
        if (DeckManager.HasInstance && cardComponent.CardData != null)
        {
            DeckManager.Instance.DiscardCard(cardComponent.CardData);
        }
        
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
    }

    private void ReturnToOriginalPosition()
    {
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.anchoredPosition = originalPosition;
    }
}