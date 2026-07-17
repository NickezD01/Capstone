using cpms_Application.Request.Material;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Request.ProgressReport;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Warehouse;
using cpms_Application.Request.WarehouseTransfer;
using cpms_Application.Request.Tasks;
using FluentValidation;

namespace cpms_Application.Validators
{
    public class MaterialVariantRequestValidator : AbstractValidator<MaterialVariantRequest>
    {
        public MaterialVariantRequestValidator()
        {
            RuleFor(x => x.MaterialId).GreaterThan(0);
            RuleFor(x => x.VariantName).NotEmpty().MaximumLength(250);
            RuleFor(x => x.SKU).MaximumLength(100);
            RuleFor(x => x.Brand).MaximumLength(150);
            RuleFor(x => x.Grade).MaximumLength(100);
            RuleFor(x => x.Size).MaximumLength(100);
            RuleFor(x => x.Color).MaximumLength(100);
            RuleFor(x => x.Specification).MaximumLength(1000);
            RuleFor(x => x.Packaging).MaximumLength(200);
            RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        }
    }

    public class CreateMaterialRequestValidator : AbstractValidator<CreateMaterialRequest>
    {
        public CreateMaterialRequestValidator()
        {
            RuleFor(x => x.ProjectId).GreaterThan(0);
            RuleFor(x => x.RequestNote).MaximumLength(1000);
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x).Must(x => x.VariantId > 0 || x.MaterialId > 0).WithMessage("VariantId is required.");
                item.RuleFor(x => x.Quantity).GreaterThan(0);
                item.RuleFor(x => x.Note).MaximumLength(1000);
            });
        }
    }

    public class ApproveMaterialRequestValidator : AbstractValidator<ApproveMaterialRequest>
    {
        public ApproveMaterialRequestValidator()
        {
            RuleFor(x => x.WarehouseId).GreaterThan(0);
            RuleFor(x => x.DecisionNote).MaximumLength(1000);
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.ItemId).GreaterThan(0);
                item.RuleFor(x => x.ApprovedQuantity).GreaterThanOrEqualTo(0);
            });
        }
    }

    public class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
    {
        public CreatePurchaseOrderRequestValidator()
        {
            RuleFor(x => x.ProjectId).GreaterThan(0);
            RuleFor(x => x.SupplierId).GreaterThan(0);
            RuleFor(x => x.WarehouseId).GreaterThan(0);
            RuleFor(x => x.Note).MaximumLength(1000);
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x).Must(x => x.VariantId > 0 || x.MaterialId > 0).WithMessage("VariantId is required.");
                item.RuleFor(x => x.Quantity).GreaterThan(0);
                item.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            });
        }
    }

    public class SubmitProgressReportRequestValidator : AbstractValidator<SubmitProgressReportRequest>
    {
        public SubmitProgressReportRequestValidator()
        {
            RuleFor(x => x.TaskId).GreaterThan(0);
            RuleFor(x => x.ProgressIncrement).GreaterThan(0).LessThanOrEqualTo(100).PrecisionScale(5, 2, true);
            RuleFor(x => x.Notes).MaximumLength(2000);
            RuleFor(x => x.SitePhotoUrl).MaximumLength(500);
        }
    }

    public class InventoryAdjustmentRequestValidator : AbstractValidator<InventoryAdjustmentRequest>
    {
        public InventoryAdjustmentRequestValidator()
        {
            RuleFor(x => x.WarehouseId).GreaterThan(0);
            RuleFor(x => x.VariantId).GreaterThan(0);
            RuleFor(x => x.QuantityDelta).NotEqual(0);
            RuleFor(x => x.Note).MaximumLength(1000);
        }
    }

    public class CreateWarehouseTransferRequestValidator : AbstractValidator<CreateWarehouseTransferRequest>
    {
        public CreateWarehouseTransferRequestValidator()
        {
            RuleFor(x => x.SourceWarehouseId).GreaterThan(0);
            RuleFor(x => x.DestinationWarehouseId).GreaterThan(0)
                .NotEqual(x => x.SourceWarehouseId).WithMessage("Source and destination warehouses must differ.");
            RuleFor(x => x.Note).MaximumLength(1000);
            RuleFor(x => x.Items).NotEmpty();
            RuleFor(x => x.Items).Must(items => items.Select(i => i.VariantId).Distinct().Count() == items.Count)
                .WithMessage("A variant may only appear once per transfer.");
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.VariantId).GreaterThan(0);
                item.RuleFor(x => x.Quantity).GreaterThan(0);
            });
        }
    }

    public class ReceiveWarehouseTransferRequestValidator : AbstractValidator<ReceiveWarehouseTransferRequest>
    {
        public ReceiveWarehouseTransferRequestValidator()
        {
            RuleFor(x => x.Items).Must(items => items.Select(i => i.TransferItemId).Distinct().Count() == items.Count)
                .WithMessage("A transfer item may only appear once per receipt.");
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.TransferItemId).GreaterThan(0);
                item.RuleFor(x => x.Quantity).GreaterThan(0);
            });
        }
    }

    public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
    {
        public CreateTaskRequestValidator()
        {
            RuleFor(x => x.ProjectId).GreaterThan(0);
            RuleFor(x => x.AssignedToUserID).GreaterThan(0);
            RuleFor(x => x.PhaseName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.TaskName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.PlannedBudget).GreaterThanOrEqualTo(0);
            RuleFor(x => x.BaselineEnd).GreaterThanOrEqualTo(x => x.BaselineStart);
            RuleForEach(x => x.Materials).ChildRules(item =>
            {
                item.RuleFor(x => x).Must(x => x.VariantId > 0 || x.MaterialId > 0).WithMessage("VariantId is required.");
                item.RuleFor(x => x.GrossQuantityRequired).GreaterThan(0);
            });
        }
    }
}
