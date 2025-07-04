using UnityEngine;
using UnityEngine.UI;

public class SliderValueViewer : MonoBehaviour
{
    [SerializeField] Text _text;
    [SerializeField] Slider _slider;

    private void Start()
    {
        _text.text = _slider.value.ToString();
        _slider.onValueChanged.AddListener(num => _text.text = num.ToString());
    }
}