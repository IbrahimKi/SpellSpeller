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
    
    public string NormalizedCode => letterCode.ToUpper();
    public bool IsValid => !string.IsNullOrEmpty(spellName) && !string.IsNullOrEmpty(letterCode);
    
    public bool HasSubtype(SpellSubtype subtype) => spellSubtypes.Contains(subtype);
    public bool HasAnySubtype(params SpellSubtype[] subtypes) => subtypes.Any(HasSubtype);
    
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
