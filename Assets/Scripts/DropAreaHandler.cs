using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropAreaHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Area Type")]
    [SerializeField] private AreaType areaType = AreaType.Play;
    
    [Header("Visual Feedback")]
    [SerializeField] private Image highlightImage;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.1f);
    [SerializeField] private Color hoverColor = new Color(0f, 1f, 0f, 0.3f);
    [SerializeField] private Color validDropColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidDropColor = new Color(1f, 0f, 0f, 0.3f);
    
    private bool _isHovered;
    private bool _isDragActive;
    
    // Events
    public static event System.Action<AreaType> OnAreaHovered;
    public static event System.Action<AreaType> OnAreaUnhovered;
    
    public enum AreaType
    {
        Play,
        Discard
    }
    
    private void Awake()
    {
        // Ensure we have required tag
        switch (areaType)
        {
            case AreaType.Play:
                gameObject.tag = "PlayArea";
                break;
            case AreaType.Discard:
                gameObject.tag = "DiscardArea";
                break;
        }
        
        // Setup highlight if not assigned
        if (highlightImage == null)
        {
            highlightImage = GetComponent<Image>();
            if (highlightImage == null)
            {
                highlightImage = gameObject.AddComponent<Image>();
                highlightImage.raycastTarget = true;
            }
        }
        
        UpdateVisual();
    }
    
    private void OnEnable()
    {
        CardDragHandler.OnCardDragStart += OnCardDragStart;
        CardDragHandler.OnCardDragEnd += OnCardDragEnd;
    }
    
    private void OnDisable()
    {
        CardDragHandler.OnCardDragStart -= OnCardDragStart;
        CardDragHandler.OnCardDragEnd -= OnCardDragEnd;
    }
    
    private void OnCardDragStart(Card card)
    {
        _isDragActive = true;
        UpdateVisual();
    }
    
    private void OnCardDragEnd(Card card)
    {
        _isDragActive = false;
        UpdateVisual();
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        UpdateVisual();
        OnAreaHovered?.Invoke(areaType);
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        UpdateVisual();
        OnAreaUnhovered?.Invoke(areaType);
    }
    
    private void UpdateVisual()
    {
        if (highlightImage == null) return;
        
        Color targetColor = normalColor;
        
        if (_isDragActive)
        {
            if (_isHovered)
            {
                targetColor = CanAcceptDrop() ? validDropColor : invalidDropColor;
            }
            else
            {
                targetColor = hoverColor;
            }
        }
        else if (_isHovered)
        {
            targetColor = hoverColor;
        }
        
        highlightImage.color = targetColor;
    }
    
    private bool CanAcceptDrop()
    {
        switch (areaType)
        {
            case AreaType.Play:
                return CardManager.HasInstance && CardManager.Instance.SelectedCards.Count > 0;
                
            case AreaType.Discard:
                return CardManager.HasInstance && 
                       CardManager.Instance.SelectedCards.Count == 1 &&
                       CombatManager.HasInstance && 
                       CombatManager.Instance.CanSpendCreativity(1);
                
            default:
                return false;
        }
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        
        // Update tag in editor
        switch (areaType)
        {
            case AreaType.Play:
                gameObject.tag = "PlayArea";
                break;
            case AreaType.Discard:
                gameObject.tag = "DiscardArea";
                break;
        }
    }
#endif
}