using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Core loop: Menu -> collect relics (each wakes the chaser) -> return to shrine -> Win. Caught -> Lose -> retry from last relic.
// Overlay panels (pause, settings, inventory, map, journal, quit confirm) pause the game while open.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private PlayerController player;
    [SerializeField] private GhostChaser chaser;

    [Header("Screens")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject hudPanel, pausePanel, winPanel, losePanel, confirmPanel;
    [SerializeField] private GameObject mobileControls;

    [Header("HUD")]
    [SerializeField] private TMP_Text questTitle;
    [SerializeField] private TMP_Text questBody;
    [SerializeField] private Image staminaFill;
    [SerializeField] private GameObject toast;
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private float toastTime = 3f;

    [Header("Inventory / Journal")]
    [SerializeField] private Image[] relicSlots;
    [SerializeField] private TMP_Text relicCountText, journalText;

    private int collected, total;
    private Vector3 checkpoint;
    private bool over, exploreMode;
    private GameObject overlay, overlayBack;

    private void Awake() => Instance = this;

    private void Start()
    {
        total = FindObjectsOfType<ObjectiveTrigger>().Length - 1; // all triggers except the shrine
        checkpoint = player.transform.position;
        Show(menuPanel);
        SetPaused(true);
        RefreshUI();
    }

    private void Update()
    {
        if (staminaFill) staminaFill.fillAmount = player.Stamina01;
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !over && !menuPanel.activeSelf)
        {
            if (overlay) ClosePanel(); else Pause();
        }
#endif
    }

    // ---- Main menu ----
    public void Play()
    {
        Show(hudPanel);
        SetPaused(false);
        RefreshUI();
        Toast(exploreMode ? "Explore Kuldhara freely. Nothing hunts you... tonight." : "Find the " + total + " cursed relics. Don't let it catch you.");
    }

    public void Explore() { exploreMode = true; Play(); }
    public void Restart() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }

    // ---- Pause / overlays ----
    public void Pause() { OpenPanel(pausePanel); }
    public void Resume() { CloseOverlay(); Show(hudPanel); SetPaused(false); }

    public void OpenPanel(GameObject panel)
    {
        overlayBack = overlay != null ? overlay : (menuPanel.activeSelf ? menuPanel : null);
        if (overlay) overlay.SetActive(false);
        if (menuPanel.activeSelf) menuPanel.SetActive(false);
        overlay = panel;
        panel.SetActive(true);
        SetPaused(true);
    }

    public void ClosePanel()
    {
        var back = overlayBack;
        CloseOverlay();
        if (back == menuPanel) menuPanel.SetActive(true);
        else if (back != null) OpenPanel(back); // e.g. back to pause menu
        else Resume();
    }

    public void AskQuit() => OpenPanel(confirmPanel);
    public void ConfirmQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ---- Objectives ----
    public void CollectRelic(Vector3 at)
    {
        collected++;
        checkpoint = at;
        RefreshUI();
        if (collected >= total) Toast("All relics found! Bring them to the Bone Shrine.");
        else Toast(exploreMode ? "Relic " + collected + "/" + total + " found." : "Relic " + collected + "/" + total + "... something woke up. RUN!");
        if (!exploreMode) chaser.StartChase(player.transform);
    }

    public void ReachShrine()
    {
        if (over) return;
        if (collected < total) { Toast("The shrine needs " + (total - collected) + " more relic(s)."); return; }
        End(winPanel);
    }

    public void PlayerCaught() { if (!over) End(losePanel); }

    public void Retry()
    {
        over = false;
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false; // CharacterController overrides transform moves while enabled
        player.transform.position = checkpoint + Vector3.up;
        cc.enabled = true;
        Play();
    }

    private void End(GameObject panel)
    {
        over = true;
        chaser.Stop();
        CloseOverlay();
        Show(panel);
        SetPaused(true);
    }

    // ---- Helpers ----
    private void RefreshUI()
    {
        bool done = collected >= total;
        questTitle.text = exploreMode && !done ? "Explore Kuldhara" : done ? "Break the Curse" : "The Cursed Relics";
        questBody.text = done ? "Bring the relics to the Bone Shrine (north-west)." : "Relics found: " + collected + " / " + total;
        for (int i = 0; i < relicSlots.Length; i++) relicSlots[i].enabled = i < collected;
        relicCountText.text = collected + " / " + total;
        journalText.text =
            "<b>The Curse of Kuldhara</b>\n\nThe villagers fled in a single night, leaving five cursed relics behind.\n\n" +
            "Relics are hidden in the graveyard, the abandoned camp, the dead forest, the rock canyon and the old ruins.\n\n" +
            "Each relic I take wakes the spirit. I must sprint to escape it.\n\n" +
            "When all " + total + " are found, the Bone Shrine in the north-west can end the curse.\n\n" +
            "<b>Found: " + collected + " / " + total + "</b>";
    }

    private void SetPaused(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
        player.enabled = !paused;
        if (mobileControls) mobileControls.SetActive(!paused && Application.isMobilePlatform);
    }

    private void CloseOverlay()
    {
        if (overlay) overlay.SetActive(false);
        overlay = overlayBack = null;
    }

    private void Show(GameObject panel)
    {
        foreach (var p in new[] { menuPanel, hudPanel, winPanel, losePanel }) p.SetActive(p == panel);
    }

    private void Toast(string msg)
    {
        StopAllCoroutines();
        StartCoroutine(ToastRoutine(msg));
    }

    private IEnumerator ToastRoutine(string msg)
    {
        toastText.text = msg;
        toast.SetActive(true);
        yield return new WaitForSecondsRealtime(toastTime);
        toast.SetActive(false);
    }
}
