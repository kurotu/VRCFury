using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Menu;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Builder.Haptics {
    public class HapticUtils {
        
        // Bump when pen senders or receivers are changed
        public static int penVersion = 8;
        
        // Bump when orf senders or receivers are changed
        public static int orfVersion = 9;
        
        // Bump when any senders are changed
        public static int beaconVersion = 6;
        
        public static string CONTACT_PEN_MAIN = "TPS_Pen_Penetrating";
        public static string CONTACT_PEN_WIDTH = "TPS_Pen_Width";
        public static string CONTACT_PEN_CLOSE = "TPS_Pen_Close";
        public static string CONTACT_PEN_ROOT = "TPS_Pen_Root";
        public static string CONTACT_ORF_MAIN = "TPS_Orf_Root";
        public static string CONTACT_ORF_NORM = "TPS_Orf_Norm";
        
        public static readonly string[] SelfContacts = {
            "Hand",
            "Finger",
            "Foot"
        };
        public static readonly string[] BodyContacts = {
            "Head",
            "Hand",
            "Foot",
            "Finger"
        };
        
        private static readonly System.Random rand = new System.Random();

        public static string RandomTag() {
            return "TPSVF_" + rand.Next(100_000_000, 999_999_999);
        }
        
        public static void AddSender(
            Transform obj,
            Vector3 pos,
            String objName,
            float radius,
            string tag,
            float height = 0,
            Quaternion rotation = default,
            bool worldScale = true
        ) {
            var child = GameObjects.Create(objName, obj);
            var sender = GameObjects.AddComponent<VRCContactSender>(child);
            sender.position = pos;
            sender.radius = radius;
            sender.collisionTags = new List<string> { tag };
            if (height > 0) {
                sender.shapeType = ContactBase.ShapeType.Capsule;
                sender.height = height;
                sender.rotation = rotation;
            }
            if (worldScale) {
                sender.position /= child.lossyScale.x;
                sender.radius /= child.lossyScale.x;
                sender.height /= child.lossyScale.x;
            }
        }

        public static void AddVersionContacts(Transform obj, string paramPrefix, bool baked, bool isPen) {
            var versionLocal = GameObjects.Create("VersionLocal", obj);
            // Version Local
            var varName = baked ? "BakedVersion" : "Version";
            var versionLocalTag = RandomTag();
            AddSender(versionLocal, Vector3.zero, "Sender", 0.01f, versionLocalTag);
            // The "TPS_" + versionTag one is there so that the TPS wizard will delete this version flag if someone runs it
            var versionLocalNum = isPen ? penVersion : orfVersion;
            AddReceiver(versionLocal, Vector3.one * 0.01f, paramPrefix + "/" + varName + "/" + versionLocalNum, "Receiver", 0.01f, new []{versionLocalTag, "TPS_" + RandomTag()}, allowOthers:false, localOnly:true);

            // Version Remote
            var versionBeaconTag = "OGB_VERSION_" + beaconVersion;
            AddSender(obj, Vector3.zero, "VersionBeacon", 1f, versionBeaconTag);
            if (!baked) {
                AddReceiver(versionLocal, Vector3.zero, paramPrefix + "/VersionMatch", "BeaconReceiver", 1f,
                    new[] { versionBeaconTag, "TPS_" + RandomTag() }, allowSelf: false, localOnly: true);
            }
        }

        public static GameObject AddReceiver(
            Transform obj,
            Vector3 pos,
            String param,
            String objName,
            float radius,
            string[] tags,
            bool allowOthers = true,
            bool allowSelf = true,
            bool localOnly = false,
            float height = 0,
            Quaternion rotation = default,
            ContactReceiver.ReceiverType type = ContactReceiver.ReceiverType.Proximity,
            bool worldScale = true
        ) {
            var child = GameObjects.Create(objName, obj);
            var receiver = GameObjects.AddComponent<VRCContactReceiver>(child);
            receiver.position = pos;
            receiver.parameter = param;
            receiver.radius = radius;
            receiver.receiverType = type;
            receiver.collisionTags = new List<string>(tags);
            receiver.allowOthers = allowOthers;
            receiver.allowSelf = allowSelf;
            receiver.localOnly = localOnly;
            if (height > 0) {
                receiver.shapeType = ContactBase.ShapeType.Capsule;
                receiver.height = height;
                receiver.rotation = rotation;
            }
            if (worldScale) {
                receiver.position /= child.lossyScale.x;
                receiver.radius /= child.lossyScale.x;
                receiver.height /= child.lossyScale.x;
            }
            return child.gameObject;
        }

        public static void RemoveTPSSenders(Transform obj) {
            var remove = new List<UnityEngine.Component>();
            foreach (Transform child in obj) {
                foreach (var sender in child.gameObject.GetComponents<VRCContactSender>()) {
                    if (IsTPSSender(sender)) {
                        Debug.Log("Deleting TPS sender on " + sender.gameObject);
                        remove.Add(sender);
                    }
                }
            }

            foreach (var c in remove) {
                AvatarCleaner.RemoveComponent(c);
            }

            if (obj.parent) {
                RemoveTPSSenders(obj.parent);
            }
        }

        public static bool IsTPSSender(VRCContactSender c) {
            if (c.collisionTags.Any(t => t == CONTACT_PEN_MAIN)) return true;
            if (c.collisionTags.Any(t => t == CONTACT_PEN_WIDTH)) return true;
            if (c.collisionTags.Any(t => t == CONTACT_ORF_MAIN)) return true;
            if (c.collisionTags.Any(t => t == CONTACT_ORF_NORM)) return true;
            return false;
        }

        public static string GetNextName(List<string> usedNames, string prefix) {
            for (int i = 0; ; i++) {
                var next = prefix + (i == 0 ? "" : i+"");
                if (!usedNames.Contains(next)) {
                    usedNames.Add(next);
                    return next;
                }
            }
        }

        private static bool IsZeroScale(Transform obj) {
            var scale = obj.localScale;
            return scale.x == 0 || scale.y == 0 || scale.z == 0;
        }
        private static bool IsNegativeScale(Transform obj) {
            var scale = obj.localScale;
            return scale.x < 0 || scale.y < 0 || scale.z < 0;
        }
        private static bool IsNonUniformScale(Transform obj) {
            var scale = obj.localScale;
            return Math.Abs(scale.x - scale.y) / scale.x > 0.05
                   || Math.Abs(scale.x - scale.z) / scale.x > 0.05;
        }
        public static void AssertValidScale(Transform obj, string type) {
            var path = AnimationUtility.CalculateTransformPath(obj, obj.root);

            var current = obj;
            while (true) {
                if (IsZeroScale(current)) {
                    throw new Exception(
                        "A haptic component exists on an object with zero scale." +
                        " This object must not be zero scale or size calculation will fail.\n\n" +
                        "Component path: " + path + "\n" +
                        "Offending object: " + GameObjects.GetPath(current));
                }
                if (IsNegativeScale(current)) {
                    throw new Exception(
                        "A haptic component exists on an object with negative scale." +
                        " This object must have a positive scale or size calculation will fail.\n\n" +
                        "Component path: " + path + "\n" +
                        "Offending object: " + GameObjects.GetPath(current));
                }
                if (IsNonUniformScale(current)) {
                    var bypass = obj.Find("ItsOkayThatOgbMightBeBroken") != null;
                    if (!bypass) {
                        throw new Exception(
                            "A haptic component exists on an object with a non-uniform scale." +
                            " This object (and all parents) must have an X, Y, and Z scale value that match" +
                            " each other, or size calculation will fail.\n\n" +
                            "Component path: " + path + "\n" +
                            "Offending object: " + GameObjects.GetPath(current));
                    }
                }

                var parent = current.parent;
                if (parent == null) break;
                current = parent;
            }
        }

        public static Transform GetMeshRoot(Renderer r) {
            if (r is SkinnedMeshRenderer skin && skin.rootBone) {
                return skin.rootBone;
            }
            return r.transform;
        }
    }
}
