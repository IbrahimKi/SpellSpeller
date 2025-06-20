using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;

[System.Serializable]
public class Resource
{
    [SerializeField] private int _currentValue;
    [SerializeField] private int _maxValue;
    
    public int CurrentValue => _currentValue;
    public int MaxValue => _maxValue;
    public float Percentage => _maxValue > 0 ? (float)_currentValue / _maxValue : 0f;
    
    public Resource(int startValue, int maxValue = -1)
    {
        _currentValue = startValue;
        _maxValue = maxValue > 0 ? maxValue : startValue;
    }
    
    public void SetCurrent(int value) => _currentValue = Mathf.Clamp(value, 0, _maxValue);
    public void ModifyBy(int delta) => SetCurrent(_currentValue + delta);
    public void Reset() => _currentValue = _maxValue;
}

public enum TurnPhase
{
    PlayerTurn,
    EnemyTurn,
    TurnTransition,
    CombatEnd
}

public class CombatManager : SingletonBehaviour<CombatManager>, IGameManager
{
    [Header("Resources")]
    [SerializeField] private int startLife = 100;
    [SerializeField] private int startCreativity = 3;
    [SerializeField] private int maxCreativity = 10;
    
    [Header("Combat Settings")]
    [SerializeField] private int startingHandSize = 5;
    [SerializeField] private float turnTransitionDelay = 0.5f;
    
    // Resources
    private Resource _life;
    private Resource _creativity;
    
    // Turn state
    private int _currentTurn = 1;
    private TurnPhase _currentPhase = TurnPhase.PlayerTurn;
    private bool _isProcessingTurn = false;
    private bool _isInCombat = false;
    private bool _isReady = false;
    
    // Targeting
    private List<EntityBehaviour> _currentTargets = new List<EntityBehaviour>();
    
    // Events - nur die wichtigsten
    public static event Action<Resource> OnLifeChanged;
    public static event Action<Resource> OnCreativityChanged;
    public static event Action<int> OnDeckSizeChanged;
    public static event Action<int> OnTurnChanged;
    public static event Action<TurnPhase> OnTurnPhaseChanged;
    public static event Action OnCombatStarted;
    public static event Action OnCombatEnded;
    public static event Action OnPlayerDeath;
    public static event Action<int> OnPlayerTurnStarted;
    public static event Action<int> OnPlayerTurnEnded;
    public static event Action<int> OnEnemyTurnStarted;
    
    // Properties
    public bool IsReady => _isReady;
    public bool IsInCombat => _isInCombat;
    public bool IsProcessingTurn => _isProcessingTurn;
    public Resource Life => _life;
    public Resource Creativity => _creativity;
    public int CurrentTurn => _currentTurn;
    public TurnPhase CurrentPhase => _currentPhase;
    public int DeckSize => DeckManager.HasInstance ? DeckManager.Instance.DeckSize : 0;
    public int DiscardSize => DeckManager.HasInstance ? DeckManager.Instance.DiscardSize : 0;
    public IReadOnlyList<EntityBehaviour> CurrentTargets => _currentTargets.AsReadOnly();
    public bool CanEndTurn => _isInCombat && _currentPhase == TurnPhase.PlayerTurn && !_isProcessingTurn;
    public bool IsPlayerTurn => _currentPhase == TurnPhase.PlayerTurn;
    
    protected override void OnAwakeInitialize()
    {
        InitializeResources();
        _currentPhase = TurnPhase.PlayerTurn;
        _isReady = true;
    }
    
    private void OnEnable()
    {
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemyKilled += HandleEnemyKilled;
            EnemyManager.OnAllEnemiesDefeated += HandleAllEnemiesDefeated;
        }
        
