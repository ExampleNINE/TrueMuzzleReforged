using MuzzleFlash;
using UnityEngine;
using Verse;

namespace TrueMuzzle
{
    public class TrueMuzzleExtension : DefModExtension
    {
        // 枪口的偏移量 (基于像素扫描的微调或强制覆盖)
        public Vector2 muzzleOffset = Vector2.zero;
        public bool ignoreTrueMuzzle = false;

        // 【全新融合功能】：枪口火焰配置
        // 只需要在武器 XML 里指定火焰的 DefName，剩下的一切 TrueMuzzle 全自动解决！
        public MuzzleFlashDef flashDef;

        // 火焰大小缩放
        public Vector2 flashScale = new Vector2(1f, 1f);
    }
}