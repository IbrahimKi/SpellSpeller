using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DragObject : MonoBehaviour
{
    [SerializeField] private bool isDraggable = true;
    
    private int _originalSortingOrder;
    private int _dragSortingOrderBonus = 10;
    private Canvas _canvas;
    
    [Header("Card System")]
    [SerializeField] private bool autoDetectCards = true;
    [SerializeField] private List<Card> attachedCards = new List<Card>();
    
    public System.Action<DragObject> OnDragStarted;
    public System.Action<DragObject> OnDragEnded;
    
    public bool IsDraggable => isDraggable;
    public List<Card> AttachedCards => attachedCards;
    
    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        
        if (_canvas != null)
            _originalSortingOrder = _canvas.sortingOrder;
        
        if (autoDetectCards)
            DetectAttachedCards();
    }
    
    public void SetDraggable(bool draggable)
    {
        isDraggable = draggable;
    }
    
    public void OnDragStart()
    {
        if (_canvas != null)
            _canvas.sortingOrder = _originalSortingOrder + _dragSortingOrderBonus;
        
        OnDragStarted?.Invoke(this);
    }
    
    public void OnDragEnd()
    {
        if (_canvas != null)
            _canvas.sortingOrder = _originalSortingOrder;
        
        OnDragEnded?.Invoke(this);
    }
    
    public void DetectAttachedCards()
    {
        attachedCards.Clear();
        
        Card mainCard = GetComponent<Card>();
        if (mainCard != null)
            attachedCards.Add(mainCard);
        
        Card[] childCards = GetComponentsInChildren<Card>();
        foreach (var card in childCards)
        {
            if (card != mainCard && !attachedCards.Contains(card))
                attachedCards.Add(card);
        }
        
        attachedCards = attachedCards.Where(card => card != null).ToList();
    }
}