using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 操作説明を格納するデータ
/// </summary>
[CreateAssetMenu(fileName = "Description", menuName = "Scriptable Objects/ControlDescriptionUI")]
public class ControlDescription : ScriptableObject
{
    public List<DescribedAction> Actions;
}

public enum ControlDescriptionType
{
    Player,
    Exhibit,
    Sarutobi,
    Tanihira,
    SarutobiAiming,
    TanihiraAiming,
    Hatano,
    HatanoAiming,
    Okubo,
    OkuboAiming,
    Takamura,
    AirPlane,
}

/// <summary>
/// 各アクションの説明
/// </summary>
[System.Serializable]
public class DescribedAction
{
    public string ActionName;
    public InputActionReference Action;
    public Sprite Icon;
    public string Description;
}