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

            // ========================================================
            // 🌟 3. O(1) 极速精准血统认祖 (完美利用 _Unique 规律)
            // ========================================================
            string defName = equipment.def.defName;
            if (defName.EndsWith("_Unique"))
            {
                // 瞬间切掉后面的 "_Unique" (7个字符)，得到原版武器名
                string baseName = defName.Substring(0, defName.Length - 7);

                // 直接使用字典在微秒级内定位原始武器数据！不需要再遍历几十万条字符串了！
                ThingDef exactBaseDef = DefDatabase<ThingDef>.GetNamedSilentFail(baseName);
                if (exactBaseDef != null)
                {
                    var baseProps = exactBaseDef.GetModExtension<MuzzleFlashProps>();
                    if (baseProps != null)
                    {
                        data.Scale = baseProps.drawSize;
                        data.FlashDef = baseProps.def != null ? baseProps.def : GetSmartFlashDef(equipment, projectile);
                        return data;
                    }
                }
            }

            // ========================================================
            // 🌟 4. 模糊追踪保底 (兼容那些后缀不叫 _Unique 的非官方 Mod 变体武器)
            // ========================================================
            ThingDef bestBaseDef = null;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def != equipment.def && def.HasModExtension<MuzzleFlashProps>())
                {
                    if (defName.IndexOf(def.defName, System.StringComparison.OrdinalIgnoreCase) >= 0)
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

            // 5. 智能引擎最终保底
            data.FlashDef = GetSmartFlashDef(equipment, projectile);
            if (data.FlashDef != null) data.Scale = data.FlashDef.defaultSize;

            return data;
        }

        public static void Prefix(Projectile __instance, ref Vector3 origin, Thing launcher, Thing equipment)
        {
            if (equipment == null || launcher == null || launcher.Map == null) return;

            if (TrueMuzzleMod.settings != null && TrueMuzzleMod.settings.enableRecoil)
            {
                var recoil = TrueMuzzleMain.RecoilCache.GetOrCreateValue(equipment);

                float mass = equipment.GetStatValue(StatDefOf.Mass);
                if (mass <= 0) mass = 1f;
                float massFactor = Mathf.Clamp(Mathf.Pow(3.5f / mass, 0.5f), 0.4f, 2.0f);
                float strength = TrueMuzzleMod.settings.recoilStrength * massFactor;

                int burstCount = 1;
                if (equipment.def.Verbs != null && equipment.def.Verbs.Count > 0)
                {
                    burstCount = equipment.def.Verbs[0].burstShotCount;
                }

                float singleShotMultiplier = (burstCount <= 1) ? 1.8f : 1.0f;

                float maxKick = 0.6f * TrueMuzzleMod.settings.recoilStrength * (burstCount <= 1 ? 1.5f : 1.0f);
                float maxAngle = 40f * TrueMuzzleMod.settings.recoilStrength * (burstCount <= 1 ? 1.5f : 1.0f);

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

            if (TrueMuzzleMod.settings == null || !TrueMuzzleMod.settings.hideMuzzleFlash)
            {
                launcher.Map.GetComponent<MapComponent_MuzzleFlashManager>().RegisterEntity(
                    new MuzzleFlashEntity(flashData.FlashDef, flashPos, drawData.Angle, finalScale)
                );
            }
        }
    }
}