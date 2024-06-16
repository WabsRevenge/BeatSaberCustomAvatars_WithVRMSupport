//#define USE_VRM_10
using AssetBundleLoadingTools.Utilities;
using CustomAvatar;
using CustomAvatar.Avatar;
using CustomAvatar.Logging;
using CustomAvatar.Player;
using HarmonyLib;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UniGLTF;
using UnityEngine;
#if USE_VRM_10
using UniVRM10;
#endif
using VRM;
using Zenject;

namespace VRMAvatar
{
    public class VRMHandPositionConstants
    {
        public VRMHandPositionConstants()
        {
        }
        public struct Finger
        {
            public Quaternion knuckleOne;
            public Quaternion knuckleTwo;
            public Quaternion knuckleThree;

            public Finger(Quaternion knuckleOne, Quaternion knuckleTwo, Quaternion knuckleThree)
            {
                this.knuckleOne = knuckleOne;
                this.knuckleTwo = knuckleTwo;
                this.knuckleThree = knuckleThree;
            }
        }

        public static Finger index = new(
                                                new Quaternion(0.008440408f, -0.001334298f, 0.2419432f, 0.9702529f),
                                                new Quaternion(0.006469311f, -0.005582907f, 0.7170327f, 0.6969872f),
                                                new Quaternion(0.005353101f, -0.006660718f, 0.8312484f, 0.5558357f)
                                                );
        public static Finger little = new(
                                                new Quaternion(0.008255607f, -0.00205866f, 0.241947f, 0.9702522f),
                                                new Quaternion(0.005930441f, -0.006101066f, 0.7170366f, 0.6969836f),
                                                new Quaternion(0.004364784f, -0.007303547f, 0.8583599f, 0.5129775f)
                                                );
        public static Finger middle = new(
                                                new Quaternion(0.008255607f, -0.00205866f, 0.241947f, 0.9702522f),
                                                new Quaternion(0.005930441f, -0.006101066f, 0.7170366f, 0.6969836f),
                                                new Quaternion(0.001539939f, -0.0083679f, 0.98344f, 0.1809834f)
                                                );
        public static Finger ring = new(
                                                new Quaternion(0.008255607f, -0.00205866f, 0.241947f, 0.9702522f),
                                                new Quaternion(0.005930441f, -0.006101066f, 0.7170366f, 0.6969836f),
                                                new Quaternion(0.002387176f, -0.008166672f, 0.9597998f, 0.2805563f)
                                                );
        public static Finger thumb = new(
                                                new Quaternion(0.5142931f, -0.0385387f, -0.02314265f, 0.8564355f),
                                                new Quaternion(0.502104f, -0.1893172f, -0.1136858f, 0.8361376f),
                                                new Quaternion(0.4902184f, -0.2618173f, -0.1572224f, 0.8163448f)
                                                );
        private static Quaternion Reflect(Quaternion quat, bool bReflect)
        {
            if (!bReflect)
            {
                return quat;
            }
            else
            {
                Quaternion temp = quat;
                temp.z = -temp.z;
                temp.y = -temp.y;
                return temp;
            }
        }

