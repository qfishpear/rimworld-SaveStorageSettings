using RimWorld;
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Verse;

namespace SaveStorageSettings
{
    internal class EverybodyGetsOneUtil
    {
        private static Assembly ego = null;
        private static bool initialized = false;
        public static bool Exists
        {
            get
            {
                if (!initialized)
                {
                    foreach (ModContentPack pack in LoadedModManager.RunningMods)
                    {
                        foreach (Assembly assembly in pack.assemblies.loadedAssemblies)
                        {
                            if (
                                assembly.GetName().Name.Equals("Everybody_Gets_One") && (
                                    assembly.GetType("Everybody_Gets_One.RepeatModeDefOf") != null ||
                                    assembly.GetType("TD_Enhancement_Pack.RepeatModeDefOf") != null
                                )
                            )
                            {
                                initialized = true;
                                ego = assembly;
                                break;
                            }
                        }
                        if (initialized)
                        {
                            break;
                        }
                    }
                    initialized = true;
                }
                return ego != null;
            }
        }

        public static bool TryGetRepeatModeDef(string defName, out BillRepeatModeDef def)
        {
            Log.Warning("Try get " + defName);
            def = null;
            if (Exists)
            {
                Type repeatModeType = ego.GetType("Everybody_Gets_One.RepeatModeDefOf") ?? ego.GetType("TD_Enhancement_Pack.RepeatModeDefOf");
                def = repeatModeType.GetField(defName, BindingFlags.Static | BindingFlags.Public).GetValue(null) as BillRepeatModeDef;
            }
            return def != null;
        }

        private static MethodInfo hasPersonCounter;
        private static MethodInfo getPersonCounter;
        private static MethodInfo setPersonCounter;
        private static MethodInfo saveSearch;
        private static MethodInfo loadSearch;
        private static bool counterMethodsReady;
        private static readonly ConditionalWeakTable<Bill, PendingPersonCounter> pendingPersonCounters = new ConditionalWeakTable<Bill, PendingPersonCounter>();

        private sealed class PendingPersonCounter
        {
            public string Payload;
        }

        public static bool TryExportPersonCounter(Bill_Production bill, out string payload)
        {
            payload = null;
            try
            {
                if (!EnsureCounterMethods())
                {
                    return false;
                }

                object hasCounter = hasPersonCounter.Invoke(null, new object[] { bill });
                if (!(hasCounter is bool) || !(bool)hasCounter)
                {
                    return false;
                }

                object search = getPersonCounter.Invoke(null, new object[] { bill });
                if (search == null || IsDefaultPersonCounter(search))
                {
                    return false;
                }

                object inactive = Invoke(search, "CloneInactive", new object[] { null });
                if (inactive == null)
                {
                    return false;
                }

                string xml = saveSearch.Invoke(null, new object[] { inactive }) as string;
                if (string.IsNullOrEmpty(xml))
                {
                    Log.Warning("SaveStorageSettings: Could not write the Everybody Gets One person filter for bill [" + bill.recipe.defName + "].");
                    return false;
                }

                payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
                return true;
            }
            catch (Exception e)
            {
                Log.Warning("SaveStorageSettings: Could not save the Everybody Gets One person filter for bill [" + bill.recipe.defName + "]. " + e);
                payload = null;
                return false;
            }
        }

        public static void RememberPersonCounter(Bill bill, string payload)
        {
            if (bill == null || string.IsNullOrEmpty(payload))
            {
                return;
            }

            pendingPersonCounters.GetOrCreateValue(bill).Payload = payload;
        }

        public static void ApplyPersonCounter(Bill bill)
        {
            PendingPersonCounter pending;
            if (bill == null || !pendingPersonCounters.TryGetValue(bill, out pending) || string.IsNullOrEmpty(pending.Payload))
            {
                return;
            }

            string payload = pending.Payload;
            pending.Payload = null;

            Bill_Production production = bill as Bill_Production;
            string billName = production != null && production.recipe != null ? production.recipe.defName : bill.GetUniqueLoadID();
            try
            {
                if (!EnsureCounterMethods())
                {
                    Log.Warning("SaveStorageSettings: Everybody Gets One is not loaded, so the saved person filter on bill [" + billName + "] was skipped.");
                    return;
                }

                byte[] bytes = Convert.FromBase64String(payload);
                string xml = Encoding.UTF8.GetString(bytes);
                object loaded = loadSearch.Invoke(null, new object[] { xml, new object[0] });
                if (loaded == null)
                {
                    Log.Warning("SaveStorageSettings: Could not read the Everybody Gets One person filter for bill [" + billName + "].");
                    return;
                }

                if (production == null)
                {
                    Log.Warning("SaveStorageSettings: Could not apply the Everybody Gets One person filter for bill [" + billName + "].");
                    return;
                }

                object installed;
                if (Named(ReadMember(loaded, "MapType"), "AllMaps"))
                {
                    installed = Invoke(loaded, "CloneForUseSingle", new object[] { null, null });
                }
                else if (production.Map != null)
                {
                    installed = Invoke(loaded, "CloneForUseSingle", new object[] { production.Map, null });
                }
                else
                {
                    Log.Warning("SaveStorageSettings: Bill [" + billName + "] has no map, so its Everybody Gets One person filter was skipped.");
                    return;
                }

                if (installed == null)
                {
                    Log.Warning("SaveStorageSettings: Could not apply the Everybody Gets One person filter for bill [" + billName + "].");
                    return;
                }

                setPersonCounter.Invoke(null, new object[] { production, installed });
            }
            catch (Exception e)
            {
                Log.Warning("SaveStorageSettings: Could not restore the Everybody Gets One person filter for bill [" + billName + "]. " + e);
            }
        }

