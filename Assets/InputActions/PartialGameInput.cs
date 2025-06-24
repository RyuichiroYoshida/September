using UnityEngine.InputSystem;

public partial class GameInput
{
    private static readonly GameInput Instance;
    public static readonly GameInput I = Instance ??= new GameInput();

    public DeviceType UseDeviceType { get; private set; } = DeviceType.Unknown;

    void OnAnyInput(InputAction.CallbackContext context)
    {
        DeviceType lastDeviceType = UseDeviceType;
        UseDeviceType = context.control.device switch
        {
            null => DeviceType.Unknown,
            Keyboard or Mouse => DeviceType.KeyboardMouse,
            Gamepad => DeviceType.Gamepad,
            _ => DeviceType.Other
        };
        
        if (lastDeviceType != UseDeviceType) UnityEngine.Debug.Log($"Change Device to : {UseDeviceType}");
    }

    void OnConstruct()
    {
        foreach (var map in asset.actionMaps)
        {
            foreach (var action in map)
            {
                action.performed += OnAnyInput;
            }
        }
    }

    void OnDestruct()
    {
        foreach (var map in asset.actionMaps)
        {
            foreach (var action in map)
            {
                action.performed -= OnAnyInput;
            }
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
