using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FistVR;
using HarmonyLib;
using PSVR2Toolkit.CAPI;
using UnityEngine;
using System.Threading;
using Mono.Cecil;

namespace Niko666
{
    [BepInAutoPlugin]
    [BepInProcess("h3vr.exe")]
    public partial class AdaptiveTrigger : BaseUnityPlugin
    {
        public static AdaptiveTrigger Instance { get; private set; }
        public static ConfigEntry<EVRControllerType> ControllerToUse;
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
            Instance = this;
            ControllerToUse = Config.Bind("General",
                                    "ControllerToUse",
                                    EVRControllerType.Both,
                                    "Enable Adaptive Trigger effect on selected controllers only. (Both, Left, Right)");
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
            if (!IpcClient.Instance().IsRunning)
            {
                bool success = IpcClient.Instance().Start();
                if (success)
                {
                    Logger.LogMessage($"PSVR2 Toolkit IPC Connected.");
                    Harmony.CreateAndPatchAll(typeof(AdaptiveTriggerPatch), null);
                    Logger.LogMessage($"Fuk U Sony! Sent from {Id} {Version}");
                }
                else
                {
                    Logger.LogMessage($"Failed to connect PSVR2 Toolkit IPC. Did you install PSVR2 Toolkit properly?");
                }
            }
        }
        public void OnDestroy()
        {
            IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Both, 9, 0);
            Thread.Sleep(20);
            IpcClient.Instance().TriggerEffectDisable(EVRControllerType.Both);
            Thread.Sleep(20);
            IpcClient.Instance().Stop();
            Thread.Sleep(20);
            Logger.LogMessage($"PSVR2 Toolkit IPC disconnected. It is now safe to turn off your computer.");
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