        public Vector3 ApplyToHand(Transform hand, bool bRightHand)
        {
            var offsetBetweenWristAndGrip = new Vector3();
            if (hand == null)
                return new Vector3(); //nothing can be done.

            //NOTE: Hand rotation can't be done here as the Tracking Device is mapped directly to the hand, overwriting rotations.

            int nFingers = 5;
            if (hand.childCount < nFingers)
                nFingers = hand.childCount;

            float magnitude_hand_to_finger = 0.04f; //default:4cm.
            float magnitude_indexfinger_beforeAfterRotation = 0.03f;//default: 3cm.
            float magnitude_first_last_fingers = 0.11f; //default 11cm.

            for (int i = 0; i < nFingers; i++)
            {
                Transform finger = hand.GetChild(i); //knuckle 1

                if (finger == null)
                    continue; //nothing can be done.

                Transform knuckleTwo = finger.GetChild(0);
                Transform knuckleThree = knuckleTwo.GetChild(0) ?? null;

                Finger fingerThing = new(Quaternion.identity, Quaternion.identity, Quaternion.identity);
                switch (i)
                {
                    case 0:
                        magnitude_hand_to_finger = (hand.position - finger.position).magnitude;
                        fingerThing = index;
                        break;
                    case 1:
                        fingerThing = little;
                        break;
                    case 2:
                        fingerThing = middle;
                        break;
                    case 3:
                        fingerThing = ring;
                        break;
                    case 4:
                        fingerThing = thumb;
                        break;
                    default:
                        break;
                }

                Vector3 before = knuckleThree ? knuckleThree.position : new Vector3(0.0f, 0.0f, 0.0f);
                finger.rotation = Reflect(fingerThing.knuckleOne, bRightHand); //kuckle 1
                if (knuckleTwo)
                    knuckleTwo.rotation = Reflect(fingerThing.knuckleTwo, bRightHand); //knuckle 2
                if (knuckleThree)
                    knuckleThree.rotation = Reflect(fingerThing.knuckleThree, bRightHand); //knuckle 3
                Vector3 after = knuckleThree ? knuckleThree.position : new Vector3(0.0f, 0.0f, 0.03f);
                if (i == 0)
                    magnitude_indexfinger_beforeAfterRotation = (before - after).magnitude;

                if (i == nFingers - 1/*last is thumb*/)
                {
                    magnitude_first_last_fingers = (hand.GetChild(0).position - hand.GetChild(i).position).magnitude;
                }
            }

            /*calculate center of grip*/
            float sign = bRightHand ? 1 : -1;
            offsetBetweenWristAndGrip.x = sign * (magnitude_indexfinger_beforeAfterRotation / 2);
            offsetBetweenWristAndGrip.y = 0.75f * magnitude_hand_to_finger;
            offsetBetweenWristAndGrip.z = -magnitude_first_last_fingers;

            return offsetBetweenWristAndGrip;
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarManager), "GetAvatarFileNames")]
    public class VRMAvatar_ListFiles
    {
        private static void Postfix(ref List<string> __result)
        {
            //NOTE: This function is called by the beatsaber plugin infrastructure.
            __result.AddRange(Directory.GetFiles(PlayerAvatarManager.kCustomAvatarsPath, "*.vrm", SearchOption.TopDirectoryOnly).Select(f => Path.GetFileName(f)).OrderBy(f => f).ToList());
        }
    }

    [HarmonyPatch(typeof(AvatarLoader), "LoadFromFileAsync", new[] { typeof(string), typeof(IProgress<float>), typeof(CancellationToken) })]

    public class VRMAvatar_LoadFromFile
    {
        private static IMaterialDescriptorGenerator GetVrmMaterialGenerator(bool useUrp, VRM.glTF_VRM_extensions vrm)
        {
            if (useUrp)
            {
                return new VRM.VRMUrpMaterialDescriptorGenerator(vrm);
            }
            else
            {
                return new VRM.VRMMaterialDescriptorGenerator(vrm);
            }
        }

        private static async Task<AvatarPrefab> LoadVRM(string path, IProgress<float> progress, CancellationToken cancellationToken, Dictionary<string, Task<AvatarPrefab>> tasks, DiContainer _container)
        {
            VRM.VRMFirstPerson.FIRSTPERSON_ONLY_LAYER = CustomAvatar.Avatar.AvatarLayers.kAlwaysVisible;
            VRM.VRMFirstPerson.THIRDPERSON_ONLY_LAYER = CustomAvatar.Avatar.AvatarLayers.kOnlyInThirdPerson;

            if (ExternalAssets.ShaderHelper.m_externalShaders == null)
            {
                //Shaders for VRM Avatars (Beat Saber Specific)
                Debug.LogWarning("Load AssetBundle: vrmmaterialchange_bs_shaders.assets");
                AssetBundleCreateRequest shadersBundleCreateRequest = AssetBundle.LoadFromStreamAsync(Assembly.GetExecutingAssembly().GetManifestResourceStream("CustomAvatar.Resources.vrmmaterialchange_bs_shaders.assets"));
                AssetBundle assetBundle = shadersBundleCreateRequest.assetBundle;
                AssetBundleRequest assetBundleRequest = assetBundle.LoadAllAssetsAsync<Shader>();
                assetBundle = shadersBundleCreateRequest.assetBundle;
                ExternalAssets.ExternalAssetsHelper.LoadExternalAssets(assetBundle);
                assetBundle.Unload(false);

                //Shaders: Replace a General with Specific shader.
                Shader result = ExternalAssets.ShaderHelper.Find("BeatSaber/MToon");
                if (result)
                    ExternalAssets.ShaderHelper.AddExternalShader("VRM/MToon", result); //Replace "VRM/Toon" Shader with BeatSaber/MToon shader.

#if USE_VRM_10 //NOTE: Cannot use as VRM1.0 requires Shader MToon10, which has not yet been converted to Beatsaber [and thus is white-out'ed].
                result = ExternalAssets.ShaderHelper.Find("VRM/UnlitTexture");
                if (result)
                    ExternalAssets.ShaderHelper.AddExternalShader("VRM10/MToon10", result); //Replace "VRM/Toon" Shader with BeatSaber/MToon shader.
#endif
            }

#if USE_VRM_10 //NOTE: Cannot use as VRM1.0 requires Shader MToon10, which has not yet been converted to Beatsaber [and thus is white-out'ed].
            _logger.LogWarning("Vrm1.0: loading.");
            Vrm10.LoadPathAsync(path); 
            Vrm10Instance instance = await Vrm10.LoadPathAsync(path);
#else
            Debug.LogWarning("Vrm0.x: loading.");

            static IMaterialDescriptorGenerator materialCallback(VRM.glTF_VRM_extensions vrm) => GetVrmMaterialGenerator(true, vrm);
            RuntimeGltfInstance instance = await VrmUtility.LoadAsync(path, null, materialCallback);
#endif
            //await ShaderRepair.FixShadersOnGameObjectAsync(instance.gameObject);

            Animator animator = instance.GetComponent<Animator>();

            GameObject obj = null;

            {
                Debug.LogWarning("New VRM Avatar");
                var avatar = new GameObject("Avatar");
                GameObject.DontDestroyOnLoad(avatar.gameObject);
                obj = avatar.gameObject;

                // obj.SetActive(false);
                //avatar.transform.position = new Vector3(0f, -100f, 0f);
                // RuntimeGltfInstance instance = loader.Load();

                instance.transform.SetParent(avatar.transform, false);
#if USE_VRM_10
#else
                instance.ShowMeshes();
#endif
                //instance.gameObject.SetActive(false); //don't set the prefab object as active. it will be instantiated later.

                VRIKManager ik = instance.gameObject.AddComponent<VRIKManager>();
                ik.AutoDetectReferences();

                VRMFirstPerson firstPerson = instance.GetComponent<VRMFirstPerson>();
                firstPerson.Setup();

                ik.references_leftThigh.Rotate(new Vector3(-0.1f, 0f, 0f), Space.World);
                ik.references_leftCalf.Rotate(new Vector3(0.1f, 0f, 0f), Space.World);

                ik.references_rightThigh.Rotate(new Vector3(-0.1f, 0f, 0f), Space.World);
                ik.references_rightCalf.Rotate(new Vector3(0.1f, 0f, 0f), Space.World);

                var leftHand = new GameObject("LeftHand");
                leftHand.transform.SetParent(avatar.transform);
                var rightHand = new GameObject("RightHand");
                rightHand.transform.SetParent(avatar.transform);

                var leftHandTarget = new GameObject("LeftHandTarget");
                //adjust hand and wrist locations [wrt Saber Stick]
                leftHandTarget.transform.SetParent(leftHand.transform);
                leftHandTarget.transform.eulerAngles = new Vector3(-10f, 0f, 90f); //rotate wrist to standard natural angle.

                VRMHandPositionConstants handPos = new();
                leftHandTarget.transform.position = handPos.ApplyToHand(ik.references_leftHand, false); //curl fingers

                ik.solver_leftArm_target = leftHandTarget.transform;

                var rightHandTarget = new GameObject("RightHandTarget");
                //adjust hand and wrist locations [wrt Saber Stick]
                rightHandTarget.transform.SetParent(rightHand.transform);
                rightHandTarget.transform.eulerAngles = new Vector3(-10f, 0f, -90f); //rotate wrist to standard natural angle.
                rightHandTarget.transform.position = handPos.ApplyToHand(ik.references_rightHand, true); //get wrist position. then curl fingers.
                ik.solver_rightArm_target = rightHandTarget.transform;

                Transform vrmFirstPersonHeadBone = firstPerson.FirstPersonBone;
                Vector3 vrmFirstPersonOffset = firstPerson.FirstPersonOffset;

                var head = new GameObject("Head");
                head.transform.SetParent(avatar.transform);
                head.transform.position = ik.references_head.position;// = vrmFirstPersonHeadBone.position + vrmFirstPersonOffset;

                var headViewpoint = new GameObject("HeadViewPoint");
                headViewpoint.transform.SetParent(head.transform);
                headViewpoint.transform.position = vrmFirstPersonHeadBone.position - vrmFirstPersonOffset;

                ik.solver_spine_headTarget = headViewpoint.transform;

                AvatarDescriptor descriptor = avatar.AddComponent<AvatarDescriptor>();
                VRMMeta meta = instance.GetComponent<VRMMeta>();
                if (meta == null)
                {
                    descriptor.name = "";
                    descriptor.author = "";
                    descriptor.cover = null;
                }
                else
                {
                    descriptor.name = meta.Meta.Title;
                    descriptor.author = meta.Meta.Author;
                    if (meta.Meta.Thumbnail != null)
                        descriptor.cover = Sprite.Create(meta.Meta.Thumbnail, new Rect(0, 0, meta.Meta.Thumbnail.width, meta.Meta.Thumbnail.height), Vector2.zero);
                    if (descriptor.name.Length == 0)
                        descriptor.name = "";
                }

                if (descriptor.name == "")
                    descriptor.name = System.IO.Path.GetFileName(path);
            }

            AvatarPrefab avatarPrefab = _container.InstantiateComponent<AvatarPrefab>(obj, new object[] { path });
            avatarPrefab.name = $"AvatarPrefab({avatarPrefab.descriptor.name})";
            avatarPrefab.gameObject.SetActive(false); //set the AvatarPrefab as Not Active [instantiated avatars will be set as active].

            tasks.Remove(path);

            return avatarPrefab;
        }

        private static bool Prefix(string path, IProgress<float> progress, CancellationToken cancellationToken, ref Task<AvatarPrefab> __result, Dictionary<string, Task<AvatarPrefab>> ____tasks, DiContainer ____container)
        {
            // Plugin.Log.Info(Path.GetExtension(path));
            if (Path.GetExtension(path) == ".vrm")
            {
                //VRM
                if (string.IsNullOrEmpty(path))
                {
                    throw new ArgumentNullException("path");
                }
                if (!UnityGame.OnMainThread)
                {
                    throw new InvalidOperationException("LoadFromFileAsync should only be called on the main thread");
                }
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    throw new IOException("File '" + fullPath + "' does not exist");
                }

                if (____tasks.TryGetValue(fullPath, out Task<AvatarPrefab> task))
                {
                    Debug.Log($"Loading Existing VRM from cache {fullPath}");

                    __result = task;
                    return false;
                }

                Debug.Log($"Loading New VRM from File {fullPath}");
                task = LoadVRM(fullPath, progress, cancellationToken, ____tasks, ____container);

                //____tasks.Add(fullPath, task); //reload avatar from cache not working atm for some reason.
                __result = task;
                return false;
            }

            //AssetBundle
            return true;
        }
    }
}


