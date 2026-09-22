using ClosedXML.Excel;
using cpms_Application.Interfaces;
using cpms_Application.Response;
using cpms_Application.Response.AiConstructionPlanner;
using cpms_Application.Response.ProjectExport;
using cpms_Application.Utilities;
using cpms_Domain;
using cpms_Domain.Models;
using System.Net;

namespace cpms_Application.Services
{
    public class ProjectExportService : IProjectExportService
    {
        private const int MaxMovementRows = 500;

        private readonly IUnitOfWork _uow;
        private readonly IClaimService _claimService;
        private readonly IProjectAccessService _projectAccess;
        private readonly IWarehouseContext _warehouseContext;

        public ProjectExportService(
            IUnitOfWork uow,
            IClaimService claimService,
            IProjectAccessService? projectAccess = null,
            IWarehouseContext? warehouseContext = null)
        {
            _uow = uow;
            _claimService = claimService;
            _projectAccess = projectAccess ?? new ProjectAccessService(uow, claimService);
            _warehouseContext = warehouseContext ?? new WarehouseContext(uow);
        }

        public async Task<ApiResponse> ExportProjectAsync(int projectId)
        {
            var user = _claimService.GetUserClaim();
            var project = await _uow.Projects.GetByIdAsync(projectId);
            if (project == null)
                return new ApiResponse().SetNotFound("Project not found.");

            bool fullView;
            if (IsRole(user, Role.ADMIN))
                fullView = true;
            else if (IsRole(user, Role.PM) && project.PMUserID == user.Id)
                fullView = true;
            else if (IsRole(user, Role.CUSTOMER) && project.CustomerUserId == user.Id)
                fullView = true;
            else if (IsRole(user, Role.WAREHOUSE_MANAGER) && await _projectAccess.CanViewProjectAsStaffAsync(project))
                fullView = false;
            else
                return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                    "You do not have access to this project's export.");

            var phases = (await _uow.Phases.GetAllAsync(p => p.ProjectId == projectId))
                .OrderBy(p => p.SequenceOrder).ThenBy(p => p.Name).ToList();
            var tasks = (await _uow.TaskItems.GetAllAsync(t => t.ProjectId == projectId))
                .OrderBy(t => t.TaskId).ToList();
            var taskById = tasks.ToDictionary(t => t.TaskId);
            var taskIds = tasks.Select(t => t.TaskId).ToList();
            var requests = (await _uow.MaterialRequests.GetAllAsync(r => r.ProjectId == projectId))
                .OrderBy(r => r.RequestId).ToList();
            var requestIds = requests.Select(r => r.RequestId).ToList();
            var lines = requestIds.Count == 0
                ? new List<MaterialRequisition>()
                : (await _uow.MaterialRequisitions.GetAllAsync(r => requestIds.Contains(r.RequestId)))
                    .OrderBy(r => r.ItemId).ToList();
            var ledger = (await _uow.MaterialBudgetTransactions.GetAllAsync(t => t.ProjectId == projectId))
                .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).ToList();
            var returns = requestIds.Count == 0
                ? new List<MaterialReturn>()
                : await _uow.MaterialReturns.GetAllAsync(r => requestIds.Contains(r.MaterialRequestId));
            var reports = taskIds.Count == 0
                ? new List<ProgressReport>()
                : (await _uow.ProgressReports.GetAllAsync(r => taskIds.Contains(r.TaskId)))
                    .OrderBy(r => r.ReportDate).ToList();

            var variantIds = lines.Select(l => l.VariantId).Distinct().ToList();
            var variants = variantIds.Count == 0
                ? new List<MaterialVariant>()
                : await _uow.MaterialVariants.GetAllAsync(v => variantIds.Contains(v.VariantId));
            var variantById = variants.ToDictionary(v => v.VariantId);
            var materialIds = variants.Select(v => v.MaterialId).Distinct().ToList();
            var materials = materialIds.Count == 0
                ? new List<Material>()
                : await _uow.Materials.GetAllAsync(m => materialIds.Contains(m.MaterialId));
            var materialById = materials.ToDictionary(m => m.MaterialId);

            var spent = ledger.Sum(t => t.Amount);
            var projectRow = new ProjectExportProjectRow
            {
                ProjectId = project.ProjectId,
                ProjectName = project.ProjectName,
                Address = project.Address,
                Status = project.Status.ToString(),
                StartDate = project.StartDate,
                BaselineStart = project.BaselineStart,
                BaselineEnd = project.BaselineEnd,
                TotalBudget = project.TotalProjectBudget,
                Currency = project.Currency,
                ProjectManager = await DisplayNameAsync(project.PMUserID),
                Customer = project.CustomerUserId.HasValue ? await DisplayNameAsync(project.CustomerUserId.Value) : string.Empty,
                TotalSpent = spent,
                RemainingBudget = project.TotalProjectBudget - spent
            };

