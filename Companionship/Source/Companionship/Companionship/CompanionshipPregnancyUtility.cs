using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Companionship
{
    /// <summary>
    /// Applies a Biotech-style pregnancy roll for Companionship's custom lovin completion.
    /// Key rule for this mod: treat the encounter as "Avoid pregnancy" regardless of the pawns'
    /// current pregnancy approach settings. This reduces the chance but does not eliminate it.
    ///
    /// Implementation uses reflection to stay resilient across RimWorld versions and avoids hard
    /// coupling to internal method signatures. When Biotech is inactive, this is a no-op.
    /// </summary>
    public static class CompanionshipPregnancyUtility
    {
        private static bool initialized;

        private static Type pregnancyUtilityType;
        private static Type pregnancyApproachType;
        private static object avoidPregnancyValue;

        // Prefer a bool-returning "Try*" method if available (assumed to internally handle chance).
        private static MethodInfo tryMethodBool;

        // Float-returning chance method (used if no bool "Try*" method is found).
        private static MethodInfo chanceMethodFloat;

        // Void method that starts pregnancy (used only after our own chance roll).
        private static MethodInfo startMethodVoid;

        // Used to temporarily override pawn pregnancy approach (optional).
        private static FieldInfo approachField;
        private static PropertyInfo approachProperty;

        public static void TryApplyPregnancy(Pawn pawnA, Pawn pawnB)
        {
            if (!ModsConfig.BiotechActive)
                return;

            if (pawnA == null || pawnB == null)
                return;

            if (pawnA.DestroyedOrNull() || pawnB.DestroyedOrNull() || pawnA.Dead || pawnB.Dead)
                return;

            if (pawnA.RaceProps == null || pawnB.RaceProps == null || !pawnA.RaceProps.Humanlike || !pawnB.RaceProps.Humanlike)
                return;

            // Vanilla human pregnancy requires a male/female pairing.
            if (!TryGetMotherAndFather(pawnA, pawnB, out Pawn mother, out Pawn father))
                return;

            EnsureInitialized();
            if (pregnancyUtilityType == null)
                return;

            // Always treat as "Avoid pregnancy" for this encounter.
            object oldMotherApproach = null;
            object oldFatherApproach = null;
            bool changedMother = false;
            bool changedFather = false;

            try
            {
                if (avoidPregnancyValue != null)
                {
                    changedMother = TryOverridePregnancyApproach(mother, avoidPregnancyValue, out oldMotherApproach);
                    changedFather = TryOverridePregnancyApproach(father, avoidPregnancyValue, out oldFatherApproach);
                }

                // Best case: bool-returning Try* method exists that does the full vanilla logic.
                if (tryMethodBool != null)
                {
                    InvokePregnancyMethod(tryMethodBool, mother, father, passApproachIfSupported: true);
                    return; // avoid double-chance
                }

                // Fallback: compute chance then start pregnancy.
                if (chanceMethodFloat == null || startMethodVoid == null)
                    return;

                float chance = InvokePregnancyChance(chanceMethodFloat, mother, father, passApproachIfSupported: true);
                if (chance <= 0f)
                    return;

                if (!Rand.Chance(chance))
                    return;

                InvokePregnancyMethod(startMethodVoid, mother, father, passApproachIfSupported: true);
            }
            catch (Exception ex)
            {
                // Never break the date pipeline if pregnancy logic fails.
                if (CompanionshipDebug.VerboseLogging)
                    Log.Message("[Companionship] Pregnancy attempt failed (non-fatal): " + ex);
            }
            finally
            {
                if (changedMother) RestorePregnancyApproach(mother, oldMotherApproach);
                if (changedFather) RestorePregnancyApproach(father, oldFatherApproach);
            }
        }

        private static bool TryGetMotherAndFather(Pawn a, Pawn b, out Pawn mother, out Pawn father)
        {
            mother = null;
            father = null;

            if (a.gender == Gender.Female && b.gender == Gender.Male)
            {
                mother = a;
                father = b;
                return true;
            }

            if (a.gender == Gender.Male && b.gender == Gender.Female)
            {
                mother = b;
                father = a;
                return true;
            }

            return false;
        }

        private static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            pregnancyUtilityType = AccessTools.TypeByName("RimWorld.PregnancyUtility");
            pregnancyApproachType = AccessTools.TypeByName("RimWorld.PregnancyApproach");

            if (pregnancyApproachType != null && pregnancyApproachType.IsEnum)
            {
                try
                {
                    avoidPregnancyValue = Enum.Parse(pregnancyApproachType, "AvoidPregnancy");
                }
                catch
                {
                    avoidPregnancyValue = null;
                }
            }

            // Try-method candidates (bool): likely does chance internally.
            tryMethodBool = FindBestMethod(
                pregnancyUtilityType,
                new[]
                {
                    "TryImpregnate",
                    "TryStartPregnancy",
                    "TryMakePregnant",
                    "LovinWillMakePregnant",
                    "TryDoLovinPregnancy",
                    "TryDoPregnancy"
                },
                typeof(bool));

            // Chance method candidates (float): used for fallback.
            chanceMethodFloat = FindBestMethod(
                pregnancyUtilityType,
                new[]
                {
                    "PregnancyChanceForPartners",
                    "GetPregnancyChanceForPartners",
                    "PregnancyChanceFor",
                    "GetPregnancyChance",
                    "LovinPregnancyChance",
                    "PregnancyChanceForLovin"
                },
                typeof(float));

            // Start method candidates (void): used after rolling chance ourselves.
            startMethodVoid = FindBestMethod(
                pregnancyUtilityType,
                new[]
                {
                    "StartPregnancy",
                    "MakePregnant",
                    "ApplyPregnancy",
                    "StartPregnancyForLovin"
                },
                typeof(void));

            // Pregnancy approach storage (optional): typically on Pawn_RelationsTracker.
            try
            {
                approachField = AccessTools.Field(typeof(Pawn_RelationsTracker), "pregnancyApproach");
                approachProperty = AccessTools.Property(typeof(Pawn_RelationsTracker), "PregnancyApproach")
                                  ?? AccessTools.Property(typeof(Pawn_RelationsTracker), "pregnancyApproach");
            }
            catch
            {
                approachField = null;
                approachProperty = null;
            }
        }

        private static MethodInfo FindBestMethod(Type type, string[] candidateNames, Type returnType)
        {
            if (type == null) return null;

            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods;
            try
            {
                methods = type.GetMethods(flags);
            }
            catch
            {
                return null;
            }

            // Build a list of all methods matching any candidate name + return type.
            List<MethodInfo> matches = new List<MethodInfo>(8);

            for (int i = 0; i < candidateNames.Length; i++)
            {
                string name = candidateNames[i];
                if (string.IsNullOrEmpty(name)) continue;

                foreach (MethodInfo m in methods)
                {
                    if (m == null) continue;
                    if (m.Name != name) continue;
                    if (m.ReturnType != returnType) continue;

                    ParameterInfo[] ps = m.GetParameters();
                    if (ps == null || ps.Length < 2) continue;
                    if (ps[0].ParameterType != typeof(Pawn) || ps[1].ParameterType != typeof(Pawn)) continue;

                    // Only accept methods where all extra params are of a "safe to default" type.
                    bool ok = true;
                    for (int p = 2; p < ps.Length; p++)
                    {
                        Type pt = ps[p].ParameterType;

                        if (pregnancyApproachType != null && pt == pregnancyApproachType) continue;
                        if (pt == typeof(float)) continue;
                        if (pt == typeof(int)) continue;
                        if (pt == typeof(bool)) continue;

                        ok = false;
                        break;
                    }

                    if (!ok) continue;

                    matches.Add(m);
                }
            }

            if (matches.Count == 0) return null;

            // Prefer fewer parameters (less ambiguity) and public methods (more stable).
            matches.Sort((a, b) =>
            {
                int pa = a.GetParameters().Length;
                int pb = b.GetParameters().Length;
                int c = pa.CompareTo(pb);
                if (c != 0) return c;

                bool apub = a.IsPublic;
                bool bpub = b.IsPublic;
                if (apub != bpub) return bpub.CompareTo(apub);

                return 0;
            });

            return matches[0];
        }

        private static bool TryOverridePregnancyApproach(Pawn pawn, object value, out object oldValue)
        {
            oldValue = null;

            if (pawn == null || pawn.relations == null) return false;
            if (pregnancyApproachType == null || value == null) return false;

            try
            {
                if (approachField != null && approachField.FieldType == pregnancyApproachType)
                {
                    oldValue = approachField.GetValue(pawn.relations);
                    approachField.SetValue(pawn.relations, value);
                    return true;
                }

                if (approachProperty != null &&
                    approachProperty.PropertyType == pregnancyApproachType &&
                    approachProperty.CanRead &&
                    approachProperty.CanWrite)
                {
                    oldValue = approachProperty.GetValue(pawn.relations, null);
                    approachProperty.SetValue(pawn.relations, value, null);
                    return true;
                }
            }
            catch
            {
                // swallow
            }

            return false;
        }

        private static void RestorePregnancyApproach(Pawn pawn, object oldValue)
        {
            if (pawn == null || pawn.relations == null) return;
            if (pregnancyApproachType == null) return;

            try
            {
                if (approachField != null && approachField.FieldType == pregnancyApproachType)
                {
                    approachField.SetValue(pawn.relations, oldValue);
                    return;
                }

                if (approachProperty != null &&
                    approachProperty.PropertyType == pregnancyApproachType &&
                    approachProperty.CanWrite)
                {
                    approachProperty.SetValue(pawn.relations, oldValue, null);
                    return;
                }
            }
            catch
            {
                // swallow
            }
        }

        private static void InvokePregnancyMethod(MethodInfo method, Pawn mother, Pawn father, bool passApproachIfSupported)
        {
            if (method == null) return;

            ParameterInfo[] ps = method.GetParameters();
            if (ps == null || ps.Length < 2) return;

            bool wantsMotherFirst = WantsMotherFirst(ps);
            Pawn first = wantsMotherFirst ? mother : father;
            Pawn second = wantsMotherFirst ? father : mother;

            object[] args = BuildArgs(ps, first, second, passApproachIfSupported);
            if (args == null) return;

            method.Invoke(null, args);
        }

        private static float InvokePregnancyChance(MethodInfo method, Pawn mother, Pawn father, bool passApproachIfSupported)
        {
            if (method == null) return 0f;

            ParameterInfo[] ps = method.GetParameters();
            if (ps == null || ps.Length < 2) return 0f;

            bool wantsMotherFirst = WantsMotherFirst(ps);
            Pawn first = wantsMotherFirst ? mother : father;
            Pawn second = wantsMotherFirst ? father : mother;

            object[] args = BuildArgs(ps, first, second, passApproachIfSupported);
            if (args == null) return 0f;

            object result = method.Invoke(null, args);
            if (result is float f) return f;
            return 0f;
        }

        private static object[] BuildArgs(ParameterInfo[] ps, Pawn first, Pawn second, bool passApproachIfSupported)
        {
            object[] args = new object[ps.Length];

            args[0] = first;
            args[1] = second;

            for (int i = 2; i < ps.Length; i++)
            {
                Type pt = ps[i].ParameterType;
                string pn = ps[i].Name ?? string.Empty;

                if (pregnancyApproachType != null && pt == pregnancyApproachType)
                {
                    if (!passApproachIfSupported || avoidPregnancyValue == null)
                        return null;

                    args[i] = avoidPregnancyValue;
                    continue;
                }

                if (pt == typeof(float))
                {
                    args[i] = 1f; // neutral multiplier
                    continue;
                }

                if (pt == typeof(int))
                {
                    args[i] = 0;
                    continue;
                }

                if (pt == typeof(bool))
                {
                    // Heuristic defaults:
                    // - If it's an "use/apply/respect/check" style flag -> true
                    // - If it's "force/ignore" style -> false
                    bool val = true;

                    if (pn.IndexOf("force", StringComparison.OrdinalIgnoreCase) >= 0) val = false;
                    if (pn.IndexOf("ignore", StringComparison.OrdinalIgnoreCase) >= 0) val = false;

                    args[i] = val;
                    continue;
                }

                // Unknown extra parameter -> don't invoke.
                return null;
            }

            return args;
        }

        private static bool WantsMotherFirst(ParameterInfo[] ps)
        {
            if (ps == null || ps.Length < 2) return true;

            string p0 = ps[0].Name ?? string.Empty;
            string p1 = ps[1].Name ?? string.Empty;

            bool p0LooksLikeMother = p0.IndexOf("mother", StringComparison.OrdinalIgnoreCase) >= 0
                                    || p0.IndexOf("female", StringComparison.OrdinalIgnoreCase) >= 0;

            bool p0LooksLikeFather = p0.IndexOf("father", StringComparison.OrdinalIgnoreCase) >= 0
                                    || p0.IndexOf("male", StringComparison.OrdinalIgnoreCase) >= 0;

            bool p1LooksLikeMother = p1.IndexOf("mother", StringComparison.OrdinalIgnoreCase) >= 0
                                    || p1.IndexOf("female", StringComparison.OrdinalIgnoreCase) >= 0;

            bool p1LooksLikeFather = p1.IndexOf("father", StringComparison.OrdinalIgnoreCase) >= 0
                                    || p1.IndexOf("male", StringComparison.OrdinalIgnoreCase) >= 0;

            if (p0LooksLikeMother && p1LooksLikeFather) return true;
            if (p0LooksLikeFather && p1LooksLikeMother) return false;

            return true; // default: (mother, father)
        }
    }
}
