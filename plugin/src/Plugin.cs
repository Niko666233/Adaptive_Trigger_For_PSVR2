using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FistVR;
using HarmonyLib;
using PSVR2Toolkit.CAPI;
using UnityEngine;

namespace Niko666
{
    [BepInAutoPlugin]
    [BepInProcess("h3vr.exe")]
    public partial class AdaptiveTrigger : BaseUnityPlugin
    {
        public enum ControllerType { Both, Left, Right }
        public static AdaptiveTrigger Instance { get; private set; }
        public static ConfigEntry<ControllerType> ControllerToUse;
        public static ConfigEntry<byte> ClickyEffectStrength;
        public static ConfigEntry<byte> RecoilFeedbackStrength;
        public static ConfigEntry<bool> UseVibrationFeedbackForRecoil;
        public static ConfigEntry<byte> VibrationFrequency;
        public static ConfigEntry<byte> DefaultStartPos;
        public static ConfigEntry<byte> DefaultEndPos;
        public static int _shotsSoFar = 0;

        public void Awake()
        {
            Instance = this;
            ControllerToUse = Config.Bind("General",
                                    "ControllerToUse",
                                    ControllerType.Both,
                                    "Enable Adaptive Trigger effect on selected controllers only. (Both, Left, Right)");
            ClickyEffectStrength = Config.Bind("General",
                                    "ClickyEffectStrength",
                                    (byte)4,
                                    "Effect strength of clicky trigger effect. (1-8)");
            RecoilFeedbackStrength = Config.Bind("General",
                                    "RecoilFeedbackStrength",
                                    (byte)8,
                                    "Effect strength of firearm recoil effect. (0-8)");
            UseVibrationFeedbackForRecoil = Config.Bind("General",
                                    "UseVibrationFeedbackForRecoil",
                                    false,
                                    "Use vibration-based feedback for recoil effect. By default the mod use force-based feedback to emulate the recoil \"kick\" effect, but it doesn't work well with high rate of fire weapons when doing full-auto shooting. Turning this option on will make the trigger vibrates instead of kicking, which is more suitable for full-auto shooting but worse the feeling when single-shot. ");
            VibrationFrequency = Config.Bind("General",
                                    "VibrationFrequency",
                                    (byte)50,
                                    "Vibration frequency for recoil effect when 'UseVibrationFeedbackForRecoil' is enabled. (1-255)");
            DefaultStartPos = Config.Bind("General",
                                    "DefaultStartPos",
                                    (byte)1,
                                    "Default start position of the trigger when the firearm does not have trigger range definition. 1 being the closest to H3VR defaults. (0-9)");
            DefaultEndPos = Config.Bind("General",
                                    "DefaultEndPos",
                                    (byte)6,
                                    "Default end position of the trigger when the firearm does not have trigger range definition. 6 being the closest to H3VR defaults. (0-9)");

            Logger = base.Logger;
            if (!IpcClient.Instance().IsRunning)
            {
                bool success = IpcClient.Instance().Start();
                if (success)
                {
                    Logger.LogMessage($"PSVR2 Toolkit IPC Connected.");
                    Harmony.CreateAndPatchAll(typeof(AdaptiveTriggerPatch), null);
                    Logger.LogMessage($"Fuck this world! Sent from {Id} {Version}");
                }
                else
                {
                    Logger.LogMessage($"Failed to connect PSVR2 Toolkit IPC. Did you install PSVR2 Toolkit properly?");
                }
            }
        }
        public void OnDestroy()
        {
            //Probably doesn't needed
            //IpcClient.Instance().TriggerEffectDisable(EVRControllerType.Both);
            //System.Threading.Thread.Sleep(20);
            IpcClient.Instance().Stop();
            AdaptiveTrigger.Logger.LogMessage($"PSVR2 Toolkit IPC disconnected. It is now safe to turn off your computer.");
        }
        public static void ShotFired(FVRFireArm fireArm)
        {
            if (fireArm.m_hand != null) _shotsSoFar++;
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
            //This is a workaround to disable trigger effect because TriggerEffectDisable() doesn't work with left controller.
            if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Both)
                IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Both, 9, 0);
            else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Left)
                IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Left, 9, 0);
            else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Right)
                IpcClient.Instance().TriggerEffectDisable(EVRControllerType.Right);
            return true;
        }
        [HarmonyPatch(typeof(FVRViveHand), "Update")]
        [HarmonyPostfix]
        public static void ClearEffectOnDrop(FVRViveHand __instance)
        {
            if (__instance.CurrentInteractable == null)
            {
                //This is a workaround to disable trigger effect because TriggerEffectDisable() doesn't work with left controller.
                if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Both)
                    IpcClient.Instance().TriggerEffectFeedback(__instance.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, 9, 0);
                else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Left)
                    IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Left, 9, 0);
                else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Right)
                    IpcClient.Instance().TriggerEffectDisable(EVRControllerType.Right);
            }
        }

        [HarmonyPatch(typeof(FVRFireArm), "Awake")]
        [HarmonyPostfix]
        public static void ShotDetect()
        {
            GM.CurrentSceneSettings.ShotFiredEvent += AdaptiveTrigger.ShotFired;
        }

        [HarmonyPatch(typeof(FVRFireArm), "FVRUpdate")]
        [HarmonyPostfix]
        public static void GlobalRecoilEffect(FVRFireArm __instance)
        {
            _ = (byte)(AdaptiveTrigger.DefaultStartPos.Value + 1);
            _ = (byte)(AdaptiveTrigger.DefaultEndPos.Value + 1);
            if (__instance.m_hand != null)
            {
                byte startPos;
                byte endPos;
                switch (__instance)
                {
                    case ClosedBoltWeapon w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    case OpenBoltReceiver w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    case Handgun w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerBreakThreshold * 10 - 1);
                        break;
                    case TubeFedShotgun w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerBreakThreshold * 10 - 1);
                        break;
                    case BoltActionRifle w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    case BreakActionWeapon:
                        startPos = 4;
                        endPos = 7;
                        break;
                    case Revolver:
                        startPos = 2;
                        endPos = 9;
                        break;
                    case SingleActionRevolver w:
                        startPos = (byte)(w.TriggerThreshold * 10 - 2);
                        endPos = (byte)(w.TriggerThreshold * 10 - 1);
                        break;
                    case RevolvingShotgun w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    case LAPD2019 w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFireThreshold * 10 - 1);
                        break;
                    case BAP w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    case PotatoGun:
                        startPos = 4;
                        endPos = 7;
                        break;
                    case GrappleGun w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerBreakThreshold * 10 - 1);
                        break;
                    case Airgun w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    case CarlGustaf w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    case RailTater w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    case FlameThrower w:
                        startPos = 3;
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    case sblp w:
                        startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                        endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                        break;
                    default:
                        startPos = (byte)(AdaptiveTrigger.DefaultStartPos.Value + 1);
                        endPos = (byte)(AdaptiveTrigger.DefaultEndPos.Value + 1);
                        break;
                }

                if (AdaptiveTrigger._shotsSoFar != 0)
                {
                    if (AdaptiveTrigger.UseVibrationFeedbackForRecoil.Value)
                    {
                        if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Both)
                            IpcClient.Instance().TriggerEffectVibration(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, 0, AdaptiveTrigger.RecoilFeedbackStrength.Value, AdaptiveTrigger.VibrationFrequency.Value);
                        else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Left && !__instance.m_hand.IsThisTheRightHand)
                            IpcClient.Instance().TriggerEffectVibration(EVRControllerType.Left, 0, AdaptiveTrigger.RecoilFeedbackStrength.Value, AdaptiveTrigger.VibrationFrequency.Value);
                        else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Right && __instance.m_hand.IsThisTheRightHand)
                            IpcClient.Instance().TriggerEffectVibration(EVRControllerType.Right, 0, AdaptiveTrigger.RecoilFeedbackStrength.Value, AdaptiveTrigger.VibrationFrequency.Value);
                    }
                    else
                        if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Both)
                            IpcClient.Instance().TriggerEffectFeedback(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, 0, AdaptiveTrigger.RecoilFeedbackStrength.Value);
                        else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Left && !__instance.m_hand.IsThisTheRightHand)
                            IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Left, 0, AdaptiveTrigger.RecoilFeedbackStrength.Value);
                        else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Right && __instance.m_hand.IsThisTheRightHand)
                            IpcClient.Instance().TriggerEffectFeedback(EVRControllerType.Right, 0, AdaptiveTrigger.RecoilFeedbackStrength.Value);
                    if (__instance.m_hand.m_buzztime > 0.02f)
                    {
                        if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Both)
                            IpcClient.Instance().TriggerEffectSlopeFeedback(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, (byte)(startPos - 1), (byte)(endPos - 1), 1, AdaptiveTrigger.ClickyEffectStrength.Value);
                        else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Left && !__instance.m_hand.IsThisTheRightHand)
                            IpcClient.Instance().TriggerEffectSlopeFeedback(EVRControllerType.Left, (byte)(startPos - 1), (byte)(endPos - 1), 1, AdaptiveTrigger.ClickyEffectStrength.Value);
                        else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Right && __instance.m_hand.IsThisTheRightHand)
                            IpcClient.Instance().TriggerEffectSlopeFeedback(EVRControllerType.Right, (byte)(startPos - 1), (byte)(endPos - 1), 1, AdaptiveTrigger.ClickyEffectStrength.Value);
                        AdaptiveTrigger._shotsSoFar = 0;
                    }
                }
                else
                {
                    if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Both)
                        IpcClient.Instance().TriggerEffectSlopeFeedback(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, (byte)(startPos - 1), (byte)(endPos - 1), 1, AdaptiveTrigger.ClickyEffectStrength.Value);
                    else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Left && !__instance.m_hand.IsThisTheRightHand)
                        IpcClient.Instance().TriggerEffectSlopeFeedback(EVRControllerType.Left, (byte)(startPos - 1), (byte)(endPos - 1), 1, AdaptiveTrigger.ClickyEffectStrength.Value);
                    else if (AdaptiveTrigger.ControllerToUse.Value == AdaptiveTrigger.ControllerType.Right && __instance.m_hand.IsThisTheRightHand)
                        IpcClient.Instance().TriggerEffectSlopeFeedback(EVRControllerType.Right, (byte)(startPos - 1), (byte)(endPos - 1), 1, AdaptiveTrigger.ClickyEffectStrength.Value);
                }
            }
        }
    }
}
