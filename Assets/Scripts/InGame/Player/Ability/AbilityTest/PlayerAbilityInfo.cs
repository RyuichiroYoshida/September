using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerAbilityInfo : MonoBehaviour
{
    [SerializeField] List<PlayerAbilityData> _playerAbilityDataPrefab;
    [SerializeField] Text _playerNameText;
    
    public void SetPlayerName(string name)
    {
        _playerNameText.text = name;
    }
    
    public void SetAbilityData(string name, float time, int index)
    {
        if (index >= _playerAbilityDataPrefab.Count) return;
        var abilityData = _playerAbilityDataPrefab[index];
        abilityData.SetAbilityData(name, time);
    }
}
