using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Craft.Parts;
using Assets.Scripts.Craft.Parts.Modifiers;
using Assets.Scripts.Helpers;
using ModApi.Craft;
using ModApi.Craft.Parts;
using ModApi.Ui;
using UI.Xml;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI {
    public class HeadsUpDisplayScript : MonoBehaviour {
        private static readonly Material GuideMaterial =
            ModApi.Common.Game.Instance.UserInterface.ResourceDatabase.GetResource<Material>("CrewDragonHUD/Materials/GuideMaterial");

        private static readonly Mesh GuideMesh = MeshCreator.CreateTorus();

        private Boolean _isInitialized;
        private IXmlLayoutController _controller;

        private IXmlElement _rollRateBarPositive;
        private IXmlElement _rollRateBarNegative;
        private IXmlElement _pitchRateBarPositive;
        private IXmlElement _pitchRateBarNegative;
        private IXmlElement _yawRateBarPositive;
        private IXmlElement _yawRateBarNegative;

        private Text _rollAngleText;
        private Text _rollRateText;
        private Text _pitchAngleText;
        private Text _pitchRateText;
        private Text _yawAngleText;
        private Text _yawRateText;

        private Text _rangeText;
        private Text _rateOfApproachText;

        private Text _targetOffsetX;
        private Text _targetOffsetY;
        private Text _targetOffsetZ;

        private Text _translationRateX; // Left/Right
        private Text _translationRateY; // Up/Down

        private Text _translationRateZ; // Forward/Back
        // private Text _debug1;
        // private Text _debug2;
        // private Text _debug3;

        private const Double RateBarMaxAngle = 38;
        private const Double RollRateBarPositiveZeroAngle = RateBarMaxAngle;
        private const Double RollRateBarNegativeZeroAngle = 0 - RateBarMaxAngle;
        private const Double PitchRateBarPositiveZeroAngle = -90 - RateBarMaxAngle;
        private const Double PitchRateBarNegativeZeroAngle = -90 + RateBarMaxAngle;
        private const Double YawRateBarPositiveZeroAngle = -180 - RateBarMaxAngle;
        private const Double YawRateBarNegativeZeroAngle = -180 + RateBarMaxAngle;

        private GameObject targetPositionMarker;
        private IList<GameObject> targetPositionMarkerChildren;
        private Material targetGuideMaterial;
        private GameObject dockingPortMarker;

        public void OnLayoutRebuilt(IXmlLayoutController xmlLayoutController) {
            this._controller = xmlLayoutController;

            this._rollRateBarPositive =
                this._controller.XmlLayout.GetElementById("positiveRollRateBar");
            this._rollRateBarNegative =
                this._controller.XmlLayout.GetElementById("negativeRollRateBar");
            this._pitchRateBarPositive =
                this._controller.XmlLayout.GetElementById("positivePitchRateBar");
            this._pitchRateBarNegative =
                this._controller.XmlLayout.GetElementById("negativePitchRateBar");
            this._yawRateBarPositive =
                this._controller.XmlLayout.GetElementById("positiveYawRateBar");
            this._yawRateBarNegative =
                this._controller.XmlLayout.GetElementById("negativeYawRateBar");

            this._rollAngleText = this.GetTextElementById("rollAngle");
            this._rollRateText = this.GetTextElementById("rollRate");
            this._pitchAngleText = this.GetTextElementById("pitchAngle");
            this._pitchRateText = this.GetTextElementById("pitchRate");
            this._yawAngleText = this.GetTextElementById("yawAngle");
            this._yawRateText = this.GetTextElementById("yawRate");

            this._rangeText = this.GetTextElementById("range");
            this._rateOfApproachText = this.GetTextElementById("rateOfApproach");

            this._targetOffsetX = this.GetTextElementById("offsetX");
            this._targetOffsetY = this.GetTextElementById("offsetY");
            this._targetOffsetZ = this.GetTextElementById("offsetZ");
            this._translationRateX = this.GetTextElementById("translationRateX");
            this._translationRateY = this.GetTextElementById("translationRateY");
            this._translationRateZ = this.GetTextElementById("translationRateZ");
            // this._debug1 = this.GetTextElementById("debug1");
            // this._debug2 = this.GetTextElementById("debug2");
            // this._debug3 = this.GetTextElementById("debug3");

            this.ClearText();
            this.SetRotationRateBars(new Vector3d(0, 0, 0));

            foreach (var layoutRoot in ((XmlLayout)this._controller.XmlLayout).GetElementsByClass("layoutRoot")) {
                layoutRoot.SetActive(true);
            }

            this._isInitialized = true;
        }

        public void Update() {
            if (Game.InFlightScene && this._isInitialized) {
                var craft = Game.Instance.FlightScene.CraftNode.CraftScript;
                var angularVelocity = craft.FlightData.AngularVelocity;
                // TODO: subtract the angular velocity of a target

                this._pitchRateText.text = $"{RadiansToDegrees(angularVelocity.x):F2} °/s";
                this._yawRateText.text = $"{RadiansToDegrees(angularVelocity.y):F2} °/s";
                this._rollRateText.text = $"{RadiansToDegrees(angularVelocity.z):F2} °/s";

                this.SetRotationRateBars(angularVelocity);

                var target = craft.FlightData.NavSphereTarget;
                if (target != null && target.Parent == craft.CraftNode.Parent) {
                    var relativeTargetPosition =
                        target.Position - craft.CraftNode.Position;

                    var relativeVelocity = target.Velocity - craft.CraftNode.Velocity;
                    var velocityTowardsTarget = Vector3d.Project(relativeVelocity, relativeTargetPosition);

                    this._translationRateX.text = $"{(Vector3d.Dot(relativeVelocity, craft.FlightData.CraftRight) * -1):F2} m/s";
                    this._translationRateY.text = $"{(Vector3d.Dot(relativeVelocity, craft.FlightData.CraftUp) * -1):F2} m/s";
                    this._translationRateZ.text = $"{Vector3d.Dot(relativeVelocity, craft.FlightData.CraftForward):F2} m/s";

                    var approachRate =
                        velocityTowardsTarget.magnitude *
                        (Vector3d.Angle(relativeTargetPosition, velocityTowardsTarget) > 90 ? -1 : 1);
                    this._rateOfApproachText.text = $"{approachRate:F2} m/s";

                    var localRelativeTargetPosition =
                        craft.CenterOfMass.InverseTransformDirection(craft.ReferenceFrame.PlanetToFrameVector(relativeTargetPosition));

                    var (dockingPort, dockingPortLocalPosition) =
                        FindClosestDockingPort(craft, localRelativeTargetPosition);

                    if (dockingPort != null) {
                        localRelativeTargetPosition -= dockingPortLocalPosition;
                    }

                    this._targetOffsetX.text = $"{(localRelativeTargetPosition.x * -1):F2} m";
                    this._targetOffsetY.text = $"{(localRelativeTargetPosition.y * -1):F2} m";
                    this._targetOffsetZ.text = $"{localRelativeTargetPosition.z:F2} m";

                    this._rangeText.text = $"{localRelativeTargetPosition.magnitude:F1} m";

                    if (target is PartScript targetPart &&
                        targetPart.GetModifier<DockingPortScript>() != null) {
                        var targetOrientationVector =
                            (craft.ReferenceFrame.FrameToPlanetRotation(targetPart.Transform.rotation) * new Vector3(0, 1, 0) * -1).ToVector3();
                        var craftOrientationVector =
                            dockingPort != null ?
                                (craft.ReferenceFrame.FrameToPlanetRotation(dockingPort.Transform.rotation) * new Vector3(0, 1, 0)).ToVector3() :
                                craft.FlightData.CraftForward.ToVector3();

                        var yawAxis = craft.FlightData.CraftUp.ToVector3();
                        var pitchAxis = craft.FlightData.CraftRight.ToVector3();
                        var rollAxis = craft.FlightData.CraftForward.ToVector3();

                        var yawOffset = Vector3.Angle(yawAxis, craftOrientationVector);
                        if (yawOffset > 30 && yawOffset < 150) {
                            var yawAngle = -1 * Vector3.SignedAngle(
                                Vector3.ProjectOnPlane(craftOrientationVector, yawAxis),
                                Vector3.ProjectOnPlane(targetOrientationVector, yawAxis),
                                yawAxis
                            );
                            this._yawAngleText.text = $"{yawAngle:F1} °";
                        } else {
                            this._yawAngleText.text = "";
                        }

                        var pitchOffset = Vector3.Angle(pitchAxis, craftOrientationVector);
                        if (pitchOffset > 30 && pitchOffset < 150) {
                            var pitchAngle = Vector3.SignedAngle(
                                Vector3.ProjectOnPlane(craftOrientationVector, pitchAxis),
                                Vector3.ProjectOnPlane(targetOrientationVector, pitchAxis),
                                pitchAxis
                            );
                            this._pitchAngleText.text = $"{pitchAngle:F1} °";
                        } else {
                            this._pitchAngleText.text = "";
                        }

                        var rollOffset = Vector3.Angle(rollAxis, craftOrientationVector);
                        if (rollOffset > 30 && rollOffset < 150) {
                            var rollAngle = Vector3.SignedAngle(
                                Vector3.ProjectOnPlane(craftOrientationVector, rollAxis),
                                Vector3.ProjectOnPlane(targetOrientationVector, rollAxis),
                                rollAxis
                            );
                            this._rollAngleText.text = $"{rollAngle:F1} °";
                        } else {
                            this._rollAngleText.text = "";
                        }

                        if (this.targetPositionMarker == null) {
                            this.targetGuideMaterial = this.targetGuideMaterial ?? new Material(GuideMaterial) {
                                color = new Color(0, 1, 0, 0.4f)
                            };
                            this.targetPositionMarker = new GameObject();
                            // this.targetPositionMarker.transform.localScale = new Vector3(1, 1, 1);
                            // this.targetPositionMarker.transform.localPosition = new Vector3(0, 0, 1);
                            var children = new List<GameObject>();
                            for (var i = 1; i <= 5; i++) {
                                var childTargetGuide = CreateTargetPortGuide(MeshCreator.CreateTorus(1, 0.1f / i), this.targetGuideMaterial);
                                childTargetGuide.transform.parent = this.targetPositionMarker.transform;
                                childTargetGuide.transform.position = new Vector3(0, i * 5, 0);
                                childTargetGuide.transform.localScale = new Vector3(i, i, i);
                                children.Add(childTargetGuide);
                                this.targetPositionMarkerChildren = children;
                            }
                        }

                        var targetRelativeOffset =
                            targetPart.Transform.InverseTransformDirection(
                                targetPart.Transform.position -
                                (dockingPort?.Transform.position ?? craft.CenterOfMass.position)
                            );

                        var translationError =
                            (Single)Math.Min(
                                1.0,
                                Math.Sqrt(Math.Pow(targetRelativeOffset.x, 2) + Math.Pow(targetRelativeOffset.z, 2)) /
                                Math.Abs(targetRelativeOffset.y / 5)
                            );
                        this.targetGuideMaterial.color =
                            new Color(
                                translationError < 0.5f ? translationError * 2 : 1,
                                translationError > 0.5f ? (1.0f - translationError) * 2 : 1,
                                0,
                                0.4f
                            );

                        if (dockingPort != null && this.dockingPortMarker == null) {
                            this.dockingPortMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            Destroy(this.dockingPortMarker.GetComponent<Collider>());
                            this.dockingPortMarker.GetComponent<Renderer>().material =
                                new Material(GuideMaterial) {
                                    color = new Color(1, 0, 0, 0.4f)
                                };
                            this.dockingPortMarker.transform.localScale = new Vector3(0.05f, 4, 0.05f);
                        }

                        this.targetPositionMarker.transform.position = craft.ReferenceFrame.PlanetToFramePosition(target.Position);
                        this.targetPositionMarker.transform.localRotation = targetPart.Transform.rotation;
                        if (dockingPort != null) {
                            this.dockingPortMarker.transform.position =
                                dockingPort.Transform.position + (this.dockingPortMarker.transform.rotation * new Vector3(0, 4, 0));
                            this.dockingPortMarker.transform.rotation = dockingPort.Transform.rotation;
                        }
                    } else {
                        this._pitchAngleText.text = "";
                        this._yawAngleText.text = "";
                    }
                } else {
                    this.DestroyGuides();

                    this.ClearText();
                }
            }
        }

        private void DestroyGuides() {
            if (this.targetPositionMarker != null) {
                foreach (var child in this.targetPositionMarkerChildren) {
                    Destroy(child);
                }

                this.targetPositionMarkerChildren = null;

                Destroy(this.targetPositionMarker);
                this.targetPositionMarker = null;
            }

            if (this.dockingPortMarker != null) {
                Destroy(this.dockingPortMarker);
                this.dockingPortMarker = null;
            }
        }

        private void SetRotationRateBars(Vector3d angularVelocity) {
            // Positive roll rate bar: 0 = 38, >45 = 0
            this._rollRateBarPositive.RectTransform.rotation =
                Quaternion.Euler(
                    0,
                    0,
                    (Single)Math.Max(
                        0,
                        RollRateBarPositiveZeroAngle - (RadiansToDegrees(angularVelocity.z) / 45) * RateBarMaxAngle
                    )
                );

            // Negative roll rate: 0 = -38, <-45 = 0
            this._rollRateBarNegative.RectTransform.rotation =
                Quaternion.Euler(
                    0,
                    0,
                    (Single)Math.Min(
                        0,
                        RollRateBarNegativeZeroAngle - (RadiansToDegrees(angularVelocity.z) / 45) * RateBarMaxAngle
                    )
                );

            // Positive pitch rate bar: 0 = -128, >45 = -90
            this._pitchRateBarPositive.RectTransform.rotation =
                Quaternion.Euler(
                    0,
                    0,
                    (Single)Math.Min(
                        -90,
                        PitchRateBarPositiveZeroAngle +
                        (RadiansToDegrees(angularVelocity.x) / 15) * RateBarMaxAngle
                    )
                );

            // Negative pitch rate: 0 = -42, <-45 = -90
            this._pitchRateBarNegative.RectTransform.rotation =
                Quaternion.Euler(
                    0,
                    0,
                    (Single)Math.Max(
                        -90,
                        PitchRateBarNegativeZeroAngle +
                        (RadiansToDegrees(angularVelocity.x) / 15) * RateBarMaxAngle
                    )
                );

            // Positive yaw rate bar: 0 = -218, >45 = -180
            this._yawRateBarPositive.RectTransform.rotation =
                Quaternion.Euler(
                    0,
                    0,
                    (Single)Math.Min(
                        -180,
                        YawRateBarPositiveZeroAngle +
                        (RadiansToDegrees(angularVelocity.y) / 45) * RateBarMaxAngle
                    )
                );

            // Negative roll rate: 0 = -166, <-45 = -180
            this._yawRateBarNegative.RectTransform.rotation =
                Quaternion.Euler(
                    0,
                    0,
                    (Single)Math.Max(
                        -180,
                        YawRateBarNegativeZeroAngle +
                        (RadiansToDegrees(angularVelocity.y) / 45) * RateBarMaxAngle
                    )
                );
        }

        private void ClearText() {
            this._targetOffsetX.text = "";
            this._targetOffsetY.text = "";
            this._targetOffsetZ.text = "";

            this._translationRateX.text = "";
            this._translationRateY.text = "";
            this._translationRateZ.text = "";

            this._rateOfApproachText.text = "";
            this._rangeText.text = "";

            this._rollAngleText.text = "";
            this._pitchAngleText.text = "";
            this._yawAngleText.text = "";
        }

        private static GameObject CreateTargetPortGuide(Mesh mesh, Material material) {
            var guide = new GameObject();
            var meshFilter = guide.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            var render = guide.AddComponent<MeshRenderer>();
            render.material = material;
            return guide;
        }

        // Convert an angle from 0 to 360 to -180 to 180
        private static Single ToOffsetAngle(Single angle) {
            return angle > 180 ? angle - 360 : angle;
        }

        private static (IPartScript part, Vector3 localPosition) FindClosestDockingPort(
            ICraftScript craft,
            Vector3 targetLocalPosition) {

            IPartScript closestPort = null;
            var closestDistance = 0d;
            Vector3 closestLocalPosition = default(Vector3);

            foreach (var partData in craft.Data.Assembly.Parts.Where(p => !p.IsDestroyed)) {
                var part = partData.PartScript;
                if (part.GetModifier<DockingPortScript>() != null) {
                    var partRelativePosition = part.Transform.position - craft.CenterOfMass.position;
                    var partLocalPosition =
                        craft.CenterOfMass.InverseTransformDirection(partRelativePosition);
                    var distance = (targetLocalPosition - partLocalPosition).magnitude;
                    if (closestPort == null || distance < closestDistance) {
                        closestPort = part;
                        closestDistance = distance;
                        closestLocalPosition = partLocalPosition;
                    }
                }
            }

            return (closestPort, closestLocalPosition);
        }

        public void Close() {
            this.DestroyGuides();
            this._controller.XmlLayout.Hide(() => Destroy(this.gameObject), true);
        }

        private Text GetTextElementById(String id) {
            return this._controller.XmlLayout.GetElementById(id).GameObject.GetComponent<Text>();
        }

        private static Double RadiansToDegrees(Double angle) {
            return (angle / Math.PI) * 180;
        }
    }
}
