using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "New Spell", menuName = "Spellcast/Spell Asset")]
public class SpellAsset : ScriptableObject
{
    [Header("Basic Spell Information")]
    [SerializeField] private string spellName = "New Spell";
    [SerializeField] private string letterCode = "";
    
    [Header("Spell Classification")]
    [SerializeField] private SpellType spellType = SpellType.Basic;
    [SerializeField] private List<SpellSubtype> spellSubtypes = new List<SpellSubtype>();
    
    [Header("Effects")]
    [SerializeField] private List<SpellEffect> effects = new List<SpellEffect>();
    
    // Properties
    public string SpellName => spellName;
    public string LetterCode => letterCode;
    public SpellType Type => spellType;
    public IReadOnlyList<SpellSubtype> Subtypes => spellSubtypes.AsReadOnly();
    public IReadOnlyList<SpellEffect> Effects => effects.AsReadOnly();
    
    /// <summary>
    /// Normalisierter Letter Code für Vergleiche
    /// </summary>
    public string NormalizedCode => letterCode.ToUpper();
    
    /// <summary>
    /// Überprüft ob der Spell gültig konfiguriert ist
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(spellName) && !string.IsNullOrEmpty(letterCode);
    
    /// <summary>
    /// Überprüft ob der Spell einen bestimmten Subtyp hat
    /// </summary>
    public bool HasSubtype(SpellSubtype subtype) => spellSubtypes.Contains(subtype);
    
    /// <summary>
    /// Überprüft ob der Spell einen der gegebenen Subtypen hat
    /// </summary>
    public bool HasAnySubtype(params SpellSubtype[] subtypes) => subtypes.Any(HasSubtype);
    
    /// <summary>
    /// Führt alle Spell-Effekte aus
    /// </summary>
    public void ExecuteEffects()
    {
        foreach (var effect in effects)
        {
            effect.Execute();
        }
    }
}

[System.Serializable]
public class SpellEffect
{
    [Header("Effect Configuration")]
    public string effectName = "Effect";
    public SpellEffectType effectType = SpellEffectType.Damage;
    public float value = 1f;
    
    /// <summary>
    /// Führt den Effekt aus - wird vom SpellcastManager aufgerufen
    /// </summary>
    public void Execute()
    {
        // TODO: Event-System Integration
        // SpellcastManager.Instance.TriggerSpellEffect(this);
        Debug.Log($"[SpellEffect] Executing {effectName}: {effectType} ({value})");
    }
}

public enum SpellType
{
    Basic,
    Element,
    School
}

public enum SpellSubtype
{
    Basic,
    Fire,
    Light,
    Nature,
    Dark,
    Time,
    Attack,
    Defense,
    Support,
    Disrupt
}

public enum SpellEffectType
{
    Damage,
    Heal,
    Buff,
    Debuff,
    Summon,
    Teleport,
    Shield,
    Custom
}