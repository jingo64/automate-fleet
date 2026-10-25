using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutomateFleet.Automation;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AutomateFleet.UI
{
    // The "Automate Fleet" standing-orders window: patrol body, factions to attack, maximum enemy strength
    public static class OrdersWindow
    {
        class FactionRow
        {
            public TIFactionState faction;
            public Toggle toggle;
        }

        static GameObject root;
        static TMP_Text fleetLabel, strengthHint, messageText;
        static TMP_Dropdown bodyDropdown;
        static Transform factionList;
        static Slider strengthSlider;
        static TMP_InputField strengthInput;
        static GameObject textTemplate, toggleTemplate;
        static readonly List<FactionRow> factionRows = new List<FactionRow>();
        static List<TISpaceBodyState> bodies = new List<TISpaceBodyState>();
        static TISpaceFleetState fleet;
        static bool updating;

        public static void Open(TISpaceFleetState target)
        {
            try
            {
                if (root == null && !Build())
                    return;
                fleet = target;
                Populate();
                root.SetActive(true);
            }
            catch (Exception e)
            {
                Main.Error("Couldn't open the Automate Fleet window: " + e);
            }
        }

        public static void Close()
        {
            if (root != null)
                root.SetActive(false);
            TIInputManager.RestoreKeybindings();
            fleet = null;
        }

        static bool Build()
        {
            var fsc = Resources.FindObjectsOfTypeAll<FleetsScreenController>().FirstOrDefault();
            if (fsc == null)
            {
                Main.Error("Fleets screen not found; can't build the Automate Fleet window");
                return false;
            }
            var statsContainer = fsc.designerShipDataHeader.transform.parent.parent.Find("Ship Data Container");
            var designerRoot = fsc.ShipDesignerCanvas.transform;
            var inputTemplate = designerRoot.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault(i => i.name == "DesignerInputText");
            textTemplate = fsc.designerWetMassTabText.gameObject;
            toggleTemplate = fsc.ShowObsoletePartsToggle.gameObject;

            // Own overlay canvas, scaled like the game's UI
            root = new GameObject("Automate Fleet Window", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below 30000, which is where TMP_Dropdown puts its open list
            canvas.sortingOrder = 29000;
            var gameScaler = fsc.ShipDesignerCanvas.rootCanvas.GetComponent<CanvasScaler>();
            var scaler = root.GetComponent<CanvasScaler>();
            if (gameScaler != null)
            {
                scaler.uiScaleMode = gameScaler.uiScaleMode;
                scaler.referenceResolution = gameScaler.referenceResolution;
                scaler.screenMatchMode = gameScaler.screenMatchMode;
                scaler.matchWidthOrHeight = gameScaler.matchWidthOrHeight;
                scaler.scaleFactor = gameScaler.scaleFactor;
            }

            // Dim and block the screen behind the window
            var blocker = UiKit.NewUIObject("Blocker", root.transform);
            UiKit.Stretch(blocker);
            blocker.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var panel = UiKit.NewUIObject("Panel", root.transform);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 640f);
            var panelImage = panel.AddComponent<Image>();
            var background = statsContainer != null ? statsContainer.GetComponent<Image>() : null;
            if (background != null)
                UiKit.CopyImage(background, panelImage);
            else
                panelImage.color = new Color(0.05f, 0.08f, 0.1f, 0.97f);
            var column = UiKit.Column(panel, 14, 8);
            column.padding.bottom = 22;
            // Grow the panel to fit its contents so the buttons never spill over the edge
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = UiKit.CloneText(fsc.designerShipDataHeader.gameObject, panel.transform, "Title", "AUTOMATE FLEET");
            title.alignment = TextAlignmentOptions.Center;
            UiKit.Layout(title.gameObject, preferredHeight: 28);
            fleetLabel = UiKit.CloneText(textTemplate, panel.transform, "Fleet", "");
            fleetLabel.alignment = TextAlignmentOptions.Center;
            UiKit.Layout(fleetLabel.gameObject, preferredHeight: 22);

            // Patrol area
            Section(panel.transform, "Patrol area", "Every orbit around this body is watched.");
            bodyDropdown = UiKit.CloneWidget(fsc.roleSelectionDropdown.gameObject, panel.transform, "Body").GetComponent<TMP_Dropdown>();
            bodyDropdown.interactable = true;
            UiKit.Layout(bodyDropdown.gameObject, preferredHeight: 28);

            // Factions
            Section(panel.transform, "Attack these factions", "Factions you're at war with are ticked to start with.");
            var listObject = UiKit.NewUIObject("Factions", panel.transform);
            UiKit.Column(listObject, 0, 2);
            UiKit.Layout(listObject, preferredHeight: 190);
            factionList = listObject.transform;

            // Strength threshold
            Section(panel.transform, "Max enemy strength", "Enemy fleets above this combat strength are not attacked; if one heads here, the game pauses and automation stops.");
            var strengthRow = UiKit.Row(panel.transform, "Strength", 26);
            strengthSlider = UiKit.CreateSlider(strengthRow.transform);
            UiKit.Layout(strengthSlider.gameObject, flexibleWidth: 1, preferredHeight: 18);
            var box = UiKit.NewUIObject("Amount Box", strengthRow.transform);
            UiKit.Layout(box, preferredWidth: 90, preferredHeight: 22);
            var inputBackground = inputTemplate?.transform.parent.Find("Input Background")?.GetComponent<Image>();
            if (inputBackground != null)
                UiKit.CopyImage(inputBackground, box.AddComponent<Image>());
            strengthInput = UiKit.CloneWidget(inputTemplate.gameObject, box.transform, "Amount").GetComponent<TMP_InputField>();
            UiKit.Stretch(strengthInput.gameObject);
            strengthInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            strengthInput.characterLimit = 9;
            strengthInput.interactable = true;
            strengthHint = UiKit.CloneText(textTemplate, panel.transform, "Strength Hint", "");
            UiKit.Layout(strengthHint.gameObject, preferredHeight: 20);

            strengthSlider.onValueChanged.AddListener(v =>
            {
                if (!updating)
                    strengthInput.SetTextWithoutNotify(((int)v).ToString(CultureInfo.InvariantCulture));
            });
            strengthInput.onSelect.AddListener(_ => TIInputManager.BlockKeybindings());
            strengthInput.onDeselect.AddListener(_ => TIInputManager.RestoreKeybindings());
            // Typing a number moves the slider, stretching its range if the number is past the end
            strengthInput.onValueChanged.AddListener(text =>
            {
                if (!updating && TryReadStrength(out var v))
                    SetStrengthSlider(v);
            });
            strengthInput.onEndEdit.AddListener(_ =>
            {
                var v = TryReadStrength(out var typed) ? typed : (int)strengthSlider.value;
                SetStrengthSlider(v);
                updating = true;
                strengthInput.SetTextWithoutNotify(v.ToString(CultureInfo.InvariantCulture));
                updating = false;
            });

            messageText = UiKit.CloneText(textTemplate, panel.transform, "Message", "", wrap: true);
            messageText.color = TIUtilities.UIRedTextColor;
            UiKit.Layout(messageText.gameObject, preferredHeight: 36);

            // Buttons
            var buttonTemplate = fsc.designerResetDesignButtonText.transform.parent.gameObject;
            var footer = UiKit.Row(panel.transform, "Buttons", 40, 10);
            footer.childAlignment = TextAnchor.MiddleCenter;
            var cancel = UiKit.CloneWidget(buttonTemplate, footer.transform, "Cancel");
            UiKit.SetLabel(cancel, "Cancel");
            UiKit.Layout(cancel, preferredWidth: 200, preferredHeight: 38);
            cancel.GetComponent<Button>().onClick.AddListener(Close);
            var start = UiKit.CloneWidget(buttonTemplate, footer.transform, "Start");
            UiKit.SetLabel(start, "Start automation");
            UiKit.Layout(start, preferredWidth: 200, preferredHeight: 38);
            start.GetComponent<Button>().onClick.AddListener(OnStart);
            foreach (var b in new[] { cancel, start })
                b.GetComponent<Button>().interactable = true;

            root.SetActive(false);
            return true;
        }

        static bool TryReadStrength(out int value) =>
            int.TryParse(strengthInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;

        static void SetStrengthSlider(int value)
        {
            updating = true;
            if (value > strengthSlider.maxValue)
                strengthSlider.maxValue = value;
            strengthSlider.SetValueWithoutNotify(value);
            updating = false;
        }

        static void Section(Transform parent, string heading, string hint)
        {
            var h = UiKit.CloneText(textTemplate, parent, heading, heading.ToUpperInvariant());
            h.fontStyle |= FontStyles.Bold;
            UiKit.Layout(h.gameObject, preferredHeight: 22);
            var t = UiKit.CloneText(textTemplate, parent, heading + " Hint", hint, wrap: true);
            t.fontSize *= 0.85f;
            t.alpha = 0.75f;
            UiKit.Layout(t.gameObject, preferredHeight: hint.Length > 70 ? 34 : 18);
        }

        static void Populate()
        {
            var player = fleet.faction;
            var existing = FleetOrders.Get(fleet);
            fleetLabel.text = fleet.displayName;
            messageText.text = "";

            // The fleet's local system: the sun-orbiting body plus its moons (an asteroid on its own is just itself)
            var here = fleet.ref_spaceBody ?? fleet.trajectory?.destination?.ref_spaceBody ?? existing?.body;
            var system = here?.ref_system ?? here;
            bodies = GameStateManager.AllSpaceBodies()
                .Where(b => b != null && b.orbits != null && b.orbits.Count > 0 && (b == system || b.ref_system == system))
                .OrderBy(b => b == system ? 0 : 1)
                .ThenBy(b => b.displayName)
                .ToList();
            if (existing?.body != null && !bodies.Contains(existing.body))
                bodies.Insert(0, existing.body);
            bodyDropdown.ClearOptions();
            bodyDropdown.AddOptions(bodies.Select(b => b.displayName).ToList());
            var selected = existing?.body ?? here;
            bodyDropdown.SetValueWithoutNotify(Math.Max(0, bodies.IndexOf(selected)));
            bodyDropdown.RefreshShownValue();

            foreach (Transform child in factionList)
                Object.Destroy(child.gameObject);
            factionRows.Clear();
            foreach (var faction in GameStateManager.AllFactions().Where(f => f != player).OrderBy(f => f.displayName))
            {
                var atWar = FleetOrders.AtWar(player, faction);
                var status = atWar ? "at war" : faction.permanentAlly(player) ? "allied" : "not at war";
                var toggle = UiKit.CloneWidget(toggleTemplate, factionList, faction.displayName).GetComponent<Toggle>();
                UiKit.SetLabel(toggle.gameObject, $"{faction.displayName}  <alpha=#99>({status})");
                UiKit.Layout(toggle.gameObject, preferredHeight: 24);
                toggle.interactable = true;
                toggle.SetIsOnWithoutNotify(existing != null ? existing.targets.Contains(faction) : atWar);
                factionRows.Add(new FactionRow { faction = faction, toggle = toggle });
            }

            var own = Mathf.Max(0f, fleet.SpaceCombatValue());
            var current = existing?.maxEnemyStrength ?? own;
            updating = true;
            // Alien fleet strengths are often overstated, so leave room for fights well above our own
            strengthSlider.maxValue = Mathf.Max(1f, own * 5f, current);
            strengthSlider.SetValueWithoutNotify(current);
            strengthInput.SetTextWithoutNotify(((int)current).ToString(CultureInfo.InvariantCulture));
            updating = false;
            strengthHint.text = $"This fleet's combat strength: {own:N0}";
        }

        static void OnStart()
        {
            if (fleet == null)
            {
                Close();
                return;
            }
            var targets = factionRows.Where(r => r.toggle.isOn).Select(r => r.faction).ToList();
            if (targets.Count == 0)
            {
                messageText.text = "Tick at least one faction to attack.";
                return;
            }
            if (bodies.Count == 0)
            {
                messageText.text = "No body selected.";
                return;
            }
            FleetOrders.Set(new Orders
            {
                fleet = fleet,
                body = bodies[Mathf.Clamp(bodyDropdown.value, 0, bodies.Count - 1)],
                targets = new HashSet<TIFactionState>(targets),
                maxEnemyStrength = TryReadStrength(out var typed) ? typed : strengthSlider.value,
            });
            Close();
        }
    }
}