        if (DeckManager.HasInstance)
        {
            DeckManager.OnDeckSizeChanged += size => OnDeckSizeChanged?.Invoke(size);
        }
    }
    
    private void OnDisable()
    {
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemyKilled -= HandleEnemyKilled;
            EnemyManager.OnAllEnemiesDefeated -= HandleAllEnemiesDefeated;
        }
    }
    
    private void InitializeResources()
    {
        _life = new Resource(startLife);
        _creativity = new Resource(startCreativity, maxCreativity);
    }
    
    public void StartCombat()
    {
        if (_isInCombat) return;
        StartCoroutine(StartCombatSequence());
    }
    
    private IEnumerator StartCombatSequence()
    {
        _isInCombat = true;
        _currentTurn = 1;
        _currentPhase = TurnPhase.PlayerTurn;
        _isProcessingTurn = false;
        
        // Check deck
        if (DeckManager.HasInstance && DeckManager.Instance.DeckSize == 0)
        {
            DeckManager.Instance.GenerateTestDeck();
            yield return null;
        }
        
        // Draw starting hand
        if (startingHandSize > 0)
        {
            yield return DrawCards(startingHandSize);
        }
        
        // Auto-target first enemy
        if (EnemyManager.HasInstance)
        {
            var firstEnemy = EnemyManager.Instance.AliveEnemies.FirstOrDefault();
            if (firstEnemy != null)
                _currentTargets.Add(firstEnemy);
        }
        
        // Fire events
        OnLifeChanged?.Invoke(_life);
        OnCreativityChanged?.Invoke(_creativity);
        OnDeckSizeChanged?.Invoke(DeckSize);
        OnTurnChanged?.Invoke(_currentTurn);
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnCombatStarted?.Invoke();
        OnPlayerTurnStarted?.Invoke(_currentTurn);
    }
    
    public void EndPlayerTurn()
    {
        if (!CanEndTurn) return;
        
        OnPlayerTurnEnded?.Invoke(_currentTurn);
        StartCoroutine(ProcessTurnTransition());
    }
    
    private IEnumerator ProcessTurnTransition()
    {
        _currentPhase = TurnPhase.TurnTransition;
        _isProcessingTurn = true;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        
        // Reset creativity
        _creativity.Reset();
        OnCreativityChanged?.Invoke(_creativity);
        
        // Refill hand
        int cardsToRefill = Mathf.Max(0, startingHandSize - CardManager.Instance.HandSize);
        if (cardsToRefill > 0)
        {
            yield return DrawCards(cardsToRefill);
        }
        
        yield return new WaitForSeconds(turnTransitionDelay);
        
        // Start enemy turn
        _currentPhase = TurnPhase.EnemyTurn;
        _isProcessingTurn = true;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnEnemyTurnStarted?.Invoke(_currentTurn);
        
        // Simple enemy action
        yield return new WaitForSeconds(1f);
        
        if (_life.CurrentValue > 0)
        {
            int damage = UnityEngine.Random.Range(5, 15);
            ModifyLife(-damage);
            yield return new WaitForSeconds(0.5f);
        }
        
        // Next turn
        _currentTurn++;
        OnTurnChanged?.Invoke(_currentTurn);
        
        // Back to player turn
        _currentPhase = TurnPhase.PlayerTurn;
        _isProcessingTurn = false;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnPlayerTurnStarted?.Invoke(_currentTurn);
    }
    
    private IEnumerator DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (DeckManager.HasInstance && CardManager.HasInstance)
            {
                var cardData = DeckManager.Instance.DrawCard();
                if (cardData != null)
                {
                    CardManager.Instance.SpawnCard(cardData, null, true);
                    yield return null;
                }
            }
        }
    }
    
    public void ModifyLife(int delta)
    {
        int oldValue = _life.CurrentValue;
        _life.ModifyBy(delta);
        
        if (_life.CurrentValue != oldValue)
        {
            OnLifeChanged?.Invoke(_life);
            if (_life.CurrentValue <= 0)
                OnPlayerDeath?.Invoke();
        }
    }
    
    public void ModifyCreativity(int delta)
    {
        int oldValue = _creativity.CurrentValue;
        _creativity.ModifyBy(delta);
        
        if (_creativity.CurrentValue != oldValue)
            OnCreativityChanged?.Invoke(_creativity);
    }
    
    public bool CanSpendCreativity(int amount) => _creativity.CurrentValue >= amount;
    
    public bool SpendCreativity(int amount)
    {
        if (!CanSpendCreativity(amount)) return false;
        ModifyCreativity(-amount);
        return true;
    }
    
    public void AddTarget(EntityBehaviour target)
    {
        if (target != null && target.IsAlive && !_currentTargets.Contains(target))
        {
            _currentTargets.Clear(); // Single target for now
            _currentTargets.Add(target);
        }
    }
    
    public void DealDamageToTargets(int damage)
    {
        foreach (var target in _currentTargets.ToList())
        {
            if (target != null && target.IsAlive)
                target.Damage(damage);
        }
    }
    
    private void HandleEnemyKilled(EntityBehaviour enemy)
    {
        _currentTargets.Remove(enemy);
        
        // Auto-target next
        if (_currentTargets.Count == 0 && EnemyManager.HasInstance)
        {
            var nextEnemy = EnemyManager.Instance.AliveEnemies.FirstOrDefault();
            if (nextEnemy != null)
                AddTarget(nextEnemy);
        }
    }
    
    private void HandleAllEnemiesDefeated()
    {
        _currentTargets.Clear();
        // Victory handling
    }
    
    public void EndCombat()
    {
        _isInCombat = false;
        _currentTargets.Clear();
        _currentPhase = TurnPhase.CombatEnd;
        OnCombatEnded?.Invoke();
    }
}
public enum TargetingMode
{
    Single,
    Multiple,
    All,
    Random
}

public enum DamageType
{
    Normal,
    Fire,
    True
}