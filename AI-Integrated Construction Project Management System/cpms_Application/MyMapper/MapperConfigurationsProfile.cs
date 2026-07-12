using AutoMapper;
using cpms_Application.Request.Category;
using cpms_Application.Request.Material;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Request.ProgressReport;
using cpms_Application.Request.Project;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Supplier;
using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Request.Tasks;
using cpms_Application.Request.User;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response.Category;
using cpms_Application.Response.Inventory;
using cpms_Application.Response.MaterialRequest;
using cpms_Application.Response.ProgressReport;
using cpms_Application.Response.Project;
using cpms_Application.Response.Tasks;
using cpms_Application.Response.UserAccount;
using cpms_Application.Response.Warehouse;
using cpms_Application.Response.PurchaseOrder;      // Bổ sung namespace chứa PurchaseOrderResponse
using cpms_Application.Response.OrderLineItem;     // Bổ sung namespace chứa OrderLineItemResponse
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            // === PROJECTS ===
            CreateMap<CreateProjectRequest, Project>();
            CreateMap<Project, ProjectResponse>();

            // === PURCHASE ORDERS (ĐÃ CẬP NHẬT CẤU TRÚC RESPONSE CHUẨN) ===
            CreateMap<CreatePurchaseOrderRequest, PurchaseOrder>();
            CreateMap<OrderLineItemDto, OrderLineItem>();

            // Cấu hình map lồng cho Project & Supplier bên trong PO Detail
            CreateMap<Project, ProjectDto>();
            CreateMap<Supplier, SupplierDto>();

            // Mappings cho PO Detail
            CreateMap<PurchaseOrder, PurchaseOrderResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Project != null ? src.Project.Currency : "VND"))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.OrderLineItems));

            // Mappings cho PO Item (Tính toán SubTotal và lấy thông tin vật tư)
            CreateMap<OrderLineItem, OrderLineItemResponse>()
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material != null ? src.Material.MaterialName : string.Empty))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Material != null ? src.Material.Unit : string.Empty))
                .ForMember(dest => dest.SubTotal, opt => opt.MapFrom(src => src.Quantity * src.UnitPrice));

            // === MATERIALS ===
            CreateMap<cpms_Application.Request.Material.MaterialRequest, Material>();
            CreateMap<UpdateMaterialRequest, Material>();

            // === WAREHOUSES & INVENTORY ===
            CreateMap<CreateWarehouseRequest, Warehouse>();
            CreateMap<Warehouse, WarehouseResponse>()
                .ForMember(dest => dest.ManagerName, opt => opt.MapFrom(src => src.Manager != null ? src.Manager.LastName : null));

            CreateMap<InventoryRecord, InventoryRecordDto>();

            CreateMap<InventoryRecord, InventoryReportResponse>()
                .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse.WarehouseName))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.MaterialName))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Material.Unit))
                .ForMember(dest => dest.AvailableQuantity, opt => opt.MapFrom(src => src.QuantityOnHand - src.ReservedQuantity))
                .ForMember(dest => dest.IsLowStock, opt => opt.MapFrom(src => (src.QuantityOnHand - src.ReservedQuantity) <= src.ReorderLevel));

            // === CATEGORIES & SUPPLIERS ===
            CreateMap<CreateCategoryRequest, Category>();
            CreateMap<UpdateCategoryRequest, Category>();
            CreateMap<Category, CategoryResponse>()
                .ForMember(dest => dest.TotalMaterials, opt => opt.MapFrom(src => src.Materials != null ? src.Materials.Count : 0));
            CreateMap<CreateSupplierRequest, Supplier>();
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
                .ForMember(dest => dest.AssignedToUserName, opt => opt.MapFrom(src => src.AssignedToUser != null ? $"{src.AssignedToUser.LastName} {src.AssignedToUser.FirstName}".Trim() : string.Empty))
                .ForMember(dest => dest.MaterialRequirements, opt => opt.MapFrom(src => src.MaterialRequirements));

            CreateMap<TaskMaterialRequirement, TaskMaterialResponse>()
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material != null ? src.Material.MaterialName : null))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Material != null ? src.Material.Unit : null))
                .ForMember(dest => dest.TaskName, opt => opt.MapFrom(src => src.TaskItem != null ? src.TaskItem.TaskName : null));

            // === PROGRESS REPORTS ===
            CreateMap<SubmitProgressReportRequest, ProgressReport>();
            CreateMap<ProgressReport, ProgressReportResponse>()
                .ForMember(dest => dest.TaskName, opt => opt.MapFrom(src => src.Task != null ? src.Task.TaskName : null))
                .ForMember(dest => dest.EngineerName, opt => opt.MapFrom(src => src.Engineer != null ? $"{src.Engineer.LastName} {src.Engineer.FirstName}".Trim() : string.Empty));

            // === MATERIAL REQUESTS ===
            CreateMap<MaterialItemRequest, MaterialRequisition>();

            CreateMap<CreateMaterialRequest, cpms_Domain.Models.MaterialRequest>()
                .ForMember(dest => dest.Requisitions, opt => opt.MapFrom(src => src.Items));

            CreateMap<cpms_Domain.Models.MaterialRequest, MaterialRequestResponse>()
                .ForMember(dest => dest.RequestedByName, opt => opt.MapFrom(src =>
                    src.Requester != null
                        ? $"{src.Requester.LastName} {src.Requester.FirstName}".Trim()
                        : "Người dùng hệ thống"))
                .ForMember(dest => dest.TaskId, opt => opt.MapFrom(src => src.TaskId))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Requisitions));

            CreateMap<MaterialRequisition, MaterialRequisitionDetailResponse>()
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material != null ? src.Material.MaterialName : null))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Material != null ? src.Material.Unit : null));



            CreateMap<ProjectBudgetHistory, ProjectBudgetHistoryResponse>()
    .ForMember(dest => dest.Currency,
        opt => opt.Ignore());
        }
    }
}