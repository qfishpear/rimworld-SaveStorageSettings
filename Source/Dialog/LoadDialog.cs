using RimWorld;
using System.Collections.Generic;
using System.IO;
using Verse;
using static SaveStorageSettings.Dialog.LoadCraftingDialog;

namespace SaveStorageSettings.Dialog
{
    class LoadFilterDialog : FileListDialog
    {
        private readonly ThingFilter ThingFilter;

        internal LoadFilterDialog(string storageTypeName, ThingFilter thingFilter) : base(storageTypeName)
        {
            this.ThingFilter = thingFilter;
            base.interactButLabel = "LoadGameButton".Translate();
        }

        protected override bool ShouldDoTypeInField
        {
            get
            {
                return false;
            }
        }

        protected override void DoFileInteraction(FileInfo fi)
        {
            IOUtil.LoadFilters(this.ThingFilter, fi);
            base.Close();
        }
    }

    class LoadStorageSettingDialog : FileListDialog
    {
        private readonly StorageSettings StorageSetting;

        internal LoadStorageSettingDialog(string storageTypeName, StorageSettings setting) : base(storageTypeName)
        {
            this.StorageSetting = setting;
            base.interactButLabel = "LoadGameButton".Translate();
        }

        protected override bool ShouldDoTypeInField
        {
            get
            {
                return false;
            }
        }

        protected override void DoFileInteraction(FileInfo fi)
        {
            IOUtil.LoadStorageSettings(this.StorageSetting, fi);
            base.Close();
        }
    }

    class LoadCraftingDialog : FileListDialog
    {
        public enum LoadType
        {
            Replace,
            Append,
        }

        private readonly LoadType Type;
        private readonly BillStack BillStack;

        internal LoadCraftingDialog(string storageTypeName, BillStack billStack, LoadType type) : base(storageTypeName)
        {
            this.Type = type;
            this.BillStack = billStack;
            this.interactButLabel = "LoadGameButton".Translate();
        }

        protected override bool ShouldDoTypeInField
        {
            get
            {
                return false;
            }
        }

        protected override void DoFileInteraction(FileInfo fi)
        {
            List<Bill> bills = IOUtil.LoadCraftingBills(fi);
            if (bills != null && bills.Count > 0)
            {
                if (this.Type == LoadType.Replace)
                {
                    this.BillStack.Clear();
                }

                foreach (Bill b in bills)
                {
                    this.BillStack.AddBill(b);
                }
                bills.Clear();
                bills = null;
            }
            base.Close();
        }
    }

    class LoadOperationDialog : FileListDialog
    {
        public enum LoadType
        {
            Replace,
            Append,
        }

        private readonly LoadType Type;
        private readonly Pawn Pawn;

        internal LoadOperationDialog(Pawn pawn, string storageTypeName, LoadType type) : base(storageTypeName)
        {
            this.Type = type;
            this.Pawn = pawn;
            this.interactButLabel = "LoadGameButton".Translate();
        }

        protected override bool ShouldDoTypeInField
        {
            get
            {
                return false;
            }
        }

        protected override void DoFileInteraction(FileInfo fi)
        {
            List<Bill> bills = IOUtil.LoadOperationBills(fi, this.Pawn);
            if (bills != null && bills.Count > 0)
            {
                if (this.Type == LoadType.Replace)
                {
                    this.Pawn.BillStack.Clear();
                }

                foreach (Bill b in bills)
                {
                    this.Pawn.BillStack.AddBill(b);
                }
                bills.Clear();
                bills = null;
            }
            base.Close();
        }
    }

    class LoadDrugPolicyDialog : FileListDialog
    {
        private readonly DrugPolicy DrugPolicy;

        internal LoadDrugPolicyDialog(string storageTypeName, DrugPolicy drugPolicy) : base(storageTypeName)
        {
            this.DrugPolicy = drugPolicy;
            this.interactButLabel = "LoadGameButton".Translate();
        }

        protected override bool ShouldDoTypeInField
        {
            get
            {
                return false;
            }
        }

        protected override void DoFileInteraction(FileInfo fi)
        {
            IOUtil.LoadDrugPolicy(this.DrugPolicy, fi);
            base.Close();
        }
    }

    class LoadFoodRestrictionDialog : FileListDialog
    {
        private readonly FoodPolicy FoodPolicy;

        internal LoadFoodRestrictionDialog(string storageTypeName, FoodPolicy foodPolicy) : base(storageTypeName)
        {
            this.FoodPolicy = foodPolicy;
            this.interactButLabel = "LoadGameButton".Translate();
        }

        protected override bool ShouldDoTypeInField
        {
            get
            {
                return false;
            }
        }

        protected override void DoFileInteraction(FileInfo fi)
        {
            IOUtil.LoadFoodPolicy(this.FoodPolicy, fi);
            base.Close();
        }
    }

    class LoadReadingPolicyDialog : FileListDialog
    {
        private readonly ReadingPolicy ReadingPolicy;

        internal LoadReadingPolicyDialog(string storageTypeName, ReadingPolicy readingPolicy) : base(storageTypeName)
        {
            this.ReadingPolicy = readingPolicy;
            this.interactButLabel = "LoadGameButton".Translate();
        }

        protected override bool ShouldDoTypeInField
        {
            get
            {
                return false;
            }
        }

        protected override void DoFileInteraction(FileInfo fi)
        {
            IOUtil.LoadReadingPolicy(this.ReadingPolicy, fi);
            base.Close();
        }
    }

}
