using RimWorld;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace SaveStorageSettings
{
    // Ingredient source lives beside the bill. Bill Ingredient Source's AddBill patch treats an unset
    // bill as new and applies the player's default, so a preset records every mode and restores it
    // after AddBill. A named storage is resolved with that mod's own rematch on the current map.
    internal static class BillIngredientSourceUtil
    {
        private static bool initialized;
        private static bool ready;
        private static MethodInfo tryGet;
        private static MethodInfo getOrCreate;
        private static MethodInfo getStableLabel;
        private static MethodInfo getStorageKind;
        private static MethodInfo validateSelectedStorage;
        private static FieldInfo useAllStorages;
        private static FieldInfo searchMode;
        private static FieldInfo selectedStorageGroup;
        private static FieldInfo selectedStorageLabel;
        private static FieldInfo selectedStorageKind;
        private static FieldInfo storageResetNotified;
        private static Type searchModeType;
        private static Type storageKindType;
        private static readonly ConditionalWeakTable<Bill, PendingSource> pendingSources = new ConditionalWeakTable<Bill, PendingSource>();

        private sealed class PendingSource
        {
            public string Search;
            public string Kind;
            public string Label;
        }

        public static bool TryExport(Bill_Production bill, out string search, out string kind, out string label)
        {
            search = null;
            kind = null;
            label = null;
            try
            {
                if (!EnsureReady() || bill == null)
                {
                    return false;
                }

                object[] args = new object[] { bill, null };
                object found = tryGet.Invoke(null, args);
                if (!(found is bool ok) || !ok || args[1] == null)
                {
                    return false;
                }

                object data = args[1];
                if (useAllStorages.GetValue(data) is bool useAll && useAll)
                {
                    search = "AllStorages";
                    return true;
                }

                object group = selectedStorageGroup.GetValue(data);
                string storedLabel = selectedStorageLabel.GetValue(data) as string;
                string storedKind = selectedStorageKind.GetValue(data)?.ToString();
                if (group != null)
                {
                    string stable = getStableLabel.Invoke(null, new object[] { group }) as string;
                    string liveKind = getStorageKind.Invoke(null, new object[] { group })?.ToString();
                    if (!string.IsNullOrEmpty(stable))
                    {
                        storedLabel = stable;
                    }
                    if (!string.IsNullOrEmpty(liveKind) && liveKind != "None")
                    {
                        storedKind = liveKind;
                    }
                }

                if (group != null || (!string.IsNullOrEmpty(storedLabel) && !string.IsNullOrEmpty(storedKind) && storedKind != "None"))
                {
                    search = "Storage";
                    kind = string.IsNullOrEmpty(storedKind) ? "None" : storedKind;
                    label = SingleLine(bill, storedLabel);
                    return true;
                }

                search = "Radius";
                return true;
            }
            catch (Exception e)
            {
                string billName = bill != null && bill.recipe != null ? bill.recipe.defName : "unknown";
                Log.Warning("SaveStorageSettings: Could not save the Bill Ingredient Source setting for bill [" + billName + "]. " + e);
                search = null;
                kind = null;
                label = null;
                return false;
            }
        }

        public static void Remember(Bill bill, string key, string value)
        {
            if (bill == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            PendingSource pending = pendingSources.GetOrCreateValue(bill);
            switch (key)
            {
                case "bisSearch":
                    pending.Search = value;
                    break;
                case "bisStorageKind":
                    pending.Kind = value;
                    break;
                case "bisStorageLabel":
                    pending.Label = value;
                    break;
            }
        }

        public static void Apply(Bill bill)
        {
            PendingSource pending;
            if (bill == null || !pendingSources.TryGetValue(bill, out pending) || string.IsNullOrEmpty(pending.Search))
            {
                return;
            }

            string search = pending.Search;
            string kind = pending.Kind;
            string label = pending.Label;
            pending.Search = null;

            Bill_Production production = bill as Bill_Production;
            string billName = production != null && production.recipe != null ? production.recipe.defName : bill.GetUniqueLoadID();
            try
            {
                if (production == null)
                {
                    Log.Warning("SaveStorageSettings: Could not apply the Bill Ingredient Source setting for bill [" + billName + "].");
                    return;
                }

                if (!EnsureReady())
                {
                    Log.Warning("SaveStorageSettings: Bill Ingredient Source is not available, so the saved ingredient source on bill [" + billName + "] was skipped.");
                    return;
                }

                object data = getOrCreate.Invoke(null, new object[] { production });
                if (data == null)
                {
                    Log.Warning("SaveStorageSettings: Could not apply the Bill Ingredient Source setting for bill [" + billName + "].");
                    return;
                }

                if (search == "AllStorages")
                {
                    WriteSource(data, true, "Storage", "None", null);
                    return;
                }

                if (search == "Radius")
                {
                    WriteSource(data, false, "Radius", "None", null);
                    return;
                }

                if (search != "Storage")
                {
                    Log.Warning("SaveStorageSettings: Unknown Bill Ingredient Source mode [" + search + "] for bill [" + billName + "].");
                    return;
                }

                string kindName = string.IsNullOrEmpty(kind) ? "None" : kind;
                WriteSource(data, false, "Storage", kindName, label);
                Map map = production.Map ?? Find.CurrentMap;
                if (map == null)
                {
                    Log.Warning("SaveStorageSettings: Bill [" + billName + "] has no map, so its ingredient storage was not matched.");
                    return;
                }

                // Radius and Look Everywhere are already complete. Validate rematches a named storage
                // and, when the name is missing, clears it back to vanilla radius with that mod's notice.
                validateSelectedStorage.Invoke(null, new object[] { map, production, data });
            }
            catch (Exception e)
            {
                Log.Warning("SaveStorageSettings: Could not restore the Bill Ingredient Source setting for bill [" + billName + "]. " + e);
            }
        }

        private static void WriteSource(object data, bool useAll, string mode, string kind, string label)
        {
            object modeValue = Enum.Parse(searchModeType, mode);
            object kindValue = Enum.Parse(storageKindType, kind);
            useAllStorages.SetValue(data, useAll);
            searchMode.SetValue(data, modeValue);
            selectedStorageGroup.SetValue(data, null);
            selectedStorageLabel.SetValue(data, label);
            selectedStorageKind.SetValue(data, kindValue);
            storageResetNotified.SetValue(data, false);
        }

        private static string SingleLine(Bill_Production bill, string label)
        {
            if (string.IsNullOrEmpty(label) || (label.IndexOf('\r') < 0 && label.IndexOf('\n') < 0))
            {
                return label;
            }

            string billName = bill != null && bill.recipe != null ? bill.recipe.defName : "unknown";
            Log.Warning("SaveStorageSettings: Ingredient storage name for bill [" + billName + "] contained a line break and was saved without it.");
            return label.Replace("\r", "").Replace("\n", "");
        }

        private static bool EnsureReady()
        {
            if (initialized)
            {
                return ready;
            }

            initialized = true;
            Type store = null;
            Type source = null;
            foreach (ModContentPack pack in LoadedModManager.RunningMods)
            {
                foreach (Assembly assembly in pack.assemblies.loadedAssemblies)
                {
                    store = assembly.GetType("BillIngredientSource.BillDataStore");
                    source = assembly.GetType("BillIngredientSource.StorageIngredientSource");
                    if (store != null && source != null)
                    {
                        searchModeType = assembly.GetType("BillIngredientSource.IngredientSearchMode");
                        storageKindType = assembly.GetType("BillIngredientSource.SelectedStorageKind");
                        break;
                    }

                    store = null;
                    source = null;
                }

                if (store != null)
                {
                    break;
                }
            }

            if (store == null || source == null || searchModeType == null || storageKindType == null)
            {
                return false;
            }

            tryGet = store.GetMethod("TryGet", BindingFlags.Static | BindingFlags.Public);
            getOrCreate = store.GetMethod("GetOrCreate", BindingFlags.Static | BindingFlags.Public);
            getStableLabel = source.GetMethod("GetStableStorageLabel", BindingFlags.Static | BindingFlags.Public);
            getStorageKind = source.GetMethod("GetStorageKind", BindingFlags.Static | BindingFlags.Public);
            validateSelectedStorage = source.GetMethod("ValidateSelectedStorage", BindingFlags.Static | BindingFlags.Public);

            Type dataType = store.Assembly.GetType("BillIngredientSource.BillData");
            if (dataType != null)
            {
                BindingFlags fields = BindingFlags.Instance | BindingFlags.Public;
                useAllStorages = dataType.GetField("UseAllStorages", fields);
                searchMode = dataType.GetField("SearchMode", fields);
                selectedStorageGroup = dataType.GetField("SelectedStorageGroup", fields);
                selectedStorageLabel = dataType.GetField("SelectedStorageLabel", fields);
                selectedStorageKind = dataType.GetField("SelectedStorageKind", fields);
                storageResetNotified = dataType.GetField("StorageResetNotified", fields);
            }

            ready = tryGet != null && getOrCreate != null && getStableLabel != null && getStorageKind != null
                && validateSelectedStorage != null && useAllStorages != null && searchMode != null
                && selectedStorageGroup != null && selectedStorageLabel != null && selectedStorageKind != null
                && storageResetNotified != null;
            return ready;
        }
    }
}
