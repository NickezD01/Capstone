using cpms_Application.Request.Material;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Request.ProgressReport;
using cpms_Application.Request.Project;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Category;
using cpms_Application.Request.Supplier;
using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Request.User;
using cpms_Application.Request.UserAccount;
using cpms_Application.Request.Warehouse;
using cpms_Application.Request.WarehouseTransfer;
using cpms_Application.Request.Tasks;
using FluentValidation;
using cpms_Domain.Models;

namespace cpms_Application.Validators
{
    public class UserRegisterRequestValidator : AbstractValidator<UserRegisterRequest>
    {
        public UserRegisterRequestValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
            RuleFor(x => x.Password).NotEmpty().MinimumLength(10).MaximumLength(128)
                .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
                .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
                .Matches("[0-9]").WithMessage("Password must contain a number.");
            RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("Passwords do not match.");
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        }
    }

    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.UserEmail).NotEmpty().EmailAddress().MaximumLength(150);
            RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
        }
    }

    public class RefreshSessionRequestValidator : AbstractValidator<RefreshSessionRequest>
    {
        public RefreshSessionRequestValidator()
        {
            RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(500);
            RuleFor(x => x.DeviceInfo).MaximumLength(500);
        }
    }

    public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
    {
        public ResetPasswordRequestValidator()
        {
            RuleFor(x => x.UserId).GreaterThan(0);
            RuleFor(x => x.Token).Matches("^[0-9]{6}$");
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10).MaximumLength(128)
                .Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]");
            RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword);
        }
    }

    public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
    {
        public ChangePasswordRequestValidator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10).MaximumLength(128)
                .Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]");
            RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword);
        }
    }

    public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
    {
        public UpdateUserRoleRequestValidator() => RuleFor(x => x.Role).IsInEnum();
    }

    public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
    {
        public CreateProjectRequestValidator()
        {
            RuleFor(x => x.ProjectName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Address).MaximumLength(500);
            RuleFor(x => x.PMUserID).GreaterThan(0);
            RuleFor(x => x.TotalProjectBudget).GreaterThanOrEqualTo(0);
            RuleFor(x => x.BaselineEnd).GreaterThanOrEqualTo(x => x.BaselineStart);
            RuleFor(x => x.StartDate).LessThanOrEqualTo(x => x.BaselineEnd);
        }
    }

    public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
    {
        public UpdateProjectRequestValidator()
        {
            RuleFor(x => x.ProjectName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Address).MaximumLength(500);
            RuleFor(x => x.BaselineEnd).GreaterThanOrEqualTo(x => x.BaselineStart);
            RuleFor(x => x.StartDate).LessThanOrEqualTo(x => x.BaselineEnd);
            RuleFor(x => x.RowVersion).NotEmpty();
        }
    }

    public class AdjustBudgetRequestValidator : AbstractValidator<AdjustBudgetRequest>
    {
        public AdjustBudgetRequestValidator()
        {
            RuleFor(x => x.ProjectId).GreaterThan(0);
            RuleFor(x => x.Amount).NotEqual(0);
            RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        }
    }

    public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
    {
        public CreateCategoryRequestValidator() => RuleFor(x => x.CategoryName).NotEmpty().MaximumLength(150);
    }

    public class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
    {
        public CreateSupplierRequestValidator()
        {
            RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ContactEmail).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
            RuleFor(x => x.ContactPhone).MaximumLength(20);
        }
    }

    public class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
    {
        public UpdateSupplierRequestValidator()
        {
            RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ContactEmail).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
            RuleFor(x => x.ContactPhone).MaximumLength(20);
        }
    }

    public class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
    {
        public CreateWarehouseRequestValidator()
        {
            RuleFor(x => x.ManagerId).GreaterThan(0);
            RuleFor(x => x.WarehouseName).NotEmpty().MaximumLength(250);
            RuleFor(x => x.Location).NotEmpty().MaximumLength(500);
        }
    }

    public class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
    {
        public UpdateWarehouseRequestValidator()
        {
            RuleFor(x => x.ManagerId).GreaterThan(0);
            RuleFor(x => x.WarehouseName).NotEmpty().MaximumLength(250);
            RuleFor(x => x.Location).NotEmpty().MaximumLength(500);
        }
    }

    public class CreateCatalogRequestValidator : AbstractValidator<CreateCatalogRequest>
    {
        public CreateCatalogRequestValidator()
        {
            RuleFor(x => x.SupplierId).GreaterThan(0);
            RuleFor(x => x).Must(x => x.VariantId > 0 || x.MaterialId > 0).WithMessage("VariantId is required.");
            RuleFor(x => x.SupplierSku).MaximumLength(100);
            RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            RuleFor(x => x.UnitPrice).GreaterThan(0).When(x => x.IsAvailable)
                .WithMessage("An available supplier offer must have a positive unit price.");
            RuleFor(x => x.MinimumOrderQuantity).GreaterThanOrEqualTo(0);
            RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0);
        }
    }

    public class UpdateCatalogRequestValidator : AbstractValidator<UpdateCatalogRequest>
    {
        public UpdateCatalogRequestValidator()
        {
            RuleFor(x => x.SupplierSku).MaximumLength(100);
            RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            RuleFor(x => x.UnitPrice).GreaterThan(0).When(x => x.IsAvailable)
                .WithMessage("An available supplier offer must have a positive unit price.");
            RuleFor(x => x.MinimumOrderQuantity).GreaterThanOrEqualTo(0);
            RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0);
        }
    }

    public class MaterialRequestValidator : AbstractValidator<cpms_Application.Request.Material.MaterialRequest>
    {
        public MaterialRequestValidator()
        {
            RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.DefaultUnit).NotEmpty().MaximumLength(50);
            RuleFor(x => x.CategoryId).GreaterThan(0);
        }
    }

    public class UpdateMaterialRequestValidator : AbstractValidator<cpms_Application.Request.Material.UpdateMaterialRequest>
    {
        public UpdateMaterialRequestValidator()
        {
            RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.DefaultUnit).NotEmpty().MaximumLength(50);
        }
    }

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
            RuleFor(x => x.ExpectedDeliveryDate)
                .Must(date => !date.HasValue || date.Value.Date >= DateTime.UtcNow.Date)
                .WithMessage("ExpectedDeliveryDate cannot be in the past.");
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x).Must(x => x.VariantId > 0 || x.MaterialId > 0).WithMessage("VariantId is required.");
                item.RuleFor(x => x.Quantity).GreaterThan(0);
                item.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            });
        }
    }

    public class PurchaseOrderActionRequestValidator : AbstractValidator<PurchaseOrderActionRequest>
    {
        public PurchaseOrderActionRequestValidator()
        {
            RuleFor(x => x.Note).MaximumLength(1000);
            RuleFor(x => x.RowVersion).MaximumLength(200);
        }
    }

    public class SubmitProgressReportRequestValidator : AbstractValidator<SubmitProgressReportRequest>
    {
        public SubmitProgressReportRequestValidator()
        {
            RuleFor(x => x.TaskId).GreaterThan(0);
            RuleFor(x => x.ProgressIncrement).GreaterThan(0).LessThanOrEqualTo(100).PrecisionScale(5, 2, true);
            RuleFor(x => x.ActualCostIncrement).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
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
            RuleFor(x => x.ReasonCode).Must(reason => InventoryAdjustmentReasons.All.Contains(reason))
                .WithMessage("Invalid inventory adjustment reason code.");
            RuleFor(x => x.Note).MaximumLength(1000);
        }
    }

    public class InventoryReturnRequestValidator : AbstractValidator<InventoryReturnRequest>
    {
        public InventoryReturnRequestValidator()
        {
            RuleFor(x => x.WarehouseId).GreaterThan(0);
            RuleFor(x => x.VariantId).GreaterThan(0);
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleFor(x => x.MaterialRequestId).GreaterThan(0).WithMessage("MaterialRequestId is required for a material return.");
            RuleFor(x => x.ReasonCode).Must(reason => MaterialReturnReasons.All.Contains(reason));
            RuleFor(x => x.Condition).Must(condition => MaterialReturnConditions.All.Contains(condition));
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
                item.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
                item.RuleFor(x => x.DamagedQuantity).GreaterThanOrEqualTo(0);
                item.RuleFor(x => x.LostQuantity).GreaterThanOrEqualTo(0);
                item.RuleFor(x => x).Must(x => x.Quantity + x.DamagedQuantity + x.LostQuantity > 0)
                    .WithMessage("A transfer receipt must account for a positive quantity.");
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

    public class ReceivePurchaseOrderRequestValidator : AbstractValidator<ReceivePurchaseOrderRequest>
    {
        public ReceivePurchaseOrderRequestValidator()
        {
            RuleFor(x => x.Note).MaximumLength(1000);
            RuleFor(x => x.RowVersion).MaximumLength(200);
            RuleFor(x => x.Items).NotEmpty();
            RuleFor(x => x.Items).Must(items => items.Select(i => i.LineItemId).Distinct().Count() == items.Count)
                .WithMessage("A line item may only appear once per receipt.");
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.LineItemId).GreaterThan(0);
                item.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
                item.RuleFor(x => x.DamagedQuantity).GreaterThanOrEqualTo(0);
                item.RuleFor(x => x.MissingQuantity).GreaterThanOrEqualTo(0);
                item.RuleFor(x => x.LotNumber).MaximumLength(100);
                item.RuleFor(x => x.BatchNumber).MaximumLength(100);
                item.RuleFor(x => x.SerialNumber).MaximumLength(200);
                item.RuleFor(x => x.ExpiryDate).GreaterThan(DateTime.UtcNow.Date).When(x => x.ExpiryDate.HasValue);
                item.RuleFor(x => x).Must(x => x.Quantity + x.DamagedQuantity + x.MissingQuantity > 0)
                    .WithMessage("A receipt line must account for a positive quantity.");
            });
        }
    }

    public class ReviewProgressReportRequestValidator : AbstractValidator<ReviewProgressReportRequest>
    {
        public ReviewProgressReportRequestValidator()
        {
            RuleFor(x => x.ReviewNote).MaximumLength(2000);
            RuleFor(x => x.RowVersion).NotEmpty();
        }
    }

    public class CorrectProgressReportRequestValidator : AbstractValidator<CorrectProgressReportRequest>
    {
        public CorrectProgressReportRequestValidator()
        {
            RuleFor(x => x.ProgressIncrement).GreaterThan(0).LessThanOrEqualTo(100).PrecisionScale(5, 2, true);
            RuleFor(x => x.ActualCostIncrement).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
            RuleFor(x => x.Notes).MaximumLength(2000);
            RuleFor(x => x.SitePhotoUrl).MaximumLength(500);
            RuleFor(x => x.RowVersion).NotEmpty();
        }
    }

    public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
    {
        public UpdateTaskRequestValidator()
        {
            RuleFor(x => x.AssignedToUserID).GreaterThan(0);
            RuleFor(x => x.PhaseName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.TaskName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.PlannedBudget).GreaterThanOrEqualTo(0);
            RuleFor(x => x.BaselineEnd).GreaterThanOrEqualTo(x => x.BaselineStart);
            RuleFor(x => x.RowVersion).NotEmpty();
        }
    }
}
