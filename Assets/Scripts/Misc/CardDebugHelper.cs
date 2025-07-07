using UnityEngine;
using GameCore.Enums;

/// <summary>
/// Debug Helper für Card State Probleme
/// USAGE: Als Component zu einer Card hinzufügen für detaillierte Logs
/// </summary>
public class CardDebugHelper : MonoBehaviour
{
    [Header("Auto Debug")]
    [SerializeField] private bool logOnStateChange = true;
    [SerializeField] private bool logOnDrag = true;
    
    private Card _card;
    private CardState _lastState;
    private Vector3 _lastPosition;
    private Transform _lastParent;
    
    private void Awake()
    {
        _card = GetComponent<Card>();
        if (_card != null)
        {
            _lastState = _card.CurrentState;
            _lastPosition = transform.position;
            _lastParent = transform.parent;
        }
    }
    
    private void Update()
    {
        if (_card == null) return;
        
        // Check for state changes
        if (logOnStateChange && _card.CurrentState != _lastState)
        {
            Debug.Log($"[CardDebug] {_card.GetCardName()} state changed: {_lastState} → {_card.CurrentState}");
            _lastState = _card.CurrentState;
            LogCurrentCardState();
        }
        
        // Check for position/parent changes
        if (logOnDrag && (transform.position != _lastPosition || transform.parent != _lastParent))
        {
            Debug.Log($"[CardDebug] {_card.GetCardName()} moved: parent={transform.parent?.name}, pos={transform.position}");
            _lastPosition = transform.position;
            _lastParent = transform.parent;
        }
    }
    
    [ContextMenu("Log Current Card State")]
    public void LogCurrentCardState()
    {
        if (_card == null)
        {
            Debug.LogError("[CardDebug] No Card component found!");
            return;
        }
        
        Debug.Log($"=== CARD DEBUG: {_card.GetCardName()} ===");
        Debug.Log($"IsValid: {_card.IsValid()}");
        Debug.Log($"IsPlayable: {_card.IsPlayable()}");
        Debug.Log($"IsSelected: {_card.IsSelected}");
        Debug.Log($"IsInteractable: {_card.IsInteractable}");
        Debug.Log($"CurrentState: {_card.CurrentState}");
        Debug.Log($"CardData: {_card.CardData?.cardName ?? "NULL"}");
        
        // Transform info
        Debug.Log($"Parent: {transform.parent?.name ?? "NULL"}");
        Debug.Log($"Position: {transform.position}");
        Debug.Log($"LocalPosition: {transform.localPosition}");
        Debug.Log($"Scale: {transform.localScale}");
        
        // RectTransform info
        var rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            Debug.Log($"AnchoredPosition: {rect.anchoredPosition}");
            Debug.Log($"SizeDelta: {rect.sizeDelta}");
            Debug.Log($"AnchorMin: {rect.anchorMin}");
            Debug.Log($"AnchorMax: {rect.anchorMax}");
            Debug.Log($"Pivot: {rect.pivot}");
        }
        
        // Parent slot info
        var parentSlot = GetComponentInParent<CardSlotBehaviour>();
        if (parentSlot != null)
        {
            Debug.Log($"In Slot: {parentSlot.SlotIndex + 1}");
            Debug.Log($"Slot Enabled: {parentSlot.IsEnabled}");
            Debug.Log($"Slot Empty: {parentSlot.IsEmpty}");
            Debug.Log($"Slot OccupyingCard: {parentSlot.OccupyingCard?.GetCardName() ?? "NULL"}");
        }
        else
        {
            Debug.Log("Not in any slot");
        }
        
        // Hand manager info
        bool inHand = false;
        CoreExtensions.TryWithManager<CardManager>(this, cm => 
        {
            inHand = cm.GetHandCards().Contains(_card);
            Debug.Log($"In Hand: {inHand}");
            Debug.Log($"Hand Count: {cm.GetHandCards().Count}");
        });
        
        Debug.Log("=== END CARD DEBUG ===");
    }
    
    [ContextMenu("Force Reset Card Transform")]
    public void ForceResetCardTransform()
    {
        var rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            
            Debug.Log($"[CardDebug] Reset transform for {_card.GetCardName()}");
        }
    }
    
    [ContextMenu("Test Card Playability")]
    public void TestCardPlayability()
    {
        if (_card == null) return;
        
        Debug.Log($"=== PLAYABILITY TEST: {_card.GetCardName()} ===");
        
        // Step by step validation
        bool validRef = _card.IsValidReference();
        bool hasCardData = _card.CardData != null;
        bool isInteractable = _card.IsInteractable;
        bool isActiveAndValid = _card.IsActiveAndValid();
        
        Debug.Log($"IsValidReference: {validRef}");
        Debug.Log($"Has CardData: {hasCardData}");
        Debug.Log($"IsInteractable: {isInteractable}");
        Debug.Log($"IsActiveAndValid: {isActiveAndValid}");
        
        bool isValid = _card.IsValid();
        bool isPlayable = _card.IsPlayable();
        
        Debug.Log($"IsValid (combined): {isValid}");
        Debug.Log($"IsPlayable (final): {isPlayable}");
        
        if (!isPlayable)
        {
            Debug.LogWarning("Card is NOT playable - check the failed conditions above");
        }
        else
        {
            Debug.Log("Card IS playable");
        }
        
        Debug.Log("=== END PLAYABILITY TEST ===");
    }
    
    [ContextMenu("Add To Hand")]
    public void AddToHand()
    {
        if (_card == null) return;
        
        CoreExtensions.TryWithManager<CardManager>(this, cm => 
        {
            bool added = cm.AddCardToHand(_card);
            Debug.Log($"[CardDebug] Add to hand result: {added}");
        });
    }
    
    [ContextMenu("Remove From Hand")]
    public void RemoveFromHand()
    {
        if (_card == null) return;
        
        CoreExtensions.TryWithManager<CardManager>(this, cm => 
        {
            bool removed = cm.RemoveCardFromHand(_card);
            Debug.Log($"[CardDebug] Remove from hand result: {removed}");
        });
    }
}