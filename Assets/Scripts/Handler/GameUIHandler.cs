using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using GameCore.Enums;
using GameCore.Data;

public class GameUIHandler : MonoBehaviour
{
    [Header("Resource Displays")]
    [SerializeField] private TextMeshProUGUI lifeText;
    [SerializeField] private Slider lifeSlider;
    [SerializeField] private TextMeshProUGUI creativityText;
    [SerializeField] private Slider creativitySlider;
    [SerializeField] private TextMeshProUGUI deckText;
    
    [Header("Card Play UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button drawButton;
    [SerializeField] private Button castComboButton;
    
    [Header("Combat UI")]
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI turnPhaseText;
    
    [Header("Visual Settings")]
    [SerializeField] private Color healthLowColor = Color.red;
    [SerializeField] private Color healthNormalColor = Color.white;
    [SerializeField] private float healthLowThreshold = 0.25f;
    
    [Header("Combo Display Colors")]
    [SerializeField] private Color comboEmptyColor = Color.gray;
    [SerializeField] private Color comboBuildingColor = Color.yellow;
    [SerializeField] private Color comboReadyColor = Color.green;
    [SerializeField] private Color comboInvalidColor = Color.red;
    
    [Header("Spell Cast Display")]
    [SerializeField] private GameObject spellCastPanel;
    [SerializeField] private TextMeshProUGUI lastSpellText;
    [SerializeField] private TextMeshProUGUI spellDamageText;
    [SerializeField] private float spellDisplayDuration = 3f;
    
    [Header("Enemy Info Display")]
    [SerializeField] private GameObject enemyInfoPanel;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyTypeText;
    [SerializeField] private TextMeshProUGUI enemyHealthText;
    [SerializeField] private Slider enemyHealthBar;
    
    [Header("Total Enemy Health")]
    [SerializeField] private GameObject totalEnemyHealthPanel;
    [SerializeField] private Slider totalEnemyHealthBar;
    [SerializeField] private TextMeshProUGUI totalEnemyHealthText;
    [SerializeField] private TextMeshProUGUI enemyCountText;
    
    [Header("Update Settings")]
    [SerializeField] private float enemyPanelUpdateInterval = 0.1f;
    
    // Performance optimization
    private float _lastButtonUpdate = 0f;
    private float _lastEnemyPanelUpdate = 0f;
    private bool _managersReady = false;
    
    // Spell tracking - SIMPLIFIED
    private Coroutine _spellDisplayCoroutine;
    
    // Enemy tracking
    private EntityBehaviour _currentDisplayedEnemy;
    private int _totalEnemyMaxHealth;
    private int _totalEnemyCurrentHealth;
    
    private void Start()
    {
        StartCoroutine(WaitForManagersAndSetup());
    }
    
    private void Update()
    {
        if (!_managersReady) return;
        
        UpdateEnemyPanelLogic();
    }
    
    private void UpdateEnemyPanelLogic()
    {
        if (Time.time - _lastEnemyPanelUpdate < enemyPanelUpdateInterval) return;
        _lastEnemyPanelUpdate = Time.time;
        
        EntityBehaviour enemyToShow = GetEnemyToDisplay();
        
        if (enemyToShow != null)
        {
            if (_currentDisplayedEnemy != enemyToShow)
            {
                _currentDisplayedEnemy = enemyToShow;
                ShowEnemyInfo(enemyToShow);
            }
            else
            {
                UpdateEnemyHealthDisplay(enemyToShow);
            }
        }
        else if (_currentDisplayedEnemy != null)
        {
            _currentDisplayedEnemy = null;
            HideEnemyInfo();
        }
    }
    
    private EntityBehaviour GetEnemyToDisplay()
    {
        return CoreExtensions.TryWithManager<EnemyManager, EntityBehaviour>(this, em =>
        {
            // Priority 1: Targeted
            var targetedEnemy = em.AllEnemies.FirstOrDefault(e => e != null && e.IsTargeted);
            if (targetedEnemy != null) return targetedEnemy;
            
            // Priority 2: Boss
            var boss = em.AllEnemies.FirstOrDefault(e => e != null && e.IsBoss() && e.IsValidEntity());
            if (boss != null) return boss;
            
            // Priority 3: Elite
            var elite = em.AllEnemies.FirstOrDefault(e => e != null && e.IsElite() && e.IsValidEntity());
            if (elite != null) return elite;
            
            // Priority 4: First alive
            return em.AllEnemies.FirstOrDefault(e => e != null && e.IsValidEntity());
        });
    }
    
    private IEnumerator WaitForManagersAndSetup()
    {
        while (!GameManager.HasInstance || !GameManager.Instance.IsInitialized)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        _managersReady = true;
        SetupEventListeners();
        SetupButtons();
        RefreshAllDisplays();
    }
    
    private void SetupButtons()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(EndTurn);
        }
        
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(PlaySelectedCards);
        }
        
