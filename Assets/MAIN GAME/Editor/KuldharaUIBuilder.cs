using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Rebuilds the whole game UI (_Gameplay/GameUI) from Layer Lab's white sprites, tinted to a Kuldhara sandstone palette,
// and wires GameManager / MapUI / SettingsUI. Layout groups position stacked content; Check() reports any overlap left.
public static class KuldharaUIBuilder
{
    const string LL = "Assets/Layer Lab/GUI Pro-CasualGame/ResourcesData/";
    const string SP = LL + "Sprites/Components/";

    const string SprButton = "Button/Button01_195_White", SprCard = "Frame/BasicFrame_Round20", SprSquare = "Button/Button_Square01_White",
        SprPopup = "Popup/Popoup01~03_White_Bg", SprPopupTop = "Popup/Popoup01~03_White_Bg_TopBasic", SprCircle = "Button/Button_Circle128_White",
        SprTrack = "Slider/Slider_Basic03_Bg", SprFill = "Slider/Slider_Basic03_Fill_White";

    static readonly Color Parchment = Hex("F4E4C1"), Sand = Hex("DDBE8A"), SandBtn = Hex("FFD99A"), Terracotta = Hex("A8481F"), Maroon = Hex("6B2A1C"),
        Gold = Hex("F2B84B"), Ink = Hex("3B2314"), Cream = Hex("FFF3DA"), Blood = Hex("B3261E");
    static readonly Color Dusk = new Color(.28f, .14f, .07f, .5f);

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    [MenuItem("Tools/Kuldhara/Build UI")]
    public static void Build()
    {
        var gp = GameObject.Find("_Gameplay");
        var gm = gp.GetComponent<GameManager>();
        var old = gp.transform.Find("GameUI");
        if (old) Object.DestroyImmediate(old.gameObject);

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

        // ---------- HUD: quest card top-left, toast top-centre, minimap + buttons top-right ----------
        var hud = Backdrop("HudPanel", R, Color.clear);
        var quest = Img("QuestCard", hud, SprCard, Parchment);
        Place(quest.rectTransform, new Vector2(0, 1), new Vector2(40, -40), new Vector2(470, 176));
        var questTitle = Txt("Title", quest.rectTransform, "The Cursed Relics", 34, Terracotta, TextAlignmentOptions.Left);
        Stretch(questTitle.rectTransform, 24, 116, 24, 12);
        var questBody = Txt("Body", quest.rectTransform, "Relics found: 0 / 5", 26, Ink, TextAlignmentOptions.Left);
        Stretch(questBody.rectTransform, 24, 70, 24, 62);
        var track = Img("StaminaTrack", quest.rectTransform, SprTrack, Maroon);
        Stretch(track.rectTransform, 24, 22, 24, 124);
        track.gameObject.AddComponent<Mask>(); // clips the square-ended fill to the track's rounded corners
        var stFill = Img("StaminaFill", track.rectTransform, "Slider/Slider_Handle_Fill", Gold);
        Stretch(stFill.rectTransform, 0, 0, 0, 0);
        stFill.preserveAspect = false; stFill.type = Image.Type.Filled; stFill.fillMethod = Image.FillMethod.Horizontal; stFill.fillOrigin = 0;

        var toast = Img("Toast", hud, SprCard, Maroon);
        Place(toast.rectTransform, new Vector2(.5f, 1), new Vector2(0, -40), new Vector2(560, 100));
        var toastText = Txt("Text", toast.rectTransform, "", 28, Cream, TextAlignmentOptions.Center);
        Stretch(toastText.rectTransform, 24, 10, 24, 10);
        toast.gameObject.SetActive(false);

        var mmPlate = Img("Minimap", hud, SprCard, Parchment);
        Place(mmPlate.rectTransform, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(250, 250));
        var mmMask = Img("MinimapMask", mmPlate.rectTransform, SprCard, Color.white);
        Stretch(mmMask.rectTransform, 10, 10, 10, 10);
        mmMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var mapTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MAIN GAME/UI/Kuldhara/MapBake.png");
        var mm = Raw("MinimapMap", mmMask.rectTransform, mapTex);
        Stretch(mm.rectTransform, 0, 0, 0, 0);
        var mmArrow = Arrow("MinimapArrow", mmMask.rectTransform, 40);

        var hudGrid = Rect("HudButtons", hud);
        Place(hudGrid, new Vector2(1, 1), new Vector2(-40, -302), new Vector2(250, 212));
        var grid = hudGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(119, 100); grid.spacing = new Vector2(12, 12);
        var hPause = HudBtn("Btn_Pause", hudGrid, "Pictoicon_Control_Pause", "Pause");
        var hMap = HudBtn("Btn_Map", hudGrid, "Pictoicon_Map_0", "Map");
        var hJournal = HudBtn("Btn_Journal", hudGrid, "Pictoicon_Book_0", "Journal");
        var hInv = HudBtn("Btn_Inventory", hudGrid, "Pictoicon_Bag", "Relics");

        // ---------- Overlay screens ----------
        var pause = Backdrop("PausePanel", R, Dusk);
        var pc = Stack(Panel(pause, new Vector2(600, 570), "Game Paused", Terracotta, Cream));
        var pResume = TextBtn("Btn_Continue", pc, "Resume", Terracotta, Cream);
        var pSettings = TextBtn("Btn_Settings", pc, "Settings", SandBtn, Ink);
        var pRestart = TextBtn("Btn_NewJourney", pc, "Restart", SandBtn, Ink);
        var pExit = TextBtn("Btn_Exit", pc, "Exit Game", SandBtn, Ink);

        var confirm = Backdrop("ConfirmPanel", R, Dusk);
        var cc = Panel(confirm, new Vector2(680, 380), "Leave Kuldhara?", Maroon, Cream);
        var cMsg = Txt("Message", cc, "The spirits will still be here when you return.", 30, Ink, TextAlignmentOptions.Center);
        Stretch(cMsg.rectTransform, 0, 104, 0, 0);
        var cBtns = Rect("Buttons", cc);
        Stretch(cBtns, 0, 0, 0, 0);
        cBtns.anchorMax = new Vector2(1, 0); cBtns.sizeDelta = new Vector2(0, 88); cBtns.pivot = new Vector2(.5f, 0);
        var row = cBtns.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 20; row.childForceExpandHeight = row.childControlWidth = row.childControlHeight = true;
        var no = TextBtn("Btn_No", cBtns, "Stay", SandBtn, Ink);
        var yes = TextBtn("Btn_Yes", cBtns, "Leave", Terracotta, Cream);

        var settings = Backdrop("SettingsPanel", R, Dusk);
        var sc = Stack(Panel(settings, new Vector2(820, 570), "Settings", Terracotta, Cream, out var sClose), 16);
        var gfx = SettingRow(sc, "Graphics");
        var qPrev = ArrowBtn("Prev", gfx, "Pictoicon_Arrow_Prev", 0);
        var qNext = ArrowBtn("Next", gfx, "Pictoicon_Arrow_Next", 1);
        var qualityLabel = Txt("Value", gfx, "", 30, Ink, TextAlignmentOptions.Center);
        Stretch(qualityLabel.rectTransform, 72, 0, 72, 0);
        var shadows = MakeToggle(SettingRow(sc, "Shadows"));
        var volume = MakeSlider(SettingRow(sc, "Volume"), 0f, 1f);
        var sens = MakeSlider(SettingRow(sc, "Look Speed"), .3f, 2.5f);

        var inv = Backdrop("InventoryPanel", R, Dusk);
        var ic = Panel(inv, new Vector2(860, 420), "Cursed Relics", Terracotta, Cream, out var iClose);
        var slotRow = Rect("Slots", ic);
        Stretch(slotRow, 0, 70, 0, 0);
        var slotGrid = slotRow.gameObject.AddComponent<GridLayoutGroup>();
        slotGrid.cellSize = new Vector2(140, 140); slotGrid.spacing = new Vector2(16, 0);
        slotGrid.childAlignment = TextAnchor.MiddleCenter;
        var slots = new List<Image>();
        for (int i = 0; i < 5; i++)
        {
            var cell = Img("Slot" + (i + 1), slotRow, SprCard, Sand);
            var relic = Img("Relic" + (i + 1), cell.rectTransform, Picto("Pictoicon_Rune"), Terracotta);
            Stretch(relic.rectTransform, 28, 28, 28, 28);
            slots.Add(relic);
        }
        var relicCount = Txt("RelicCount", ic, "0 / 5", 34, Terracotta, TextAlignmentOptions.Center);
        Stretch(relicCount.rectTransform, 0, 0, 0, 0);
        relicCount.rectTransform.anchorMax = new Vector2(1, 0); relicCount.rectTransform.pivot = new Vector2(.5f, 0); relicCount.rectTransform.sizeDelta = new Vector2(0, 56);

        var map = Backdrop("MapPanel", R, Dusk);
        var mc = Panel(map, new Vector2(800, 880), "Map of Kuldhara", Terracotta, Cream, out var mClose);
        var mapImg = Raw("MapImage", mc, mapTex);
        var mr = mapImg.rectTransform;
        Place(mr, new Vector2(.5f, .5f), Vector2.zero, new Vector2(720, 720));
        var mPlayer = Arrow("PlayerMarker", mr, 44);
        var targets = new List<Transform>(); var markers = new List<RectTransform>();
        foreach (Transform t in gp.transform.Find("Relics")) { targets.Add(t); markers.Add(Marker("Marker_" + t.name, mr, "Pictoicon_Mappin", Blood, 44)); }
        targets.Add(gp.transform.Find("Shrine_Goal")); markers.Add(Marker("Marker_Shrine", mr, "Pictoicon_Home_0", Gold, 52));

        var journal = Backdrop("JournalPanel", R, Dusk);
        var jc = Panel(journal, new Vector2(900, 800), "Journal", Terracotta, Cream, out var jClose);
        var journalText = Txt("Text", jc, "", 30, Ink, TextAlignmentOptions.TopLeft);
        Stretch(journalText.rectTransform, 8, 0, 8, 0);

        // ---------- Main menu / win / lose ----------
        var menu = Backdrop("MenuPanel", R, new Color(Dusk.r, Dusk.g, Dusk.b, .35f));
        var menuCard = Img("Card", menu, SprPopup, Parchment);
        Place(menuCard.rectTransform, new Vector2(.28f, .5f), Vector2.zero, new Vector2(620, 660));
        menuCard.rectTransform.pivot = new Vector2(.5f, .5f);
        var mList = Rect("Content", menuCard.rectTransform);
        Stretch(mList, 40, 40, 40, 36);
        Stack(mList);
        Le(Txt("Title", mList, "KULDHARA", 92, Terracotta, TextAlignmentOptions.Center).gameObject, 110);
        Le(Txt("Subtitle", mList, "The Cursed Village of the Thar", 30, Ink, TextAlignmentOptions.Center).gameObject, 56);
        var bNew = TextBtn("Btn_NewJourney", mList, "New Journey", Terracotta, Cream);
        var bExplore = TextBtn("Btn_Explore", mList, "Explore", SandBtn, Ink);
        var bSettings = TextBtn("Btn_Settings", mList, "Settings", SandBtn, Ink);
        var bExit = TextBtn("Btn_Exit", mList, "Exit Game", SandBtn, Ink);

        var lose = Backdrop("LosePanel", R, Dusk);
        var lc = Stack(Panel(lose, new Vector2(720, 570), "Caught", Blood, Cream));
        Le(Txt("Sub", lc, "The spirit of Kuldhara claims another soul...", 30, Ink, TextAlignmentOptions.Center).gameObject, 80);
        var lRetry = TextBtn("Btn_Continue", lc, "Retry from Last Relic", Terracotta, Cream);
        var lRestart = TextBtn("Btn_NewJourney", lc, "Restart", SandBtn, Ink);
        var lExit = TextBtn("Btn_Exit", lc, "Exit Game", SandBtn, Ink);

        var win = Backdrop("WinPanel", R, Dusk);
        var wc = Stack(Panel(win, new Vector2(720, 470), "Curse Broken", Gold, Ink));
        Le(Txt("Sub", wc, "Every relic rests at the haveli. Kuldhara sleeps at last.", 30, Ink, TextAlignmentOptions.Center).gameObject, 80);
        var wRestart = TextBtn("Btn_NewJourney", wc, "Play Again", Terracotta, Cream);
        var wExit = TextBtn("Btn_Exit", wc, "Exit Game", SandBtn, Ink);

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
        SetArray(so, "relicSlots", slots.ToArray());
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
        Debug.Log("Kuldhara UI built. Layout problems: " + Check());
    }

