using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Assets.Scripts.Flight.UI;
using Assets.Scripts.UI;
using ModApi;
using ModApi.Common;
using ModApi.Mods;
using ModApi.Scenes.Events;
using ModApi.Ui;
using UI.Xml;
using UnityEngine;
using Object = System.Object;

namespace Assets.Scripts {
    /// <summary>
    /// A singleton object representing this mod that is instantiated and initialize when the mod is loaded.
    /// </summary>
    public class Mod : GameMod {
        private HeadsUpDisplayScript hudScript;

        /// <summary>
        /// Prevents a default instance of the <see cref="Mod"/> class from being created.
        /// </summary>
        private Mod() : base() {
        }

        /// <summary>
        /// Gets the singleton instance of the mod object.
        /// </summary>
        /// <value>The singleton instance of the mod object.</value>
        public static Mod Instance { get; } = GetModInstance<Mod>();

        /// <summary>
        /// Called when the mod is initialized.
        /// </summary>
        protected override void OnModInitialized() {
            base.OnModInitialized();

            Game.Instance.UserInterface.AddBuildUserInterfaceXmlAction(
                "Ui/Xml/Flight/ViewPanel",
                OnBuildFlightSceneViewPanel
            );

            Game.Instance.UserInterface.AddBuildUserInterfaceXmlAction(
                UserInterfaceIds.Flight.FlightSceneUI,
                OnBuildFlightSceneUI
            );
        }

        private static readonly XNamespace XmlNs = XmlLayoutConstants.XmlNamespace;
        private const String CameraPanelButtonId = "toggle-camera-panel-button";
        private const String ToggleDockingHudButtonId = "toggle-docking-hud-button";

        private void OnBuildFlightSceneViewPanel(BuildUserInterfaceXmlRequest request) {
            Debug.Log("OnBuildFlightSceneViewPanel");

            var cameraPanelButton =
                request.XmlDocument.Descendants(XmlNs + "ContentButton")
                    .FirstOrDefault(n => n.Attribute("id")?.Value == CameraPanelButtonId);

            if (cameraPanelButton != null) {
                cameraPanelButton.AddAfterSelf(
                    XElement.Parse(
                        $"<ContentButton" +
                        $"    name=\"ToggleDockingHUD\"" +
                        $"    id=\"{ToggleDockingHudButtonId}\"" +
                        $"    class=\"view-button audio-btn-click\"" +
                        $"    tooltip=\"Toggle Docking HUD\"" +
                        $"    xmlns=\"{XmlNs}\">" +
                        $"    <Image sprite=\"CrewDragonHUD/Sprites/CrewDragonHUD_icon\" />" +
                        $"</ContentButton>"
                    )
                );
            } else {
                Debug.LogWarning(
                    $"CrewDragonHUD unable to load UI because the '{CameraPanelButtonId}' button was not found."
                );
            }
        }

        private void OnBuildFlightSceneUI(BuildUserInterfaceXmlRequest request) {
            request.AddOnLayoutRebuiltAction(
                xmlLayoutController => {
                    var viewPanel = (XmlElement)xmlLayoutController.XmlLayout.GetElementById("view-panel");
                    var button = FindChildElementById(viewPanel, ToggleDockingHudButtonId);
                    if (button != null) {
                        button.AddOnClickEvent(OnToggleHudButtonClicked);
                    } else {
                        Debug.LogWarning("Unable to register Toggle Docking HUD button click action");
                    }
                });
        }

        private static IXmlElement FindChildElementById(
            XmlElement root,
            String id) {
            var queue = new Queue<XmlElement>(root.childElements);
            while (queue.Count > 0) {
                var element = queue.Dequeue();
                if (element.id == id) {
                    return element;
                }

                foreach (var child in element.childElements) {
                    queue.Enqueue(child);
                }
            }

            return null;
        }

        private void OnToggleHudButtonClicked() {
            if (this.hudScript == null) {
                if (Game.InFlightScene) {
                    hudScript = Game.Instance.UserInterface.BuildUserInterfaceFromResource<HeadsUpDisplayScript>(
                        "CrewDragonHUD/HeadsUpDisplay",
                        (script, controller) => script.OnLayoutRebuilt(controller));

                    Game.Instance.SceneManager.SceneTransitionStarted += SceneManagerOnSceneTransitionStarted;
                }
            } else {
                this.hudScript.Close();
                this.hudScript = null;
                Game.Instance.SceneManager.SceneTransitionStarted -= SceneManagerOnSceneTransitionStarted;
            }
        }

        private void SceneManagerOnSceneTransitionStarted(object sender, SceneTransitionEventArgs e) {
            if (this.hudScript != null) {
                this.hudScript.Close();
                this.hudScript = null;
                Game.Instance.SceneManager.SceneTransitionStarted -= SceneManagerOnSceneTransitionStarted;
            }
        }
    }
}
