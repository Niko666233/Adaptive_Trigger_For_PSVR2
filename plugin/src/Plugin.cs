using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FistVR;
using HarmonyLib;
using UnityEngine;
using PSVR2Toolkit;
using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection;


namespace Niko666
{
    [BepInAutoPlugin]
    [BepInProcess("h3vr.exe")]
    public partial class AdaptiveTrigger : BaseUnityPlugin
    {
        public enum HeadsetVibrationType { Disabled, OnHit, OnRecoil, Both }
        public static AdaptiveTrigger Instance { get; private set; }
        public static ConfigEntry<VRControllerType> ControllerToUse;
        public static ConfigEntry<HeadsetVibrationType> HeadsetVibration;
        public static ConfigEntry<byte> HeadsetVibrationFrequency;
        public static ConfigEntry<bool> AllowDualStageTriggerEffect;
        public static ConfigEntry<bool> DisableEffectWhenEmpty;
        public static ConfigEntry<byte> ClickyEffectStrength;
        public static ConfigEntry<byte> RecoilFeedbackStrength;
        public static ConfigEntry<bool> UseVibrationFeedbackForRecoil;
        public static ConfigEntry<byte> VibrationFrequency;
        public static ConfigEntry<bool> OverrideTriggerEffectPos;
        public static ConfigEntry<byte> OverrideStartPos;
        public static ConfigEntry<byte> OverrideEndPos;
        public static int _leftShotsSoFar = 0;
        public static int _rightShotsSoFar = 0;
        public static bool _LeftHandTriggerEffectApplied = false;
        public static bool _LeftHandRecoilEffectApplied = false;
        public static bool _LeftHandTriggerEffectCleared = false;
        public static bool _RightHandTriggerEffectApplied = false;
        public static bool _RightHandRecoilEffectApplied = false;
        public static bool _RightHandTriggerEffectCleared = false;

        public void Awake()
        {
            // 获取当前插件 DLL 所在的目录路径
            string pluginDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // 将插件目录添加到进程的 DLL 搜索路径中，使 P/Invoke 能找到同目录下的 CAPI Loader
            SetDllDirectory(pluginDirectory);
            //File.Copy(pluginLoaction + "/psvr2_toolkit_capi_loader.dll", Paths.GameRootPath + "/psvr2_toolkit_capi_loader.dll", true);
            Instance = this;
            ControllerToUse = Config.Bind("General",
                                    "ControllerToUse",
                                    VRControllerType.Both,
                                    "Enable Adaptive Trigger effect on selected controllers only. (Both, Left, Right)");
            HeadsetVibration = Config.Bind("General",
                                    "HeadsetVibration",
                                    HeadsetVibrationType.Both,
                                    "Enable headset vibration effect. (Disabled, OnHit, OnRecoil, Both)");
            HeadsetVibrationFrequency = Config.Bind("General",
                                    "HeadsetVibrationFrequency",
                                    (byte)15,
                                    "Headset vibration frequency. (1-25)");
            AllowDualStageTriggerEffect = Config.Bind("General",
                                    "AllowDualStageTriggerEffect",
                                    true,
                                    "Enable dual stage trigger effect. This will attempt to add a little bit of resistance before the each \"stage\" of the trigger. (I have no idea how a real Dual Stage trigger feels so sorry if it doesn't feel right)");
            DisableEffectWhenEmpty = Config.Bind("General",
                                    "DisableEffectWhenEmpty",
                                    false,
                                    "Disable trigger effect when the gun is empty or the hammer is not cocked.");
            ClickyEffectStrength = Config.Bind("General",
                                    "ClickyEffectStrength",
                                    (byte)4,
                                    "Effect strength of clicky trigger effect. Going too high might cause weak recoil effect. (1-8)");
            RecoilFeedbackStrength = Config.Bind("General",
                                    "RecoilFeedbackStrength",
                                    (byte)8,
                                    "Effect strength of firearm recoil effect. (0-8)");
            UseVibrationFeedbackForRecoil = Config.Bind("General",
                                    "UseVibrationFeedbackForRecoil",
                                    false,
                                    "Use vibration-based feedback for recoil effect. By default the mod use force-based feedback to emulate the recoil \"kick\" effect, but it doesn't work well with extremely high RPM weapons when doing full-auto shooting. Turning this option on will make the trigger vibrates instead of kicking, which is more suitable for full-auto shooting but worse the feeling when single-shot. ");
            VibrationFrequency = Config.Bind("General",
                                    "VibrationFrequency",
                                    (byte)50,
                                    "Vibration frequency for recoil effect when 'UseVibrationFeedbackForRecoil' is enabled. (1-255)");
            OverrideTriggerEffectPos = Config.Bind("General",
                                    "OverrideTriggerEffectPos",
                                    false,
                                    "Override trigger effect position with user set values instead of reading from firearm's trigger thresholds. Not recommend but could be useful if you want to. Also this will disable dual stage trigger effect and empty effect.");
            OverrideStartPos = Config.Bind("General",
                                    "OverrideStartPos",
                                    (byte)2,
                                    "Override start position of the trigger effect. (0-9)");
            OverrideEndPos = Config.Bind("General",
                                    "OverrideStartPos",
                                    (byte)7,
                                    "Override end position of the trigger effect. (0-9)");

            Logger = base.Logger;
            try
            {
                PSVR2ToolkitCAPI.Init();
                Logger.LogMessage($"PSVR2 Toolkit CAPI Initialized.");
                Harmony.CreateAndPatchAll(typeof(AdaptiveTriggerPatch), null);
                Logger.LogMessage($"Fuk U Sony! Sent from {Id} {Version}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize PSVR2 Toolkit CAPI: {ex.Message}\n You need to install or update PSVR2 Toolkit!");
            }
        }
        public void OnDestroy()
        {
            if (_headsetVibrationCoroutine != null)
            {
                StopCoroutine(_headsetVibrationCoroutine);
            }
            PSVR2ToolkitCAPI.SetHmdRumble(0);
            SetTriggerEffectOff(VRControllerType.Both);
            PSVR2ToolkitCAPI.Deinit();
            Logger.LogMessage($"PSVR2 Toolkit CAPI Deinitialized. It is now safe to turn off your computer.");
        }

