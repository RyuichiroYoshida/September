#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

[Overlay(typeof(SceneView), "TimeScale Controller")]
public class TimeScaleOverlay : Overlay
{
    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement();
        root.style.flexDirection = FlexDirection.Row;
        root.style.alignItems = Align.Center;
        root.style.paddingLeft = 4;
        root.style.paddingRight = 4;

        // 再生直後に 0 になってしまう問題を回避
        int initial = Mathf.RoundToInt(Time.timeScale);
        if (initial <= 0)
        {
            initial = 1;
            Time.timeScale = 1f; // 明示的に初期値設定
        }

        var valueLabel = new Label(initial.ToString())
        {
            style =
            {
                unityTextAlign = TextAnchor.MiddleRight,
                width = 24
            }
        };

        var slider = new SliderInt(0, 10)
        {
            value = initial,
            style =
            {
                width = 120
            }
        };

        slider.RegisterValueChangedCallback(evt =>
        {
            Time.timeScale = evt.newValue;
            valueLabel.text = evt.newValue.ToString();
        });

        root.Add(valueLabel);
        root.Add(slider);
        return root;
    }
}
#endif