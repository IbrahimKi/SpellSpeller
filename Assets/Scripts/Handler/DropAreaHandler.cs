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
        DeckArea
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
        
        canAcceptDrop = areaType switch
        {
            DropAreaType.PlayArea => ValidatePlayAreaDrop(cardComponent),
            DropAreaType.DiscardArea => ValidateDiscardAreaDrop(cardComponent),
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
        // CardDragHandler handles the actual drop logic
        Invoke(nameof(ResetColor), 0.1f);
    }
    
    private void ResetColor()
    {
        dropAreaImage.color = normalColor;
    }
}