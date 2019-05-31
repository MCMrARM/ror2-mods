using BepInEx;
using MiniRpcLib;
using MiniRpcLib.Action;
using RoR2;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace TruePause {

    [BepInPlugin(ModGuid, "True Pause", "1.0.0")]
    [BepInDependency(MiniRpcPlugin.Dependency)]
    public class TruePausePlugin : BaseUnityPlugin {

        private const string ModGuid = "com.github.mcmrarm.truepause";

        private static FieldInfo appPauseScreenInstanceField = typeof(RoR2Application).GetField("pauseScreenInstance", BindingFlags.NonPublic | BindingFlags.Instance);

        private IRpcAction<bool> NetRequestPauseAction;
        private IRpcAction<bool> NetSetPausedAction;

        private float oldTimeScale;
        private bool netPaused = false;

        public void Start() {
            var miniRpc = MiniRpc.CreateInstance(ModGuid);
            NetRequestPauseAction = miniRpc.RegisterAction<bool>(Target.Server, NetRequestPause);
            NetSetPausedAction = miniRpc.RegisterAction<bool>(Target.Client, NetSetPaused);
            
            On.RoR2.UI.PauseScreenController.OnEnable += (orig, self) => {
                orig(self);
                if (!NetworkServer.dontListen && !netPaused)
                    NetRequestPauseAction.Invoke(true);
            };
            On.RoR2.UI.PauseScreenController.OnDisable += (orig, self) => {
                orig(self);
                if (!NetworkServer.dontListen && netPaused)
                    NetRequestPauseAction.Invoke(false);
            };
        }

        private bool IsPauseScreenVisible() {
            var currentPauseScreen = (GameObject)appPauseScreenInstanceField.GetValue(RoR2Application.instance);
            return (currentPauseScreen != null);
        }

        private void SetPauseScreenVisible(bool paused) {
            bool wasPaused = IsPauseScreenVisible();
            if (paused && !wasPaused) {
                GameObject o = Instantiate(Resources.Load<GameObject>("Prefabs/UI/PauseScreen"), RoR2Application.instance.transform);
                appPauseScreenInstanceField.SetValue(RoR2Application.instance, o);
            } else if (!paused && wasPaused) {
                var currentPauseScreen = (GameObject)appPauseScreenInstanceField.GetValue(RoR2Application.instance);
                Destroy(currentPauseScreen);
                appPauseScreenInstanceField.SetValue(RoR2Application.instance, null);
            }
        }
        
        [Server]
        private void NetRequestPause(NetworkUser user, bool paused) {
            NetSetPausedAction.Invoke(paused);
        }

        [Client]
        private void NetSetPaused(NetworkUser user, bool paused) {
            if (netPaused == paused)
                return;
            netPaused = paused;
            SetPauseScreenVisible(paused);
            if (netPaused) {
                RoR2Application.onPauseStartGlobal();
                oldTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            } else {
                Time.timeScale = oldTimeScale;
                RoR2Application.onPauseEndGlobal();
            }
        }

    }
}
