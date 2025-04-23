using System;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHpBarManager : MonoBehaviour
{
    Slider hpBar;
    private void Start()
    {
        hpBar = GetComponent<Slider>();
    }

    public void SetHpBar(float maxHp)
    {
        hpBar.maxValue = maxHp;
    }

    public void FillUpdate(int maxHp, int currentHp)
    {
        hpBar.value = (float)currentHp / (float)maxHp;
    }
}
