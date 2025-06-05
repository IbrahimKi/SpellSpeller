using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;

[System.Serializable]
public class Resource
{
    [SerializeField] private int _startValue;
    [SerializeField] private int _currentValue;
    [SerializeField] private int _maxValue;
    
    public int StartValue => _startValue;
    public int CurrentValue => _currentValue;
    public int MaxValue => _maxValue;
    public float Percentage => _maxValue > 0 ? (float)_currentValue / _maxValue : 0f;
    
    public Resource(int startValue, int maxValue = -1)
    {
        _startValue = startValue;
        _currentValue = startValue;
        _maxValue = maxValue > 0 ? maxValue : startValue;
    }
    
    public void SetCurrent(int value) => _currentValue = Mathf.Clamp(value, 0, _maxValue);
    public void ModifyBy(int delta) => SetCurrent(_currentValue + delta);
    public void Reset() => _currentValue = _startValue;
    
    public void SetMax(int newMax)
    {
        _maxValue = newMax;
        _currentValue = Mathf.Min(_currentValue, _maxValue);
    }
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
    [Header("Player Resources")]
    [SerializeField] private int startLife = 100;
    [SerializeField] private int startCreativity = 3;
    [SerializeField] private int maxCreativity = 10;
    
    [Header("Combat Start Settings")]
    [SerializeField] private int startingHandSize = 5;
    [SerializeField] private bool autoDrawOnCombatStart = true;
    [SerializeField] private float managerWaitTimeout = 3f;
    
    [Header("Turn Settings")]
    [SerializeField] private bool refillHandOnTurnEnd = true;
    [SerializeField] private bool resetCreativityOnTurnEnd = true;
    [SerializeField] private bool shuffleDiscardOnTurnEnd = true;
    [SerializeField] private float turnTransitionDelay = 0.5f;
    
    [Header("Entity Settings")]
    [SerializeField] private bool autoTargetFirstEnemy = true;
    [SerializeField] private TargetingMode defaultTargetingMode = TargetingMode.Single;
    
    // Resources
    private Resource _life;
    private Resource _creativity;
    
    // Turn tracking - IMPROVED
    private int _currentTurn = 1;
    private TurnPhase _currentPhase = TurnPhase.PlayerTurn;
    private TurnPhase _previousPhase;
    private bool _isProcessingTurn = false;
    
    // Targeting
    private List<EntityBehaviour> _currentTargets = new List<EntityBehaviour>();
    private TargetingMode _currentTargetingMode = TargetingMode.Single;
    
    // Manager state tracking
    private bool _isReady = false;
    private bool _managersReady = false;
    
    // Combat state
    private bool _isInCombat = false;
    
    // Events - IMPROVED: More granular turn events
    public static event Action<Resource> OnLifeChanged;
    public static event Action<int> OnDeckSizeChanged;
    public static event Action<Resource> OnCreativityChanged;
    public static event Action<int> OnTurnChanged;
    public static event Action<TurnPhase> OnTurnPhaseChanged;
    
    // Events - Combat
    public static event Action OnCombatStarted;
    public static event Action OnCombatEnded;
    public static event Action OnPlayerDeath;
    public static event Action OnHandDrawn;
    public static event Action OnDeckEmpty;
    
    // Events - Turn System - IMPROVED: Clearer turn flow
    public static event Action<int> OnPlayerTurnStarted;
    public static event Action<int> OnPlayerTurnEnded;
    public static event Action<int> OnEnemyTurnStarted;
    public static event Action<int> OnEnemyTurnEnded;
    public static event Action OnTurnTransitionStarted;
    public static event Action OnTurnTransitionCompleted;
    
    // Events - Targeting
    public static event Action<List<EntityBehaviour>> OnTargetsChanged;
    public static event Action<TargetingMode> OnTargetingModeChanged;
    
    // Properties
    public bool IsReady => _isReady;
    public bool IsInCombat => _isInCombat;
    public bool ManagersReady => _managersReady;
    public bool IsProcessingTurn => _isProcessingTurn; // NEW: For UI button states
    public Resource Life => _life;
    public Resource Creativity => _creativity;
    public int CurrentTurn => _currentTurn;
    public TurnPhase CurrentPhase => _currentPhase; // NEW: Current turn phase
    public int DeckSize => DeckManager.HasInstance ? DeckManager.Instance.DeckSize : 0;
    public int DiscardSize => DeckManager.HasInstance ? DeckManager.Instance.DiscardSize : 0;
    public IReadOnlyList<EntityBehaviour> CurrentTargets => _currentTargets.AsReadOnly();
    public TargetingMode CurrentTargetingMode => _currentTargetingMode;
    
