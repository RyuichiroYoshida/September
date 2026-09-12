using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Slider の Fill 画像を、斜めの切断面で減少させます。
/// Fill 画像の素材自体は斜めのものをそのまま使用できます。
/// </summary>
[ExecuteAlways]
public sealed class DiagonalSliderFill : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)]
    private float fillAmount = 1f;

    [SerializeField, Range(-1f, 1f)]
    private float slope = 0.2f;

    [SerializeField] private Shader fillShader;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int SlopeId = Shader.PropertyToID("_Slope");
    private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
    private static readonly int BarAspectId = Shader.PropertyToID("_BarAspect");

    private Image fillImage;
    private Slider slider;
    private Material runtimeMaterial;

    /// <summary>
    /// 外部から変更できるHP表示量。0〜1で指定します。
    /// </summary>
    public float FillAmount
    {
        get => fillAmount;
        set
        {
            fillAmount = Mathf.Clamp01(value);

            if (runtimeMaterial != null)
                ApplyFillAmount(fillAmount);
        }
    }

    /// <summary>
    /// 外部からHP表示量を変更します。0〜1で指定します。
    /// </summary>
    public void SetFillAmount(float value)
    {
        FillAmount = value;
    }

    /// <summary>
    /// Image.DOFillAmount と同じように、斜めHPバーをTweenします。
    /// targetValue は0〜1の割合です。
    /// </summary>
    public Tweener DOFillAmount(float targetValue, float duration)
    {
        targetValue = Mathf.Clamp01(targetValue);

        return DOTween.To(
                () => FillAmount,
                value => FillAmount = value,
                targetValue,
                duration)
            .SetTarget(this);
    }

    private void Awake()
    {
        Setup();
    }

    private void OnEnable()
    {
        Setup();
        ApplyEditPreview();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            Setup();
            ApplyEditPreview();
        }
    }

    private void Setup()
    {
        if (runtimeMaterial != null)
            return;

        fillImage = GetComponent<Image>();
        slider = GetComponentInParent<Slider>();

        if (fillImage == null)
        {
            Debug.LogError("DiagonalSliderFill must be attached to the target Image object.", this);
            enabled = false;
            return;
        }

        Shader shader = fillShader;
        if (shader == null)
        {
            Debug.LogError("DiagonalSliderFill: Fill ShaderにDiagonalHpFill.shaderを割り当ててください。", this);
            enabled = false;
            return;
        }

        runtimeMaterial = new Material(shader)
        {
            name = $"{shader.name} ({gameObject.name})"
        };

        runtimeMaterial.SetTexture(MainTexId, fillImage.mainTexture);
        runtimeMaterial.SetColor(ColorId, fillImage.color);
        runtimeMaterial.SetFloat(SlopeId, slope);

        if (fillImage.sprite != null)
        {
            Vector4 outerUv = DataUtility.GetOuterUV(fillImage.sprite);
            runtimeMaterial.SetVector(UvRectId, outerUv);
        }
        else
        {
            runtimeMaterial.SetVector(UvRectId, new Vector4(0f, 0f, 1f, 1f));
        }

        UpdateBarAspect();
        fillImage.material = runtimeMaterial;
    }

    private void Start()
    {
        if (!enabled || fillImage == null)
            return;

        if (!Application.isPlaying)
            return;

        if (slider == null)
        {
            ApplyFillAmount(fillAmount);
            return;
        }

        // Slider標準の長方形カットを止め、画像全体をシェーダーで切る。
        if (slider.fillRect == fillImage.rectTransform)
            slider.fillRect = null;

        RectTransform rect = fillImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        slider.onValueChanged.AddListener(OnSliderValueChanged);
        OnSliderValueChanged(slider.value);
    }

    private void LateUpdate()
    {
        if (!enabled || runtimeMaterial == null)
            return;

        // Tween・外部API・Inspectorの値を共通の表示元にする。
        // Image.fillAmountで上書きするとTweenの結果が描画前に消えてしまう。
        ApplyEditPreview();
    }

    private void UpdateBarAspect()
    {
        if (runtimeMaterial == null || fillImage == null)
            return;

        Rect rect = fillImage.rectTransform.rect;
        float aspect = rect.height > Mathf.Epsilon
            ? rect.width / rect.height
            : 1f;

        runtimeMaterial.SetFloat(BarAspectId, aspect);
    }

    private void ApplyEditPreview()
    {
        if (runtimeMaterial == null)
            return;

        // Inspector上で変更した値を即座にマテリアルへ反映する。
        runtimeMaterial.SetFloat(SlopeId, slope);
        ApplyFillAmount(fillAmount);
        UpdateBarAspect();
    }

    private void OnSliderValueChanged(float value)
    {
        float range = slider.maxValue - slider.minValue;
        float normalizedValue = range > Mathf.Epsilon
            ? Mathf.InverseLerp(slider.minValue, slider.maxValue, value)
            : 0f;

        FillAmount = normalizedValue;
    }

    private void ApplyFillAmount(float value)
    {
        runtimeMaterial.SetFloat(FillAmountId, Mathf.Clamp01(value));
    }

    private void OnDestroy()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(OnSliderValueChanged);

        if (fillImage != null && fillImage.material == runtimeMaterial)
            fillImage.material = null;

        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);
        }
    }
}
