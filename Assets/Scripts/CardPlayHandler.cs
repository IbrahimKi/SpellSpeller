using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class CardPlayHandler : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button drawButton;
    
    [Header("Settings")]
    [SerializeField] private int initialHandSize = 5;
    
    private List<Card> _selectedCards = new List<Card>();
    private bool _managersInitialized = false;
    
    // Events
    public static event System.Action OnManagersReady;
    
    private void Awake()
    {
        SetupButtons();
    }
    
    private void Start()
    {
        StartCoroutine(WaitForManagerInitialization());
    }
    
    private void SetupButtons()
    {
        playButton?.onClick.AddListener(PlaySelectedCards);
        clearButton?.onClick.AddListener(ClearSelection);
        drawButton?.onClick.AddListener(DrawCard);
        
        SetButtonsInteractable(false);
    }
    
    private IEnumerator WaitForManagerInitialization()
    {
        // Wait for GameManager
        while (!GameManager.HasInstance || !GameManager.Instance.IsInitialized)
        {
            yield return null;
        }
        
        OnManagersInitialized();
    }
    
    private void OnManagersInitialized()
    {
        _managersInitialized = true;
        SetButtonsInteractable(true);
        SubscribeToEvents();
        OnManagersReady?.Invoke();
    }
    
    private void SetButtonsInteractable(bool interactable)
    {
        if (playButton) playButton.interactable = interactable && _selectedCards.Count > 0;
        if (clearButton) clearButton.interactable = interactable && _selectedCards.Count > 0;
        if (drawButton) drawButton.interactable = interactable && CanDraw();
    }
    
    private void SubscribeToEvents()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnSelectionChanged += OnSelectionChanged;
            CardManager.OnHandUpdated += OnHandUpdated;
        }
        
        if (CombatManager.HasInstance)
        {
            CombatManager.OnCombatStarted += OnCombatStarted;
        }
    }
    
    private void OnDestroy()
    {
        playButton?.onClick.RemoveAllListeners();
        clearButton?.onClick.RemoveAllListeners();
        drawButton?.onClick.RemoveAllListeners();
        
        UnsubscribeFromEvents();
    }
    
    private void UnsubscribeFromEvents()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnSelectionChanged -= OnSelectionChanged;
            CardManager.OnHandUpdated -= OnHandUpdated;
        }
        
        if (CombatManager.HasInstance)
        {
            CombatManager.OnCombatStarted -= OnCombatStarted;
        }
    }
    
    // Event Handlers
    private void OnCombatStarted() => ClearSelection();
    
    private void OnSelectionChanged(List<Card> selectedCards)
    {
        _selectedCards.Clear();
        if (selectedCards != null) _selectedCards.AddRange(selectedCards);
        UpdateButtonStates();
    }
    
    private void OnHandUpdated(List<Card> handCards) 
    {
        UpdateButtonStates();
    }
    
    // Core Actions
    public void PlaySelectedCards()
    {
        if (!_managersInitialized || _selectedCards.Count == 0) return;
        
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.Instance.ProcessCardPlay(_selectedCards);
        }
    }
    
    public void DrawCard()
    {
        if (!_managersInitialized || !CanDraw()) return;
        
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.Instance.DrawCard();
        }
    }
    
    public void ClearSelection()
    {
        if (!_managersInitialized) return;
        
        if (CardManager.HasInstance)
        {
            CardManager.Instance.ClearSelection();
        }
        
        _selectedCards.Clear();
    }
    
    private void UpdateButtonStates()
    {
        bool hasCards = _selectedCards.Count > 0;
        bool isPlayerTurn = CombatManager.HasInstance && CombatManager.Instance.IsPlayerTurn;
        
        if (playButton) playButton.interactable = _managersInitialized && hasCards && isPlayerTurn;
        if (clearButton) clearButton.interactable = _managersInitialized && hasCards;
        if (drawButton) drawButton.interactable = _managersInitialized && CanDraw() && isPlayerTurn;
    }
    
    private bool CanDraw()
    {
        return CardManager.HasInstance && !CardManager.Instance.IsHandFull && 
               DeckManager.HasInstance && !DeckManager.Instance.IsDeckEmpty;
    }
    
    // Properties
    public bool HasSelectedCards => _selectedCards.Count > 0;
    public int SelectedCardCount => _selectedCards.Count;
    public bool ManagersInitialized => _managersInitialized;
}