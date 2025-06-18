using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class DropAreaHandler : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private Color highlightColor = new Color(0f, 1f, 0f, 0.3f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.3f);
    
    private Image dropAreaImage;
    private GameObject currentDraggedCard;
    private bool canAcceptDrop = false;
    
    void Awake()
    {
        dropAreaImage = GetComponent<Image>();
        if (dropAreaImage == null)
        {
            dropAreaImage = gameObject.AddComponent<Image>();
        }
        
        // Set initial color
        if (dropAreaImage.color.a == 0)
        {
            dropAreaImage.color = normalColor;
        }
    }
    
    void OnEnable()
    {
        // Subscribe to drag events
        CardDragHandler.OnCardDragStart.AddListener(OnCardDragStart);
        CardDragHandler.OnCardDragEnd.AddListener(OnCardDragEnd);
    }
    
    void OnDisable()
    {
        // Unsubscribe from events
        CardDragHandler.OnCardDragStart.RemoveListener(OnCardDragStart);
        CardDragHandler.OnCardDragEnd.RemoveListener(OnCardDragEnd);
    }
    
    private void OnCardDragStart(GameObject card)
    {
        currentDraggedCard = card;
        Debug.Log($"[DropAreaHandler] Card drag started: {card.name}");
        
        // Check if this area can accept the card
        UpdateDropValidity();
    }
    
    private void OnCardDragEnd(GameObject card)
    {
        currentDraggedCard = null;
        canAcceptDrop = false;
        dropAreaImage.color = normalColor;
        Debug.Log($"[DropAreaHandler] Card drag ended: {card.name}");
    }
    
    private void UpdateDropValidity()
    {
        if (currentDraggedCard == null)
        {
            canAcceptDrop = false;
            dropAreaImage.color = normalColor;
            return;
        }
        
        Card cardComponent = currentDraggedCard.GetComponent<Card>();
        if (cardComponent == null)
        {
            canAcceptDrop = false;
            dropAreaImage.color = normalColor;
            return;
        }
        
        // Check based on area type
        if (gameObject.CompareTag("PlayArea"))
        {
            // For play area, check if we can play the card
            var cardList = new List<Card> { cardComponent };
            canAcceptDrop = SpellcastManager.CheckCanPlayCards(cardList);
        }
        else if (gameObject.CompareTag("DiscardArea"))
        {
            // For discard area, check if we can discard
            canAcceptDrop = SpellcastManager.CheckCanDiscardCard(cardComponent);
        }
        else if (gameObject.CompareTag("CardSlot"))
        {
            // For card slots, check if slot is empty
            var slot = GetComponent<CardSlot>();
            canAcceptDrop = slot != null && slot.CanAcceptCard(currentDraggedCard);
        }
        else
        {
            canAcceptDrop = false;
        }
        
        // Update visual feedback
        dropAreaImage.color = canAcceptDrop ? highlightColor : invalidColor;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentDraggedCard != null)
        {
            // Update validity check when hovering
            UpdateDropValidity();
            
            // Show enhanced feedback when hovering
            if (canAcceptDrop)
            {
                // Make color slightly brighter when hovering
                Color hoverColor = highlightColor;
                hoverColor.a = Mathf.Min(1f, highlightColor.a * 1.5f);
                dropAreaImage.color = hoverColor;
            }
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentDraggedCard != null)
        {
            // Return to base validity color
            dropAreaImage.color = canAcceptDrop ? highlightColor : invalidColor;
        }
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedCard = eventData.pointerDrag;
        
        if (droppedCard != null && canAcceptDrop)
        {
            // The actual drop is handled by CardDragHandler
            Debug.Log($"[DropAreaHandler] Valid drop on {gameObject.name}");
        }
        else
        {
            Debug.Log($"[DropAreaHandler] Invalid drop rejected on {gameObject.name}");
        }
        
        // Reset color after drop
        dropAreaImage.color = normalColor;
    }
    
    // Helper method to manually update validity (useful for turn changes)
    public void RefreshDropValidity()
    {
        if (currentDraggedCard != null)
        {
            UpdateDropValidity();
        }
    }
    
#if UNITY_EDITOR
    [ContextMenu("Test Colors")]
    private void TestColors()
    {
        if (dropAreaImage == null)
            dropAreaImage = GetComponent<Image>();
            
        if (dropAreaImage != null)
        {
            Debug.Log("Testing colors - watch the area change:");
            Debug.Log("Normal: " + normalColor);
            Debug.Log("Highlight: " + highlightColor);
            Debug.Log("Invalid: " + invalidColor);
        }
    }
#endif
}