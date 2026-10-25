using System;
using System.Linq;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutomateFleet.UI
{
    // Simple message popup used when automation stops
    public static class AlertWindow
    {
        static GameObject root;
        static TMP_Text titleText, bodyText;

        public static void Show(string title, string message)
        {
            try
            {
                if (root == null && !Build())
                    return;
                titleText.text = title.ToUpperInvariant();
                bodyText.text = message;
                root.SetActive(true);
            }
            catch (Exception e)
            {
                Main.Error("Couldn't show alert: " + e);
            }
        }

        static bool Build()
        {
            var fsc = Resources.FindObjectsOfTypeAll<FleetsScreenController>().FirstOrDefault();
            if (fsc == null)
                return false;
            var statsContainer = fsc.designerShipDataHeader.transform.parent.parent.Find("Ship Data Container");

            root = new GameObject("Automate Fleet Alert", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29001;
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

            var blocker = UiKit.NewUIObject("Blocker", root.transform);
            UiKit.Stretch(blocker);
            blocker.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);

            var panel = UiKit.NewUIObject("Panel", root.transform);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 240f);
            var image = panel.AddComponent<Image>();
            var background = statsContainer != null ? statsContainer.GetComponent<Image>() : null;
            if (background != null)
                UiKit.CopyImage(background, image);
            else
                image.color = new Color(0.05f, 0.08f, 0.1f, 0.97f);
            UiKit.Column(panel, 16, 10);

            titleText = UiKit.CloneText(fsc.designerShipDataHeader.gameObject, panel.transform, "Title", "");
            titleText.alignment = TextAlignmentOptions.Center;
            UiKit.Layout(titleText.gameObject, preferredHeight: 28);
            bodyText = UiKit.CloneText(fsc.designerWetMassTabText.gameObject, panel.transform, "Message", "", wrap: true);
            UiKit.Layout(bodyText.gameObject, preferredHeight: 120);

            var row = UiKit.Row(panel.transform, "Buttons", 40);
            row.childAlignment = TextAnchor.MiddleCenter;
            var ok = UiKit.CloneWidget(fsc.designerResetDesignButtonText.transform.parent.gameObject, row.transform, "OK");
            UiKit.SetLabel(ok, "OK");
            UiKit.Layout(ok, preferredWidth: 160, preferredHeight: 38);
            var button = ok.GetComponent<Button>();
            button.interactable = true;
            button.onClick.AddListener(() =>
            {
                AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_Acknowledge");
                root.SetActive(false);
            });

            root.SetActive(false);
            return true;
        }
    }
}
