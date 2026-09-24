using ClosedXML.Excel;
using cpms_Application.Response;
using cpms_Application.Response.AiConstructionPlanner;
using cpms_Application.Services;
using cpms_Domain;
using cpms_Domain.Models;
using System.Net;

namespace cpms_Tests;

public class ProjectExportServiceTests
{
    [Fact]
    public async Task OwningPmGetsFullWorkbook()
    {
        var uow = CreateFixture();
        var service = new ProjectExportService(uow, new FakeClaimService(5, Role.PM));

        var response = await service.ExportProjectAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        using var workbook = OpenWorkbook(response);
        Assert.Equal(
            new[] { "Project", "Phases", "Tasks", "Gantt", "Material Requests", "Request Lines", "Budget Ledger", "Budget Summary", "Progress" },
            workbook.Worksheets.Select(w => w.Name).ToArray());
        Assert.Equal("Tower A", CellValue(workbook, "Project", "Project Name"));
        Assert.Equal(956m, CellValue(workbook, "Budget Summary", "Remaining Budget"));
        Assert.Equal("Excavate", CellValue(workbook, "Tasks", "Task Name"));
        Assert.Equal(44m, CellValue(workbook, "Request Lines", "Debited Amount"));
        Assert.Equal(4m, CellValue(workbook, "Request Lines", "Net Issued Quantity"));
    }

    [Fact]
    public async Task AssignedCustomerGetsIdenticalWorkbook()
    {
        var uow = CreateFixture();
        var pmService = new ProjectExportService(uow, new FakeClaimService(5, Role.PM));
        var customerService = new ProjectExportService(uow, new FakeClaimService(20, Role.CUSTOMER));

        var pmResponse = await pmService.ExportProjectAsync(1);
        var customerResponse = await customerService.ExportProjectAsync(1);

        Assert.True(customerResponse.IsSuccess, customerResponse.ErrorMessage);
        using var pmWorkbook = OpenWorkbook(pmResponse);
        using var customerWorkbook = OpenWorkbook(customerResponse);
        Assert.Equal(
            pmWorkbook.Worksheets.Select(w => w.Name).ToArray(),
            customerWorkbook.Worksheets.Select(w => w.Name).ToArray());
        Assert.Equal(
            CellValue(pmWorkbook, "Budget Summary", "Remaining Budget"),
            CellValue(customerWorkbook, "Budget Summary", "Remaining Budget"));
        Assert.Equal(
            CellValue(pmWorkbook, "Project", "Total Budget"),
            CellValue(customerWorkbook, "Project", "Total Budget"));
    }