        private static void SetTriggerEffectOff(VRControllerType controller)
        {
            var command = new ScePadTriggerEffectCommand
            {
                mode = ScePadTriggerEffectMode.SCE_PAD_TRIGGER_EFFECT_MODE_OFF
            };
            PSVR2ToolkitCAPI.SetTriggerEffect(controller, ref command);
        }
        public static void ShotFired(FVRFireArm fireArm)
        {
            if (fireArm.m_hand != null)
            {
                if (fireArm.m_hand.IsThisTheRightHand)
                    _rightShotsSoFar++;
                else
                    _leftShotsSoFar++;
            }
        }

        public static void ApplyTriggerEffect(byte startPos, byte endPos, VRControllerType Hand, float buzztime)
        {
            bool shouldApply = ControllerToUse.Value == Hand || ControllerToUse.Value == VRControllerType.Both;
            if (Hand != VRControllerType.Left && Hand != VRControllerType.Right) return;

            bool isLeft = Hand == VRControllerType.Left;
            ref bool effectCleared = ref isLeft ? ref _LeftHandTriggerEffectCleared : ref _RightHandTriggerEffectCleared;
            ref bool effectApplied = ref isLeft ? ref _LeftHandTriggerEffectApplied : ref _RightHandTriggerEffectApplied;
            ref bool recoilApplied = ref isLeft ? ref _LeftHandRecoilEffectApplied : ref _RightHandRecoilEffectApplied;
            ref int shotsSoFar = ref isLeft ? ref _leftShotsSoFar : ref _rightShotsSoFar;

            effectCleared = false;
            if (shotsSoFar != 0)
            {
                if (!recoilApplied && shouldApply)
                {
                    if (UseVibrationFeedbackForRecoil.Value)
                    {
                        var cmd = new ScePadTriggerEffectCommand
                        {
                            mode = ScePadTriggerEffectMode.SCE_PAD_TRIGGER_EFFECT_MODE_VIBRATION,
                            commandData = { vibrationPosition = 0, vibrationAmplitude = RecoilFeedbackStrength.Value, vibrationFrequency = VibrationFrequency.Value }
                        };
                        PSVR2ToolkitCAPI.SetTriggerEffect(Hand, ref cmd);
                    }
                    else
                    {
                        var cmd = new ScePadTriggerEffectCommand
                        {
                            mode = ScePadTriggerEffectMode.SCE_PAD_TRIGGER_EFFECT_MODE_FEEDBACK,
                            commandData = { feedbackPosition = 0, feedbackStrength = RecoilFeedbackStrength.Value }
                        };
                        PSVR2ToolkitCAPI.SetTriggerEffect(Hand, ref cmd);
                    }
                    recoilApplied = true;
                }
                if (buzztime > 0.01f && shouldApply)
                {
                    var cmd = new ScePadTriggerEffectCommand
                    {
                        mode = ScePadTriggerEffectMode.SCE_PAD_TRIGGER_EFFECT_MODE_SLOPE_FEEDBACK,
                        commandData = { slopeStartPosition = (byte)Mathf.Clamp(startPos - 1, 0, 9), slopeEndPosition = (byte)Mathf.Clamp(endPos - 1, 0, 9), slopeStartStrength = 1, slopeEndStrength = ClickyEffectStrength.Value }
                    };
                    PSVR2ToolkitCAPI.SetTriggerEffect(Hand, ref cmd);
                    recoilApplied = false;
                    shotsSoFar = 0;
                }
            }
            else
            {
                if (!effectApplied && shouldApply)
                {
                    var cmd = new ScePadTriggerEffectCommand
                    {
                        mode = ScePadTriggerEffectMode.SCE_PAD_TRIGGER_EFFECT_MODE_SLOPE_FEEDBACK,
                        commandData = { slopeStartPosition = (byte)Mathf.Clamp(startPos - 1, 0, 9), slopeEndPosition = (byte)Mathf.Clamp(endPos - 1, 0, 9), slopeStartStrength = 1, slopeEndStrength = ClickyEffectStrength.Value }
                    };
                    PSVR2ToolkitCAPI.SetTriggerEffect(Hand, ref cmd);
                    effectApplied = true;
                }
            }
        }

