using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using InGame.Player.Ability;
using UnityEngine;
using UnityEngine.UI;

public class AbilityTestUi : MonoBehaviour
{
    [SerializeField] private Text _playersAbilityText;
    private readonly StringBuilder _stringBuilder = new StringBuilder();

    private void Update()
    {
        _stringBuilder.Clear();
        var info = AbilityExecutor.Instance.PlayerActiveAbilityInfo;
        if (info == null) return;
        _stringBuilder.AppendLine("Players Ability Info");
        foreach (var playerRef in info.Keys)
        {
            _stringBuilder.AppendLine($"Player {playerRef.PlayerId}");
            foreach (var ability in info[playerRef])
            {
                var abilityName = ability.Instance.AbilityName.ToString();
                var time = ability.Instance.CurrentCooldown;
                var maxTime = ability.Instance.Cooldown;
                _stringBuilder.AppendLine($"Ability: {abilityName} Cooldown: {time:F1}/{maxTime:F1}");
            }
        }
        _playersAbilityText.text = _stringBuilder.ToString();
    }
}