        private static bool EnsureCounterMethods()
        {
            if (counterMethodsReady)
            {
                return hasPersonCounter != null && getPersonCounter != null && setPersonCounter != null && saveSearch != null && loadSearch != null;
            }

            counterMethodsReady = true;
            if (!Exists)
            {
                return false;
            }

            Type extensions = ego.GetType("Everybody_Gets_One.MapCompExtensions");
            if (extensions == null)
            {
                return false;
            }

            hasPersonCounter = extensions.GetMethod("HasPersonCounter", BindingFlags.Static | BindingFlags.Public);
            getPersonCounter = extensions.GetMethod("GetPersonCounter", BindingFlags.Static | BindingFlags.Public);
            setPersonCounter = extensions.GetMethod("SetPersonCounter", BindingFlags.Static | BindingFlags.Public);

            Type querySearch = null;
            Type scribeXml = null;
            foreach (ModContentPack pack in LoadedModManager.RunningMods)
            {
                foreach (Assembly assembly in pack.assemblies.loadedAssemblies)
                {
                    querySearch = assembly.GetType("TD_Find_Lib.QuerySearch");
                    if (querySearch != null)
                    {
                        scribeXml = assembly.GetType("TD_Find_Lib.ScribeXmlFromString");
                        break;
                    }
                }

                if (querySearch != null)
                {
                    break;
                }
            }

            if (scribeXml != null)
            {
                saveSearch = scribeXml.GetMethod("SaveAsString", BindingFlags.Static | BindingFlags.Public);
                foreach (MethodInfo method in scribeXml.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    if (method.Name == "LoadFromString" && method.IsGenericMethodDefinition)
                    {
                        loadSearch = method.MakeGenericMethod(querySearch);
                        break;
                    }
                }
            }

            return hasPersonCounter != null && getPersonCounter != null && setPersonCounter != null && saveSearch != null && loadSearch != null;
        }

        // Everybody Gets One's untouched filter: everyone, colonists, exclude slaves, exclude quest lodgers, this map.
        private static bool IsDefaultPersonCounter(object search)
        {
            try
            {
                return IsUntouchedPersonCounter(search);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsUntouchedPersonCounter(object search)
        {
            if (!Named(ReadMember(search, "MapType"), "ChosenMaps"))
            {
                return false;
            }

            if (!Named(ReadMember(search, "ListType"), "Everyone"))
            {
                return false;
            }

            object children = ReadMember(search, "Children");
            object matchAll = ReadMember(children, "matchAllQueries");
            object anyMin = ReadMember(children, "anyMin");
            if (!(matchAll is bool) || !(bool)matchAll || !(anyMin is int) || (int)anyMin != 0)
            {
                return false;
            }

            IList queries = ReadMember(children, "queries") as IList;
            if (queries == null || queries.Count != 3)
            {
                return false;
            }

            return IsPawnProperty(queries[0], "IsColonist", true)
                && IsPawnProperty(queries[1], "IsSlaveOfColony", false)
                && IsExcludedQuestLodger(queries[2]);
        }

        private static bool IsPawnProperty(object query, string propertyName, bool include)
        {
            return query != null
                && query.GetType().Name == "ThingQueryBasicProperty"
                && IsEnabled(query, include)
                && IsDirectSelection(query)
                && Named(ReadMember(query, "sel"), propertyName);
        }

        private static bool IsExcludedQuestLodger(object query)
        {
            object selected = query == null ? null : ReadMember(query, "sel");
            return query != null
                && query.GetType().Name == "ThingQueryQuest"
                && IsEnabled(query, false)
                && IsDirectSelection(query)
                && selected is bool
                && !(bool)selected;
        }

        private static bool IsEnabled(object query, bool include)
        {
            object enabled = ReadMember(query, "enabled");
            object included = ReadMember(query, "include");
            return enabled is bool && (bool)enabled && included is bool && (bool)included == include;
        }

        private static bool IsDirectSelection(object query)
        {
            object extraOption = ReadMember(query, "extraOption");
            return extraOption is int && (int)extraOption == 0;
        }

        private static bool Named(object value, string name)
        {
            return value != null && value.ToString() == name;
        }

        private static object ReadMember(object target, string name)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Type type = target == null ? null : target.GetType();
            while (type != null)
            {
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null)
                {
                    return property.GetValue(target, null);
                }

                FieldInfo field = type.GetField(name, flags);
                if (field != null)
                {
                    return field.GetValue(target);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static object Invoke(object target, string methodName, object[] args)
        {
            if (target == null)
            {
                return null;
            }

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
            {
                return null;
            }

            return method.Invoke(target, args);
        }
    }
}