        public static void ClearTriggerEffect(VRControllerType Hand)
        {
            switch (Hand)
            {
                case VRControllerType.Left:
                    if (_LeftHandTriggerEffectCleared) return;
                    SetTriggerEffectOff(VRControllerType.Left);
                    _LeftHandTriggerEffectApplied = false;
                    _LeftHandRecoilEffectApplied = false;
                    _leftShotsSoFar = 0;
                    _LeftHandTriggerEffectCleared = true;
                    break;
                case VRControllerType.Right:
                    if (_RightHandTriggerEffectCleared) return;
                    SetTriggerEffectOff(VRControllerType.Right);
                    _RightHandTriggerEffectApplied = false;
                    _RightHandRecoilEffectApplied = false;
                    _rightShotsSoFar = 0;
                    _RightHandTriggerEffectCleared = true;
                    break;
                case VRControllerType.Both:
                    if (_LeftHandTriggerEffectCleared && _RightHandTriggerEffectCleared) return;
                    SetTriggerEffectOff(VRControllerType.Both);
                    _LeftHandTriggerEffectApplied = false;
                    _LeftHandRecoilEffectApplied = false;
                    _leftShotsSoFar = 0;
                    _LeftHandTriggerEffectCleared = true;
                    _RightHandTriggerEffectApplied = false;
                    _RightHandRecoilEffectApplied = false;
                    _rightShotsSoFar = 0;
                    _RightHandTriggerEffectCleared = true;
                    break;
            }
        }

        private static Coroutine _headsetVibrationCoroutine;
        public static void ApplyHeadsetVibration(float duration, byte freq)
        {
            if (_headsetVibrationCoroutine != null)
            {
                // 停止当前正在运行的震动协程，防止叠加
                Instance.StopCoroutine(_headsetVibrationCoroutine);
            }
            _headsetVibrationCoroutine = Instance.StartCoroutine(VibrationFadeRoutine(duration, freq));
        }
        private static IEnumerator VibrationFadeRoutine(float duration, byte startFreq)
        {
            byte clampedStartFreq = (byte)Mathf.Clamp(startFreq, 0, 25);
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                // 随时间推移，从初始频率平滑插值到 0
                float currentFreq = Mathf.Lerp(clampedStartFreq, 0, elapsedTime / duration);
                PSVR2ToolkitCAPI.SetHmdRumble((byte)currentFreq);
                yield return null; // 等待下一帧
            }