        if (clearButton != null)
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(ClearSelection);
        }
        
        if (drawButton != null)
        {
            drawButton.onClick.RemoveAllListeners();
            drawButton.onClick.AddListener(DrawCard);
        }
        
        if (castComboButton != null)
        {
            castComboButton.onClick.RemoveAllListeners();
            castComboButton.onClick.AddListener(CastCombo);
        }
        
        UpdateButtonsThrottled();
    }
    
    private void SetupEventListeners()
    {
        // Combat events
        CoreExtensions.TryWithManager<CombatManager>(this, manager =>
        {
            CombatManager.OnLifeChanged += UpdateLifeDisplay;
            CombatManager.OnCreativityChanged += UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged += UpdateDeckDisplay;
            CombatManager.OnTurnChanged += UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged += UpdateTurnPhaseDisplay;
        });
        
        // Card events
        CoreExtensions.TryWithManager<CardManager>(this, manager =>
        {
            CardManager.OnSelectionChanged += OnCardSelectionChanged;
            CardManager.OnHandUpdated += OnHandUpdated;
        });
        
        // Spell events - SIMPLIFIED
        CoreExtensions.TryWithManager<SpellcastManager>(this, manager =>
        {
            SpellcastManager.OnComboStateChanged += UpdateComboDisplay;
            SpellcastManager.OnSpellFound += OnSpellFound;
            SpellcastManager.OnSpellNotFound += OnSpellNotFound;
            SpellcastManager.OnComboCleared += OnComboCleared;
            SpellcastManager.OnSpellCast += OnSpellCast;
            SpellcastManager.OnSpellDamageDealt += OnSpellDamageDealt; // NEW direct damage tracking
        });
        
        // Enemy events
        CoreExtensions.TryWithManager<EnemyManager>(this, manager =>
        {
            EnemyManager.OnEnemySpawned += OnEnemySpawned;
            EnemyManager.OnEnemyDespawned += OnEnemyDespawned;
            EntityBehaviour.OnEntityHealthChanged += OnEntityHealthChanged;
        });
    }
    
    private void OnDestroy()
    {
        // Unsubscribe all events (simplified - same structure as setup)
        CoreExtensions.TryWithManager<CombatManager>(this, manager =>
        {
            CombatManager.OnLifeChanged -= UpdateLifeDisplay;
            CombatManager.OnCreativityChanged -= UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged -= UpdateDeckDisplay;
            CombatManager.OnTurnChanged -= UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged -= UpdateTurnPhaseDisplay;
        });
        
        CoreExtensions.TryWithManager<CardManager>(this, manager =>
        {
            CardManager.OnSelectionChanged -= OnCardSelectionChanged;
            CardManager.OnHandUpdated -= OnHandUpdated;
        });
        
        CoreExtensions.TryWithManager<SpellcastManager>(this, manager =>
        {
            SpellcastManager.OnComboStateChanged -= UpdateComboDisplay;
            SpellcastManager.OnSpellFound -= OnSpellFound;
            SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
            SpellcastManager.OnComboCleared -= OnComboCleared;
            SpellcastManager.OnSpellCast -= OnSpellCast;
            SpellcastManager.OnSpellDamageDealt -= OnSpellDamageDealt;
        });
        
        CoreExtensions.TryWithManager<EnemyManager>(this, manager =>
        {
            EnemyManager.OnEnemySpawned -= OnEnemySpawned;
            EnemyManager.OnEnemyDespawned -= OnEnemyDespawned;
            EntityBehaviour.OnEntityHealthChanged -= OnEntityHealthChanged;
        });
    }
    
    // ===== BUTTON ACTIONS =====
    private void PlaySelectedCards()
    {
        if (!_managersReady) return;
        CoreExtensions.TryWithManager<SpellcastManager>(this, sm => sm.PlaySelectedCards());
    }
    
    private void ClearSelection()
    {
        if (!_managersReady) return;
        CoreExtensions.TryWithManager<SpellcastManager>(this, sm => sm.ClearSelection());
    }
    
    private void DrawCard()
    {
        if (!_managersReady) return;
        
        if (!CanDraw())
        {
            Debug.LogWarning("[GameUIHandler] Cannot draw - conditions not met");
            return;
        }
        
        CoreExtensions.TryWithManager<SpellcastManager>(this, sm => sm.DrawCard());
    }
    
    private void CastCombo()
    {
        if (!_managersReady) return;
        CoreExtensions.TryWithManager<SpellcastManager>(this, sm => sm.TryCastCurrentCombo());
    }
    
    private void EndTurn()
    {
        if (!_managersReady) return;
        CoreExtensions.TryWithManager<CombatManager>(this, cm => 
        {
            if (cm.CanEndTurn)
                cm.EndPlayerTurn();
        });
    }
    
    private bool CanDraw()
    {
        if (!_managersReady) return false;
        return CoreExtensions.TryWithManager<CardManager, bool>(this, cm => cm.CanDrawCard());
    }
    
    // ===== RESOURCE DISPLAYS =====
    private void UpdateLifeDisplay(Resource life)
    {
        if (lifeText != null)
        {
            lifeText.text = $"Life: {life.CurrentValue}/{life.MaxValue}";
            lifeText.color = life.Percentage <= healthLowThreshold ? healthLowColor : healthNormalColor;
        }
        
        if (lifeSlider != null)
            lifeSlider.value = life.Percentage;
    }
    
    private void UpdateCreativityDisplay(Resource creativity)
    {
        if (creativityText != null)
            creativityText.text = $"Creativity: {creativity.CurrentValue}/{creativity.MaxValue}";
        
        if (creativitySlider != null)
            creativitySlider.value = creativity.Percentage;
    }
    
    private void UpdateDeckDisplay(int deckSize)
    {
        if (deckText != null)
        {
            int discardSize = CoreExtensions.TryWithManager<CombatManager, int>(this, cm => cm.DiscardSize);
            deckText.text = $"Deck: {deckSize} | Discard: {discardSize}";
        }
        
        UpdateButtonsThrottled();
    }
    
    private void UpdateTurnDisplay(int turn)
    {
        if (turnText != null)
            turnText.text = $"Turn {turn}";
    }
    
    private void UpdateTurnPhaseDisplay(TurnPhase phase)
    {
        if (turnPhaseText != null)
        {
            turnPhaseText.text = phase switch
            {
                TurnPhase.PlayerTurn => "Your Turn",
                TurnPhase.EnemyTurn => "Enemy Turn",
                TurnPhase.TurnTransition => "Processing...",
                TurnPhase.CombatEnd => "Combat Ended",
                _ => phase.ToString()
            };
            
            turnPhaseText.color = phase switch
            {
                TurnPhase.PlayerTurn => Color.green,
                TurnPhase.EnemyTurn => Color.red,
                TurnPhase.TurnTransition => Color.yellow,
                TurnPhase.CombatEnd => Color.gray,
                _ => Color.white
            };
        }
        
        UpdateButtonsThrottled();
    }
    
    // ===== COMBO DISPLAY =====
    private void UpdateComboDisplay(string combo, ComboState state)
    {
        if (comboText == null) return;
        
        string displayText;
        Color displayColor;
        
        switch (state)
        {
            case ComboState.Empty:
                displayText = "Combo: -";
                displayColor = comboEmptyColor;
                break;
                
            case ComboState.Building:
                displayText = $"Combo: {combo}";
                displayColor = comboBuildingColor;
                break;
                
            case ComboState.Ready:
                displayText = $"Combo: {combo} [SPACE TO CAST]";
                displayColor = comboReadyColor;
                break;
                
            case ComboState.Invalid:
                displayText = $"Combo: {combo} ✗";
                displayColor = comboInvalidColor;
                break;
                
            default:
                displayText = $"Combo: {combo}";
                displayColor = comboEmptyColor;
                break;
        }
        
        comboText.text = displayText;
        comboText.color = displayColor;
        
        UpdateCastComboButton(state == ComboState.Ready);
    }
    
    // ===== CARD SELECTION =====
    private void OnCardSelectionChanged(List<Card> selectedCards)
    {
        UpdateCardPlayUI(selectedCards);
        UpdateButtonsThrottled();
    }
    
    private void OnHandUpdated(List<Card> handCards)
    {
        UpdateButtonsThrottled();
    }
    
    private void UpdateCardPlayUI(List<Card> selectedCards)
    {
        if (statusText == null) return;
        
        if (selectedCards?.Count > 0)
        {
            string letters = CardManager.GetLetterSequenceFromCards(selectedCards);
            statusText.text = $"Selected: {letters}";
        }
        else
        {
            statusText.text = "Select cards to play";
        }
    }
    
    private void UpdateButtonsThrottled()
    {
        if (Time.unscaledTime - _lastButtonUpdate < 0.1f) return;
        UpdateAllButtons();
        _lastButtonUpdate = Time.unscaledTime;
    }
    
    private void UpdateAllButtons()
    {
        if (!_managersReady) return;
        
        bool isPlayerTurn = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.IsPlayerTurn);
        bool hasSelectedCards = CoreExtensions.TryWithManager<CardManager, bool>(this, cm => cm.SelectedCards?.Count > 0);
        
        if (playButton != null) 
            playButton.interactable = hasSelectedCards && isPlayerTurn;
            
        if (clearButton != null) 
            clearButton.interactable = hasSelectedCards;
            
        UpdateDrawButton();
        UpdateEndTurnButton();
    }
    
    private void UpdateCastComboButton(bool canCast)
    {
        if (castComboButton == null) return;
        
        bool isPlayerTurn = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.IsPlayerTurn);
        castComboButton.interactable = canCast && isPlayerTurn;
        
        var buttonText = castComboButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = canCast ? "CAST COMBO!" : "Cast Combo";
        }
    }
    
    private void UpdateDrawButton()
    {
        if (drawButton == null) return;
        
        bool canDraw = CanDraw();
        drawButton.interactable = canDraw;
    }
    
    private void UpdateEndTurnButton()
    {
        if (endTurnButton != null)
        {
            bool canEndTurn = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.CanEndTurn);
            endTurnButton.interactable = canEndTurn;
            
            var buttonText = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                bool isProcessing = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.IsProcessingTurn);
                buttonText.text = isProcessing ? "Processing..." : "End Turn";
            }
        }
    }
    
    // ===== SPELL EVENTS - SIMPLIFIED =====
    private void OnSpellFound(SpellAsset spell, string usedLetters)
    {
        if (statusText != null)
        {
            statusText.text = $"Cast: {spell.SpellName}!";
            statusText.color = Color.green;
        }
    }
    
    private void OnSpellNotFound(string attemptedLetters)
    {
        if (statusText != null)
        {
            statusText.text = "No spell found";
            statusText.color = Color.red;
        }
    }
    
    private void OnComboCleared()
    {
        if (comboText != null)
        {
            comboText.text = "Combo: -";
            comboText.color = comboEmptyColor;
        }
        
        UpdateCastComboButton(false);
    }
    
    // ===== SPELL CAST DISPLAY - SIMPLIFIED =====
    private void OnSpellCast(SpellAsset spell, List<CardData> cards)
    {
        ShowSpellCastDisplay(spell);
    }
    
    // NEW: Direct damage tracking from SpellcastManager
    private void OnSpellDamageDealt(SpellAsset spell, int totalDamage)
    {
        UpdateSpellDamageDisplay(spell, totalDamage);
    }
    
    private void ShowSpellCastDisplay(SpellAsset spell)
    {
        if (spellCastPanel == null || spell == null) return;
        
        spellCastPanel.SetActive(true);
        
        if (lastSpellText != null)
            lastSpellText.text = spell.SpellName;
            
        // Reset damage display
        if (spellDamageText != null)
            spellDamageText.text = "Casting...";
        
        if (_spellDisplayCoroutine != null)
            StopCoroutine(_spellDisplayCoroutine);
        _spellDisplayCoroutine = StartCoroutine(HideSpellDisplayAfterDelay());
    }
    
    private void UpdateSpellDamageDisplay(SpellAsset spell, int totalDamage)
    {
        if (spellDamageText == null || !spellCastPanel.activeSelf) return;
        
        var primaryEffect = spell.Effects?.FirstOrDefault();
        if (primaryEffect != null)
        {
            switch (primaryEffect.effectType)
            {
                case SpellEffectType.Damage:
                    spellDamageText.text = $"Damage: {totalDamage}";
                    spellDamageText.color = Color.red;
                    break;
                case SpellEffectType.Heal:
                    spellDamageText.text = $"Heal: +{Mathf.RoundToInt(primaryEffect.value)}";
                    spellDamageText.color = Color.green;
                    break;
                case SpellEffectType.Buff:
                    spellDamageText.text = "Buff Applied";
                    spellDamageText.color = Color.blue;
                    break;
            }
        }
    }
    
    private IEnumerator HideSpellDisplayAfterDelay()
    {
        yield return new WaitForSeconds(spellDisplayDuration);
        
        if (spellCastPanel != null)
            spellCastPanel.SetActive(false);
    }
    
    // ===== ENEMY INFO DISPLAY =====
    private void ShowEnemyInfo(EntityBehaviour enemy)
    {
        if (enemyInfoPanel == null || enemy == null) return;
        
        enemyInfoPanel.SetActive(true);
        
        if (enemyNameText != null)
            enemyNameText.text = enemy.EntityName;
            
        if (enemyTypeText != null)
        {
            string type = enemy.IsBoss() ? "BOSS" : enemy.IsElite() ? "Elite" : "Enemy";
            enemyTypeText.text = type;
            enemyTypeText.color = enemy.IsBoss() ? Color.red : enemy.IsElite() ? Color.yellow : Color.white;
        }
        
        UpdateEnemyHealthDisplay(enemy);
    }
    
    private void HideEnemyInfo()
    {
        if (enemyInfoPanel != null)
            enemyInfoPanel.SetActive(false);
    }
    
    private void UpdateEnemyHealthDisplay(EntityBehaviour enemy)
    {
        if (enemy == null) return;
        
        if (enemyHealthText != null)
            enemyHealthText.text = $"{enemy.CurrentHealth}/{enemy.MaxHealth} HP";
            
        if (enemyHealthBar != null)
            enemyHealthBar.value = enemy.HealthPercentage;
    }
    
    // ===== TOTAL ENEMY HEALTH =====
    private void OnEnemySpawned(EntityBehaviour enemy)
    {
        UpdateTotalEnemyHealth();
    }
    
    private void OnEnemyDespawned(EntityBehaviour enemy)
    {
        UpdateTotalEnemyHealth();
    }
    
    private void OnEntityHealthChanged(EntityBehaviour entity, int oldHealth, int newHealth)
    {
        if (entity.IsEnemy())
        {
            UpdateTotalEnemyHealth();
        }
    }
    
    private void UpdateTotalEnemyHealth()
    {
        _totalEnemyMaxHealth = 0;
        _totalEnemyCurrentHealth = 0;
        
        CoreExtensions.TryWithManager<EnemyManager>(this, em =>
        {
            foreach (var enemy in em.AllEnemies)
            {
                if (enemy != null && enemy.IsValidEntity())
                {
                    _totalEnemyMaxHealth += enemy.MaxHealth;
                    _totalEnemyCurrentHealth += Mathf.Max(0, enemy.CurrentHealth);
                }
            }
            
            UpdateTotalEnemyHealthDisplay(em.AliveEnemyCount);
        });
    }
    
    private void UpdateTotalEnemyHealthDisplay(int enemyCount)
    {
        if (totalEnemyHealthPanel != null)
        {
            totalEnemyHealthPanel.SetActive(_totalEnemyMaxHealth > 0);
        }
        
        if (totalEnemyHealthBar != null && _totalEnemyMaxHealth > 0)
        {
            totalEnemyHealthBar.value = (float)_totalEnemyCurrentHealth / _totalEnemyMaxHealth;
        }
        
        if (totalEnemyHealthText != null)
        {
            totalEnemyHealthText.text = $"{_totalEnemyCurrentHealth} / {_totalEnemyMaxHealth}";
        }
        
        if (enemyCountText != null)
        {
            enemyCountText.text = $"Enemies: {enemyCount}";
        }
    }
    
    private void RefreshAllDisplays()
    {
        if (!_managersReady) return;
        
        CoreExtensions.TryWithManager<CombatManager>(this, combat =>
        {
            UpdateLifeDisplay(combat.Life);
            UpdateCreativityDisplay(combat.Creativity);
            UpdateDeckDisplay(combat.DeckSize);
            UpdateTurnDisplay(combat.CurrentTurn);
            UpdateTurnPhaseDisplay(combat.CurrentPhase);
        });
        
        CoreExtensions.TryWithManager<CardManager>(this, cm =>
            UpdateCardPlayUI(cm.SelectedCards)
        );
        
        UpdateTotalEnemyHealth();
        UpdateAllButtons();
    }
}