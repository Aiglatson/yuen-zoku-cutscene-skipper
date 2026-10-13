using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YuenSkip
{
    /// <summary>
    /// 「一键进入结局后状态」—— 把游戏状态摆成「已通关 + 全部可玩内容解锁」,
    /// 然后跳到 C1 中枢, 之后一切交还给游戏自己。
    ///
    /// ★ 设计原则: 只写「游戏自己的状态字段」, 绝不碰剧情引擎的推进 ★
    ///
    ///   本类只做两件事:
    ///     1) 把 System_Var 的解锁表 / 关卡号 / 通关标记, 设成「通关后该有的值」
    ///     2) 设 LoadSceneName = "C1", 然后 LoadScene("Loder") 跳过去
    ///
    ///   跳过去之后不再干预: C1 中枢会自己按这些字段决定读哪一段、开哪些按钮。
    ///
    /// ★ 为什么这样不会「剧情回到开头」★
    ///
    ///   那个 bug 的成因是「强行把剧情行往下推」: 越过中枢块 -> 冲进 C_0 ->
    ///   跑到尾行 Load/Middle1 -> Middle 表按 "Mid"+state_Middle 取块 ->
    ///   取不到 -> SerchID 静默返回 0 -> 从头开始。
    ///   本实现从不调用 ExcelData/Story, 也不改 bace_Index, 所以没有这个入口。
    ///
    /// ★ 状态取值的依据 (都能在游戏自己的数据里核对) ★
    ///
    ///   1) 中枢选块判据 —— System_Story.ResourcesLoad():
    ///        C1 场景下:  state_C_Old == state_C  ->  "C_クリア後"   (通关后中枢) ← 要这个
    ///                    state_C_Old != state_C  ->  "C_" + state_C
    ///      所以目标是 C=10 且 C_Old == C。
    ///
    ///   2) 剧情表真实块 —— 离线解析游戏资源 (Yuen-Zoku_Data/resources.assets) 得到:
    ///        Bace_C: 524 行 / 28 块, 真实块头为
    ///          C_デバッグ C_デフォルト C_クリア後 C_0 C_1 ... C_10
    ///        "C_クリア後" 起于第 10 行, 内容为通关后中枢的 3 行台词。
    ///
    ///   3) 各解锁表的目标取值 —— 对照一份「正常通关存档样本」
    ///      (仅作目标状态参照, 不会覆盖玩家存档)。
    ///      有这份样本说明: 这个状态是游戏自己正常游玩能到达的, 不是硬造出来的。
    /// </summary>
    internal static class EndingState
    {
        // ---- 目标值 ----
        private const int C_Goal  = 10;    // C 线通关章节
        private const int D_Goal  = 12;    // D 线通关进度
        private const int Middle  = 8;     // 逃脱章进度 (避开 ==7 时被路由回 Middle1)
        private const int Escape  = 4;     // 逃脱状态
        private const int Level   = 10;    // 每项性感带等级上限
        private const int Exp     = 1869;  // 10 级的规范化经验值

        internal static bool Apply()
        {
            try
            {
                var log = new System.Text.StringBuilder();
                log.Append("=== 一键进入结局后状态 ===\n");

                // ---------- 1) D 舞台: 全部可点 ----------
                // C_System_UI_Select 判据: StageUnlock["Dn"] == 2 才可点
                FillInt(System_Var.StageUnlock, 2, log, "StageUnlock");

                // ---------- 2) 玩法: 全部解锁 (-1 = 该位置不适用, 必须保留) ----------
                if (System_Var.PlayStyleUnlock != null)
                {
                    int usable = 0, changed = 0;
                    var keys = new List<string>(System_Var.PlayStyleUnlock.Keys);
                    foreach (string k in keys)
                    {
                        int v = System_Var.PlayStyleUnlock[k];
                        if (v < 0) continue;              // -1 不适用, 不能动
                        usable++;
                        if (v != 1) { System_Var.PlayStyleUnlock[k] = 1; changed++; }
                    }
                    System_Var.PlayStyleUnlock_AllCount = usable;
                    log.Append("  PlayStyleUnlock: 可用 " + usable + " 项全解锁 (新解锁 " + changed
                        + "), AllCount=" + usable + "\n");
                }

                // ---------- 3) 技能 / 爱抚道具 ----------
                FillInt(System_Var.Skill, 1, log, "Skill");
                FillInt(System_Var.Skill_StorySwitch_C, 1, log, "Skill_StorySwitch_C");
                FillInt(System_Var.aibuItem, 1, log, "aibuItem");
                FillInt(System_Var.aibuItem_StorySwitch_C, 1, log, "aibuItem_StorySwitch_C");

                // D 线子剧情开关 (2 = 已播过)
                SetKeys(System_Var.Skill_StorySwitch_D, new string[] {
                    "Zettixyou_Manko","FinishZettixyou_Manko","Zettixyou_Anaru",
                    "KantixyouMania","Zettixyou_Kuri","BigKuri","Zettixyou_Titi",
                    "MirukuTiti","MirukuTiti2","FinishZettixyou_Titi","Zettixyou_Other",
                    "Hounixyou","AheMaster","Mazo"
                }, 2, log, "Skill_StorySwitch_D=2");

                // 通关后开放「技能开关」(见 C_System_UI_Information.Add_Skill_Toggle)
                System_Var.SkillToggleUnlock = true;
                for (int i = 0; i < System_Var.config_SkillOff.Length; i++)
                    System_Var.config_SkillOff[i] = false;

                // ---------- 4) 装备持有量 (C_System_UI_Soubi 判据: get_ItemCount >= 1) ----------
                var item = new Dictionary<string, int>();
                item["Roter_Anaru_Nomal"] = 5; item["Roter_Anaru_Strong"] = 5; item["Roter_Anaru_Elect"] = 5;
                item["Roter_Manko_Nomal"] = 5; item["Roter_Manko_Strong"] = 5; item["Roter_Manko_Elect"] = 5;
                item["Roter_Kuri_Nomal"]  = 1; item["Roter_Kuri_Strong"]  = 1; item["Roter_Kuri_Elect"]  = 1;
                item["Roter_Titi_Nomal"]  = 1; item["Roter_Titi_Strong"]  = 1; item["Roter_Titi_Elect"]  = 1;
                item["Hounixyouki"] = 1; item["DoubleBaibu"] = 1;
                item["DoubleBaibu_Liquid_Manko"] = 1; item["DoubleBaibu_Liquid_Anaru"] = 1;
                item["Waight"] = 3; item["Camera"] = 1; item["Nouha"] = 1; item["TClip_A"] = 1;
                item["Sakunixyuuki_A"] = 1; item["Sakunixyuuki_B"] = 1;
                item["ElectricPad"] = 1; item["KuriKeninki_Nomal"] = 1;
                item["KuriKeninki_Role"] = 1; item["KuriKeninki_Elect"] = 1;
                item["SindouBaibu_Anaru"] = 1; item["SindouBaibu_Manko"] = 1;
                item["Mekakusi"] = 1; item["Hanakeninki"] = 1; item["KantixyouBaibu"] = 1;
                item["AnaruWasher"] = 1; item["MilkDrinker"] = 1; item["AiekiDrinker"] = 1;
                SetDict(System_Var.get_ItemCount, item, log, "get_ItemCount");

                // Item_StorySwitch_C = 「解锁提示已在 C 线播过」的记账表
                var isc = new Dictionary<string, int>();
                foreach (string k in new List<string>(System_Var.Item_StorySwitch_C.Keys))
                    isc[k] = 1;
                isc["Roter_Anaru"] = 0; isc["Roter_Manko"] = 0; isc["Waight"] = 0;
                SetDict(System_Var.Item_StorySwitch_C, isc, log, "Item_StorySwitch_C");

                // ---------- 5) 等级 (10 级封顶) ----------
                SetStatus(System_Var.Status, Level, log);
                SetStatusOld(System_Var.Status_Old, Level, log);
                if (System_Var.StatusExp_All != null)
                {
                    var keys = new List<string>(System_Var.StatusExp_All.Keys);
                    foreach (string k in keys) System_Var.StatusExp_All[k] = Exp;
                }
                System_Var.slaveLv_All_Old   = Level * 5;
                System_Var.slaveLv_Anaru_Old = Level;
                System_Var.slaveLv_Manko_Old = Level;
                System_Var.slaveLv_Kuri_Old  = Level;
                System_Var.slaveLv_Titi_Old  = Level;
                System_Var.slaveLv_Other_Old = Level;
                log.Append("  等级: 全项目 " + Level + " 级 (合计 " + (Level * 5) + "), 经验 " + Exp + "\n");

                // ---------- 6) 剧情进度 / 通关标记 ----------
                System_Var.event_Opning  = true;    // 不再播开场
                System_Var.event_Ending  = true;    // 通关
                System_Var.event_Ending2 = true;    // 通关后: 中枢直接开菜单, 不弹调教更新窗
                System_Var.state_C       = C_Goal;
                System_Var.state_C_Old   = C_Goal;  // ★ 相等 -> ResourcesLoad 读 "C_クリア後"
                System_Var.state_C_Goal  = C_Goal;
                System_Var.state_D       = D_Goal;
                System_Var.state_D_Goal  = D_Goal;
                System_Var.state_Middle  = Middle;
                System_Var.event_Escape_State    = Escape;
                System_Var.config_OnDo_GameClear = true;   // 存进 OtherSettingData

                log.Append("  剧情: state_C=" + C_Goal + " 且 C_Old==C -> 中枢读 C_クリア後\n");
                log.Append("        state_D=" + D_Goal + ", state_Middle=" + Middle
                    + ", event_Ending/Ending2=true\n");

                // D7 退出闸门: System_UI_Main 要求 playMessage[7,2,1..2]==2 才允许离开 D7
                int[,,] pm = System_Var.playMessage;
                if (pm != null && pm.GetLength(0) > 7 && pm.GetLength(1) > 2 && pm.GetLength(2) > 2)
                {
                    pm[7, 2, 1] = 2;
                    pm[7, 2, 2] = 2;
                    log.Append("  playMessage[7,2,1..2]=2 (解除 D7 退出限制)\n");
                }

                // ---------- 7) 新物品角标全部置为「已看过」----------
                ZeroToTwo(System_Var.newIcon_StageUnlock);
                ZeroToTwo(System_Var.newIcon_aibuItemUnlock);
                ZeroToTwo(System_Var.newIcon_PlayStyleUnlock_D1);
                ZeroToTwo(System_Var.newIcon_PlayStyleUnlock_D2);
                ZeroToTwo(System_Var.newIcon_PlayStyleUnlock_D3);
                ZeroToTwo(System_Var.newIcon_PlayStyleUnlock_D4);
                ZeroToTwo(System_Var.newIcon_PlayStyleUnlock_D5);
                ZeroToTwo(System_Var.newIcon_PlayStyleUnlock_D6);
                ZeroToTwo(System_Var.newIcon_PlayStyleUnlock_D7);
                ZeroToTwo(System_Var.newIcon_GetItemCount_D1);
                ZeroToTwo(System_Var.newIcon_GetItemCount_D2);
                ZeroToTwo(System_Var.newIcon_GetItemCount_D3);
                ZeroToTwo(System_Var.newIcon_GetItemCount_D4);
                ZeroToTwo(System_Var.newIcon_GetItemCount_D5);
                ZeroToTwo(System_Var.newIcon_GetItemCount_D6);
                ZeroToTwo(System_Var.newIcon_GetItemCount_D7);

                // ---------- 8) 日记 ----------
                var diary = new Dictionary<string, int>();
                diary["Zetixyou_MankoCount"]     = 24;
                diary["Zetixyou_AnaruCount"]     = 16;
                diary["Zetixyou_KuritorisuCount"] = 5;
                diary["Zetixyou_TitiCount"]      = 13;
                diary["Zetixyou_OtherCount"]     = 8;
                diary["SixyaseiCount"]           = 119;
                SetDict(System_Var.Diary, diary, log, "Diary");

                // ---------- 9) 立即写盘 ----------
                System_Var.Data_Save("Story");
                System_Var.Data_Save("Config");
                System_Var.Data_Save("OtherSetting");
                log.Append("  已写回 storyData / configData / OtherSettingData\n");

                // ---------- 10) 跳到 C1 中枢 ----------
                System_Var.LoadSceneName = "C1";
                SceneManager.LoadScene("Loder");
                log.Append("  跳转 -> C1 (经 Loder)\n");

                SkipPlugin.Log(log.ToString());
                return true;
            }
            catch (Exception e)
            {
                SkipPlugin.Log("进入结局后状态失败: " + e);
                return false;
            }
        }

        // ---------- 小工具 ----------

        /// <summary>把 Status 各项设为 lv; 合计项设为 lv*5 (Set_SlaveLevel 会自己重算)。</summary>
        private static void SetStatus(Dictionary<string, int> d, int lv, System.Text.StringBuilder log)
        {
            if (d == null) return;
            foreach (string k in new List<string>(d.Keys))
                d[k] = k.EndsWith("_All", StringComparison.Ordinal) ? lv * 5 : lv;
            log.Append("  Status: " + d.Count + " 项 -> " + lv + " 级\n");
        }

        /// <summary>Status_Old 里 SlaveLv_All 保持 0 (与通关样本一致), 其余 = lv。</summary>
        private static void SetStatusOld(Dictionary<string, int> d, int lv, System.Text.StringBuilder log)
        {
            if (d == null) return;
            foreach (string k in new List<string>(d.Keys))
                d[k] = k.EndsWith("_All", StringComparison.Ordinal) ? 0 : lv;
            log.Append("  Status_Old: " + d.Count + " 项\n");
        }

        private static void FillInt(Dictionary<string, int> d, int v, System.Text.StringBuilder log, string name)
        {
            if (d == null) return;
            foreach (string k in new List<string>(d.Keys)) d[k] = v;
            log.Append("  " + name + ": " + d.Count + " 项 -> " + v + "\n");
        }

        private static void SetKeys(Dictionary<string, int> d, string[] keys, int v,
                                    System.Text.StringBuilder log, string name)
        {
            if (d == null) return;
            int n = 0;
            foreach (string k in keys)
                if (d.ContainsKey(k)) { d[k] = v; n++; }
            log.Append("  " + name + ": " + n + " 项\n");
        }

        private static void SetDict(Dictionary<string, int> d, Dictionary<string, int> src,
                                    System.Text.StringBuilder log, string name)
        {
            if (d == null) return;
            int n = 0;
            foreach (var kv in src)
                if (d.ContainsKey(kv.Key)) { d[kv.Key] = kv.Value; n++; }
            log.Append("  " + name + ": 写入 " + n + " 项\n");
        }

        private static void ZeroToTwo(int[] a)
        {
            if (a == null) return;
            for (int i = 0; i < a.Length; i++) a[i] = 2;
        }
    }
}
