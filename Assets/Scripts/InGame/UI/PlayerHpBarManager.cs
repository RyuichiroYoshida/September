using System;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHpBarManager : MonoBehaviour
{
    Slider _hpBar;
    RectTransform _rect;
    [SerializeField] private float _x;
    [SerializeField] private float _y;
    private void Start()
    {
        _hpBar = GetComponent<Slider>();
        _rect = GetComponent<RectTransform>();
        _rect.anchoredPosition = new Vector2(_x, _y);
    }

    public void SetHpBar(int maxHp)
    {
        _hpBar.maxValue = maxHp;
    }

    public void FillUpdate(int currentHp, int maxHp)
    {
        _hpBar.value = (float)currentHp / (float)maxHp;
    }

    public void HideHpBar()
    {
        _hpBar.gameObject.SetActive(false);
    }

    public void ShowHpBar()
    {
        _hpBar.gameObject.SetActive(true);
    }
}
