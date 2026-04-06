using RimWorld;
using UnityEngine;
using Verse;

namespace TrueMuzzle
{
    // 这个类负责保存玩家的设置数据到硬盘
    public class TrueMuzzleSettings : ModSettings
    {
        public bool enableRecoil = true;
        public float recoilStrength = 1.0f;

        // 隐藏枪口火焰功能
        public bool hideMuzzleFlash = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableRecoil, "enableRecoil", true);
            Scribe_Values.Look(ref recoilStrength, "recoilStrength", 1.0f);
            Scribe_Values.Look(ref hideMuzzleFlash, "hideMuzzleFlash", false);
            base.ExposeData();
        }
    }

    // 这个类负责在游戏的 Mod 选项里画出 UI 界面
    public class TrueMuzzleMod : Mod
    {
        public static TrueMuzzleSettings settings;

        public TrueMuzzleMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<TrueMuzzleSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // --- 视觉设置区域 ---
            listing.Label("<b><color=#e0e0e0>--- 视觉效果 (Visuals) ---</color></b>");
            listing.CheckboxLabeled("隐藏枪口火焰 (Hide Muzzle Flash)", ref settings.hideMuzzleFlash,
                "开启后，将完全不再渲染任何枪口火焰，但仍然保留精准的弹道起始点修复与后坐力物理效果。");

            listing.Gap(16f);

            // --- 物理设置区域 ---
            listing.Label("<b><color=#e0e0e0>--- 物理反馈 (Physics) ---</color></b>");
            listing.CheckboxLabeled("启用真实后坐力 (Enable True Recoil)", ref settings.enableRecoil,
                "开启后，武器开火时会有逼真的向后顿挫和枪口上抬，并附带平滑的弹簧阻尼回弹效果。");

            if (settings.enableRecoil)
            {
                listing.Gap(6f);
                listing.Label($"后坐力强度乘数 (Recoil Strength): {settings.recoilStrength.ToStringPercent()}");
                settings.recoilStrength = listing.Slider(settings.recoilStrength, 0.1f, 3.0f);

                // 🌟 核心修复：使用原生的富文本颜色标签，彻底消灭了对 GUI.color 的依赖，永不报错！
                listing.Label("<color=#a0a0a0>系统提示：当前后坐力引擎已启用 [质量感知] 与 [高射速叠加]。</color>");
                listing.Label("<color=#a0a0a0> - 武器的物理重量(Mass)会自动影响后坐力大小：枪越重越沉稳，越轻越跳跃。</color>");
                listing.Label("<color=#a0a0a0> - 高射速武器(如机枪)在持续开火时，后坐力会自然叠加直至达到上限。</color>");
            }

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "TrueMuzzle: Reforged";
        }
    }
}