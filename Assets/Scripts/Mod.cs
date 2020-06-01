using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Assets.Scripts.Craft;
using Assets.Scripts.Craft.Parts.Modifiers;
using Assets.Scripts.Flight.GameView.Cameras;
using Assets.Scripts.Flight.GameView.UI.Inspector;
using Assets.Scripts.UI;
using ModApi.Craft.Parts;
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

            Game.Instance.SceneManager.SceneLoaded += SceneManagerOnSceneLoaded;
        }

        private void SceneManagerOnSceneLoaded(
            Object sender,
            SceneEventArgs e) {
            if (Game.InFlightScene) {
                // Add docking port cameras
                var craft = Game.Instance.FlightScene.CraftNode.CraftScript;

                foreach (var partData in craft.Data.Assembly.Parts.Where(p => !p.IsDestroyed)) {
                    var part = partData.PartScript;
                    if (part.GetModifier<DockingPortScript>() != null) {
                        var existingScript = part.GetModifier<CameraVantageScript>();
                        if (existingScript == null) {
                            // Automatically add a CameraVantage Part Modifier to all DockingPort parts
                            var cameraData =
                                PartModifierData.CreateFromDefaultXml<CameraVantageData>(part.Data);

                            cameraData.ViewMode = ViewMode.FirstPerson;

                            var cameraVantageScript = (CameraVantageScript)cameraData.CreateScript();
                            var cameraAlignment =
                                cameraVantageScript.gameObject.AddComponent<DockingPortCameraAlignment>();

                            cameraAlignment.Initialize(cameraVantageScript, part);
                        } else if (existingScript.gameObject.GetComponent<DockingPortCameraAlignment>() == null) {
                            var cameraAlignment =
                                existingScript.gameObject.AddComponent<DockingPortCameraAlignment>();

                            cameraAlignment.Initialize(existingScript, part);
                        }
                    }
                }
            }
        }

        private static readonly XNamespace XmlNs = XmlLayoutConstants.XmlNamespace;
        private const String CameraPanelButtonId = "toggle-camera-panel-button";
        private const String ToggleDockingHudButtonId = "toggle-docking-hud-button";

        private void OnBuildFlightSceneViewPanel(BuildUserInterfaceXmlRequest request) {
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
                    hudScript =
                        Game.Instance.UserInterface.BuildUserInterfaceFromResource<HeadsUpDisplayScript>(
                            "CrewDragonHUD/HeadsUpDisplay",
                            (script, controller) => script.OnLayoutRebuilt(controller)
                        );

                    Game.Instance.SceneManager.SceneTransitionStarted +=
                        SceneManagerOnSceneTransitionStarted;
                }
            } else {
                this.hudScript.Close();
                this.hudScript = null;
                Game.Instance.SceneManager.SceneTransitionStarted -=
                    SceneManagerOnSceneTransitionStarted;
            }
        }

        private void SceneManagerOnSceneTransitionStarted(Object sender, SceneTransitionEventArgs e) {
            if (this.hudScript != null) {
                this.hudScript.Close();
                this.hudScript = null;
                Game.Instance.SceneManager.SceneTransitionStarted -=
                    SceneManagerOnSceneTransitionStarted;
            }
        }
    }

    internal class DockingPortCameraAlignment : MonoBehaviour {
        private static readonly PropertyInfo InteractiveCameraController_TouchingProperty =
            typeof(InteractiveCameraController).GetProperty("Touching", BindingFlags.Instance | BindingFlags.NonPublic);

        private CameraVantageScript cameraVantage;
        private IPartScript dockingPort;

        public void Initialize(
            CameraVantageScript cameraScript,
            IPartScript dockingPortScript) {
            this.cameraVantage = cameraScript;
            this.dockingPort = dockingPortScript;
            this.firstUpdate = true;
        }

        private static readonly Vector2 Center = new Vector2(-90, 180);
        private bool firstUpdate;

        public void Update() {
            if (this.cameraVantage.CameraController is FirstPersonCameraController controller) {
                if (this.firstUpdate) {
                    controller.DeltaRotation = Center;
                    this.firstUpdate = false;
                    return;
                }

                var touching = (Boolean)InteractiveCameraController_TouchingProperty.GetValue(controller);
                if (!touching && controller.DeltaRotation != Center) {
                    var rot = controller.DeltaRotation;
                    controller.DeltaRotation =
                        new Vector2(
                            rot.x > -90 ? Math.Max(-90, rot.x - Time.unscaledDeltaTime * 20) : Math.Min(-90, rot.x + Time.unscaledDeltaTime * 20),
                            rot.y > 180 ? Math.Max(180, rot.y - Time.unscaledDeltaTime * 40) : Math.Min(180, rot.y + Time.unscaledDeltaTime * 40)
                        );
                }
            }
        }
    }
}