            var requestRows = requests.Select(r => new ProjectExportRequestRow
            {
                RequestId = r.RequestId,
                TaskId = r.TaskId,
                TaskName = r.TaskId.HasValue && taskById.TryGetValue(r.TaskId.Value, out var reqTask) ? reqTask.TaskName : string.Empty,
                Status = r.Status,
                EstimatedCost = r.EstimatedCost,
                ActualCost = r.ActualCost,
                BudgetDebited = r.BudgetDebitedAmount,
                RequestDate = r.RequestDate,
                ApprovedAt = r.ApprovedAt,
                DecisionNote = r.DecisionNote
            }).ToList();

            var lineRows = lines.Select(l =>
            {
                var returned = returns
                    .Where(r => r.MaterialRequestId == l.RequestId && r.VariantId == l.VariantId)
                    .Sum(r => r.Quantity);
                return new ProjectExportRequestLineRow
                {
                    ItemId = l.ItemId,
                    RequestId = l.RequestId,
                    MaterialName = MaterialNameOf(l.VariantId, variantById, materialById),
                    VariantName = variantById.TryGetValue(l.VariantId, out var v) ? v.VariantName : $"Variant {l.VariantId}",
                    Unit = variantById.TryGetValue(l.VariantId, out var u) ? u.Unit : null,
                    Quantity = l.Quantity,
                    ApprovedQuantity = l.ApprovedQuantity,
                    IssuedQuantity = l.IssuedQuantity,
                    ReturnedQuantity = returned,
                    NetIssuedQuantity = Math.Max(0, l.IssuedQuantity - returned),
                    UnitActualCost = l.UnitActualCost,
                    DebitedAmount = ledger.Where(t => t.ItemId == l.ItemId).Sum(t => t.Amount)
                };
            }).ToList();

