using System;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class GameInput
{
    private static GameInput _instance;
    private bool _subscribed;
    
    public static GameInput I => _instance ??= Init();

    public DeviceType UseDeviceType { get; private set; } = DeviceType.Unknown;
    public event Action<DeviceType> OnDeviceTypeChanged;

    public bool IsMoveInput { get; private set; } = true;
    public bool IsActionInput {  get; private set; } = true;
    public bool IsLookInput { get; private set; } = true;

    private bool _isInputBlockedByUI;
    public bool IsInputBlockedByUI
    {
        get => _isInputBlockedByUI;
        set
        {
            _isInputBlockedByUI = value;
            UpdateInputState();
        }
    }

    private void UpdateInputState()
    {
        ToggleMoveInput(IsMoveInput);
        ToggleActionInput(IsActionInput);
        ToggleLookInput(IsLookInput);
    }

    static GameInput Init()
    {
        // インスタンスの生成、イベントの購読
        _instance = new GameInput();
        _instance.SubscribeToInput();
        if (Application.isPlaying) _instance.Enable();
        _instance.UpdateInputState();
        return _instance;
    }
    
    /// <summary> 再生開始時にリセットをかける </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        if (_instance != null)
        {
            _instance.UnsubscribeFromInput();
            _instance.Disable();
            _instance.Dispose();
        }
        _instance = null;
    }

    void SubscribeToInput()
    {
        if (_subscribed) return;
        _subscribed = true;
        
        foreach (var map in asset.actionMaps)
        {
            foreach (var action in map)
            {
                action.started += DeviceDetection;
                action.performed += DeviceDetection;
                action.canceled += DeviceDetection;
            }
        }
    }

    void UnsubscribeFromInput()
    {
        if (!_subscribed) return;
        _subscribed = false;
        
        foreach (var map in asset.actionMaps)
        {
            foreach (var action in map)
            {
                action.started -= DeviceDetection;
                action.performed -= DeviceDetection;
                action.canceled -= DeviceDetection;
            }
        }
    }

    void DeviceDetection(InputAction.CallbackContext context)
    {
        DeviceType lastDeviceType = UseDeviceType;
        UseDeviceType = context.control.device switch
        {
            null => DeviceType.Unknown,
            Keyboard or Mouse => DeviceType.KeyboardMouse,
            Gamepad => DeviceType.Gamepad,
            _ => DeviceType.Other
        };

        if (lastDeviceType != UseDeviceType)
        {
            OnDeviceTypeChanged?.Invoke(UseDeviceType);
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"Change Device to : {UseDeviceType}");
#endif
        }
    }

    public void ToggleMoveInput(bool value)
    {
        IsMoveInput = value;
        if (value && !_isInputBlockedByUI)
        {
            Player.Jump.Enable();
            Player.Move.Enable();
            Player.Dash.Enable();
            Player.Aim.Enable();
        }
        else
        {
            Player.Jump.Disable();
            Player.Move.Disable();
            Player.Dash.Disable();
            Player.Aim.Disable();
        }
    }
    public void ToggleActionInput(bool value)
    {
        IsActionInput = value;
        if (value && !_isInputBlockedByUI)
        {
            Player.Attack.Enable();
            Player.Ability1.Enable();
            Player.Ability2.Enable();
            Player.Interact.Enable();
            Player.LockOn.Enable();
        }
        else
        {
            Player.Attack.Disable();
            Player.Ability1.Disable();
            Player.Ability2.Disable();
            Player.Interact.Disable();
            Player.LockOn.Disable();
        }
    }

    public void ToggleLookInput(bool value)
    {
        IsLookInput = value;
        if (value && !_isInputBlockedByUI)
        {
            Player.Look.Enable();
        }
        else
        {
            Player.Look.Disable();
        }
    }

    public enum DeviceType
    {
        Unknown,
        KeyboardMouse,
        Gamepad,
        Other
    }
}
