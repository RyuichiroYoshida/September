using System;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHpBarManager : MonoBehaviour
{
    Slider _slider;
    RectTransform _rect;
    [SerializeField] private float _x;
    [SerializeField] private float _y;
    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _rect = GetComponent<RectTransform>();
        _rect.anchoredPosition = new Vector2(_x, _y);
    }

    public void SetHpBar(int maxHp)
    {
        _slider.maxValue = maxHp;
        _slider.value = maxHp;
    }

    public void FillUpdate(int currentHp, int maxHp)
    {
        _slider.value = (float)currentHp / (float)maxHp;
    }

    public void HideHpBar()
    {
        _slider.gameObject.SetActive(false);
    }

    public void ShowHpBar()
    {
        _slider.gameObject.SetActive(true);
    }
}
