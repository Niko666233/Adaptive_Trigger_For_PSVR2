using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FistVR;
using HarmonyLib;
using PSVR2Toolkit.CAPI;
using UnityEngine;

// TODO: Change 'YourName' to your name. 
namespace Niko666
{
    // TODO: Change 'YourPlugin' to the name of your plugin
    [BepInAutoPlugin]
    [BepInProcess("h3vr.exe")]
    public partial class AdaptiveTrigger : BaseUnityPlugin
    {
        public static ConfigEntry<byte> ClickyEffectStrength;
        public static ConfigEntry<byte> RecoilFeedbackStrength;
        public static ConfigEntry<bool> UseVibrationFeedbackForRecoil;
        public static ConfigEntry<byte> VibrationFrequency;
        public static int _shotsSoFar = 0;
        /* == Quick Start == 
         * Your plugin class is a Unity MonoBehaviour that gets added to a global game object when the game starts.
         * You should use Awake to initialize yourself, read configs, register stuff, etc.
         * If you need to use Update or other Unity event methods those will work too.
         *
         * Some references on how to do various things:
         * Adding config settings to your plugin: https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/4_configuration.html
         * Hooking / Patching game methods: https://harmony.pardeike.net/articles/patching.html
         * Also check out the Unity documentation: https://docs.unity3d.com/560/Documentation/ScriptReference/index.html
         * And the C# documentation: https://learn.microsoft.com/en-us/dotnet/csharp/
         */

        public void Awake()
        {
            ClickyEffectStrength = Config.Bind("General",
                                                "ClickyEffectStrength",
                                                (byte)4,
                                                "Effect strength of clicky trigger effect. (0-8)");
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
            Logger = base.Logger;
            if (!IpcClient.Instance().IsRunning)
            {
                bool success = IpcClient.Instance().Start();
                if (success)
                {
                    Logger.LogMessage($"PSVR2 Toolkit IPC Connected.");
                    Harmony.CreateAndPatchAll(typeof(AdaptiveTriggerPatch), null);
                    // Your plugin's ID, Name, and Version are available here.
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
            IpcClient.Instance().TriggerEffectDisable(EVRControllerType.Both);
            System.Threading.Thread.Sleep(20);
            IpcClient.Instance().Stop();
            AdaptiveTrigger.Logger.LogMessage($"PSVR2 Toolkit IPC disconnected. It is now safe to turn off your computer.");
        }

        public static void ShotFired(FVRFireArm fireArm)
        {
            if (fireArm.m_hand != null) _shotsSoFar++;
        }

        // The line below allows access to your plugin's logger from anywhere in your code, including outside of __instance file.
        // Use it with 'YourPlugin.Logger.LogInfo(message)' (or any of the other Log* methods)
        internal new static ManualLogSource Logger { get; private set; }
    }
    class AdaptiveTriggerPatch : MonoBehaviour
    {
        [HarmonyPatch(typeof(FVRViveHand), "Update")]
        [HarmonyPostfix]
        public static void ClearEffectOnDrop(FVRViveHand __instance)
        {
            bool hasSetEffect = false;
            if (__instance.CurrentInteractable == null && !hasSetEffect)
            {
                IpcClient.Instance().TriggerEffectDisable(__instance.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left);
                hasSetEffect = true;
            }
            else hasSetEffect = false;
        }

        [HarmonyPatch(typeof(FVRFireArm), "Awake")]
        [HarmonyPostfix]
        public static void ShotDetect(FVRFireArm __instance)
        {
            GM.CurrentSceneSettings.ShotFiredEvent += AdaptiveTrigger.ShotFired;
        }

        [HarmonyPatch(typeof(FVRFireArm), "FVRUpdate")]
        [HarmonyPostfix]
        public static void GlobalRecoilEffect(FVRFireArm __instance)
        {
            byte startPos = 3;
            byte endPos = 7;
            bool hasSetEffect2 = false;
            if (__instance.m_hand != null)
            {
                if (!hasSetEffect2)
                {
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
                        case BreakActionWeapon w:
                            startPos = (byte)4;
                            endPos = (byte)7;
                            break;
                        case Revolver w:
                            startPos = (byte)2;
                            endPos = (byte)9;
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
                        case PotatoGun w:
                            startPos = (byte)4;
                            endPos = (byte)7;
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
                            startPos = (byte)3;
                            endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                            break;
                        case sblp w:
                            startPos = (byte)(w.TriggerResetThreshold * 10 - 1);
                            endPos = (byte)(w.TriggerFiringThreshold * 10 - 1);
                            break;
                        default:
                            startPos = (byte)3;
                            endPos = (byte)7;
                            break;
                    }
                    hasSetEffect2 = true;
                }
                if (AdaptiveTrigger._shotsSoFar != 0)
                {
                    if (AdaptiveTrigger.UseVibrationFeedbackForRecoil.Value)
                    {
                        IpcClient.Instance().TriggerEffectVibration(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, 4, AdaptiveTrigger.RecoilFeedbackStrength.Value, AdaptiveTrigger.VibrationFrequency.Value);
                    }
                    else
                        IpcClient.Instance().TriggerEffectFeedback(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, 1, AdaptiveTrigger.RecoilFeedbackStrength.Value);
                    if (__instance.m_hand.m_buzztime > 0.02f)
                    {
                        IpcClient.Instance().TriggerEffectSlopeFeedback(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, (byte)(startPos - 1), (byte)(endPos - 1), 1, AdaptiveTrigger.ClickyEffectStrength.Value);
                        AdaptiveTrigger._shotsSoFar = 0;
                    }
                }
                else
                {
                    IpcClient.Instance().TriggerEffectSlopeFeedback(__instance.m_hand.IsThisTheRightHand ? EVRControllerType.Right : EVRControllerType.Left, (byte)(startPos - 1), (byte)(endPos - 1), 1, AdaptiveTrigger.ClickyEffectStrength.Value);
                }
            }
            else hasSetEffect2 = false;
        }
    }
}
