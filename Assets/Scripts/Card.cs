using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
public enum CardState
{
    Idle,
    Selected,
    Dragging,
    Playing,
    Disabled
}

public class Card : MonoBehaviour
{
    [Header("Card Data")]
    [SerializeField] private CardData cardData;
    
    [Header("UI References")]
    [SerializeField] private UnityEngine.UI.Image cardImageDisplay;
    [SerializeField] private TMPro.TextMeshProUGUI nameText;
    [SerializeField] private TMPro.TextMeshProUGUI descriptionText;
    [SerializeField] private TMPro.TextMeshProUGUI tierText;
    [SerializeField] private TMPro.TextMeshProUGUI letterValuesText;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject selectionHighlight;
    [SerializeField] private CanvasGroup canvasGroup;
    
    // State management
    private CardState _currentState = CardState.Idle;
    private CardState _previousState = CardState.Idle;
    private bool _stateTransitionInProgress = false;
    
    // Events with state context
    public static event System.Action<Card, CardState, CardState> OnStateChanged;
    public static event System.Action<Card> OnCardPlayed;
    public static event System.Action<Card> OnCardSelected;
    public static event System.Action<Card> OnCardDeselected;
    
    // Properties
    public CardData Data => cardData;
    public CardState CurrentState => _currentState;
    public bool IsSelected => _currentState == CardState.Selected;
    public bool IsDragging => _currentState == CardState.Dragging;
    public bool IsInteractable => _currentState != CardState.Disabled && _currentState != CardState.Playing;
    
    private DragObject _dragObject;
    
    private void Awake()
    {
        _dragObject = GetComponent<DragObject>();
        
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }
    
    private void Start()
    {
        UpdateCardDisplay();
        SubscribeToDragEvents();
        ChangeState(CardState.Idle);
    }
    
    private void SubscribeToDragEvents()
    {
        if (_dragObject != null)
        {
            _dragObject.OnDragStarted += HandleDragStart;
            _dragObject.OnDragEnded += HandleDragEnd;
        }
    }
    
    private void UnsubscribeFromDragEvents()
    {
        if (_dragObject != null)
        {
            _dragObject.OnDragStarted -= HandleDragStart;
            _dragObject.OnDragEnded -= HandleDragEnd;
        }
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromDragEvents();
        
        if (IsSelected)
            OnCardDeselected?.Invoke(this);
    }
    
    public bool ChangeState(CardState newState)
    {
        if (_stateTransitionInProgress || _currentState == newState) 
            return false;
        
        if (!IsValidStateTransition(_currentState, newState))
        {
            Debug.LogWarning($"[EnhancedCard] Invalid state transition: {_currentState} → {newState}");
            return false;
        }
        
        _stateTransitionInProgress = true;
        _previousState = _currentState;
        _currentState = newState;
        
        ApplyStateVisuals();
        OnStateChanged?.Invoke(this, _previousState, _currentState);
        
        _stateTransitionInProgress = false;
        return true;
    }
    
    private bool IsValidStateTransition(CardState from, CardState to)
    {
        // Define valid state transitions
        return (from, to) switch
        {
            (CardState.Idle, CardState.Selected) => true,
            (CardState.Idle, CardState.Dragging) => true,
            (CardState.Idle, CardState.Disabled) => true,
            (CardState.Selected, CardState.Idle) => true,
            (CardState.Selected, CardState.Dragging) => true,
            (CardState.Selected, CardState.Playing) => true,
            (CardState.Dragging, CardState.Idle) => true,
            (CardState.Dragging, CardState.Selected) => true,
            (CardState.Playing, CardState.Idle) => true,
            (CardState.Disabled, CardState.Idle) => true,
            _ => false
        };
    }
    
    private void ApplyStateVisuals()
    {
        switch (_currentState)
        {
            case CardState.Idle:
                if (selectionHighlight != null) selectionHighlight.SetActive(false);
                if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.interactable = true; }
                transform.localScale = Vector3.one;
                break;
                
            case CardState.Selected:
                if (selectionHighlight != null) selectionHighlight.SetActive(true);
                if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.interactable = true; }
                transform.localScale = Vector3.one;
                break;
                
            case CardState.Dragging:
                if (selectionHighlight != null) selectionHighlight.SetActive(false);
                if (canvasGroup != null) canvasGroup.alpha = 0.8f;
                transform.localScale = Vector3.one * 1.1f;
                break;
                
            case CardState.Playing:
                if (canvasGroup != null) { canvasGroup.alpha = 0.7f; canvasGroup.interactable = false; }
                break;
                
            case CardState.Disabled:
                if (selectionHighlight != null) selectionHighlight.SetActive(false);
                if (canvasGroup != null) { canvasGroup.alpha = 0.5f; canvasGroup.interactable = false; }
                break;
        }
    }
    
    private void HandleDragStart(DragObject dragObject)
    {
        if (_currentState == CardState.Selected)
            ChangeState(CardState.Dragging);
        else if (_currentState == CardState.Idle)
            ChangeState(CardState.Dragging);
    }
    
    private void HandleDragEnd(DragObject dragObject)
    {
        if (_currentState == CardState.Dragging)
            ChangeState(CardState.Idle);
    }
    
    private void OnMouseDown()
    {
        if (!IsInteractable || _stateTransitionInProgress) return;
        
        HandleCardClick();
    }
    
    private void HandleCardClick()
    {
        switch (_currentState)
        {
            case CardState.Idle:
                if (ChangeState(CardState.Selected))
                    OnCardSelected?.Invoke(this);
                break;
                
            case CardState.Selected:
                if (ChangeState(CardState.Idle))
                    OnCardDeselected?.Invoke(this);
                break;
        }
    }
    
    public void SetCardData(CardData newCardData)
    {
        if (newCardData == null)
        {
            Debug.LogWarning($"[EnhancedCard] Trying to set null CardData on {gameObject.name}");
            return;
        }
        
        cardData = newCardData;
        UpdateCardDisplay();
    }
    
    private void UpdateCardDisplay()
    {
        if (cardData == null) return;
        
        if (cardImageDisplay != null) cardImageDisplay.sprite = cardData.cardImage;
        if (nameText != null) nameText.text = cardData.cardName;
        if (descriptionText != null) descriptionText.text = cardData.description;
        if (tierText != null) tierText.text = $"Tier {cardData.tier}";
        if (letterValuesText != null) letterValuesText.text = cardData.letterValues;
    }
    
    public void ForceSelect()
    {
        ChangeState(CardState.Selected);
        OnCardSelected?.Invoke(this);
    }
    
    public void ForceDeselect()
    {
        ChangeState(CardState.Idle);
        OnCardDeselected?.Invoke(this);
    }
    
    // FIX: Added missing method that CardManager calls
    public void DeselectCard()
    {
        ForceDeselect();
    }
    
    public void PlayCard()
    {
        if (ChangeState(CardState.Playing))
            OnCardPlayed?.Invoke(this);
    }
    
    public void ResetCardState()
    {
        ChangeState(CardState.Idle);
    }
    
    public void SetInteractable(bool interactable)
    {
        ChangeState(interactable ? CardState.Idle : CardState.Disabled);
    }
    
    // FIX: Added event cleanup for pooling
    public void ClearEventSubscriptions()
    {
        // This method can be called before returning card to pool
        // to prevent memory leaks from lingering event subscriptions
        UnsubscribeFromDragEvents();
    }
}