    // ---------- Self-check ----------
    // Every text and control in a screen must not intersect another one, and text must fit its rect. Parent/child pairs are allowed.
    [MenuItem("Tools/Kuldhara/Check UI Layout")]
    public static int Check()
    {
        int problems = 0;
        var ui = GameObject.Find("_Gameplay").transform.Find("GameUI");
        foreach (Transform screen in ui)
        {
            var toast = screen.Find("Toast");
            bool was = screen.gameObject.activeSelf, toastWas = toast && toast.gameObject.activeSelf;
            screen.gameObject.SetActive(true);
            if (toast) toast.gameObject.SetActive(true);
            foreach (var g in screen.GetComponentsInChildren<LayoutGroup>()) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)g.transform);

            var items = new List<RectTransform>();
            foreach (var t in screen.GetComponentsInChildren<TMP_Text>())
            {
                t.ForceMeshUpdate();
                if (t.isTextOverflowing) { problems++; Debug.LogWarning("UI text overflow: " + screen.name + "/" + t.name, t); }
                items.Add(t.rectTransform);
            }
            foreach (var s in screen.GetComponentsInChildren<Selectable>()) items.Add((RectTransform)s.transform);
            if (screen.name == "HudPanel") foreach (Transform c in screen) items.Add((RectTransform)c); // HUD blocks sit side by side

