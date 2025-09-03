using UnityEngine;
using System.Collections;
using System;

[System.Serializable]
public class Resource
{
    [SerializeField] private int _current;
    [SerializeField] private int _max;
    
    public int CurrentValue => _current;
    public int MaxValue => _max;
    public float Percentage => _max > 0 ? (float)_current / _max : 0f;
    
    public Resource(int start, int max = -1)
    {
        _current = start;
        _max = max > 0 ? max : start;
    }
    
    public void ModifyBy(int delta) => _current = Mathf.Clamp(_current + delta, 0, _max);
    public void Reset() => _current = _max;
}

public class CombatManager : SingletonBehaviour<CombatManager>, IGameManager
{
    [Header("Resources")]
    [SerializeField] private int startLife = 100;
    [SerializeField] private int startCreativity = 3;
    [SerializeField] private int maxCreativity = 10;
    
    [Header("Combat")]
    [SerializeField] private int startingHandSize = 5;
    
    // Resources
    private Resource _life;
    private Resource _creativity;
    
    // State
    private int _currentTurn = 1;
    private TurnPhase _currentPhase = TurnPhase.PlayerTurn;
    private bool _isProcessingTurn;
    private bool _isInCombat;
    
    public bool IsReady { get; private set; }
    
    // Events
    public static event Action<Resource> OnLifeChanged;
    public static event Action<Resource> OnCreativityChanged;
    public static event Action<int> OnTurnChanged;
    public static event Action<TurnPhase> OnTurnPhaseChanged;
    public static event Action OnCombatStarted;
    public static event Action OnPlayerDeath;
    
    // Properties
    public Resource Life => _life;
    public Resource Creativity => _creativity;
    public int CurrentTurn => _currentTurn;
    public TurnPhase CurrentPhase => _currentPhase;
    public bool IsInCombat => _isInCombat;
    public bool IsProcessingTurn => _isProcessingTurn;
    public bool IsPlayerTurn => _currentPhase == TurnPhase.PlayerTurn;
    public bool CanEndTurn => _isInCombat && IsPlayerTurn && !_isProcessingTurn;
    
    protected override void OnAwakeInitialize()
    {
        _life = new Resource(startLife);
        _creativity = new Resource(startCreativity, maxCreativity);
        IsReady = true;
    }
    
    public void StartCombat()
    {
        if (_isInCombat) return;
        StartCoroutine(StartCombatSequence());
    }
    
    IEnumerator StartCombatSequence()
    {
        _isInCombat = true;
        _currentTurn = 1;
        _currentPhase = TurnPhase.PlayerTurn;
        
        // Setup deck
        var deck = CoreExtensions.GetManager<DeckManager>();
        if (deck?.DeckSize == 0) deck.GenerateTestDeck();
        
        // Draw starting hand
        for (int i = 0; i < startingHandSize; i++)
        {
            deck?.TryDrawCard();
            yield return null;
        }
        
        OnLifeChanged?.Invoke(_life);
        OnCreativityChanged?.Invoke(_creativity);
        OnTurnChanged?.Invoke(_currentTurn);
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnCombatStarted?.Invoke();
    }
    
    public void EndPlayerTurn()
    {
        if (!CanEndTurn) return;
        StartCoroutine(ProcessTurn());
    }
    
    IEnumerator ProcessTurn()
    {
        _isProcessingTurn = true;
        _currentPhase = TurnPhase.TurnTransition;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        
        // Reset creativity
        _creativity.Reset();
        OnCreativityChanged?.Invoke(_creativity);
        
        var inputController = CardInputController.Instance;
        if (inputController != null)
            inputController.OnTurnEnd();

        // Reset selections
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager != null)
        {
            selectionManager.ClearSelection();
            selectionManager.ClearHighlight();
        }
        
        // Refill hand
        var cardManager = CoreExtensions.GetManager<CardManager>();
        var deckManager = CoreExtensions.GetManager<DeckManager>();
        if (cardManager != null && deckManager != null)
        {
            int toDraw = startingHandSize - cardManager.HandSize;
            for (int i = 0; i < toDraw; i++)
            {
                deckManager.TryDrawCard();
                yield return null;
            }
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // Enemy turn
        _currentPhase = TurnPhase.EnemyTurn;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        
        yield return new WaitForSeconds(1f);
        
        // Simple enemy damage
        ModifyLife(-UnityEngine.Random.Range(5, 15));
        
        yield return new WaitForSeconds(0.5f);
        
        // Next turn
        _currentTurn++;
        _currentPhase = TurnPhase.PlayerTurn;
        _isProcessingTurn = false;
        
        OnTurnChanged?.Invoke(_currentTurn);
        OnTurnPhaseChanged?.Invoke(_currentPhase);
    }
    
    public void ModifyLife(int delta)
    {
        _life.ModifyBy(delta);
        OnLifeChanged?.Invoke(_life);
        
        if (_life.CurrentValue <= 0)
            OnPlayerDeath?.Invoke();
    }
    
    public void ModifyCreativity(int delta)
    {
        _creativity.ModifyBy(delta);
        OnCreativityChanged?.Invoke(_creativity);
    }
    
    
    // Simple helper methods
    public bool CanSpendCreativity(int amount) => _creativity.CanAfford(amount);
    public void SpendCreativity(int amount) => _creativity.Spend(amount);
    
    public bool CanPerformPlayerAction(PlayerActionType actionType)
    {
        return IsPlayerTurn && !IsProcessingTurn;
    }

    public bool CanSpendResource(ResourceType resourceType, int amount)
    {
        return resourceType switch
        {
            ResourceType.Creativity => _creativity.CanAfford(amount),
            ResourceType.Life => _life.CanAfford(amount),
            _ => false
        };
    }

    public bool TryModifyResource(ResourceType resourceType, int delta)
    {
        switch (resourceType)
        {
            case ResourceType.Creativity:
                ModifyCreativity(delta);
                return true;
            case ResourceType.Life:
                ModifyLife(delta);
                return true;
            default:
                return false;
        }
    }
    public void DealDamageToTargets(int damage)
    {
        CoreExtensions.TryWithManagerStatic<EnemyManager>( em => 
        {
            var target = em.AliveEnemies.GetWeakest();
            target?.TakeDamage(damage, DamageType.Normal);
        });
    }

    public int DeckSize
    {
        get
        {
            return CoreExtensions.TryWithManagerStatic<DeckManager, int>(null, dm => dm.DeckSize);
        }
    }

    public int DiscardSize
    {
        get
        {
            return CoreExtensions.TryWithManagerStatic<DeckManager, int>(null, dm => dm.DiscardSize);
        }
    }


    public static event System.Action<int> OnDeckSizeChanged
    {
        add => DeckManager.OnDeckSizeChanged += value;
        remove => DeckManager.OnDeckSizeChanged -= value;
    }}