    // NEW: Turn control properties for UI
    public bool CanEndTurn => _isInCombat && _currentPhase == TurnPhase.PlayerTurn && !_isProcessingTurn;
    public bool IsPlayerTurn => _currentPhase == TurnPhase.PlayerTurn;
    public bool IsEnemyTurn => _currentPhase == TurnPhase.EnemyTurn;
    
    protected override void OnAwakeInitialize()
    {
        Debug.Log("[CombatManager] OnAwakeInitialize called");
        InitializeResources();
        _currentTargetingMode = defaultTargetingMode;
        _currentTurn = 1;
        _currentPhase = TurnPhase.PlayerTurn;
        _isReady = true;
        _managersReady = true;
        Debug.Log($"[CombatManager] Initialized - IsReady: {_isReady}");
    }
    
    private void Start()
    {
        Debug.Log("[CombatManager] Start called - waiting for GameManager to start combat");
    }
    
    private void OnEnable()
    {
        // Subscribe to Entity Events
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemyKilled += HandleEnemyKilled;
            EnemyManager.OnAllEnemiesDefeated += HandleAllEnemiesDefeated;
            EnemyManager.OnTargetsChanged += HandleEnemyTargetsChanged;
        }
        
        if (UnitManager.HasInstance)
        {
            UnitManager.OnUnitKilled += HandleUnitKilled;
            UnitManager.OnAllUnitsDefeated += HandleAllUnitsDefeated;
        }
        
        // Subscribe to Deck Events
        if (DeckManager.HasInstance)
        {
            DeckManager.OnDeckSizeChanged += OnDeckManagerSizeChanged;
            DeckManager.OnDeckInitialized += () => OnDeckSizeChanged?.Invoke(DeckSize);
            DeckManager.OnDeckEmpty += HandleDeckEmpty;
        }
        
