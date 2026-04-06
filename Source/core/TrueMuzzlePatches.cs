using HarmonyLib;
using MuzzleFlash;
using RimWorld;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace TrueMuzzle
{
    [StaticConstructorOnStartup]
    public static class TrueMuzzleMain
    {
        public static readonly ConditionalWeakTable<Thing, WeaponDrawData> DrawCache = new ConditionalWeakTable<Thing, WeaponDrawData>();
        public static readonly ConditionalWeakTable<Thing, RecoilData> RecoilCache = new ConditionalWeakTable<Thing, RecoilData>();

        static TrueMuzzleMain()
        {
            var harmony = new Harmony("com.truemuzzle.mod");
            harmony.PatchAll();
            Log.Message("[TrueMuzzle] 核心补丁已加载 - 弹道、火焰与真实后坐力系统已全线激活！");
        }
    }

    public class WeaponDrawData
    {
        public Vector3 Location;
        public float Angle;

        public WeaponDrawData(Vector3 loc, float ang)
        {
            Location = loc;
            Angle = ang;
        }
    }

    public class RecoilData
    {
        public float currentKick = 0f;
        public float currentAngle = 0f;

        public float kickVelocity = 0f;
        public float angleVelocity = 0f;

        public float lastFrameTime = 0f;
    }

    [HarmonyPatch(typeof(PawnRenderUtility), "DrawEquipmentAiming")]
    public static class Patch_DrawEquipmentAiming
    {
        public static void Prefix(Thing eq, ref Vector3 drawLoc, ref float aimAngle)
        {
            if (eq == null) return;

            if (TrueMuzzleMod.settings != null && TrueMuzzleMod.settings.enableRecoil)
            {
                var recoil = TrueMuzzleMain.RecoilCache.GetOrCreateValue(eq);

                if (Time.time != recoil.lastFrameTime)
                {
                    float deltaTime = Time.time - recoil.lastFrameTime;
                    recoil.lastFrameTime = Time.time;

                    recoil.currentKick = Mathf.SmoothDamp(recoil.currentKick, 0f, ref recoil.kickVelocity, 0.06f);
                    recoil.currentAngle = Mathf.SmoothDamp(recoil.currentAngle, 0f, ref recoil.angleVelocity, 0.08f);
                }

                if (recoil.currentKick > 0.001f || recoil.currentAngle > 0.1f)
                {
                    Vector3 kickDir = new Vector3(0, 0, -recoil.currentKick).RotatedBy(aimAngle);
                    drawLoc += kickDir;

                    bool isFlipped = aimAngle > 200f && aimAngle < 340f;
                    aimAngle += isFlipped ? recoil.currentAngle : -recoil.currentAngle;
                }
            }

            if (TrueMuzzleMain.DrawCache.TryGetValue(eq, out var data))
            {
                data.Location = drawLoc;
                data.Angle = aimAngle;
            }
            else
            {
                TrueMuzzleMain.DrawCache.Add(eq, new WeaponDrawData(drawLoc, aimAngle));
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), "Launch", typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef))]
    public static class Patch_Projectile_Launch
    {
        public class WeaponFlashData
        {
            public MuzzleFlashDef FlashDef;
            public Vector2 Scale;
        }

        private static readonly Dictionary<ThingDef, WeaponFlashData> FlashDataCache = new Dictionary<ThingDef, WeaponFlashData>();

        private static MuzzleFlashDef GetSmartFlashDef(Thing equipment, Projectile projectile)
        {
            if (equipment.def.techLevel < TechLevel.Industrial) return null;

            string weaponName = equipment.def.defName.ToLower();
            string projName = projectile.def.defName.ToLower();

            if (weaponName.Contains("beam") || projName.Contains("beam") ||
                weaponName.Contains("ray") || projName.Contains("ray") ||
                weaponName.Contains("bow") || projName.Contains("arrow") ||
                weaponName.Contains("javelin") || weaponName.Contains("pilum"))
                return null;

            if (weaponName.Contains("charge") || projName.Contains("charge") ||
                weaponName.Contains("plasma") || projName.Contains("plasma"))
                return DefDatabase<MuzzleFlashDef>.GetNamedSilentFail("MF_ChargedMuzzleFalsh");

            if (weaponName.Contains("smg") || weaponName.Contains("pistol") ||
                weaponName.Contains("revolver") || weaponName.Contains("pdw") ||
                weaponName.Contains("machinepi"))
                return DefDatabase<MuzzleFlashDef>.GetNamedSilentFail("MF_StandardMuzzleFalshTwo");

            if (weaponName.Contains("assault") || weaponName.Contains("rifle") || weaponName.Contains("carbine"))
                return DefDatabase<MuzzleFlashDef>.GetNamedSilentFail("MF_StandardMuzzleFalshThree");

            return DefDatabase<MuzzleFlashDef>.GetNamedSilentFail("MF_StandardMuzzleFalsh");
        }

        private static WeaponFlashData ResolveFlashData(Thing equipment, Projectile projectile)
        {
            WeaponFlashData data = new WeaponFlashData();

            var tmExt = equipment.def.GetModExtension<TrueMuzzleExtension>();
            if (tmExt != null && tmExt.ignoreTrueMuzzle) return data;

            if (tmExt != null && tmExt.flashDef != null)
            {
                data.FlashDef = tmExt.flashDef;
                data.Scale = tmExt.flashScale;
                return data;
            }

            var mfProps = equipment.def.GetModExtension<MuzzleFlashProps>();
            if (mfProps != null && mfProps.def != null)
            {
                data.FlashDef = mfProps.def;
                data.Scale = mfProps.drawSize;
                return data;
            }

            ThingDef bestBaseDef = null;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def != equipment.def && def.HasModExtension<MuzzleFlashProps>())
                {
                    if (equipment.def.defName.IndexOf(def.defName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (bestBaseDef == null || def.defName.Length > bestBaseDef.defName.Length)
                        {
                            bestBaseDef = def;
                        }
                    }
                }
            }

            if (bestBaseDef != null)
            {
                var baseProps = bestBaseDef.GetModExtension<MuzzleFlashProps>();
                if (baseProps != null)
                {
                    data.Scale = baseProps.drawSize;
                    data.FlashDef = baseProps.def != null ? baseProps.def : GetSmartFlashDef(equipment, projectile);
                    return data;
                }
            }

            data.FlashDef = GetSmartFlashDef(equipment, projectile);
            if (data.FlashDef != null) data.Scale = data.FlashDef.defaultSize;

            return data;
        }

        public static void Prefix(Projectile __instance, ref Vector3 origin, Thing launcher, Thing equipment)
        {
            if (equipment == null || launcher == null || launcher.Map == null) return;

            // =============================================================
            // 🌟 高级后坐力动能注入 (加入单发武器爆发力增强)
            // =============================================================
            if (TrueMuzzleMod.settings != null && TrueMuzzleMod.settings.enableRecoil)
            {
                var recoil = TrueMuzzleMain.RecoilCache.GetOrCreateValue(equipment);

                // 1. 质量感知系数
                float mass = equipment.GetStatValue(StatDefOf.Mass);
                if (mass <= 0) mass = 1f;
                float massFactor = Mathf.Clamp(Mathf.Pow(3.5f / mass, 0.5f), 0.4f, 2.0f);
                float strength = TrueMuzzleMod.settings.recoilStrength * massFactor;

                // 2. 射击模式感知：读取武器的开火模式
                int burstCount = 1;
                if (equipment.def.Verbs != null && equipment.def.Verbs.Count > 0)
                {
                    burstCount = equipment.def.Verbs[0].burstShotCount;
                }

                // 🌟 如果每次开火只射出一发子弹，给予 1.8 倍的瞬间动能加成！
                float singleShotMultiplier = (burstCount <= 1) ? 1.8f : 1.0f;

                // 🌟 如果是单发武器，临时放宽后退的硬性上限（放宽 1.5 倍），防止威力被限制住
                float maxKick = 0.6f * TrueMuzzleMod.settings.recoilStrength * (burstCount <= 1 ? 1.5f : 1.0f);
                float maxAngle = 40f * TrueMuzzleMod.settings.recoilStrength * (burstCount <= 1 ? 1.5f : 1.0f);

                // 将所有力量乘数融合进去
                recoil.currentKick = Mathf.Clamp(recoil.currentKick + (0.15f * strength * singleShotMultiplier), 0f, maxKick);
                recoil.currentAngle = Mathf.Clamp(recoil.currentAngle + (Rand.Range(6f, 12f) * strength * singleShotMultiplier), 0f, maxAngle);
            }

            if (!FlashDataCache.TryGetValue(equipment.def, out WeaponFlashData flashData))
            {
                flashData = ResolveFlashData(equipment, __instance);
                FlashDataCache[equipment.def] = flashData;
            }

            if (flashData.FlashDef == null) return;

            if (!TrueMuzzleMain.DrawCache.TryGetValue(equipment, out var drawData)) return;

            Vector3 actualDrawLoc = drawData.Location;
            if (Mathf.Abs(actualDrawLoc.x - launcher.DrawPos.x) < 0.05f && Mathf.Abs(actualDrawLoc.z - launcher.DrawPos.z) < 0.05f)
            {
                actualDrawLoc += new Vector3(0, 0, 0.4f).RotatedBy(drawData.Angle);
            }

            var tmExt = equipment.def.GetModExtension<TrueMuzzleExtension>();
            Vector2 offset2D = (tmExt != null && tmExt.muzzleOffset != Vector2.zero)
                ? tmExt.muzzleOffset
                : MuzzleScanner.GetMuzzleOffset(equipment);

            if (offset2D == Vector2.zero) return;

            bool isFlipped = drawData.Angle > 200f && drawData.Angle < 340f;
            Vector3 offset3D = new Vector3(offset2D.x, 0, isFlipped ? -offset2D.y : offset2D.y);
            offset3D = offset3D.RotatedBy(drawData.Angle - 90f);

            origin = actualDrawLoc + offset3D;

            Vector2 finalScale = flashData.Scale == Vector2.zero ? new Vector2(1f, 1f) : flashData.Scale;
            float pushDistance = finalScale.x * flashData.FlashDef.drawOffsetMultiplier.x;
            if (pushDistance == 0f) pushDistance = finalScale.x * 0.5f;

            Vector3 flashPushOffset = new Vector3(pushDistance, 0, 0).RotatedBy(drawData.Angle - 90f);
            Vector3 flashPos = origin + flashPushOffset;
            flashPos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.5f;

            // 隐藏枪口火焰设置的判断
            if (TrueMuzzleMod.settings == null || !TrueMuzzleMod.settings.hideMuzzleFlash)
            {
                launcher.Map.GetComponent<MapComponent_MuzzleFlashManager>().RegisterEntity(
                    new MuzzleFlashEntity(flashData.FlashDef, flashPos, drawData.Angle, finalScale)
                );
            }
        }
    }
}