            // 确保震动完全停止
            PSVR2ToolkitCAPI.SetHmdRumble(0);
            _headsetVibrationCoroutine = null;
        }
        internal new static ManualLogSource Logger { get; private set; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
    class AdaptiveTriggerPatch : MonoBehaviour
    {
        [HarmonyPatch(typeof(SteamVR_LoadLevel), "Begin")]
        [HarmonyPrefix]
        public static bool BeginPatch()
        {
            if (GM.CurrentSceneSettings != null)
                GM.CurrentSceneSettings.ShotFiredEvent -= AdaptiveTrigger.ShotFired;
            // Reload the config
            AdaptiveTrigger.Instance.Config.Reload();
            AdaptiveTrigger._LeftHandTriggerEffectCleared = false;
            AdaptiveTrigger._RightHandTriggerEffectCleared = false;
            AdaptiveTrigger.ClearTriggerEffect(VRControllerType.Both);
            return true;
        }

        [HarmonyPatch(typeof(FVRFireArm), "Awake")]
        [HarmonyPostfix]
        public static void ShotDetect()
        {
            GM.CurrentSceneSettings.ShotFiredEvent += AdaptiveTrigger.ShotFired;
        }

        [HarmonyPatch(typeof(FVRPlayerBody), "HitEffect")]
        [HarmonyPrefix]
        public static bool HitEffectPatch()
        {
            if (AdaptiveTrigger.HeadsetVibration.Value == AdaptiveTrigger.HeadsetVibrationType.OnHit || AdaptiveTrigger.HeadsetVibration.Value == AdaptiveTrigger.HeadsetVibrationType.Both)
            { AdaptiveTrigger.ApplyHeadsetVibration(3f, AdaptiveTrigger.HeadsetVibrationFrequency.Value); }
            return true;
        }

        [HarmonyPatch(typeof(FVRViveHand), "Update")]
        [HarmonyPostfix]
        public static void ClearEffectOnDropButAlsoDoHeadsetVibration(FVRViveHand __instance)
        {
            if (__instance.CurrentInteractable == null)
            {
                AdaptiveTrigger.ClearTriggerEffect(__instance.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
            }
            if (__instance.CurrentInteractable != null && __instance.CurrentInteractable is FVRFireArm && __instance.m_isBuzzing && (AdaptiveTrigger._rightShotsSoFar > 0 || AdaptiveTrigger._leftShotsSoFar > 0))
            {
                if ((AdaptiveTrigger.HeadsetVibration.Value == AdaptiveTrigger.HeadsetVibrationType.OnRecoil || AdaptiveTrigger.HeadsetVibration.Value == AdaptiveTrigger.HeadsetVibrationType.Both) && __instance.m_curBuzz != null)
                {
                    AdaptiveTrigger.ApplyHeadsetVibration(__instance.m_curBuzz.BuzzLength, AdaptiveTrigger.HeadsetVibrationFrequency.Value);
                }
            }
        }

        private static bool IsWeaponEmpty(FVRFireArm w)
        {
            bool hasMag = w is Handgun hg ? hg.Magazine != null : (w is BAP bap ? bap.Magazine != null : false);
            bool magHasRound = w is Handgun hg2 ? hg2.Magazine.HasARound() : (w is BAP bap2 ? bap2.Magazine.HasARound() : false);
            bool isHammerCocked = w is Handgun hg3 ? hg3.m_isHammerCocked : (w is BAP bap3 ? bap3.m_isHammerCocked : (w is Airgun ag ? ag.m_isHammerCocked : (w is RailTater rt ? rt.m_isHammerCocked : (w is SingleActionRevolver sar ? sar.m_isHammerCocked : false))));
            bool chamberIsFull = w is Handgun hg4 ? hg4.Chamber.IsFull : (w is BAP bap4 ? bap4.Chamber.IsFull : (w is Airgun ag2 ? ag2.Chamber.IsFull : (w is RailTater rt2 ? rt2.Chamber.IsFull : false)));

            return (hasMag && !magHasRound && !chamberIsFull && !isHammerCocked) || (!hasMag && !chamberIsFull && !isHammerCocked);
        }
        [HarmonyPatch(typeof(FVRFireArm), "FVRUpdate")]
        [HarmonyPostfix]
        public static void GlobalTriggerEffect(FVRFireArm __instance)
        {
            if (__instance.m_hand != null)
            {
                if (__instance.savedGrip != null)
                {
                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                }
                else
                {
                    AdaptiveTrigger._LeftHandTriggerEffectCleared = false;
                    AdaptiveTrigger._RightHandTriggerEffectCleared = false;
                    byte startPos;
                    byte endPos;
                    if (AdaptiveTrigger.OverrideTriggerEffectPos.Value)
                    {
                        startPos = (byte)Mathf.Clamp(AdaptiveTrigger.OverrideStartPos.Value, 0, 9);
                        endPos = (byte)Mathf.Clamp(AdaptiveTrigger.OverrideEndPos.Value, 0, 9);
                        AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                    }
                    else
                        switch (__instance)
                        {
                            case ClosedBoltWeapon w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && IsWeaponEmpty(w))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    if (w.UsesDualStageFullAuto && AdaptiveTrigger.AllowDualStageTriggerEffect.Value)
                                    {
                                        if (w.m_triggerFloat < w.TriggerFiringThreshold)
                                        {
                                            startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                            endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                            AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                        }
                                        else
                                        {
                                            startPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                            endPos = (byte)Mathf.Clamp((int)(w.TriggerDualStageThreshold * 10 - 1), 0, 9);
                                            AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                        }
                                    }
                                    else
                                        AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }

                            case OpenBoltReceiver w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && IsWeaponEmpty(w))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case Handgun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerBreakThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && IsWeaponEmpty(w))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case TubeFedShotgun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerBreakThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && IsWeaponEmpty(w))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case BoltActionRifle w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && IsWeaponEmpty(w))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case BreakActionWeapon w:
                                startPos = 4;
                                endPos = 7;
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case Revolver:
                                startPos = 2;
                                endPos = 9;
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case SingleActionRevolver w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerThreshold * 10 - 2), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && !w.m_isHammerCocked)
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case RevolvingShotgun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case LAPD2019 w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFireThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case BAP w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && IsWeaponEmpty(w))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case PotatoGun:
                                startPos = 4;
                                endPos = 7;
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case GrappleGun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerBreakThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case Airgun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && !w.m_isHammerCocked)
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case CarlGustaf w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case RailTater w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && !w.m_isHammerCocked)
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case FlameThrower w:
                                startPos = 3;
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case sblp w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            default:
                                startPos = 2;
                                endPos = 7;
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                        }
                }
            }
        }

        [HarmonyPatch(typeof(AttachableFirearmPhysicalObject), "UpdateInteraction")]
        [HarmonyPostfix]
        public static void AttachableFirearmPhysicalObjectTriggerEffect(AttachableFirearmPhysicalObject __instance)
        {
            if (__instance.m_hand != null)
            {
                AdaptiveTrigger._LeftHandTriggerEffectCleared = false;
                AdaptiveTrigger._RightHandTriggerEffectCleared = false;
                byte startPos = 2;
                byte endPos = 7;
                if (AdaptiveTrigger.OverrideTriggerEffectPos.Value)
                {
                    startPos = (byte)Mathf.Clamp(AdaptiveTrigger.OverrideStartPos.Value + 1, 0, 10);
                    endPos = (byte)Mathf.Clamp(AdaptiveTrigger.OverrideEndPos.Value + 1, 0, 10);
                }
                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
            }
        }

        [HarmonyPatch(typeof(AttachableFirearmInterface), "UpdateInteraction")]
        [HarmonyPostfix]
        public static void AttachableFirearmInterfaceTriggerEffect(AttachableFirearmInterface __instance)
        {
            if (__instance.m_hand != null)
            {
                AdaptiveTrigger._LeftHandTriggerEffectCleared = false;
                AdaptiveTrigger._RightHandTriggerEffectCleared = false;
                byte startPos = 2;
                byte endPos = 7;
                if (AdaptiveTrigger.OverrideTriggerEffectPos.Value)
                {
                    startPos = (byte)Mathf.Clamp(AdaptiveTrigger.OverrideStartPos.Value + 1, 0, 10);
                    endPos = (byte)Mathf.Clamp(AdaptiveTrigger.OverrideEndPos.Value + 1, 0, 10);
                }
                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? VRControllerType.Right : VRControllerType.Left, __instance.m_hand.m_buzztime);
            }
        }
    }
}

