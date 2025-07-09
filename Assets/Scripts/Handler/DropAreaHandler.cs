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
    
    [Header("Drop Area Type")]
    [SerializeField] private DropAreaType areaType = DropAreaType.PlayArea;
    
    private Image dropAreaImage;
    private GameObject currentDraggedCard;
    private bool canAcceptDrop = false;
    
    public enum DropAreaType
    {
        PlayArea,
        DiscardArea,
        SlotArea
    }
    
    void Awake()
    {
        InitializeComponent();
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
    
    private void InitializeComponent()
    {
        dropAreaImage = GetComponent<Image>();
        if (dropAreaImage == null)
            dropAreaImage = gameObject.AddComponent<Image>();
        
        if (dropAreaImage.color.a == 0)
            dropAreaImage.color = normalColor;
    }
    
    // === DRAG EVENT HANDLERS ===
    
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
        if (!cardComponent.IsPlayable())
        {
            canAcceptDrop = false;
            dropAreaImage.color = normalColor;
            return;
        }
        
        // Validiere basierend auf Area Type
        canAcceptDrop = areaType switch
        {
            DropAreaType.PlayArea => ValidatePlayAreaDrop(cardComponent),
            DropAreaType.DiscardArea => ValidateDiscardAreaDrop(cardComponent),
            DropAreaType.SlotArea => ValidateSlotAreaDrop(cardComponent),
            _ => false
        };
        
        dropAreaImage.color = canAcceptDrop ? highlightColor : invalidColor;
    }
    
    private bool ValidatePlayAreaDrop(Card card)
    {
        var cardList = new List<Card> { card };
        return SpellcastManager.CheckCanPlayCards(cardList) &&
               CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => 
                   cm.CanPerformPlayerAction(PlayerActionType.PlayCards));
    }
    
    private bool ValidateDiscardAreaDrop(Card card)
    {
        return SpellcastManager.CheckCanDiscardCard(card) &&
               CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => 
                   cm.CanSpendResource(ResourceType.Creativity, 1));
    }
    
    private bool ValidateSlotAreaDrop(Card card)
    {
        return CoreExtensions.TryWithManager<CardSlotManager, bool>(this, csm => 
            csm.IsEnabled && csm.HasEmptySlots);
    }
    
    // === UI EVENT HANDLERS ===
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentDraggedCard != null && canAcceptDrop)
        {
            Color hoverColor = highlightColor;
            hoverColor.a = Mathf.Min(1f, highlightColor.a * 1.5f);
            dropAreaImage.color = hoverColor;
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
        if (eventData.pointerDrag != null && canAcceptDrop)
        {
            Card cardComponent = eventData.pointerDrag.GetComponent<Card>();
            if (cardComponent != null)
            {
                HandleDrop(cardComponent, eventData);
            }
        }
        
        Invoke(nameof(ResetColor), 0.1f);
    }
    
    private void HandleDrop(Card card, PointerEventData eventData)
    {
        switch (areaType)
        {
            case DropAreaType.SlotArea:
                HandleSlotAreaDrop(card, eventData);
                break;
                
            case DropAreaType.PlayArea:
                // Handled by CardDragHandler
                break;
                
            case DropAreaType.DiscardArea:
                // Handled by CardDragHandler
                break;
        }
    }
    
    private void HandleSlotAreaDrop(Card card, PointerEventData eventData)
    {
        CoreExtensions.TryWithManager<CardSlotManager>(this, csm => 
        {
            if (csm.IsEnabled && csm.HasEmptySlots)
            {
                csm.TryPlaceCardInSlot(card);
            }
        });
    }
    
    private void ResetColor()
    {
        dropAreaImage.color = normalColor;
    }
    
    // === PUBLIC API ===
    
    public void RefreshDropValidity()
    {
        if (currentDraggedCard != null)
            UpdateDropValidity();
    }
    
    public void SetAreaType(DropAreaType type)
    {
        areaType = type;
        RefreshDropValidity();
    }

#if UNITY_EDITOR
    [ContextMenu("Test Drop Validation")]
    private void TestDropValidation()
    {
        Debug.Log($"[DropAreaHandler] Area Type: {areaType}");
        Debug.Log($"  Can Accept Drop: {canAcceptDrop}");
        Debug.Log($"  Current Dragged Card: {currentDraggedCard?.name ?? "None"}");
    }
#endif
}