// NEUE DATEI: Assets/Scripts/Handler/DeckAreaHandler.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class DeckAreaHandler : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    [SerializeField] private Color highlightColor = new Color(0.5f, 0.8f, 0.5f, 0.7f);
    
    private Image deckImage;
    private float lastClickTime = 0f;
    private float doubleClickTime = 0.3f;
    
    void Awake()
    {
        deckImage = GetComponent<Image>();
        if (deckImage == null)
            deckImage = gameObject.AddComponent<Image>();
            
        deckImage.color = normalColor;
        
        // Ensure tag is set
        if (!gameObject.CompareTag("DeckArea"))
            gameObject.tag = "DeckArea";
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        // Handled by GroupDragHandler
        Debug.Log("[DeckAreaHandler] Cards dropped on deck");
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        float timeSinceLastClick = Time.time - lastClickTime;
        
        if (timeSinceLastClick <= doubleClickTime)
        {
            // Double click - draw with cost
            var inputController = CardInputController.Instance;
            inputController?.TryDrawWithCost();
        }
        
        lastClickTime = Time.time;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        deckImage.color = highlightColor;
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        deckImage.color = normalColor;
    }
}