            for (int a = 0; a < items.Count; a++)
                for (int b = a + 1; b < items.Count; b++)
                {
                    if (items[a].IsChildOf(items[b]) || items[b].IsChildOf(items[a])) continue;
                    if (!World(items[a]).Overlaps(World(items[b]))) continue;
                    problems++;
                    Debug.LogWarning("UI overlap in " + screen.name + ": " + items[a].name + " / " + items[b].name, items[a]);
                }

            if (toast) toast.gameObject.SetActive(toastWas);
            screen.gameObject.SetActive(was);
        }
        return problems;
    }

    static Rect World(RectTransform r)
    {
        var c = new Vector3[4];
        r.GetWorldCorners(c);
        return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
    }

    // ---------- Builders ----------
    static string Picto(string n) => "Icon_PictoIcons/128/" + n;

    static Sprite Spr(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(SP + path + (path.StartsWith("Icon_PictoIcons") ? ".Png" : ".png"));
        if (!s) Debug.LogError("Missing UI sprite: " + path);
        return s;
    }

    static RectTransform Rect(string name, RectTransform parent)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        return r;
    }

    // Pivot follows the anchor, so pos is the inset from that screen edge.
    static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
    }

    static void Stretch(RectTransform r, float left, float bottom, float right, float top)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top);
    }

    static Image Img(string name, RectTransform parent, string sprite, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.sprite = Spr(sprite);
        img.color = color;
        img.raycastTarget = false;
        if (img.sprite && img.sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        else img.preserveAspect = true;
        return img;
    }

    static RawImage Raw(string name, RectTransform parent, Texture tex)
    {
        var raw = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        raw.rectTransform.SetParent(parent, false);
        raw.texture = tex;
        raw.raycastTarget = false;
        return raw;
    }

    // Full-screen layer; a non-clear colour also blocks clicks to what is behind it.
    static RectTransform Backdrop(string name, RectTransform parent, Color color)
    {
        var r = Rect(name, parent);
        Stretch(r, 0, 0, 0, 0);
        if (color.a > 0) r.gameObject.AddComponent<Image>().color = color;
        return r;
    }

    // Popup with title bar, returns the empty content area below the bar.
    static RectTransform Panel(RectTransform backdrop, Vector2 size, string title, Color bar, Color titleColor) =>
        Panel(backdrop, size, title, bar, titleColor, false, out _);

    static RectTransform Panel(RectTransform backdrop, Vector2 size, string title, Color bar, Color titleColor, out Button close) =>
        Panel(backdrop, size, title, bar, titleColor, true, out close);

    static RectTransform Panel(RectTransform backdrop, Vector2 size, string title, Color bar, Color titleColor, bool closable, out Button close)
    {
        const float barH = 96;
        var p = Img("Panel", backdrop, SprPopup, Parchment);
        Place(p.rectTransform, new Vector2(.5f, .5f), Vector2.zero, size);
        var top = Img("TitleBar", p.rectTransform, SprPopupTop, bar);
        top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = Vector2.one; top.rectTransform.pivot = new Vector2(.5f, 1);
        top.rectTransform.anchoredPosition = Vector2.zero; top.rectTransform.sizeDelta = new Vector2(-6, barH); // sprite is a few px wider than the popup body
        var t = Txt("Title", top.rectTransform, title, 46, titleColor, TextAlignmentOptions.Center);
        Stretch(t.rectTransform, 96, 8, 96, 8);

        close = null;
        if (closable)
        {
            var x = Img("Btn_Close", top.rectTransform, SprSquare, Parchment);
            Place(x.rectTransform, new Vector2(1, .5f), new Vector2(-16, 0), new Vector2(68, 68));
            x.pixelsPerUnitMultiplier = x.sprite.rect.height / 68f;
            x.raycastTarget = true;
            close = x.gameObject.AddComponent<Button>();
            Tint(close);
            var icon = Img("Icon", x.rectTransform, Picto("PictoIcon_Close"), Maroon);
            Stretch(icon.rectTransform, 18, 20, 18, 16);
        }

        var content = Rect("Content", p.rectTransform);
        Stretch(content, 36, 32, 36, barH + 24);
        return content;
    }

    static RectTransform Stack(RectTransform r, float spacing = 14)
    {
        var v = r.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.childControlWidth = v.childControlHeight = v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperCenter;
        return r;
    }

    static void Le(GameObject go, float height)
    {
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = height;
    }

    static TMP_Text Txt(string name, RectTransform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LL + "Fonts/LilitaOne-Regular SDF.asset");
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
        t.enableAutoSizing = true; t.fontSizeMin = size * .6f; t.fontSizeMax = size;
        return t;
    }

    static void Tint(Button b)
    {
        var c = b.colors;
        c.highlightedColor = new Color(1f, .95f, .85f); c.pressedColor = new Color(.8f, .7f, .6f); c.selectedColor = Color.white;
        b.colors = c;
    }

    // Layer Lab's main button; the multiplier shrinks its 195px-tall slices to the height we draw it at.
    static Button TextBtn(string name, RectTransform parent, string label, Color color, Color textColor, float height = 88)
    {
        var img = Img(name, parent, SprButton, color);
        img.pixelsPerUnitMultiplier = img.sprite.rect.height / height;
        img.raycastTarget = true;
        Le(img.gameObject, height);
        var b = img.gameObject.AddComponent<Button>();
        Tint(b);
        var t = Txt("Label", img.rectTransform, label, 36, textColor, TextAlignmentOptions.Center);
        Stretch(t.rectTransform, 16, 12, 16, 6); // sprite has a darker lip along the bottom edge
        return b;
    }

    static Button HudBtn(string name, RectTransform parent, string icon, string caption)
    {
        var img = Img(name, parent, SprSquare, Parchment);
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        Tint(b);
        var i = Img("Icon", img.rectTransform, Picto(icon), Terracotta);
        Stretch(i.rectTransform, 30, 44, 30, 10);
        var t = Txt("Caption", img.rectTransform, caption, 22, Ink, TextAlignmentOptions.Center);
        Stretch(t.rectTransform, 6, 12, 6, 60);
        return b;
    }

    static Button ArrowBtn(string name, RectTransform parent, string icon, float side)
    {
        var img = Img(name, parent, SprSquare, SandBtn);
        Place(img.rectTransform, new Vector2(side, .5f), Vector2.zero, new Vector2(60, 60));
        img.pixelsPerUnitMultiplier = img.sprite.rect.height / 60f;
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        Tint(b);
        var i = Img("Icon", img.rectTransform, Picto(icon), Terracotta);
        Stretch(i.rectTransform, 14, 16, 14, 12);
        return b;
    }

    // Rotated by MapUI, so the pivot must stay centred.
    static RectTransform Arrow(string name, RectTransform parent, float size)
    {
        var a = Img(name, parent, Picto("Pictoicon_Arrow_Up"), Blood);
        Place(a.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
        return a.rectTransform;
    }

    static RectTransform Marker(string name, RectTransform parent, string icon, Color color, float size)
    {
        var m = Img(name, parent, Picto(icon), color);
        Place(m.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
        return m.rectTransform;
    }

    // One row of a settings list; returns the empty right-hand area for its control.
    static RectTransform SettingRow(RectTransform list, string label)
    {
        var bg = Img("Row_" + label, list, SprCard, Sand);
        Le(bg.gameObject, 84);
        var t = Txt("Label", bg.rectTransform, label, 32, Ink, TextAlignmentOptions.Left);
        Stretch(t.rectTransform, 28, 8, 0, 8);
        t.rectTransform.anchorMax = new Vector2(.42f, 1);
        var area = Rect("Control", bg.rectTransform);
        area.anchorMin = new Vector2(.42f, 0); area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(12, 12); area.offsetMax = new Vector2(-24, -12);
        return area;
    }

    static Toggle MakeToggle(RectTransform area)
    {
        var bg = Img("Toggle_Shadows", area, "UI_Etc/Toggle02_CheckBox_White_bg", Parchment);
        Place(bg.rectTransform, new Vector2(1, .5f), Vector2.zero, new Vector2(60, 60));
        bg.raycastTarget = true;
        var check = Img("Check", bg.rectTransform, "UI_Etc/Toggle02_CheckBox_Icon_White", Terracotta);
        Stretch(check.rectTransform, 10, 10, 10, 10);
        var t = bg.gameObject.AddComponent<Toggle>();
        t.targetGraphic = bg; t.graphic = check; t.isOn = true;
        return t;
    }

    static Slider MakeSlider(RectTransform area, float min, float max)
    {
        const float inset = 24; // half the handle, so it stays inside the track at both ends
        var root = Rect("Slider", area);
        Stretch(root, 0, 0, 0, 0);
        var track = Img("Track", root, SprTrack, Maroon);
        track.rectTransform.anchorMin = new Vector2(0, .5f); track.rectTransform.anchorMax = new Vector2(1, .5f);
        track.rectTransform.sizeDelta = new Vector2(0, 24);
        track.raycastTarget = true;

        var fillArea = Rect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f); fillArea.sizeDelta = new Vector2(-inset * 2, 24);
        var fill = Img("Fill", fillArea, SprFill, Gold);
        fill.pixelsPerUnitMultiplier = fill.sprite.rect.height / 24f;
        fill.rectTransform.sizeDelta = new Vector2(inset * 2, 0);

        var handleArea = Rect("Handle Area", root);
        Stretch(handleArea, inset, 0, inset, 0);
        var handle = Img("Handle", handleArea, SprCircle, Terracotta);
        handle.rectTransform.sizeDelta = new Vector2(inset * 2, inset * 2);
        handle.raycastTarget = true;

        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.minValue = min; s.maxValue = max; s.value = 1f;
        return s;
    }

    // Mobile canvas references 640x360, so these numbers read 3x larger on a 1080p screen.
    static void RestyleMobile(Canvas mobile)
    {
        if (!mobile) return;
        foreach (var j in mobile.GetComponentsInChildren<MobileJoystick>(true))
        {
            var so = new SerializedObject(j);
            var bg = so.FindProperty("background").objectReferenceValue as RectTransform;
            var knob = so.FindProperty("handle").objectReferenceValue as RectTransform;
            Round(bg, new Color(Parchment.r, Parchment.g, Parchment.b, .55f));
            Round(knob, Terracotta);
            so.FindProperty("handleRange").floatValue = 25f; // keeps the knob on the 100-unit base
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        // action buttons on a grid with a clear gap, right-bottom
        ActionBtn(mobile, "Sprint Btn", "Pictoicon_Shoes", new Vector2(-50, 50));
        ActionBtn(mobile, "Jump Btn", "Pictoicon_Arrow_Up", new Vector2(-125, 50));
        ActionBtn(mobile, "Torch Btn", "Pictoicon_Bulb", new Vector2(-50, 125));
    }

    static void Round(RectTransform r, Color color)
    {
        var img = r ? r.GetComponent<Image>() : null;
        if (!img) return;
        img.sprite = Spr(SprCircle); img.color = color; img.type = Image.Type.Simple; img.preserveAspect = true;
    }

    static void ActionBtn(Canvas mobile, string name, string icon, Vector2 pos)
    {
        var r = mobile.transform.Find(name) as RectTransform;
        if (!r) { Debug.LogError("Missing mobile button: " + name); return; }
        r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = new Vector2(64, 64);
        Round(r, Parchment);
        var child = r.Find("Image")?.GetComponent<Image>();
        if (!child) return;
        child.sprite = Spr(Picto(icon)); child.color = Terracotta; child.preserveAspect = true;
        child.rectTransform.anchorMin = Vector2.zero; child.rectTransform.anchorMax = Vector2.one;
        child.rectTransform.offsetMin = child.rectTransform.offsetMax = Vector2.zero;
        child.rectTransform.sizeDelta = new Vector2(-28, -28);
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