        // Subscribe to Spell Events
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnSpellEffectTriggered += HandleSpellEffect;
        }
    }
    
    private void OnDisable()
    {
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemyKilled -= HandleEnemyKilled;
            EnemyManager.OnAllEnemiesDefeated -= HandleAllEnemiesDefeated;
            EnemyManager.OnTargetsChanged -= HandleEnemyTargetsChanged;
        }
        
        if (UnitManager.HasInstance)
        {
            UnitManager.OnUnitKilled -= HandleUnitKilled;
            UnitManager.OnAllUnitsDefeated -= HandleAllUnitsDefeated;
        }
        
        if (DeckManager.HasInstance)
        {
            DeckManager.OnDeckSizeChanged -= OnDeckManagerSizeChanged;
            DeckManager.OnDeckEmpty -= HandleDeckEmpty;
        }
        
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnSpellEffectTriggered -= HandleSpellEffect;
        }
    }
    
    private void InitializeResources()
    {
        _life = new Resource(startLife);
        _creativity = new Resource(startCreativity, maxCreativity);
    }
    
    // Combat State Management
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
        _previousPhase = TurnPhase.EnemyTurn; 
        _isProcessingTurn = false;
        
        Debug.Log("[CombatManager] Starting combat sequence");
        
        // Check für Deck direkt über GameManager
        if (GameManager.HasInstance && GameManager.Instance.DeckManager?.DeckSize == 0)
        {
            Debug.Log("[CombatManager] Empty deck detected - generating test deck");
            GameManager.Instance.DeckManager.GenerateTestDeck();
            yield return new WaitForSeconds(0.1f);
        }
        
        // Draw starting hand
        if (autoDrawOnCombatStart && startingHandSize > 0)
            yield return DrawStartingHand();
        
        // Auto-target first enemy
        if (autoTargetFirstEnemy && EnemyManager.HasInstance)
        {
            var firstEnemy = EnemyManager.Instance.AliveEnemies.FirstOrDefault();
            if (firstEnemy != null)
            {
                SetTargets(new List<EntityBehaviour> { firstEnemy });
            }
        }
        
        // Trigger initial UI updates
        TriggerAllResourceEvents();
        
        OnCombatStarted?.Invoke();
        StartPlayerTurn();
        
        Debug.Log("[CombatManager] Combat started successfully");
    }
    
    // IMPROVED: Cleaner turn management
    private void StartPlayerTurn()
    {
        _currentPhase = TurnPhase.PlayerTurn;
        _isProcessingTurn = false;
        
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnPlayerTurnStarted?.Invoke(_currentTurn);
        
        Debug.Log($"[CombatManager] Player Turn {_currentTurn} started");
    }
    
    private void StartEnemyTurn()
    {
        _currentPhase = TurnPhase.EnemyTurn;
        _isProcessingTurn = true;
        
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnEnemyTurnStarted?.Invoke(_currentTurn);
        
        Debug.Log($"[CombatManager] Enemy Turn {_currentTurn} started");
        
        // Simple enemy AI - just wait then end turn
        StartCoroutine(ProcessEnemyTurn());
    }
    
    private IEnumerator ProcessEnemyTurn()
    {
        // Simple enemy behavior - wait and potentially attack
        yield return new WaitForSeconds(1f);
        
        // Basic enemy action: damage player if possible
        if (Life.CurrentValue > 0)
        {
            int damage = UnityEngine.Random.Range(5, 15);
            ModifyLife(-damage);
            Debug.Log($"[CombatManager] Enemies dealt {damage} damage to player");
            yield return new WaitForSeconds(0.5f);
        }
        
        EndEnemyTurn();
    }
    
    private void EndEnemyTurn()
    {
        _previousPhase = TurnPhase.EnemyTurn;  // DIESE ZEILE FEHLT!
        OnEnemyTurnEnded?.Invoke(_currentTurn);
        StartTurnTransition();
    }
    
    // IMPROVED: Public method for UI button - cleaner interface
    public void EndPlayerTurn()
    {
        if (!CanEndTurn)
        {
            Debug.LogWarning($"[CombatManager] Cannot end turn - CanEndTurn: {CanEndTurn}, Phase: {_currentPhase}, Processing: {_isProcessingTurn}");
            return;
        }
    
        Debug.Log($"[CombatManager] Player ending turn {_currentTurn}");
        _previousPhase = TurnPhase.PlayerTurn;  // DIESE ZEILE FEHLT!
        OnPlayerTurnEnded?.Invoke(_currentTurn);
        StartTurnTransition();
    }
    
    // IMPROVED: Separate turn transition phase
    private void StartTurnTransition()
    {
        _currentPhase = TurnPhase.TurnTransition;
        _isProcessingTurn = true;
        
        OnTurnTransitionStarted?.Invoke();
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        
        StartCoroutine(ProcessTurnTransition());
    }
    
    private IEnumerator ProcessTurnTransition()
    {
        Debug.Log($"[CombatManager] Starting turn transition from turn {_currentTurn} (previous phase: {_previousPhase})");
        
        // 1. Shuffle discard pile and played cards back into deck
        if (shuffleDiscardOnTurnEnd)
        {
            yield return ShuffleDiscardIntoDeck();
        }
        
        // 2. Reset creativity to start value
        if (resetCreativityOnTurnEnd)
        {
            _creativity.Reset();
            OnCreativityChanged?.Invoke(_creativity);
        }
        
        // 3. Refill hand to starting hand size
        if (refillHandOnTurnEnd)
        {
            yield return RefillHand();
        }
        
        // 4. Wait for transition delay
        yield return new WaitForSeconds(turnTransitionDelay);
        
        // 5. Increment turn counter
        _currentTurn++;
        OnTurnChanged?.Invoke(_currentTurn);
        
        OnTurnTransitionCompleted?.Invoke();
        
        // 6. Alternate between player and enemy turns based on who just went
        if (_previousPhase == TurnPhase.PlayerTurn)
        {
            StartEnemyTurn();
        }
        else
        {
            StartPlayerTurn();
        }
        
        Debug.Log($"[CombatManager] Turn transition completed. Now on turn {_currentTurn}");
    }
    
    // LEGACY: Keep old EndTurn method for backwards compatibility
    public void EndTurn()
    {
        EndPlayerTurn();
    }
    
    private IEnumerator DrawStartingHand()
    {
        var cardManager = GameManager.Instance?.CardManager;
        var deckManager = GameManager.Instance?.DeckManager;
        
        if (cardManager == null || deckManager == null)
        {
            Debug.LogWarning("[CombatManager] Managers not available for drawing starting hand");
            yield break;
        }
        
        Debug.Log($"[CombatManager] Drawing {startingHandSize} cards for starting hand");
        
        for (int i = 0; i < startingHandSize; i++)
        {
            var cardData = deckManager.DrawCard();
            if (cardData != null)
            {
                cardManager.SpawnCard(cardData, null, true);
                yield return null;
            }
            else
            {
                Debug.LogWarning($"[CombatManager] Could not draw card {i+1}/{startingHandSize}");
                break;
            }
        }
        
        OnHandDrawn?.Invoke();
    }
    
    public void EndCombat()
    {
        if (_isInCombat)
        {
            _isInCombat = false;
            _currentTargets.Clear();
            _currentTurn = 1;
            _currentPhase = TurnPhase.CombatEnd;
            _isProcessingTurn = false;
            OnCombatEnded?.Invoke();
        }
    }
    
    public void ResetCombat()
    {
        // Clear all entities
        if (EnemyManager.HasInstance)
            EnemyManager.Instance.DespawnAllEnemies();
        if (UnitManager.HasInstance)
            UnitManager.Instance.DespawnAllUnits();
        
        _currentTargets.Clear();
        _life.Reset();
        _creativity.Reset();
        _currentTurn = 1;
        _currentPhase = TurnPhase.PlayerTurn;
        _isInCombat = false;
        _isProcessingTurn = false;
        
        TriggerAllResourceEvents();
    }
    
    // HELPER: Trigger all resource events at once
    private void TriggerAllResourceEvents()
    {
        OnLifeChanged?.Invoke(_life);
        OnCreativityChanged?.Invoke(_creativity);
        OnDeckSizeChanged?.Invoke(DeckSize);
        OnTurnChanged?.Invoke(_currentTurn);
        OnTurnPhaseChanged?.Invoke(_currentPhase);
    }
    
    private IEnumerator ShuffleDiscardIntoDeck()
    {
        var deckManager = GameManager.Instance?.DeckManager;
        var cardManager = GameManager.Instance?.CardManager;
        
        if (deckManager == null || cardManager == null)
        {
            Debug.LogWarning("[CombatManager] Managers not available for shuffling discard");
            yield break;
        }
        
        Debug.Log("[CombatManager] Shuffling discard pile and played cards back into deck");
        
        // Collect all played cards first
        foreach (var card in deckManager.GetDiscardPileContents())
        {
            if (card != null)
            {
                deckManager.AddCardToBottom(card);
            }
        }
        
        // Shuffle discard pile back into deck (at bottom)
        deckManager.ShuffleDiscardIntoDeck();
        
        OnDeckSizeChanged?.Invoke(DeckSize);
        yield return new WaitForSeconds(0.1f);
    }
    
    private IEnumerator RefillHand()
    {
        var cardManager = GameManager.Instance?.CardManager;
        var deckManager = GameManager.Instance?.DeckManager;
        
        if (cardManager == null || deckManager == null)
        {
            Debug.LogWarning("[CombatManager] Managers not available for refilling hand");
            yield break;
        }
        
        int currentHandSize = cardManager.HandSize;
        int cardsToRefill = Mathf.Max(0, startingHandSize - currentHandSize);
        
        Debug.Log($"[CombatManager] Refilling hand: {currentHandSize} -> {startingHandSize} (drawing {cardsToRefill} cards)");
        
        for (int i = 0; i < cardsToRefill; i++)
        {
            var cardData = deckManager.DrawCard();
            if (cardData != null)
            {
                cardManager.SpawnCard(cardData, null, true);
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                Debug.LogWarning($"[CombatManager] Could not draw card {i+1}/{cardsToRefill} during refill");
                break;
            }
        }
        
        OnHandDrawn?.Invoke();
    }
    
    // Resource Management
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
    
    // Targeting System
    public void SetTargetingMode(TargetingMode mode)
    {
        if (_currentTargetingMode != mode)
        {
            _currentTargetingMode = mode;
            OnTargetingModeChanged?.Invoke(mode);
            
            // Clear targets if switching from multi to single
            if (mode == TargetingMode.Single && _currentTargets.Count > 1)
            {
                var firstTarget = _currentTargets[0];
                _currentTargets.Clear();
                _currentTargets.Add(firstTarget);
                OnTargetsChanged?.Invoke(_currentTargets);
            }
        }
    }
    
    public void SetTargets(List<EntityBehaviour> targets)
    {
        _currentTargets.Clear();
        if (targets != null)
        {
            _currentTargets.AddRange(targets.Where(t => t != null && t.IsAlive));
        }
        OnTargetsChanged?.Invoke(_currentTargets);
    }
    
    public void AddTarget(EntityBehaviour target)
    {
        if (target == null || !target.IsAlive) return;
        
        if (_currentTargetingMode == TargetingMode.Single)
        {
            _currentTargets.Clear();
        }
        
        if (!_currentTargets.Contains(target))
        {
            _currentTargets.Add(target);
            OnTargetsChanged?.Invoke(_currentTargets);
        }
    }
    
    public void RemoveTarget(EntityBehaviour target)
    {
        if (_currentTargets.Remove(target))
        {
            OnTargetsChanged?.Invoke(_currentTargets);
        }
    }
    
    public void ClearTargets()
    {
        _currentTargets.Clear();
        OnTargetsChanged?.Invoke(_currentTargets);
    }
    
    // Combat Actions
    public void DealDamageToTargets(int damage, DamageType damageType = DamageType.Normal)
    {
        foreach (var target in _currentTargets.ToList())
        {
            if (target != null && target.IsAlive)
            {
                target.Damage(damage);
            }
        }
    }
    
    public void DealDamageToAllEnemies(int damage, DamageType damageType = DamageType.Normal)
    {
        if (EnemyManager.HasInstance)
        {
            foreach (var enemy in EnemyManager.Instance.AliveEnemies)
            {
                enemy.Damage(damage);
            }
        }
    }
    
    public void HealAllUnits(int amount)
    {
        if (UnitManager.HasInstance)
        {
            foreach (var unit in UnitManager.Instance.AliveUnits)
            {
                unit.Heal(amount);
            }
        }
    }
    
    // Event Handlers
    private void HandleEnemyTargetsChanged(List<EntityBehaviour> targets)
    {
        SetTargets(targets);
    }
    
    private void HandleEnemyKilled(EntityBehaviour enemy)
    {
        RemoveTarget(enemy);
        
        // Auto-target next enemy if no targets remain
        if (_currentTargets.Count == 0 && autoTargetFirstEnemy && EnemyManager.HasInstance)
        {
            var nextEnemy = EnemyManager.Instance.AliveEnemies.FirstOrDefault();
            if (nextEnemy != null)
            {
                AddTarget(nextEnemy);
            }
        }
    }
    
    private void HandleUnitKilled(EntityBehaviour unit)
    {
        RemoveTarget(unit);
    }
    
    private void HandleAllEnemiesDefeated()
    {
        ClearTargets();
        Debug.Log("[CombatManager] All enemies defeated - Victory!");
        // TODO: Victory handling
    }
    
    private void HandleAllUnitsDefeated()
    {
        Debug.Log("[CombatManager] All units defeated!");
        // TODO: Handle all units lost
    }
    
    private void HandleSpellEffect(SpellEffect effect)
    {
        switch (effect.effectType)
        {
            case SpellEffectType.Damage:
                if (_currentTargets.Count > 0)
                {
                    DealDamageToTargets(Mathf.RoundToInt(effect.value));
                    Debug.Log($"[CombatManager] Spell damage {effect.value} applied to {_currentTargets.Count} target(s)");
                }
                else
                {
                    Debug.LogWarning("[CombatManager] No targets for damage spell");
                }
                break;
            
            case SpellEffectType.Heal:
                ModifyLife(Mathf.RoundToInt(effect.value));
                break;
            
            case SpellEffectType.Buff:
                if (effect.effectName.ToLower().Contains("creativity"))
                    ModifyCreativity(Mathf.RoundToInt(effect.value));
                break;
        }
    }
    
    private void OnDeckManagerSizeChanged(int deckSize) => OnDeckSizeChanged?.Invoke(deckSize);
    private void HandleDeckEmpty() => OnDeckEmpty?.Invoke();
    
#if UNITY_EDITOR
    [ContextMenu("Log Combat Status")]
    public void LogCombatStatus()
    {
        Debug.Log($"[CombatManager] Combat Status:");
        Debug.Log($"  In Combat: {IsInCombat}");
        Debug.Log($"  Turn: {_currentTurn}");
        Debug.Log($"  Phase: {_currentPhase}");
        Debug.Log($"  Processing Turn: {_isProcessingTurn}");
        Debug.Log($"  Can End Turn: {CanEndTurn}");
        Debug.Log($"  Life: {_life.CurrentValue}/{_life.MaxValue}");
        Debug.Log($"  Creativity: {_creativity.CurrentValue}/{_creativity.MaxValue}");
        Debug.Log($"  Targets: {_currentTargets.Count}");
        Debug.Log($"  Targeting Mode: {_currentTargetingMode}");
    }
    
    [ContextMenu("End Turn (Debug)")]
    public void DebugEndTurn()
    {
        EndPlayerTurn();
    }
    
    [ContextMenu("Start Enemy Turn (Debug)")]
    public void DebugStartEnemyTurn()
    {
        StartEnemyTurn();
    }
#endif
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