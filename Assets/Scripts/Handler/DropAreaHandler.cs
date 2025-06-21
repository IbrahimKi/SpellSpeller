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
        
        if (dropAreaImage.color.a == 0)
        {
            dropAreaImage.color = normalColor;
        }
    }
    
    void OnEnable()
    {
        CardDragHandler.OnCardDragStart.AddListener(OnCardDragStart);
        CardDragHandler.OnCardDragEnd.AddListener(OnCardDragEnd);
    }
    
    void OnDisable()
    {
        CardDragHandler.OnCardDragStart.RemoveListener(OnCardDragStart);
        CardDragHandler.OnCardDragEnd.RemoveListener(OnCardDragEnd);
    }
    
    private void OnCardDragStart(GameObject card)
    {
        currentDraggedCard = card;
        UpdateDropValidity();
    }
    
    private void OnCardDragEnd(GameObject card)
    {
        currentDraggedCard = null;
        canAcceptDrop = false;
        dropAreaImage.color = normalColor;
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
            // For play area - always allow adding to combo during player turn
            var cardList = new List<Card> { cardComponent };
            canAcceptDrop = SpellcastManager.CheckCanPlayCards(cardList);
        }
        else if (gameObject.CompareTag("DiscardArea"))
        {
            // For discard area - check resources
            canAcceptDrop = SpellcastManager.CheckCanDiscardCard(cardComponent);
        }
        else
        {
            canAcceptDrop = false;
        }
        
        // Update visual
        dropAreaImage.color = canAcceptDrop ? highlightColor : invalidColor;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentDraggedCard != null)
        {
            UpdateDropValidity();
            
            if (canAcceptDrop)
            {
                // Enhanced hover feedback
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
            dropAreaImage.color = canAcceptDrop ? highlightColor : invalidColor;
        }
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        // Drop handling is done in CardDragHandler
        // This just provides visual feedback
        
        if (eventData.pointerDrag != null && canAcceptDrop)
        {
            // Visual feedback for successful drop
            dropAreaImage.color = highlightColor;
        }
        
        // Reset color after short delay
        Invoke(nameof(ResetColor), 0.1f);
    }
    
    private void ResetColor()
    {
        dropAreaImage.color = normalColor;
    }
    
    public void RefreshDropValidity()
    {
        if (currentDraggedCard != null)
        {
            UpdateDropValidity();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Test Visual Feedback")]
    private void TestVisualFeedback()
    {
        if (dropAreaImage == null)
            dropAreaImage = GetComponent<Image>();
            
        if (dropAreaImage != null)
        {
            dropAreaImage.color = highlightColor;
            Invoke(nameof(ResetColor), 1f);
        }
    }
#endif
}