            using var workbook = new XLWorkbook();
            ExcelSheetWriter.AddSheet(workbook, "Project", new[] { projectRow });
            if (fullView)
            {
                ExcelSheetWriter.AddSheet(workbook, "Phases", phases.Select(p => new ProjectExportPhaseRow
                {
                    PhaseId = p.PhaseId,
                    Name = p.Name,
                    Description = p.Description,
                    SequenceOrder = p.SequenceOrder,
                    BaselineStart = p.BaselineStart,
                    BaselineEnd = p.BaselineEnd,
                    Status = p.Status.ToString()
                }).ToList());
                var assigneeIds = tasks.Select(t => t.AssignedToUserID).Distinct().ToList();
                var assignees = assigneeIds.Count == 0
                    ? new Dictionary<int, string>()
                    : (await _uow.UserAccounts.GetAllAsync(u => assigneeIds.Contains(u.Id)))
                        .ToDictionary(u => u.Id, u => FormatName(u));
                ExcelSheetWriter.AddSheet(workbook, "Tasks", tasks.Select(t => new ProjectExportTaskRow
                {
                    TaskId = t.TaskId,
                    PhaseName = t.PhaseName,
                    TaskName = t.TaskName,
                    AssignedTo = assignees.TryGetValue(t.AssignedToUserID, out var name) ? name : string.Empty,
                    PlannedBudget = t.PlannedBudget,
                    ActualCost = t.ActualCost,
                    BaselineStart = t.BaselineStart,
                    BaselineEnd = t.BaselineEnd,
                    ActualProgressPct = t.ActualProgressPct,
                    Status = t.Status.ToString()
                }).ToList());
            }
            ExcelSheetWriter.AddSheet(workbook, "Material Requests", requestRows);
            ExcelSheetWriter.AddSheet(workbook, "Request Lines", lineRows);
            if (fullView)
            {
                ExcelSheetWriter.AddSheet(workbook, "Budget Ledger", ledger.Select(t => new ProjectExportLedgerRow
                {
                    Id = t.Id,
                    TransactionType = t.TransactionType,
                    RequestId = t.RequestId,
                    ItemId = t.ItemId,
                    Quantity = t.Quantity,
                    Amount = t.Amount,
                    OldActualCost = t.OldActualCost,
                    NewActualCost = t.NewActualCost,
                    DebitedBefore = t.DebitedBefore,
                    DebitedAfter = t.DebitedAfter,
                    PerformedByUserId = t.PerformedByUserId,
                    CreatedAt = t.CreatedAt,
                    Note = t.Note
                }).ToList());
                ExcelSheetWriter.AddSheet(workbook, "Budget Summary", new[] { new ProjectExportBudgetRow
                {
                    TotalBudget = project.TotalProjectBudget,
                    TotalSpent = spent,
                    RemainingBudget = project.TotalProjectBudget - spent,
                    TotalEstimated = requests.Sum(r => r.EstimatedCost),
                    TotalActual = requests.Sum(r => r.ActualCost),
                    Currency = project.Currency
                } });
                ExcelSheetWriter.AddSheet(workbook, "Progress", reports.Select(r => new ProjectExportProgressRow
                {
                    ReportId = r.ReportId,
                    TaskId = r.TaskId,
                    TaskName = taskById.TryGetValue(r.TaskId, out var repTask) ? repTask.TaskName : string.Empty,
                    ReportDate = r.ReportDate,
                    ProgressIncrement = r.ProgressIncrement,
                    ActualCostIncrement = r.ActualCostIncrement,
                    Status = r.Status.ToString(),
                    ReviewedAt = r.ReviewedAt,
                    Notes = r.Notes
                }).ToList());
            }
            else
            {
                var warehouse = await _warehouseContext.GetActiveWarehouseAsync();
                var inventoryRows = new List<ProjectExportInventoryRow>();
                var movementRows = new List<ProjectExportMovementRow>();
                if (warehouse != null && variantIds.Count > 0)
                {
                    var records = await _uow.Inventories.GetAllAsync(i =>
                        i.WarehouseId == warehouse.WarehouseId && variantIds.Contains(i.VariantId));
                    inventoryRows = records.OrderBy(i => i.VariantId).Select(i => new ProjectExportInventoryRow
                    {
                        VariantId = i.VariantId,
                        MaterialName = MaterialNameOf(i.VariantId, variantById, materialById),
                        VariantName = variantById.TryGetValue(i.VariantId, out var v) ? v.VariantName : $"Variant {i.VariantId}",
                        Unit = variantById.TryGetValue(i.VariantId, out var u) ? u.Unit : null,
                        QuantityOnHand = i.QuantityOnHand,
                        ReservedQuantity = i.ReservedQuantity,
                        AvailableQuantity = Math.Max(0, i.QuantityOnHand - i.ReservedQuantity - i.QuarantineQuantity),
                        AverageUnitCost = i.AverageUnitCost,
                        WarehouseName = warehouse.WarehouseName
                    }).ToList();
                    var movements = await _uow.InventoryTransactions.GetAllIgnoringQueryFiltersAsync(t =>
                        t.WarehouseId == warehouse.WarehouseId && variantIds.Contains(t.VariantId));
                    movementRows = movements
                        .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.TransactionId)
                        .Take(MaxMovementRows)
                        .Select(t => new ProjectExportMovementRow
                        {
                            TransactionId = t.TransactionId,
                            TransactionType = t.TransactionType,
                            VariantName = variantById.TryGetValue(t.VariantId, out var v) ? v.VariantName : $"Variant {t.VariantId}",
                            Quantity = t.Quantity,
                            QuantityBefore = t.QuantityBefore,
                            QuantityAfter = t.QuantityAfter,
                            Reference = string.IsNullOrWhiteSpace(t.ReferenceType) ? string.Empty : $"{t.ReferenceType}#{t.ReferenceId}",
                            TransactionDate = t.TransactionDate,
                            PerformedByUserId = t.PerformedByUserId
                        }).ToList();
                }
                ExcelSheetWriter.AddSheet(workbook, "Inventory", inventoryRows);
                ExcelSheetWriter.AddSheet(workbook, "Stock Movements", movementRows);
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return new ApiResponse().SetOk(new ConstructionPlanExcelFileResponse
            {
                Content = stream.ToArray(),
                FileName = ExcelSheetWriter.BuildDownloadFileName(null, $"{project.ProjectName}-export")
            });
        }

        private async Task<string> DisplayNameAsync(int userId)
        {
            var account = await _uow.UserAccounts.GetByIdAsync(userId);
            return account == null ? string.Empty : FormatName(account);
        }

        private static string FormatName(UserAccount account) =>
            $"{account.LastName} {account.FirstName}".Trim();

        private static string MaterialNameOf(
            int variantId,
            Dictionary<int, MaterialVariant> variantById,
            Dictionary<int, Material> materialById)
        {
            if (variantById.TryGetValue(variantId, out var variant) &&
                materialById.TryGetValue(variant.MaterialId, out var material))
                return material.MaterialName;
            return string.Empty;
        }

        private static bool IsRole(ClaimDTO claim, Role role) =>
            string.Equals(claim.Role, role.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
