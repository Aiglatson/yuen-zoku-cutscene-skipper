using System;

namespace YuenSkip
{
    /// <summary>
    /// 纯逻辑, 不依赖 UnityEngine —— 方便单独做单元测试。
    ///
    /// ★ 这里的规则是「哪些行的 waitType 允许被改写」, 直接决定了会不会把剧情搞乱。★
    ///
    /// 背景 (离线解析 resources.assets 得到的剧情表实测结论):
    ///
    ///   全部 6 张表共 1654 行「有文本」的行, waitType **清一色是 Click**。
    ///   Auto 行里 **一个带文本的都没有** —— Auto 全是「无文本的握手/设置行」:
    ///       Bace_C: Auto 38 行 = BodyType 14 + System/true 24
    ///       Bace_D: Auto 17 行 = 空tag 9 + Face 8
    ///       Middle: Auto  1 行 = BodyType 1
    ///
    ///   而 Auto 行在游戏里是**自带刹车**的 (System_Story):
    ///       ExcelData()      : data_WaitType == "Auto"  -> autoNextIndex = false
    ///       Story() "Auto"   : if (!story_End && autoNextIndex) bace_Index++
    ///       Window_Main()    : 计时到了才 autoNextIndex = true
    ///   对比 case "Skip": 无条件 bace_Index++  ← 没有任何刹车
    ///
    ///   ★ 所以把 Auto 改成 Skip, 等于把刹车拆掉 ★
    ///     C1 中枢块 (C_デフォルト row 5..9 / C_クリア後 row 10..13) 的**末行正是
    ///     Auto + System/true**。一旦它被改成 Skip, 索引就会冲过块尾进入 C_0,
    ///     而 C_0 的末行是 Load/Middle1 -> 于是跑到 Middle1;
    ///     Middle 的 Mid1..Mid4/Mid7 末行是 Load/C1 -> 又回 C1 -> 又冲过中枢
    ///     => C1 <-> Middle1 无限往返, 并且 AddState_Middle 每轮把 state_Middle++
    ///        涨到 8 -> "Mid8" 不存在 -> SerchID 静默返回 0 -> 读 Mid0 = 剧情回到开头。
    ///
    /// 因此规则收紧为:
    ///   - 对话快进(F10): 只改 **Click 且有文本**的行。Auto 一律不碰。
    ///   - 跳过动画(Tab): 只改动画类行 (Time/Sound/Video/Camera/Effect)。
    ///   - System/* 与 Load/* 行: **任何功能都不得改写** (握手点 / 场景切换点)。
    /// </summary>
    public static class WaitTypeRules
    {
        /// <summary>这一行的等待类型是否属于「过场动画」</summary>
        public static bool IsAnimation(string waitType)
        {
            if (string.IsNullOrEmpty(waitType)) return false;
            switch (waitType)
            {
                case "Time":
                case "Sound":
                case "Video":
                case "Camera":
                case "Effect":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 这一行的等待类型是否是「需要玩家点击推进的对话」。
        ///
        /// ★ 只认 Click。Auto 不算对话 ★ —— 实测 Auto 行没有一行带文本,
        /// 它们是无文本的握手/设置行, 且自带刹车, 改了就会冲过块尾 (见类注释)。
        /// </summary>
        public static bool IsDialogue(string waitType)
        {
            if (string.IsNullOrEmpty(waitType)) return false;
            return waitType == "Click";
        }

        /// <summary>游戏自带的「无条件推进」行 —— 它自己会走, 不是停点。</summary>
        public static bool IsNativeSkip(string waitType)
        {
            return waitType == "Skip";
        }

        /// <summary>
        /// 这一行的 eventTag 是否是「保护行」: 握手点 (System/*) 或场景切换点 (Load/*)。
        ///
        /// 这两类行是剧情与场景同步的关键:
        ///   System/*  -> 等窗口状态机消费 systemSwitch / 置 story_End
        ///   Load/*    -> 等黑幕淡完由游戏自己 LoadScene
        /// 强行改它们的 waitType 会造成状态脱节 (卡死 / 场景反复重载 / 剧情回到开头)。
        /// </summary>
        public static bool IsProtectedTag(string eventTag)
        {
            if (string.IsNullOrEmpty(eventTag)) return false;
            return eventTag.Contains("System") || eventTag.Contains("Load");
        }
    }
}