    [Theory]
    [InlineData(6, "PM")]
    [InlineData(21, "CUSTOMER")]
    [InlineData(30, "SUPPLIER")]
    [InlineData(31, "WORKER")]
    public async Task UnauthorizedCallersAreForbidden(int userId, string role)
    {
        var uow = CreateFixture();
        var service = new ProjectExportService(
            uow,
            new FakeClaimService(userId, Enum.Parse<Role>(role)));

        var response = await service.ExportProjectAsync(1);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MissingProjectReturnsNotFound()
    {
        var uow = CreateFixture();
        var service = new ProjectExportService(uow, new FakeClaimService(1, Role.ADMIN));

        var response = await service.ExportProjectAsync(99);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminGetsFullWorkbook()
    {
        var uow = CreateFixture();
        var service = new ProjectExportService(uow, new FakeClaimService(1, Role.ADMIN));

        var response = await service.ExportProjectAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        using var workbook = OpenWorkbook(response);
        Assert.Equal(9, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Budget Ledger"));
        Assert.True(workbook.Worksheets.Contains("Gantt"));
    }

    [Fact]
    public async Task FullWorkbookContainsGanttSheet()
    {
        var uow = CreateFixture();
        var service = new ProjectExportService(uow, new FakeClaimService(5, Role.PM));

        var response = await service.ExportProjectAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        using var workbook = OpenWorkbook(response);
        var gantt = workbook.Worksheet("Gantt");
        Assert.Contains("Legend", gantt.Cell(1, 1).GetString());
        Assert.Equal(new DateTime(2026, 10, 1), gantt.Cell(2, 5).GetDateTime());
        Assert.Equal("Foundation", gantt.Cell(3, 1).GetString());
        Assert.True(gantt.Cell(3, 1).Style.Font.Bold);
        Assert.Equal("Excavate", gantt.Cell(4, 1).GetString().Trim());
        Assert.Equal(XLColor.FromHtml("#BDD7EE"), gantt.Cell(4, 5).Style.Fill.BackgroundColor);
        Assert.NotEqual(XLColor.FromHtml("#BDD7EE"), gantt.Cell(4, 14).Style.Fill.BackgroundColor);
        Assert.Equal("Old works", gantt.Cell(5, 1).GetString().Trim());
        Assert.Equal("\u26a0", gantt.Cell(5, 4).GetString());
    }

    [Fact]
    public async Task LinkedWarehouseManagerGetsInventoryView()
    {
        var uow = CreateFixture();
        var service = new ProjectExportService(uow, new FakeClaimService(10, Role.WAREHOUSE_MANAGER));

        var response = await service.ExportProjectAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        using var workbook = OpenWorkbook(response);
        Assert.Equal(
            new[] { "Project", "Material Requests", "Request Lines", "Inventory", "Stock Movements" },
            workbook.Worksheets.Select(w => w.Name).ToArray());
        Assert.Equal(45m, CellValue(workbook, "Inventory", "Available Quantity"));
        Assert.Equal("ISSUE", CellValue(workbook, "Stock Movements", "Transaction Type"));
    }

    [Fact]
    public async Task UnlinkedWarehouseManagerIsForbidden()
    {
        var uow = CreateFixture();
        var service = new ProjectExportService(uow, new FakeClaimService(99, Role.WAREHOUSE_MANAGER));

        var response = await service.ExportProjectAsync(1);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static TestUnitOfWork CreateFixture()
    {
        var uow = new TestUnitOfWork();
        var project = new Project
        {
            ProjectId = 1,
            ProjectName = "Tower A",
            Address = "District 7",
            Status = ProjectStatus.IN_PROGRESS,
            StartDate = new DateTime(2026, 10, 1),
            BaselineStart = new DateTime(2026, 10, 1),
            BaselineEnd = new DateTime(2027, 6, 1),
            TotalProjectBudget = 1000,
            Currency = "VND",
            PMUserID = 5,
            CustomerUserId = 20
        };
        var phase = new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Project = project,
            Name = "Foundation",
            SequenceOrder = 0,
            BaselineStart = new DateTime(2026, 10, 1),
            BaselineEnd = new DateTime(2026, 12, 31),
            Status = PhaseStatus.IN_PROGRESS
        };
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            Project = project,
            PhaseId = 1,
            Phase = phase,
            PhaseName = "Foundation",
            TaskName = "Excavate",
            AssignedToUserID = 5,
            PlannedBudget = 500,
            ActualCost = 44,
            BaselineStart = new DateTime(2026, 10, 1),
            BaselineEnd = new DateTime(2026, 11, 30),
            ActualProgressPct = 25,
            Status = cpms_Domain.Models.TaskStatus.IN_PROGRESS
        };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "Main", ManagerId = 10, IsActive = true };
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Grade 60", Unit = "kg", IsActive = true };
        var item = new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            VariantId = 1,
            Variant = variant,
            Quantity = 10,
            ApprovedQuantity = 10,
            IssuedQuantity = 5,
            UnitActualCost = 11,
            NeededByDate = new DateTime(2026, 10, 15)
        };
        var request = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            TaskId = 1,
            WarehouseId = 1,
            Warehouse = warehouse,
            RequestedBy = 5,
            RequestDate = new DateTime(2026, 10, 2),
            Status = MaterialRequestStatuses.Issued,
            RequestNote = "Foundation steel",
            EstimatedCost = 600,
            ActualCost = 110,
            BudgetDebitedAmount = 44,
            Requisitions = new List<MaterialRequisition> { item }
        };
        item.MaterialRequest = request;
        uow.ProjectRecords.Add(project);
        uow.PhaseRecords.Add(phase);
        uow.TaskRecords.Add(task);
        uow.TaskRecords.Add(new TaskItem
        {
            TaskId = 2,
            ProjectId = 1,
            Project = project,
            PhaseId = 1,
            Phase = phase,
            PhaseName = "Foundation",
            TaskName = "Old works",
            AssignedToUserID = 5,
            PlannedBudget = 100,
            ActualCost = 0,
            BaselineStart = new DateTime(2000, 1, 1),
            BaselineEnd = new DateTime(2000, 2, 1),
            ActualProgressPct = 10,
            Status = cpms_Domain.Models.TaskStatus.IN_PROGRESS
        });
        uow.UserAccountRecords.AddRange(new[]
        {
            new UserAccount { Id = 5, Role = Role.PM, IsEmailVerified = true, FirstName = "Pat", LastName = "Manager" },
            new UserAccount { Id = 20, Role = Role.CUSTOMER, IsEmailVerified = true, FirstName = "Cara", LastName = "Client" }
        });
        uow.WarehouseRecords.Add(warehouse);
        uow.MaterialRecords.Add(material);
        uow.VariantRecords.Add(variant);
        uow.InventoryRecords.Add(new InventoryRecord
        {
            InventoryId = 1,
            WarehouseId = 1,
            Warehouse = warehouse,
            VariantId = 1,
            Variant = variant,
            QuantityOnHand = 50,
            ReservedQuantity = 5,
            AverageUnitCost = 12
        });
        uow.TransactionRecords.Add(new InventoryTransaction
        {
            TransactionId = 1,
            InventoryId = 1,
            WarehouseId = 1,
            VariantId = 1,
            TransactionType = InventoryTransactionTypes.Issue,
            Quantity = -5,
            QuantityBefore = 55,
            QuantityAfter = 50,
            ReferenceId = 1,
            ReferenceType = "MATERIAL_REQUEST",
            UnitCost = 12,
            TotalValue = 60,
            PerformedByUserId = 10,
            TransactionDate = new DateTime(2026, 10, 5)
        });
        uow.RequestRecords.Add(request);
        uow.RequisitionRecords.Add(item);
        uow.MaterialReturnRecords.Add(new MaterialReturn
        {
            ReturnId = 1,
            MaterialRequestId = 1,
            WarehouseId = 1,
            VariantId = 1,
            Quantity = 1,
            RecordedByUserId = 10,
            ReturnedAt = new DateTime(2026, 10, 6)
        });
        uow.MaterialBudgetTransactionRecords.AddRange(new[]
        {
            new MaterialBudgetTransaction
            {
                Id = 1,
                ProjectId = 1,
                RequestId = 1,
                ItemId = 1,
                VariantId = 1,
                Quantity = 5,
                TransactionType = MaterialBudgetTransactionTypes.IssueDebit,
                Amount = 55,
                OldActualCost = 110,
                NewActualCost = 110,
                DebitedBefore = 0,
                DebitedAfter = 55,
                PerformedByUserId = 10,
                CreatedAt = new DateTime(2026, 10, 5)
            },
            new MaterialBudgetTransaction
            {
                Id = 2,
                ProjectId = 1,
                RequestId = 1,
                ItemId = 1,
                VariantId = 1,
                Quantity = 1,
                TransactionType = MaterialBudgetTransactionTypes.ReturnReversal,
                Amount = -11,
                OldActualCost = 110,
                NewActualCost = 110,
                DebitedBefore = 55,
                DebitedAfter = 44,
                PerformedByUserId = 10,
                CreatedAt = new DateTime(2026, 10, 6)
            }
        });
        uow.ProgressReportRecords.Add(new ProgressReport
        {
            ReportId = 1,
            TaskId = 1,
            Task = task,
            ReportedByUserId = 5,
            ReportDate = new DateTime(2026, 10, 20),
            ProgressIncrement = 25,
            ActualCostIncrement = 44,
            Status = ProgressReportStatus.APPROVED,
            ReviewedAt = new DateTime(2026, 10, 21),
            Notes = "On track"
        });
        return uow;
    }

    private static XLWorkbook OpenWorkbook(ApiResponse response)
    {
        var file = Assert.IsType<ConstructionPlanExcelFileResponse>(response.Result);
        Assert.NotEmpty(file.Content);
        Assert.StartsWith("Tower A-export-", file.FileName);
        Assert.EndsWith(".xlsx", file.FileName);
        return new XLWorkbook(new MemoryStream(file.Content));
    }

    private static object CellValue(XLWorkbook workbook, string sheet, string header)
    {
        var worksheet = workbook.Worksheet(sheet);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var column = 1; column <= lastColumn; column++)
        {
            if (string.Equals(worksheet.Cell(1, column).GetString(), header, StringComparison.Ordinal))
            {
                var value = worksheet.Cell(2, column).Value;
                if (value.IsNumber)
                    return (decimal)value.GetNumber();
                if (value.IsDateTime)
                    return value.GetDateTime();
                if (value.IsBoolean)
                    return value.GetBoolean();
                return worksheet.Cell(2, column).GetString();
            }
        }
        throw new InvalidOperationException($"Header '{header}' not found in sheet '{sheet}'.");
    }
}
