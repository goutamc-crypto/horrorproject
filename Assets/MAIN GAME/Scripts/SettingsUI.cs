using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Graphics quality, shadows, master volume and look sensitivity. Saved in PlayerPrefs, applied on load.
public class SettingsUI : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private TMP_Text qualityLabel;
    [SerializeField] private Toggle shadowsToggle;
    [SerializeField] private Slider volumeSlider, sensitivitySlider;

    private int quality;

    private void Awake()
    {
        quality = PlayerPrefs.GetInt("quality", QualitySettings.GetQualityLevel());
        shadowsToggle.isOn = PlayerPrefs.GetInt("shadows", 1) == 1;
        volumeSlider.value = PlayerPrefs.GetFloat("volume", 1f);
        sensitivitySlider.value = PlayerPrefs.GetFloat("sensitivity", 1f);
        shadowsToggle.onValueChanged.AddListener(_ => Apply());
        volumeSlider.onValueChanged.AddListener(_ => Apply());
        sensitivitySlider.onValueChanged.AddListener(_ => Apply());
        Apply();
    }

    public void NextQuality() { quality = (quality + 1) % QualitySettings.names.Length; Apply(); }
    public void PrevQuality() { quality = (quality + QualitySettings.names.Length - 1) % QualitySettings.names.Length; Apply(); }

    private void Apply()
    {
        QualitySettings.SetQualityLevel(quality, true);
        QualitySettings.shadows = shadowsToggle.isOn ? ShadowQuality.All : ShadowQuality.Disable;
        AudioListener.volume = volumeSlider.value;
        player.LookSensitivityScale = sensitivitySlider.value;
        qualityLabel.text = QualitySettings.names[quality];

        PlayerPrefs.SetInt("quality", quality);
        PlayerPrefs.SetInt("shadows", shadowsToggle.isOn ? 1 : 0);
        PlayerPrefs.SetFloat("volume", volumeSlider.value);
        PlayerPrefs.SetFloat("sensitivity", sensitivitySlider.value);
    }
}