        public static void ApplyTriggerEffect(byte startPos, byte endPos, EVRControllerType Hand, float buzztime)
        {
            switch (Hand)
            {
                case EVRControllerType.Left:
                    _LeftHandTriggerEffectCleared = false;
                    if (_leftShotsSoFar != 0)
                    {
                        if (!_LeftHandRecoilEffectApplied)
                        {
                            if (UseVibrationFeedbackForRecoil.Value)
                            {
                                if (ControllerToUse.Value == EVRControllerType.Left || ControllerToUse.Value == EVRControllerType.Both)
                                    IpcClient.Instance().TriggerEffectVibration(EVRControllerType.Left, 0, RecoilFeedbackStrength.Value, VibrationFrequency.Value);
                            }
                            else
                            {
                                if (ControllerToUse.Value == EVRControllerType.Left || ControllerToUse.Value == EVRControllerType.Both)
                                    IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Left, 0, RecoilFeedbackStrength.Value);
                            }
                            _LeftHandRecoilEffectApplied = true;
                        }
                        if (buzztime > 0.01f)
                        {
                            if (ControllerToUse.Value == EVRControllerType.Left || ControllerToUse.Value == EVRControllerType.Both)
                                IpcClient.Instance().TriggerEffectSlopeFeedback(EVRControllerType.Left, (byte)Mathf.Clamp(startPos - 1, 0, 9), (byte)Mathf.Clamp(endPos - 1, 0, 9), 1, ClickyEffectStrength.Value);
                            _LeftHandRecoilEffectApplied = false;
                            _leftShotsSoFar = 0;
                        }
                    }
                    else
                    {
                        if (!_LeftHandTriggerEffectApplied)
                        {
                            if (ControllerToUse.Value == EVRControllerType.Left || ControllerToUse.Value == EVRControllerType.Both)
                                IpcClient.Instance().TriggerEffectSlopeFeedback(EVRControllerType.Left, (byte)Mathf.Clamp(startPos - 1, 0, 9), (byte)Mathf.Clamp(endPos - 1, 0, 9), 1, ClickyEffectStrength.Value);
                            _LeftHandTriggerEffectApplied = true;
                        }
                    }
                    break;
                case EVRControllerType.Right:
                    _RightHandTriggerEffectCleared = false;
                    if (_rightShotsSoFar != 0)
                    {
                        if (!_RightHandRecoilEffectApplied)
                        {
                            if (UseVibrationFeedbackForRecoil.Value)
                            {
                                if (ControllerToUse.Value == EVRControllerType.Right || ControllerToUse.Value == EVRControllerType.Both)
                                    IpcClient.Instance().TriggerEffectVibration(EVRControllerType.Right, 0, RecoilFeedbackStrength.Value, VibrationFrequency.Value);
                            }
                            else
                            {
                                if (ControllerToUse.Value == EVRControllerType.Right || ControllerToUse.Value == EVRControllerType.Both)
                                    IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Right, 0, RecoilFeedbackStrength.Value);
                            }
                            _RightHandRecoilEffectApplied = true;
                        }
                        if (buzztime > 0.01f)
                        {
                            if (ControllerToUse.Value == EVRControllerType.Right || ControllerToUse.Value == EVRControllerType.Both)
                                IpcClient.Instance().TriggerEffectSlopeFeedback(EVRControllerType.Right, (byte)Mathf.Clamp(startPos - 1, 0, 9), (byte)Mathf.Clamp(endPos - 1, 0, 9), 1, ClickyEffectStrength.Value);
                            _RightHandRecoilEffectApplied = false;
                            _rightShotsSoFar = 0;
                        }
                    }
                    else
                    {
                        if (!_RightHandTriggerEffectApplied)
                        {
                            if (ControllerToUse.Value == EVRControllerType.Right || ControllerToUse.Value == EVRControllerType.Both)
                                IpcClient.Instance().TriggerEffectSlopeFeedback(EVRControllerType.Right, (byte)Mathf.Clamp(startPos - 1, 0, 9), (byte)Mathf.Clamp(endPos - 1, 0, 9), 1, ClickyEffectStrength.Value);
                            _RightHandTriggerEffectApplied = true;
                        }
                    }
                    break;
                case EVRControllerType.Both:
                    break;
            }
        }

        public static void ClearTriggerEffect(EVRControllerType Hand)
        {
            switch (Hand)
            {
                case EVRControllerType.Left:
                    if (_LeftHandTriggerEffectCleared) return;
                    //This is a workaround to disable trigger effect because TriggerEffectDisable() doesn't work with left controller.
                    IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Left, 9, 0);
                    _LeftHandTriggerEffectApplied = false;
                    _LeftHandRecoilEffectApplied = false;
                    _leftShotsSoFar = 0;
                    _LeftHandTriggerEffectCleared = true;
                    break;
                case EVRControllerType.Right:
                    if (_RightHandTriggerEffectCleared) return;
                    IpcClient.Instance().TriggerEffectDisable(EVRControllerType.Right);
                    _RightHandTriggerEffectApplied = false;
                    _RightHandRecoilEffectApplied = false;
                    _rightShotsSoFar = 0;
                    _RightHandTriggerEffectCleared = true;
                    break;
                case EVRControllerType.Both:
                    if (_LeftHandTriggerEffectCleared && _RightHandTriggerEffectCleared) return;
                    //This is a workaround to disable trigger effect because TriggerEffectDisable() doesn't work with left controller.
                    IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Both, 9, 0);
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
        internal new static ManualLogSource Logger { get; private set; }
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
            AdaptiveTrigger.ClearTriggerEffect(EVRControllerType.Both);
            return true;
        }

        [HarmonyPatch(typeof(FVRFireArm), "Awake")]
        [HarmonyPostfix]
        public static void ShotDetect()
        {
            GM.CurrentSceneSettings.ShotFiredEvent += AdaptiveTrigger.ShotFired;
        }

        [HarmonyPatch(typeof(FVRViveHand), "Update")]
        [HarmonyPostfix]
        public static void ClearEffectOnDrop(FVRViveHand __instance)
        {
            if (__instance.CurrentInteractable == null)
            {
                AdaptiveTrigger.ClearTriggerEffect(__instance.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
            }
        }

        [HarmonyPatch(typeof(FVRFireArm), "FVRUpdate")]
        [HarmonyPostfix]
        public static void GlobalTriggerEffect(FVRFireArm __instance)
        {
            if (__instance.m_hand != null)
            {
                if (__instance.savedGrip != null)
                {
                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
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
                        AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                    }
                    else
                        switch (__instance)
                        {
                            case ClosedBoltWeapon w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && ((w.Magazine == null && !w.Chamber.IsFull && !w.IsHammerCocked) || (w.Magazine != null && !w.Magazine.HasARound() && !w.Chamber.IsFull && !w.IsHammerCocked)))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
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
                                            AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                        }
                                        else
                                        {
                                            startPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                            endPos = (byte)Mathf.Clamp((int)(w.TriggerDualStageThreshold * 10 - 1), 0, 9);
                                            AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                        }
                                    }
                                    else
                                        AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case OpenBoltReceiver w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && ((w.Magazine == null && !w.Chamber.IsFull && !w.IsHammerCocked) || (w.Magazine != null && !w.Magazine.HasARound() && !w.Chamber.IsFull && !w.IsHammerCocked)))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case Handgun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerBreakThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && w.TriggerType == Handgun.TriggerStyle.SA && ((w.Magazine == null && !w.Chamber.IsFull && !w.m_isHammerCocked) || (w.Magazine != null && !w.Magazine.HasARound() && !w.Chamber.IsFull && !w.m_isHammerCocked)))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case TubeFedShotgun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerBreakThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && ((w.Magazine == null && !w.Chamber.IsFull && !w.IsHammerCocked) || (w.Magazine != null && !w.Magazine.HasARound() && !w.Chamber.IsFull && !w.IsHammerCocked)))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case BoltActionRifle w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && ((w.Magazine == null && !w.Chamber.IsFull && !w.IsHammerCocked) || (w.Magazine != null && !w.Magazine.HasARound() && !w.Chamber.IsFull && !w.IsHammerCocked)))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case BreakActionWeapon w:
                                startPos = 4;
                                endPos = 7;
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case Revolver:
                                startPos = 2;
                                endPos = 9;
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case SingleActionRevolver w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerThreshold * 10 - 2), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && !w.m_isHammerCocked)
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case RevolvingShotgun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case LAPD2019 w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFireThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case BAP w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && ((w.Magazine == null && !w.Chamber.IsFull && !w.m_isHammerCocked) || (w.Magazine != null && !w.Magazine.HasARound() && !w.Chamber.IsFull && !w.m_isHammerCocked)))
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case PotatoGun:
                                startPos = 4;
                                endPos = 7;
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case GrappleGun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerBreakThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case Airgun w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && !w.m_isHammerCocked)
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case CarlGustaf w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case RailTater w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                if (AdaptiveTrigger.DisableEffectWhenEmpty.Value && !w.m_isHammerCocked)
                                {
                                    AdaptiveTrigger.ClearTriggerEffect(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
                                    break;
                                }
                                else
                                {
                                    AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                    break;
                                }
                            case FlameThrower w:
                                startPos = 3;
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            case sblp w:
                                startPos = (byte)Mathf.Clamp((int)(w.TriggerResetThreshold * 10 - 1), 0, 9);
                                endPos = (byte)Mathf.Clamp((int)(w.TriggerFiringThreshold * 10 - 1), 0, 9);
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
                                break;
                            default:
                                startPos = 2;
                                endPos = 7;
                                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
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
                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
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
                AdaptiveTrigger.ApplyTriggerEffect(startPos, endPos, __instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, __instance.m_hand.m_buzztime);
            }
        }
    }
}

