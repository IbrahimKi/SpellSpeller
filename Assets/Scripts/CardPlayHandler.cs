using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

public class CardPlayHandler : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button drawButton;
    [SerializeField] private TMPro.TextMeshProUGUI statusDisplay;
    
    [Header("Draw Settings")]
    [SerializeField] private List<CardData> drawPool = new List<CardData>();
    [SerializeField] private int cardsPerDraw = 1;
    
    private List<Card> _selectedCards = new List<Card>();
    private string _cachedLetterSequence = "";
    private bool _isDirty = true;
    private bool _isDestroyed = false;
    
    private void Awake()
    {
        if (playButton != null)
            playButton.onClick.AddListener(PlaySelectedCards);
        if (clearButton != null)
            clearButton.onClick.AddListener(ClearSelection);
        if (drawButton != null)
            drawButton.onClick.AddListener(DrawCards);
    }
    
    private void OnEnable()
    {
        if (_isDestroyed) return;
        
        CardManager.OnSelectionChanged += OnSelectionChanged;
        CardManager.OnHandUpdated += OnHandUpdated;
        SpellcastManager.OnSpellFound += OnSpellFound;
        SpellcastManager.OnSpellNotFound += OnSpellNotFound;
    }
    
    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }
    
    private void UnsubscribeFromEvents()
    {
        CardManager.OnSelectionChanged -= OnSelectionChanged;
        CardManager.OnHandUpdated -= OnHandUpdated;
        SpellcastManager.OnSpellFound -= OnSpellFound;
        SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
    }
    
    private void OnDestroy()
    {
        _isDestroyed = true;
        UnsubscribeFromEvents();
        
        if (playButton != null)
            playButton.onClick.RemoveListener(PlaySelectedCards);
        if (clearButton != null)
            clearButton.onClick.RemoveListener(ClearSelection);
        if (drawButton != null)
            drawButton.onClick.RemoveListener(DrawCards);
    }
    
    public void DrawCards()
    {
        if (_isDestroyed || CardManager.Instance == null || drawPool.Count == 0) return;
        if (CardManager.Instance.IsHandFull) return;
        
        for (int i = 0; i < cardsPerDraw && !CardManager.Instance.IsHandFull; i++)
        {
            CardData randomCard = drawPool[Random.Range(0, drawPool.Count)];
            CardManager.Instance.SpawnCard(randomCard, null, true);
        }
    }
    
    private void OnHandUpdated(List<Card> handCards)
    {
        if (_isDestroyed || drawButton == null) return;
        drawButton.interactable = !CardManager.Instance.IsHandFull && drawPool.Count > 0;
    }
    
    private void OnSelectionChanged(List<Card> selectedCards)
    {
        if (_isDestroyed) return;
        
        _selectedCards.Clear();
        if (selectedCards != null)
            _selectedCards.AddRange(selectedCards);
        _isDirty = true;
        UpdateUI();
    }
    
    public void PlaySelectedCards()
    {
        if (_isDestroyed || _selectedCards.Count == 0) return;
        
        if (_isDirty)
        {
            _cachedLetterSequence = ExtractLetterSequence(_selectedCards);
            _isDirty = false;
        }
        
        if (string.IsNullOrEmpty(_cachedLetterSequence)) return;
        
        if (SpellcastManager.Instance != null)
            SpellcastManager.Instance.ProcessCardPlay(_selectedCards, _cachedLetterSequence);
    }
    
    public void ClearSelection()
    {
        if (_isDestroyed) return;
        
        if (CardManager.Instance != null)
            CardManager.Instance.ClearSelection();
        _selectedCards.Clear();
        _cachedLetterSequence = "";
        _isDirty = true;
        UpdateUI();
    }
    
    private string ExtractLetterSequence(List<Card> cards)
    {
        if (cards == null || cards.Count == 0) return "";
        
        var letterBuilder = new StringBuilder();
        foreach (var card in cards)
        {
            if (card?.Data?.letterValues != null)
                letterBuilder.Append(card.Data.letterValues);
        }
        return letterBuilder.ToString();
    }
    
    private void UpdateUI()
    {
        if (_isDestroyed) return;
        
        bool hasCards = _selectedCards.Count > 0;
        
        if (playButton != null)
            playButton.interactable = hasCards;
        
        if (clearButton != null)
            clearButton.interactable = hasCards;
        
        if (drawButton != null)
            drawButton.interactable = !CardManager.Instance.IsHandFull && drawPool.Count > 0;
        
        if (statusDisplay != null)
        {
            if (hasCards)
            {
                if (_isDirty)
                {
                    _cachedLetterSequence = ExtractLetterSequence(_selectedCards);
                    _isDirty = false;
                }
                statusDisplay.text = $"Letters: {_cachedLetterSequence}";
            }
            else
            {
                statusDisplay.text = "Select cards to play";
            }
        }
    }
    
    private void OnSpellFound(string spellName, string usedLetters)
    {
        if (_isDestroyed || statusDisplay == null) return;
        
        statusDisplay.text = $"Spell Cast: {spellName}!";
        statusDisplay.color = Color.green;
        
        CancelInvoke(nameof(ResetStatusDisplay));
        Invoke(nameof(ResetStatusDisplay), 2f);
    }
    
    private void OnSpellNotFound(string attemptedLetters)
    {
        if (_isDestroyed || statusDisplay == null) return;
        
        statusDisplay.text = $"No spell found";
        statusDisplay.color = Color.red;
        
        CancelInvoke(nameof(ResetStatusDisplay));
        Invoke(nameof(ResetStatusDisplay), 1.5f);
    }
    
    private void ResetStatusDisplay()
    {
        if (_isDestroyed || statusDisplay == null) return;
        
        statusDisplay.color = Color.white;
        UpdateUI();
    }
    
    public bool HasSelectedCards => _selectedCards.Count > 0;
    public int SelectedCardCount => _selectedCards.Count;
}