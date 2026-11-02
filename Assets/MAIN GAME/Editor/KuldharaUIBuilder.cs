using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Rebuilds the whole game UI (_Gameplay/GameUI) from the Kuldhara sprite sheet and wires GameManager / MapUI / SettingsUI.
// Many sheet sprites have mockup text baked in; Cover() paints a sampled colour patch over it before our own text goes on top.
public static class KuldharaUIBuilder
{
    const string SP = "Assets/MAIN GAME/UI/Kuldhara/Sprites/";
    static readonly Color Gold = new Color(1f, .84f, .52f), Ink = new Color(.24f, .14f, .07f);
    static readonly Dictionary<string, Texture2D> raw = new Dictionary<string, Texture2D>();

    [MenuItem("Tools/Kuldhara/Build UI")]
    public static void Build()
    {
        var gp = GameObject.Find("_Gameplay");
        var gm = gp.GetComponent<GameManager>();
        var old = gp.transform.Find("GameUI");
        if (old) Object.DestroyImmediate(old.gameObject);
        EnsureHelperSprites();

        var root = new GameObject("GameUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(gp.transform);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var R = (RectTransform)root.transform;

        // ---------- HUD ----------
        var hud = Full("HudPanel", R, 0);
        var portrait = Img("Portrait", hud, "HUD_Portrait", new Vector2(0, 1), new Vector2(110, -110), new Vector2(170, 165));
        var stBack = Img("StaminaBack", hud, "Bar_Stamina", new Vector2(0, 1), new Vector2(390, -110), new Vector2(380, 60));
        stBack.color = new Color(.35f, .3f, .3f);
        var stFill = Img("StaminaFill", stBack.rectTransform, "Bar_Stamina", new Vector2(.5f, .5f), Vector2.zero, new Vector2(380, 60));
        stFill.type = Image.Type.Filled; stFill.fillMethod = Image.FillMethod.Horizontal; stFill.fillOrigin = 0;

        var mmMask = Img("MinimapMask", hud, "UI_Circle", new Vector2(1, 1), new Vector2(-150, -150), new Vector2(225, 225));
        mmMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var mm = new GameObject("MinimapMap", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        Place(mm.rectTransform, mmMask.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(225, 225));
        mm.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MAIN GAME/UI/Kuldhara/MapBake.png");
        mm.raycastTarget = false;
        Img("MinimapFrame", hud, "HUD_MinimapRing", new Vector2(1, 1), new Vector2(-150, -150), new Vector2(290, 300));
        var mmArrow = Arrow("MinimapArrow", hud, new Vector2(1, 1), new Vector2(-150, -150), 42);

        var quest = Img("QuestTracker", hud, "Notify_QuestUpdated", new Vector2(1, 1), new Vector2(-260, -350), new Vector2(460, 128));
        Cover(quest, "Notify_QuestUpdated", .27f, .14f, .97f, .86f, .5f, .5f);
        var questTitle = Txt("Title", quest.rectTransform, "The Cursed Relics", 30, Gold, new Vector2(.28f, .5f), new Vector2(.97f, .9f), TextAlignmentOptions.Left);
        var questBody = Txt("Body", quest.rectTransform, "Relics found: 0 / 5", 24, Color.white, new Vector2(.28f, .12f), new Vector2(.97f, .52f), TextAlignmentOptions.Left);

        var toast = Img("Toast", hud, "Notify_ItemCollected", new Vector2(.5f, 1), new Vector2(0, -90), new Vector2(580, 160));
        Cover(toast, "Notify_ItemCollected", .25f, .14f, .97f, .86f, .5f, .5f);
        var toastText = Txt("Text", toast.rectTransform, "", 28, Color.white, new Vector2(.25f, .1f), new Vector2(.96f, .9f), TextAlignmentOptions.Left);
        toast.gameObject.SetActive(false);

        // ---------- Overlay screens (built before HUD buttons so the buttons can target them) ----------
        var pause = Backdrop("PausePanel", R, .7f);
        Img("BannerL", pause, "Banner_Red", new Vector2(.5f, .5f), new Vector2(-430, 60), new Vector2(230, 360));
        Img("BannerR", pause, "Banner_Red", new Vector2(.5f, .5f), new Vector2(430, 60), new Vector2(230, 360));
        Txt("Title", pause, "Game Paused", 72, Gold, new Vector2(.3f, .78f), new Vector2(.7f, .92f), TextAlignmentOptions.Center);

        var confirm = Backdrop("ConfirmPanel", R, .75f);
        var alert = Img("Popup", confirm, "Popup_Alert", new Vector2(.5f, .5f), Vector2.zero, new Vector2(640, 480));
        var yes = HotSpot("Btn_Yes", alert.rectTransform, .06f, .07f, .5f, .3f);
        var no = HotSpot("Btn_No", alert.rectTransform, .5f, .07f, .94f, .3f);

        var settings = Backdrop("SettingsPanel", R, .75f);
        var sp = Img("Panel", settings, "Panel_Quest", new Vector2(.5f, .5f), Vector2.zero, new Vector2(780, 830));
        Cover(sp, "Panel_Quest", .07f, .1f, .93f, .8f, .5f, .45f);
        Cover(sp, "Panel_Quest", .3f, .84f, .78f, .97f, .62f, .9f);
        Txt("Title", sp.rectTransform, "Settings", 44, Gold, new Vector2(.3f, .83f), new Vector2(.78f, .98f), TextAlignmentOptions.Center);
        var qualityLabel = Row(sp.rectTransform, "Graphics", .66f);
        var qPrev = IconBtn("Prev", sp.rectTransform, "Misc_Pointer", new Vector2(.56f, .66f), 56, 0);
        var qNext = IconBtn("Next", sp.rectTransform, "Misc_Pointer", new Vector2(.9f, .66f), 56, 180);
        Row(sp.rectTransform, "Shadows", .5f).text = "";
        var shadows = MakeToggle(sp.rectTransform, new Vector2(.73f, .5f));
        Row(sp.rectTransform, "Volume", .34f).text = "";
        var volume = MakeSlider(sp.rectTransform, .34f, 0f, 1f);
        Row(sp.rectTransform, "Look Speed", .18f).text = "";
        var sens = MakeSlider(sp.rectTransform, .18f, .3f, 2.5f);
        var sClose = IconBtn("Close", sp.rectTransform, "Icon_close", new Vector2(.95f, .95f), 80, 0);

        var inv = Backdrop("InventoryPanel", R, .75f);
        var ip = Img("Panel", inv, "Panel_Inventory", new Vector2(.5f, .5f), Vector2.zero, new Vector2(790, 730));
        var slots = new List<Image>();
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 4; c++)
            {
                // painted slot cells, measured from the sprite (normalised, bottom-left origin)
                float[] xs = { .05f, .29f, .52f, .74f }, y0s = { .535f, .34f, .07f }, y1s = { .82f, .54f, .31f };
                float x0 = xs[c], x1 = x0 + .21f, y0 = y0s[r], y1 = y1s[r];
                var cell = new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f);
                if (r == 0) Cover(ip, "Panel_Inventory", x0 + .015f, y0 + .015f, x1 - .015f, y1 - .015f, .45f, .45f); // hide painted mockup items
                var s = Img("Relic" + (slots.Count + 1), ip.rectTransform, "Misc_Seal", cell, Vector2.zero, new Vector2(100, 100));
                slots.Add(s);
            }
        Cover(ip, "Panel_Inventory", .3f, .03f, .72f, .1f, .5f, .065f);
        var relicCount = Txt("RelicCount", ip.rectTransform, "0 / 5", 30, Gold, new Vector2(.3f, .02f), new Vector2(.72f, .11f), TextAlignmentOptions.Center);
        var iClose = HotSpot("Btn_Close", ip.rectTransform, .89f, .88f, .99f, .99f);

        var map = Backdrop("MapPanel", R, .75f);
        var mp = Img("Panel", map, "Panel_Map", new Vector2(.5f, .5f), Vector2.zero, new Vector2(1300, 870));
        var mapImg = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        mapImg.transform.SetParent(mp.transform, false);
        var mr = mapImg.rectTransform;
        // square map area centred in the frame's parchment
        mr.anchorMin = mr.anchorMax = new Vector2(.47f, .47f); mr.sizeDelta = new Vector2(690, 690); mr.anchoredPosition = Vector2.zero;
        mapImg.texture = mm.texture;
        var mPlayer = Arrow("PlayerMarker", mr, new Vector2(.5f, .5f), Vector2.zero, 44);
        var targets = new List<Transform>(); var markers = new List<RectTransform>();
        foreach (Transform t in gp.transform.Find("Relics")) { targets.Add(t); markers.Add(Img("Marker_" + t.name, mr, "Icon_pin", new Vector2(.5f, .5f), Vector2.zero, new Vector2(46, 46)).rectTransform); }
        var shrine = gp.transform.Find("Shrine_Goal");
        targets.Add(shrine); markers.Add(Img("Marker_Shrine", mr, "Icon_crown", new Vector2(.5f, .5f), Vector2.zero, new Vector2(54, 54)).rectTransform);
        var mClose = IconBtn("Close", mp.rectTransform, "Icon_close", new Vector2(.97f, .95f), 90, 0);

        var journal = Backdrop("JournalPanel", R, .75f);
        var jp = Img("Panel", journal, "Panel_Journal", new Vector2(.5f, .5f), Vector2.zero, new Vector2(1220, 930));
        Cover(jp, "Panel_Journal", .53f, .1f, .9f, .82f, .72f, .5f);
        var journalText = Txt("Text", jp.rectTransform, "", 26, Ink, new Vector2(.555f, .1f), new Vector2(.9f, .76f), TextAlignmentOptions.TopLeft);
        var jClose = IconBtn("Close", jp.rectTransform, "Icon_close", new Vector2(.96f, .93f), 90, 0);

        // ---------- Main menu / win / lose ----------
        var menu = Backdrop("MenuPanel", R, .45f);
        Img("Logo", menu, "Logo", new Vector2(.3f, .5f), new Vector2(0, 230), new Vector2(760, 530));
        var bNew = ImgBtn("Btn_NewJourney", menu, "Btn_NewJourney", new Vector2(.3f, .5f), new Vector2(0, -90));
        var bExplore = ImgBtn("Btn_Explore", menu, "Btn_Explore", new Vector2(.3f, .5f), new Vector2(0, -195));
        var bSettings = ImgBtn("Btn_Settings", menu, "Btn_Settings", new Vector2(.3f, .5f), new Vector2(0, -300));
        var bExit = ImgBtn("Btn_Exit", menu, "Btn_Exit", new Vector2(.3f, .5f), new Vector2(0, -405));

        var pResume = ImgBtn("Btn_Continue", pause, "Btn_Continue", new Vector2(.5f, .5f), new Vector2(0, 150));
        var pSettings = ImgBtn("Btn_Settings", pause, "Btn_Settings", new Vector2(.5f, .5f), new Vector2(0, 45));
        var pRestart = ImgBtn("Btn_NewJourney", pause, "Btn_NewJourney", new Vector2(.5f, .5f), new Vector2(0, -60));
        var pExit = ImgBtn("Btn_Exit", pause, "Btn_Exit", new Vector2(.5f, .5f), new Vector2(0, -165));

        var lose = Backdrop("LosePanel", R, .8f);
        Txt("Title", lose, "CAUGHT", 110, new Color(.9f, .2f, .15f), new Vector2(.2f, .68f), new Vector2(.8f, .88f), TextAlignmentOptions.Center);
        Txt("Sub", lose, "The spirit of Kuldhara claims another soul...", 36, Gold, new Vector2(.2f, .58f), new Vector2(.8f, .68f), TextAlignmentOptions.Center);
        var lRetry = ImgBtn("Btn_Continue", lose, "Btn_Continue", new Vector2(.5f, .5f), new Vector2(0, -20));
        var lRestart = ImgBtn("Btn_NewJourney", lose, "Btn_NewJourney", new Vector2(.5f, .5f), new Vector2(0, -125));
        var lExit = ImgBtn("Btn_Exit", lose, "Btn_Exit", new Vector2(.5f, .5f), new Vector2(0, -230));

        var win = Backdrop("WinPanel", R, .8f);
        Img("BannerL", win, "Banner_Camel", new Vector2(.5f, .5f), new Vector2(-480, 40), new Vector2(230, 350));
        Img("BannerR", win, "Banner_Camel", new Vector2(.5f, .5f), new Vector2(480, 40), new Vector2(230, 350));
        Txt("Title", win, "CURSE BROKEN", 100, Gold, new Vector2(.2f, .68f), new Vector2(.8f, .88f), TextAlignmentOptions.Center);
        Txt("Sub", win, "Every relic rests at the shrine. Kuldhara sleeps at last.", 36, Color.white, new Vector2(.2f, .58f), new Vector2(.8f, .68f), TextAlignmentOptions.Center);
        var wRestart = ImgBtn("Btn_NewJourney", win, "Btn_NewJourney", new Vector2(.5f, .5f), new Vector2(0, -40));
        var wExit = ImgBtn("Btn_Exit", win, "Btn_Exit", new Vector2(.5f, .5f), new Vector2(0, -145));

        // HUD icon buttons (left of minimap)
        var hPause = IconBtn("Btn_Pause", hud, "Icon_pause", new Vector2(1, 1), 88, 0, new Vector2(-340, -70));
        var hMap = IconBtn("Btn_Map", hud, "Icon_pin", new Vector2(1, 1), 88, 0, new Vector2(-340, -165));
        var hJournal = IconBtn("Btn_Journal", hud, "Icon_book", new Vector2(1, 1), 88, 0, new Vector2(-435, -70));
        var hInv = IconBtn("Btn_Inventory", hud, "Misc_Chest", new Vector2(1, 1), 88, 0, new Vector2(-435, -165));

        // ---------- Wiring ----------
        On(bNew, gm.Play); On(bExplore, gm.Explore); On(bExit, gm.AskQuit);
        On(pResume, gm.Resume); On(pRestart, gm.Restart); On(pExit, gm.AskQuit);
        On(lRetry, gm.Retry); On(lRestart, gm.Restart); On(lExit, gm.AskQuit);
        On(wRestart, gm.Restart); On(wExit, gm.AskQuit);
        On(yes, gm.ConfirmQuit); On(no, gm.ClosePanel);
        On(hPause, gm.Pause);
        foreach (var b in new[] { sClose, iClose, mClose, jClose }) On(b, gm.ClosePanel);
        OnOpen(bSettings, gm, settings.gameObject); OnOpen(pSettings, gm, settings.gameObject);
        OnOpen(hMap, gm, map.gameObject); OnOpen(hJournal, gm, journal.gameObject); OnOpen(hInv, gm, inv.gameObject);

        var mobile = FindInScene<Canvas>("Canvas");
        RestyleMobile(mobile);

        var so = new SerializedObject(gm);
        Set(so, "menuPanel", menu.gameObject); Set(so, "hudPanel", hud.gameObject); Set(so, "pausePanel", pause.gameObject);
        Set(so, "winPanel", win.gameObject); Set(so, "losePanel", lose.gameObject); Set(so, "confirmPanel", confirm.gameObject);
        Set(so, "mobileControls", mobile ? mobile.gameObject : null);
        Set(so, "questTitle", questTitle); Set(so, "questBody", questBody); Set(so, "staminaFill", stFill);
        Set(so, "toast", toast.gameObject); Set(so, "toastText", toastText);
        Set(so, "relicCountText", relicCount); Set(so, "journalText", journalText);
        var slotsProp = so.FindProperty("relicSlots");
        slotsProp.arraySize = 5;
        for (int i = 0; i < 5; i++) slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        for (int i = 5; i < slots.Count; i++) Object.DestroyImmediate(slots[i].gameObject); // only 5 relics
        so.ApplyModifiedPropertiesWithoutUndo();

        var player = Object.FindObjectOfType<PlayerController>();
        var mapUI = new SerializedObject(root.AddComponent<MapUI>());
        Set(mapUI, "player", player.transform); Set(mapUI, "minimap", mm); Set(mapUI, "minimapArrow", mmArrow);
        Set(mapUI, "mapArea", mr); Set(mapUI, "mapPlayer", mPlayer);
        SetArray(mapUI, "targets", targets.ToArray()); SetArray(mapUI, "targetMarkers", markers.ToArray());
        mapUI.ApplyModifiedPropertiesWithoutUndo();

        var setUI = root.AddComponent<SettingsUI>();
        var ss = new SerializedObject(setUI);
        Set(ss, "player", player); Set(ss, "qualityLabel", qualityLabel); Set(ss, "shadowsToggle", shadows);
        Set(ss, "volumeSlider", volume); Set(ss, "sensitivitySlider", sens);
        ss.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(qPrev.onClick, setUI.PrevQuality);
        UnityEventTools.AddPersistentListener(qNext.onClick, setUI.NextQuality);

        foreach (var p in new[] { hud, pause, confirm, settings, inv, map, journal, lose, win }) p.gameObject.SetActive(false);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gp.scene);
        Debug.Log("Kuldhara UI built.");
    }

    // ---------- Builders ----------
    static Sprite Spr(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(SP + n + ".png");

    static void Place(RectTransform r, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
    }

    static Image Img(string name, RectTransform parent, string sprite, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Place(img.rectTransform, parent, anchor, pos, size);
        img.sprite = Spr(sprite);
        img.raycastTarget = false;
        // only plain frames slice; sprites with baked text must scale uniformly so Cover() rects line up
        if (sprite.StartsWith("Slot_") || sprite == "UI_SoftRect") img.type = Image.Type.Sliced;
        return img;
    }

    static RectTransform Full(string name, RectTransform parent, float dim)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        if (dim > 0) { var i = r.gameObject.AddComponent<Image>(); i.color = new Color(0, 0, 0, dim); }
        return r;
    }

    static RectTransform Backdrop(string name, RectTransform parent, float dim) => Full(name, parent, dim);

    static TMP_Text Txt(string name, RectTransform parent, string text, float size, Color color, Vector2 min, Vector2 max, TextAlignmentOptions align)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.anchorMin = min; t.rectTransform.anchorMax = max; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
        t.enableAutoSizing = true; t.fontSizeMin = size * .5f; t.fontSizeMax = size;
        if (color != Ink) { t.fontStyle = FontStyles.Bold; t.outlineWidth = .18f; t.outlineColor = new Color32(30, 15, 5, 255); }
        return t;
    }

    static Button ImgBtn(string name, RectTransform parent, string sprite, Vector2 anchor, Vector2 pos)
    {
        var s = Spr(sprite);
        float h = 96f, w = h * s.rect.width / s.rect.height;
        var img = Img(name, parent, sprite, anchor, pos, new Vector2(w, h));
        img.type = Image.Type.Simple; img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        var cb = b.colors; cb.highlightedColor = new Color(1f, .93f, .75f); cb.pressedColor = new Color(.75f, .65f, .5f); b.colors = cb;
        return b;
    }

    static Button IconBtn(string name, RectTransform parent, string sprite, Vector2 anchor, float size, float rotZ, Vector2 pos = default)
    {
        var img = Img(name, parent, sprite, anchor, pos, new Vector2(size, size));
        img.type = Image.Type.Simple; img.preserveAspect = true; img.raycastTarget = true;
        img.rectTransform.localEulerAngles = new Vector3(0, 0, rotZ);
        return img.gameObject.AddComponent<Button>();
    }

    // Invisible clickable area over a button that is painted into a sprite (normalised rect in parent).
    static Button HotSpot(string name, RectTransform parent, float x0, float y0, float x1, float y1)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var r = img.rectTransform; r.SetParent(parent, false);
        r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
        img.color = new Color(1, 1, 1, 0);
        return img.gameObject.AddComponent<Button>();
    }

    // Paints over baked mockup text with the sprite's own colour sampled at (sx, sy).
    static void Cover(Image target, string sprite, float x0, float y0, float x1, float y1, float sx, float sy)
    {
        if (!raw.ContainsKey(sprite)) { var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(SP + sprite + ".png")); raw[sprite] = t; }
        var tex = raw[sprite];
        // average a small patch for a cleaner colour
        Color c = Color.clear; int n = 0;
        for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++) { c += tex.GetPixel((int)(sx * tex.width) + dx * 2, (int)(sy * tex.height) + dy * 2); n++; }
        c /= n; c.a = 1;
        var img = new GameObject("Cover", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var r = img.rectTransform; r.SetParent(target.rectTransform, false);
        r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
        img.sprite = Spr("UI_SoftRect"); img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = false;
    }

    static RectTransform Arrow(string name, RectTransform parent, Vector2 anchor, Vector2 pos, float size)
    {
        var pivot = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        Place(pivot, parent, anchor, pos, new Vector2(size, size));
        var a = Img("Icon", pivot, "Misc_Pointer", new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
        a.preserveAspect = true;
        a.rectTransform.localEulerAngles = new Vector3(0, 0, -90); // sprite points left -> make it point up
        return pivot;
    }

    static TMP_Text Row(RectTransform panel, string label, float y)
    {
        Txt("Label_" + label, panel, label, 34, Ink, new Vector2(.1f, y - .05f), new Vector2(.5f, y + .05f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
        return Txt("Value_" + label, panel, "", 32, Ink, new Vector2(.6f, y - .05f), new Vector2(.86f, y + .05f), TextAlignmentOptions.Center);
    }

    static Toggle MakeToggle(RectTransform panel, Vector2 at)
    {
        var bg = Img("Toggle_Shadows", panel, "Slot_Empty", at, Vector2.zero, new Vector2(80, 80));
        bg.raycastTarget = true;
        var check = Img("Check", bg.rectTransform, "Misc_Seal", new Vector2(.5f, .5f), Vector2.zero, new Vector2(60, 60));
        var t = bg.gameObject.AddComponent<Toggle>();
        t.targetGraphic = bg; t.graphic = check; t.isOn = true;
        return t;
    }

    static Slider MakeSlider(RectTransform panel, float y, float min, float max)
    {
        var bg = Img("Slider", panel, "Bar_SmallSkull", new Vector2(.73f, y), Vector2.zero, new Vector2(330, 56));
        bg.color = new Color(.45f, .4f, .4f);
        var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(bg.rectTransform, false); fillArea.anchorMin = new Vector2(.2f, .3f); fillArea.anchorMax = new Vector2(.95f, .7f); fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
        var fill = Img("Fill", fillArea, "UI_SoftRect", new Vector2(0, 0), Vector2.zero, Vector2.zero);
        fill.rectTransform.anchorMax = new Vector2(1, 1); fill.color = new Color(1f, .75f, .25f);
        var handleArea = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(bg.rectTransform, false); handleArea.anchorMin = new Vector2(.2f, 0); handleArea.anchorMax = new Vector2(.95f, 1); handleArea.offsetMin = handleArea.offsetMax = Vector2.zero;
        var handle = Img("Handle", handleArea, "Misc_Seal", new Vector2(0, .5f), Vector2.zero, new Vector2(56, 56));
        handle.raycastTarget = true;
        var s = bg.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.minValue = min; s.maxValue = max; s.value = 1f;
        bg.raycastTarget = true;
        return s;
    }

    static void RestyleMobile(Canvas mobile)
    {
        if (!mobile) return;
        foreach (var j in mobile.GetComponentsInChildren<MobileJoystick>(true))
        {
            var so = new SerializedObject(j);
            var bgR = so.FindProperty("background").objectReferenceValue as RectTransform;
            var hR = so.FindProperty("handle").objectReferenceValue as RectTransform;
            if (bgR && bgR.GetComponent<Image>()) { bgR.GetComponent<Image>().sprite = Spr("Joystick_Base"); bgR.GetComponent<Image>().color = Color.white; }
            if (hR && hR.GetComponent<Image>()) { hR.GetComponent<Image>().sprite = Spr("Joystick_Knob"); hR.GetComponent<Image>().color = Color.white; }
        }
        foreach (var b in mobile.GetComponentsInChildren<MobileButton>(true))
        {
            var img = b.GetComponent<Image>(); if (!img) continue;
            img.sprite = Spr(b.name.ToLower().Contains("sprint") ? "Action_Run" : "Action_Walk");
            img.color = Color.white; img.preserveAspect = true;
            foreach (var t in b.GetComponentsInChildren<TMP_Text>(true)) t.gameObject.SetActive(false);
            foreach (var t in b.GetComponentsInChildren<Text>(true)) t.gameObject.SetActive(false);
        }
    }

    // UI_Circle (minimap mask), UI_SoftRect (cover patches), HUD_MinimapRing (minimap frame with hollow centre).
    static void EnsureHelperSprites()
    {
        bool made = false;
        if (!File.Exists(SP + "UI_Circle.png")) { WriteTex("UI_Circle", 256, (x, y) => Mathf.Clamp01(127.5f - Vector2.Distance(new Vector2(x, y), new Vector2(127.5f, 127.5f)))); made = true; }
        if (!File.Exists(SP + "UI_SoftRect.png")) { WriteTex("UI_SoftRect", 64, (x, y) => Mathf.Clamp01(Mathf.Min(Mathf.Min(x, 63 - x), Mathf.Min(y, 63 - y)) / 10f)); made = true; }
        if (!File.Exists(SP + "HUD_MinimapRing.png"))
        {
            var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(SP + "HUD_Minimap.png"));
            var px = t.GetPixels32(); float cx = t.width / 2f, cy = t.height / 2f, rIn = Mathf.Min(t.width, t.height) * .39f;
            for (int i = 0; i < px.Length; i++)
            {
                float d = Vector2.Distance(new Vector2(i % t.width, i / t.width), new Vector2(cx, cy));
                px[i].a = (byte)Mathf.Min(px[i].a, 255 * Mathf.Clamp01(d - rIn));
            }
            t.SetPixels32(px); t.Apply(); File.WriteAllBytes(SP + "HUD_MinimapRing.png", t.EncodeToPNG()); made = true;
        }
        if (!made) return;
        AssetDatabase.Refresh();
        foreach (var n in new[] { "UI_Circle", "UI_SoftRect", "HUD_MinimapRing" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(SP + n + ".png");
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
            if (n == "UI_SoftRect") ti.spriteBorder = new Vector4(12, 12, 12, 12);
            ti.SaveAndReimport();
        }
    }

    static void WriteTex(string name, int size, System.Func<int, int, float> alpha)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) t.SetPixel(x, y, new Color(1, 1, 1, alpha(x, y)));
        t.Apply(); File.WriteAllBytes(SP + name + ".png", t.EncodeToPNG());
    }

    // ---------- Wiring helpers ----------
    static void On(Button b, UnityAction a) => UnityEventTools.AddPersistentListener(b.onClick, a);
    static void OnOpen(Button b, GameManager gm, GameObject panel) => UnityEventTools.AddObjectPersistentListener(b.onClick, gm.OpenPanel, panel);
    static void Set(SerializedObject so, string prop, Object v) => so.FindProperty(prop).objectReferenceValue = v;
    static void SetArray(SerializedObject so, string prop, Object[] v)
    {
        var p = so.FindProperty(prop); p.arraySize = v.Length;
        for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = v[i];
    }
    static T FindInScene<T>(string name) where T : Component
    {
        foreach (var c in Resources.FindObjectsOfTypeAll<T>()) if (c.name == name && c.gameObject.scene.IsValid()) return c;
        return null;
    }
}
