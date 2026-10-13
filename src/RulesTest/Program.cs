using System;
using YuenSkip;

class RulesTest
{
    static int fails;

    static void Check(string waitType, bool expectAnim, bool expectDlg, string note = "")
    {
        bool anim = WaitTypeRules.IsAnimation(waitType);
        bool dlg = WaitTypeRules.IsDialogue(waitType);
        bool ok = anim == expectAnim && dlg == expectDlg;
        if (!ok) fails++;
        Console.WriteLine((ok ? "PASS" : "FAIL") + "  '" + (waitType ?? "<null>") + "'"
            + " -> anim=" + anim + " (want " + expectAnim + ")"
            + ", dlg=" + dlg + " (want " + expectDlg + ")"
            + (note.Length > 0 ? "   // " + note : ""));
    }

    static void CheckTag(string tag, bool expectProtected, string note = "")
    {
        bool p = WaitTypeRules.IsProtectedTag(tag);
        bool ok = p == expectProtected;
        if (!ok) fails++;
        Console.WriteLine((ok ? "PASS" : "FAIL") + "  tag '" + (tag ?? "<null>") + "'"
            + " -> protected=" + p + " (want " + expectProtected + ")"
            + (note.Length > 0 ? "   // " + note : ""));
    }

    static int Main()
    {
        Console.WriteLine("== 动画类: Tab 可跳 ==");
        Check("Time", true, false);
        Check("Sound", true, false, "Middle 的 Mid0..Mid4/Mid7 入口行");
        Check("Video", true, false, "Opning 的 Op1 播片行");
        Check("Camera", true, false);
        Check("Effect", true, false);

        Console.WriteLine();
        Console.WriteLine("== 对话类: F10 只快进 Click, 绝不碰 Auto ==");
        Check("Click", false, true, "全部 1654 行有文本的行都是 Click");
        Check("Auto", false, false, "★ Auto 无一带文本, 且自带刹车, 改了会循环");

        Console.WriteLine();
        Console.WriteLine("== 保护行标签: System/Load 一律不得改写 ==");
        CheckTag("System/true", true, "C1 中枢块末行, 被改会冲过块尾");
        CheckTag("System/SubEnd", true, "Sub_C 块末行");
        CheckTag("Load/C1", true, "Middle 章节回跳点");
        CheckTag("Load/Middle1", true, "C_0/C_8 尾行, 被改会跑进 Middle");
        CheckTag("Load/Middle2", true, "Mid5 尾行");
        CheckTag("Load/D7", true, "Mid6 尾行");
        CheckTag("BodyType/auto", false, "纯外观设置行");
        CheckTag("Face/苦痛", false, "纯表情行");
        CheckTag("", false);
        CheckTag(null, false);

        Console.WriteLine();
        Console.WriteLine("== 特殊值 ==");
        Check("Skip", false, false, "游戏自带的无条件推进行");
        Check("", false, false);
        Check(null, false, false);
        Check("time", false, false, "大小写敏感, 与原版 switch 一致");
        Check("Garbage", false, false);

        Console.WriteLine();
        Console.WriteLine(fails == 0 ? "ALL PASS" : (fails + " FAILED"));
        return fails == 0 ? 0 : 1;
    }
}
