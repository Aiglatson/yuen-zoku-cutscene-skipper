using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YuenSkip
{
    /// <summary>
    /// 淫縛の館 —— 过场跳过 / 对话快进 / 一键进入结局后状态
    ///
    /// 三个功能:
    ///   Tab  —— 按一下, 跳过当前这段过场动画; 碰到对话自动解除。
    ///   F10  —— 快进所有对话 (Click/Auto), 过场动画照常播放, 不强制跳过游戏内容。
    ///   F11  —— ★ 一键进入「结局后 + 全解锁」状态, 跳过全部剧情。
    ///
    /// 关于 F11:
    ///   它只做一件事 —— 把 System_Var 的解锁表/关卡号/通关标记摆成通关后该有的值,
    ///   然后 LoadScene("Loder") 跳到 C1 中枢。跳过去之后全部交还给游戏自己。
    ///   它**不修改游戏目录里的任何文件**, 只写存档 (storyData/configData/OtherSettingData),
    ///   而且「新游戏」依旧走原版流程 —— 正常游玩的路没有被堵死。
    ///
    /// 关于 Tab/F10 的实现原则 (原 v4 保留):
    ///   在 System_Story.Story() 执行前, 把某一行的 waitType 改成 "Skip"。
    ///   推进动作始终由游戏自己的 case "Skip" 完成, 不会出现状态脱节。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SkipPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.yuen.skipcutscene";
        public const string PluginName = "Yuen-Zoku Cutscene Skipper";
        public const string PluginVersion = "1.0.0";

        // ---------- 配置 ----------
        internal static ConfigEntry<string> CfgSkipKey;          // Tab
        internal static ConfigEntry<string> CfgDialogueSkipKey;  // F10
        internal static ConfigEntry<string> CfgEndingKey;        // F11
        internal static ConfigEntry<bool>   CfgShowHint;
        internal static ConfigEntry<float>  CfgHintSeconds;

        // ---------- 状态 ----------
        /// <summary>Tab: 按一下 = 跳过当前这段过场动画, 碰到对话自动解除。</summary>
        internal static bool TabArmed;

        /// <summary>F10: 快进所有对话 (只对带文本的 Click 行生效)。</summary>
        internal static bool DialogueSkip;

        internal static KeyCode SkipKey        = KeyCode.Tab;
        internal static KeyCode DialogueSkipKey = KeyCode.F10;
        internal static KeyCode EndingKey      = KeyCode.F11;

        private static ManualLogSource _log;

        // 短暂提示 (显示一小会儿后淡出)
        private static float  _hintUntil;
        private static string _hintMain = "";
        private static string _hintSub  = "";

        // (v4 遗留字段已移除: 提示现在由 F10/F11 的切换直接触发)

        internal static void Log(string msg)
        {
            if (_log != null) _log.LogInfo(msg);
            else Debug.Log("[YuenSkip] " + msg);
        }

        /// <summary>显示一小会儿的提示, 之后自动淡出消失</summary>
        internal static void Hint(string main, string sub)
        {
            _hintMain = main;
            _hintSub  = sub;
            _hintUntil = Time.unscaledTime + Mathf.Clamp(CfgHintSeconds.Value, 1f, 15f);
        }

        private void Awake()
        {
            _log = Logger;

            CfgSkipKey = Config.Bind("General", "SkipKey", "Tab",
                "按一下此键 = 跳过当前这段过场动画 (碰到对话会自动停)");
            CfgDialogueSkipKey = Config.Bind("General", "DialogueSkipKey", "F10",
                "按此键切换「对话快进」: 所有对话立即推进, 过场动画照常播放");
            CfgEndingKey = Config.Bind("General", "EndingStateKey", "F11",
                "★ 按此键 = 进入「结局后 + 全部内容解锁」状态 (跳过全部剧情直达 C1 中枢)");
            CfgShowHint = Config.Bind("General", "ShowHint", true,
                "是否显示操作提示 (只显示一小会儿然后淡出)");
            CfgHintSeconds = Config.Bind("General", "HintSeconds", 4f,
                new ConfigDescription("提示停留的秒数 (含淡出)",
                    new AcceptableValueRange<float>(1f, 15f)));

            SkipKey         = ParseKey(CfgSkipKey.Value, KeyCode.Tab, "SkipKey");
            DialogueSkipKey = ParseKey(CfgDialogueSkipKey.Value, KeyCode.F10, "DialogueSkipKey");
            EndingKey       = ParseKey(CfgEndingKey.Value, KeyCode.F11, "EndingStateKey");

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(PatchStory));

            SceneManager.sceneLoaded += OnSceneLoaded;
            DontDestroyOnLoad(gameObject);

            Log(PluginName + " v" + PluginVersion + " loaded");
            Log("  " + SkipKey + "  = 跳过当前这段过场动画 (碰到对话会停)");
            Log("  " + DialogueSkipKey + " = 对话快进 开关");
            Log("  " + EndingKey + " = ★ 进入结局后状态 (全解锁, 跳过全部剧情)");

            Hint(SkipKey + " 跳过动画    " + DialogueSkipKey + " 快进对话    "
                 + EndingKey + " 直达结局后",
                 "三个功能都不会破坏正常游玩流程");
        }

        private static KeyCode ParseKey(string name, KeyCode fallback, string which)
        {
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), name, true); }
            catch
            {
                Log(which + " not recognized (" + name + "), falling back to " + fallback);
                return fallback;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log("Scene loaded: " + scene.name);
        }

        private void Update()
        {
            // ---- Tab: 按一下 = 跳过当前这段动画 (碰到对话自动解除) ----
            if (Input.GetKeyDown(SkipKey))
            {
                TabArmed = true;
                Log("Skip key pressed -> armed");
            }

            // ---- F10: 对话快进 开/关 ----
            if (Input.GetKeyDown(DialogueSkipKey))
            {
                DialogueSkip = !DialogueSkip;
                Hint(DialogueSkip ? "对话快进：开启" : "对话快进：关闭",
                     DialogueSkip ? "所有对话立即推进, 过场动画照常播放"
                                  : "按 " + SkipKey + " 可单独跳过一段过场动画");
                Log("Dialogue fast-forward: " + (DialogueSkip ? "ON" : "OFF"));
            }

            // ---- F11: 一键进入结局后状态 (全解锁) ----
            if (Input.GetKeyDown(EndingKey))
            {
                Log("Ending-state key pressed -> 进入结局后状态");
                Hint("正在进入「结局后」状态…", "全部内容解锁, 跳过剧情, 直达中枢");
                EndingState.Apply();
            }
        }

        private void OnGUI()
        {
            if (!CfgShowHint.Value) return;
            if (_hintMain.Length == 0) return;
            if (Time.unscaledTime > _hintUntil) return;

            // 最后 0.8 秒淡出
            float alpha = Mathf.Clamp01((_hintUntil - Time.unscaledTime) / 0.8f);

            var main = new GUIStyle(GUI.skin.label);
            main.fontSize = 20;
            main.fontStyle = FontStyle.Bold;
            main.alignment = TextAnchor.MiddleCenter;
            main.normal.textColor = new Color(1f, 1f, 1f, alpha);

            var sub = new GUIStyle(main);
            sub.fontSize = 15;
            sub.fontStyle = FontStyle.Normal;
            sub.normal.textColor = new Color(1f, 1f, 1f, 0.75f * alpha);

            float w = 720f;
            float x = (Screen.width - w) / 2f;
            float y = Screen.height - 120f;

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
            GUI.Box(new Rect(x, y - 6f, w, 62f), GUIContent.none, new GUIStyle(GUI.skin.box));
            GUI.color = old;

            GUI.Label(new Rect(x, y, w, 30f), _hintMain, main);
            if (_hintSub.Length > 0)
                GUI.Label(new Rect(x, y + 28f, w, 24f), _hintSub, sub);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    /// <summary>
    /// 唯一的 Harmony 补丁: 在 System_Story.Story() 之前, 视情况把 waitType 改成 "Skip"。
    ///
    /// 规则 (按优先级):
    ///   1. F10 对话快进 开 -> 只对 Click / Auto 生效 (对话, 不跳游戏内容)
    ///   2. Tab 已按下       -> 只对动画类生效 (Time/Sound/Video/Camera/Effect);
    ///                          碰到非动画行 -> 自动解除
    ///   3. 其它             -> 完全不动, 保持游戏原样
    ///
    /// 安全措施: 凡是 eventTag 里含 "Load" 的行一律放过。
    ///   这类行是「场景切换点」, 游戏故意让它停住, 等黑幕淡完再 LoadScene。
    ///   强行把它改成 "Skip" 会让剧情冲过切换点, 造成剧情与场景脱节
    ///   (实测表现为卡死 / 场景反复重载), 所以这里必须交给游戏自己处理。
    /// </summary>
    [HarmonyPatch(typeof(System_Story))]
    internal static class PatchStory
    {
        private static FieldInfo _waitType;
        private static FieldInfo _eventTag;
        private static FieldInfo _message;
        private static bool _loggedMissingField;

        [HarmonyPatch("Story")]
        [HarmonyPrefix]
        private static void Prefix(System_Story __instance)
        {
            if (!SkipPlugin.TabArmed && !SkipPlugin.DialogueSkip) return;

            if (_waitType == null) _waitType = AccessTools.Field(typeof(System_Story), "waitType");
            if (_waitType == null)
            {
                if (!_loggedMissingField)
                {
                    _loggedMissingField = true;
                    SkipPlugin.Log("field 'waitType' not found, skip disabled");
                }
                return;
            }

            string current = _waitType.GetValue(__instance) as string;

            // ---- 保护行: 握手点(System/*) 与场景切换点(Load/*) 一律放过 ----
            // 这两类行是剧情与场景的同步游标, 改写它们的 waitType 会导致
            // 冲过块尾 -> 剧情回到开头 / 场景反复重载 (见 WaitTypeRules 类注释)。
            if (CurrentRowIsProtected(__instance))
            {
                if (SkipPlugin.TabArmed)
                {
                    SkipPlugin.TabArmed = false;
                    SkipPlugin.Log("到达保护行(System/Load) -> 解除 Tab (交给游戏自己处理)");
                }
                return;
            }

            // 1) 对话快进 (F10): 只快进「真正有文本的 Click 行」。
            //    ★ 绝不碰 Auto ★ —— Auto 行是全无文本的握手/设置行, 且自带刹车
            //    (ExcelData 把它 autoNextIndex 关掉, 等窗口状态机放行);
            //    改成无形进量的 "Skip" 会拆掉这个刹车, 直接造成中枢 <-> Middle1 循环。
            if (SkipPlugin.DialogueSkip && WaitTypeRules.IsDialogue(current)
                && CurrentRowHasText(__instance))
            {
                _waitType.SetValue(__instance, "Skip");
                return;
            }

            // 2) 跳过动画 (Tab): 只处理动画类行
            if (SkipPlugin.TabArmed)
            {
                if (WaitTypeRules.IsAnimation(current))
                {
                    _waitType.SetValue(__instance, "Skip");
                    return;
                }

                SkipPlugin.TabArmed = false;
                SkipPlugin.Log("Reached non-animation waitType '" + current + "' -> skip disarmed");
            }
        }

        /// <summary>当前行是否是保护行 (eventTag 含 System 或 Load)。</summary>
        private static bool CurrentRowIsProtected(System_Story inst)
        {
            string tag = GetTag(inst);
            return WaitTypeRules.IsProtectedTag(tag);
        }

        /// <summary>当前行是否带文本 (即真正的对话行)。</summary>
        private static bool CurrentRowHasText(System_Story inst)
        {
            if (_message == null) _message = AccessTools.Field(typeof(System_Story), "data_Message");
            if (_message == null) return true;   // 取不到就保守当作有文本, 保持旧行为
            try
            {
                string m = _message.GetValue(inst) as string;
                return !string.IsNullOrEmpty(m);
            }
            catch { return false; }
        }

        private static string GetTag(System_Story inst)
        {
            if (_eventTag == null) _eventTag = AccessTools.Field(typeof(System_Story), "data_EventTag");
            if (_eventTag == null) return null;
            try { return _eventTag.GetValue(inst) as string; }
            catch { return null; }
        }
    }
}
