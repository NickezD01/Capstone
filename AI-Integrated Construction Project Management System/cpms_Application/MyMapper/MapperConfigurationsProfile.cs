using AutoMapper;
using cpms_Application.Request.Category;
using cpms_Application.Request.Material;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Request.ProgressReport;
using cpms_Application.Request.Project;
using cpms_Application.Request.ProjectPhase;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Supplier;
using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Request.Tasks;
using cpms_Application.Request.User;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response.Category;
using cpms_Application.Response.Inventory;
using cpms_Application.Response.MaterialRequest;
using cpms_Application.Response.Material;
using cpms_Application.Response.ProgressReport;
using cpms_Application.Response.Project;
using cpms_Application.Response.ProjectPhase;
using cpms_Application.Response.Tasks;
using cpms_Application.Response.UserAccount;
using cpms_Application.Response.Warehouse;
using cpms_Application.Response.PurchaseOrder;
using cpms_Application.Response.OrderLineItem;
using cpms_Application.Response.Supplier;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace cpms_Application.MyMapper
{
    public class MapperConfigurationsProfile : Profile
    {
        public MapperConfigurationsProfile()
        {
            // === USER ACCOUNTS ===
            CreateMap<UpdateUserRoleRequest, UserAccount>();
            CreateMap<UpdateUserRequest, UserAccount>();
            CreateMap<UserAccount, UserProfileResponse>();
            CreateMap<UserAccount, AccountResponse>();

            // === PROJECTS & PHASES ===
            CreateMap<CreateProjectRequest, Project>();
            CreateMap<CreateProjectPhaseRequest, ProjectPhase>();
            CreateMap<UpdateProjectPhaseRequest, ProjectPhase>();
            CreateMap<ProjectPhase, ProjectPhaseResponse>();

            CreateMap<Project, ProjectResponse>()
                .ForMember(dest => dest.RowVersion, opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)))
                .ForMember(dest => dest.BudgetConfigured, opt => opt.MapFrom(src => src.TotalProjectBudget > 0))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.PMName, opt => opt.MapFrom(src => src.ProjectManager != null ? $"{src.ProjectManager.LastName} {src.ProjectManager.FirstName}".Trim() : string.Empty))
                .ForMember(dest => dest.TotalTasks, opt => opt.MapFrom(src => src.Tasks.Count))
                .ForMember(dest => dest.ActualCost, opt => opt.MapFrom(src => src.Tasks.Sum(task => task.ActualCost)))
                .ForMember(dest => dest.PlannedTaskBudget, opt => opt.MapFrom(src => src.Tasks.Where(task => task.Status != cpms_Domain.Models.TaskStatus.CANCELLED && task.Status != cpms_Domain.Models.TaskStatus.REJECTED).Sum(task => task.PlannedBudget)))
                .ForMember(dest => dest.ReportedTaskActualCost, opt => opt.MapFrom(src => src.Tasks.Sum(task => task.ActualCost)))
                .ForMember(dest => dest.PurchaseOrderCommittedCost, opt => opt.MapFrom(src => src.PurchaseOrders.Where(order => order.Status != PurchaseOrderStatus.REJECTED && order.Status != PurchaseOrderStatus.CANCELLED).Sum(order => order.TotalAmount)))
                .ForMember(dest => dest.PurchaseOrderReceivedCost, opt => opt.MapFrom(src => src.PurchaseOrders.Where(order => order.Status != PurchaseOrderStatus.REJECTED && order.Status != PurchaseOrderStatus.CANCELLED).SelectMany(order => order.OrderLineItems).Sum(line => line.ReceivedQuantity * line.UnitPrice)))
                .ForMember(dest => dest.RemainingProcurementBudget, opt => opt.MapFrom(src => Math.Max(0, src.TotalProjectBudget - src.PurchaseOrders.Where(order => order.Status != PurchaseOrderStatus.REJECTED && order.Status != PurchaseOrderStatus.CANCELLED).Sum(order => order.TotalAmount))))
                .ForMember(dest => dest.TotalAIAlerts, opt => opt.MapFrom(src => src.AIAlerts.Count));

            // === PURCHASE ORDERS ===
            CreateMap<CreatePurchaseOrderRequest, PurchaseOrder>();
            CreateMap<OrderLineItemDto, OrderLineItem>();
            CreateMap<Project, ProjectDto>();
            CreateMap<Supplier, SupplierDto>().ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.CompanyName));
            CreateMap<PurchaseOrder, PurchaseOrderResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.RowVersion, opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)))
                .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Project != null ? src.Project.Currency : "VND"))
                .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse != null ? src.Warehouse.WarehouseName : string.Empty))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.OrderLineItems));
            CreateMap<OrderLineItem, OrderLineItemResponse>()
                .ForMember(dest => dest.OrderLineItemId, opt => opt.MapFrom(src => src.LineItemId))
                .ForMember(dest => dest.MaterialId, opt => opt.MapFrom(src => src.Variant.MaterialId))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Variant.Material.MaterialName))
                .ForMember(dest => dest.VariantName, opt => opt.MapFrom(src => src.Variant.VariantName))
                .ForMember(dest => dest.SKU, opt => opt.MapFrom(src => src.Variant.SKU))
                .ForMember(dest => dest.Brand, opt => opt.MapFrom(src => src.Variant.Brand))
                .ForMember(dest => dest.Grade, opt => opt.MapFrom(src => src.Variant.Grade))
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Variant.Size))
                .ForMember(dest => dest.Specification, opt => opt.MapFrom(src => src.Variant.Specification))
                .ForMember(dest => dest.Packaging, opt => opt.MapFrom(src => src.Variant.Packaging))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Variant.Unit))
                .ForMember(dest => dest.SubTotal, opt => opt.MapFrom(src => src.Quantity * src.UnitPrice));

            // === MATERIALS ===
            CreateMap<cpms_Application.Request.Material.MaterialRequest, Material>();
            CreateMap<UpdateMaterialRequest, Material>();
            CreateMap<MaterialVariantRequest, MaterialVariant>();
            CreateMap<MaterialVariant, MaterialVariantResponse>()
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.MaterialName));
            CreateMap<Material, MaterialResponse>();

            // === WAREHOUSES & INVENTORY ===
            CreateMap<CreateWarehouseRequest, Warehouse>();
            CreateMap<Warehouse, WarehouseResponse>()
                .ForMember(dest => dest.ManagerName, opt => opt.MapFrom(src => src.Manager != null ? src.Manager.LastName : null));
            CreateMap<InventoryRecord, InventoryRecordDto>();
            CreateMap<InventoryRecord, InventoryReportResponse>()
                .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse.WarehouseName))
                .ForMember(dest => dest.MaterialId, opt => opt.MapFrom(src => src.Variant.MaterialId))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Variant.Material.MaterialName))
                .ForMember(dest => dest.VariantName, opt => opt.MapFrom(src => src.Variant.VariantName))
                .ForMember(dest => dest.SKU, opt => opt.MapFrom(src => src.Variant.SKU))
                .ForMember(dest => dest.Brand, opt => opt.MapFrom(src => src.Variant.Brand))
                .ForMember(dest => dest.Grade, opt => opt.MapFrom(src => src.Variant.Grade))
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Variant.Size))
                .ForMember(dest => dest.Specification, opt => opt.MapFrom(src => src.Variant.Specification))
                .ForMember(dest => dest.Packaging, opt => opt.MapFrom(src => src.Variant.Packaging))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Variant.Unit))
                .ForMember(dest => dest.RowVersion, opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)))
                .ForMember(dest => dest.IsLowStock, opt => opt.MapFrom(src => src.AvailableQuantity <= src.ReorderLevel));
            CreateMap<InventoryTransaction, InventoryTransactionResponse>();

            // === CATEGORIES & SUPPLIERS ===
            CreateMap<CreateCategoryRequest, Category>();
            CreateMap<UpdateCategoryRequest, Category>();
            CreateMap<Category, CategoryResponse>()
                .ForMember(dest => dest.TotalMaterials, opt => opt.MapFrom(src => src.Materials != null ? src.Materials.Count : 0));
            CreateMap<CreateSupplierRequest, Supplier>();
            CreateMap<Supplier, SupplierResponse>();
            CreateMap<CreateCatalogRequest, SupplierCatalog>();

            // === TASKS ===
            CreateMap<CreateTaskRequest, TaskItem>()
                .ForMember(dest => dest.TaskId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.ActualCost, opt => opt.Ignore())
                .ForMember(dest => dest.ActualProgressPct, opt => opt.Ignore())
                .ForMember(dest => dest.Project, opt => opt.Ignore())
                .ForMember(dest => dest.AssignedToUser, opt => opt.Ignore())
                .ForMember(dest => dest.MaterialRequirements, opt => opt.Ignore())
                .ForMember(dest => dest.ProgressReports, opt => opt.Ignore());

            CreateMap<TaskItem, TaskResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.RowVersion, opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)))
                .ForMember(dest => dest.ProjectPhaseId, opt => opt.MapFrom(src => src.ProjectPhaseId))
                .ForMember(dest => dest.PhaseName, opt => opt.MapFrom(src => src.ProjectPhase.PhaseName))
                .ForMember(dest => dest.AssignedToUserName, opt => opt.MapFrom(src => src.AssignedToUser != null ? $"{src.AssignedToUser.LastName} {src.AssignedToUser.FirstName}".Trim() : string.Empty))
                .ForMember(dest => dest.MaterialRequirements, opt => opt.MapFrom(src => src.MaterialRequirements));

            CreateMap<TaskMaterialRequirement, TaskMaterialResponse>()
                .ForMember(dest => dest.MaterialId, opt => opt.MapFrom(src => src.Variant.MaterialId))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Variant.Material.MaterialName))
                .ForMember(dest => dest.VariantName, opt => opt.MapFrom(src => src.Variant.VariantName))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Variant.Unit))
                .ForMember(dest => dest.TaskName, opt => opt.MapFrom(src => src.TaskItem != null ? src.TaskItem.TaskName : null));

            // === PROGRESS REPORTS ===
            CreateMap<SubmitProgressReportRequest, ProgressReport>();
            CreateMap<ProgressReport, ProgressReportResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.RowVersion, opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)))
                .ForMember(dest => dest.TaskName, opt => opt.MapFrom(src => src.Task != null ? src.Task.TaskName : null))
                .ForMember(dest => dest.ReportedByName, opt => opt.MapFrom(src => src.Reporter != null ? $"{src.Reporter.LastName} {src.Reporter.FirstName}".Trim() : string.Empty));

            // === MATERIAL REQUESTS ===
            CreateMap<MaterialItemRequest, MaterialRequisition>();
            CreateMap<CreateMaterialRequest, cpms_Domain.Models.MaterialRequest>()
                .ForMember(dest => dest.Requisitions, opt => opt.MapFrom(src => src.Items));
            CreateMap<cpms_Domain.Models.MaterialRequest, MaterialRequestResponse>()
                .ForMember(dest => dest.RowVersion, opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)))
                .ForMember(dest => dest.RequestedByName, opt => opt.MapFrom(src => src.Requester != null ? $"{src.Requester.LastName} {src.Requester.FirstName}".Trim() : "System user"))
                .ForMember(dest => dest.TaskId, opt => opt.MapFrom(src => src.TaskId))
                .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse != null ? src.Warehouse.WarehouseName : null))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Requisitions));
            CreateMap<MaterialRequisition, MaterialRequisitionDetailResponse>()
                .ForMember(dest => dest.MaterialId, opt => opt.MapFrom(src => src.Variant.MaterialId))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Variant.Material.MaterialName))
                .ForMember(dest => dest.VariantName, opt => opt.MapFrom(src => src.Variant.VariantName))
                .ForMember(dest => dest.SKU, opt => opt.MapFrom(src => src.Variant.SKU))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Variant.Unit));

            CreateMap<ProjectBudgetHistory, ProjectBudgetHistoryResponse>().ForMember(dest => dest.Currency, opt => opt.Ignore());
        }
    }
}
