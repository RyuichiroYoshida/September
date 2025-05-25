using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerAbilityData : MonoBehaviour
{
    [SerializeField] Text _abilityNameText;
    [SerializeField] Text _abilityTimeText;
    
    public void SetAbilityData(string name, float time)
    {
        _abilityTimeText.text = time.ToString("F1");
        _abilityNameText.text = name;
    }
}
