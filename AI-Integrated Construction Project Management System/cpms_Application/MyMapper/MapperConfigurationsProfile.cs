using AutoMapper;
using cpms_Application.Request.Category;
using cpms_Application.Request.Material; // Chứa UpdateMaterialRequest, MaterialItemRequest
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
            // ==========================================
            // USER ACCOUNT MAPPING
            // ==========================================
            CreateMap<UpdateUserRoleRequest, UserAccount>();
            CreateMap<UpdateUserRequest, UserAccount>();
            CreateMap<UserAccount, UserProfileResponse>();
            CreateMap<UserAccount, AccountResponse>();

            // ==========================================
            // PROJECT MAPPING
            // ==========================================
            CreateMap<CreateProjectRequest, Project>();
            CreateMap<Project, ProjectResponse>();

            // ==========================================
            // PURCHASING & SUPPLIER MAPPING
            // ==========================================
            CreateMap<CreatePurchaseOrderRequest, PurchaseOrder>();
            CreateMap<OrderLineItemDto, OrderLineItem>();

            // 🚀 ĐỊNH DANH RÕ RÀNG: Tránh xung đột giữa DTO thêm vật tư và Thực thể Phiếu yêu cầu vật tư
            CreateMap<cpms_Application.Request.Material.MaterialRequest, Material>();
            CreateMap<UpdateMaterialRequest, Material>();

            CreateMap<Warehouse, WarehouseResponse>()
                .ForMember(dest => dest.ManagerName, opt => opt.MapFrom(src => src.Manager != null ? src.Manager.LastName : null));

            CreateMap<InventoryRecord, InventoryRecordDto>();
            CreateMap<CreateCategoryRequest, Category>();
            CreateMap<UpdateCategoryRequest, Category>();
            CreateMap<Category, CategoryResponse>()
                .ForMember(dest => dest.TotalMaterials, opt => opt.MapFrom(src => src.Materials != null ? src.Materials.Count : 0));
            CreateMap<CreateSupplierRequest, Supplier>();
            CreateMap<CreateCatalogRequest, SupplierCatalog>();

            // ==========================================
            // WAREHOUSE & INVENTORY RECORD MAPPING
            // ==========================================
            CreateMap<CreateWarehouseRequest, Warehouse>();

            CreateMap<InventoryRecord, InventoryReportResponse>()
                .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse.WarehouseName))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.MaterialName))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Material.Unit))
                .ForMember(dest => dest.AvailableQuantity, opt => opt.MapFrom(src => src.QuantityOnHand - src.ReservedQuantity))
                .ForMember(dest => dest.IsLowStock, opt => opt.MapFrom(src => (src.QuantityOnHand - src.ReservedQuantity) <= src.ReorderLevel));

            // ==========================================
            // TASKS & PROGRESS REPORT MAPPING
            // ==========================================
            CreateMap<CreateTaskRequest, TaskItem>();
            CreateMap<TaskItem, TaskResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.AssignedToUserName, opt => opt.MapFrom(src => $"{src.AssignedToUser.LastName} {src.AssignedToUser.FirstName}")); 

            CreateMap<SubmitProgressReportRequest, ProgressReport>();
            CreateMap<ProgressReport, ProgressReportResponse>()
                .ForMember(dest => dest.TaskName, opt => opt.MapFrom(src => src.Task.TaskName))
                .ForMember(dest => dest.EngineerName, opt => opt.MapFrom(src => $"{src.Engineer.LastName} {src.Engineer.FirstName}")); 

            // ========================================================
            // MATERIAL REQUEST (PHIẾU YÊU CẦU VẬT TƯ) MAPPINGS
            // ========================================================

            // 1. Map từ từng dòng chi tiết trong Request body sang Entity DB
            CreateMap<MaterialItemRequest, MaterialRequisition>();

            // 2. Map từ Entity phiếu tổng sang Phiếu tổng Response DTO trả về cho Client
            CreateMap<cpms_Domain.Models.MaterialRequest, MaterialRequestResponse>()
                .ForMember(dest => dest.RequestedByName, opt => opt.MapFrom(src => $"{src.Requester.LastName} {src.Requester.FirstName}"))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Requisitions));

            // 3. Map từ Entity chi tiết dòng vật tư sang DTO dòng vật tư hiển thị kèm tên cụ thể
            CreateMap<MaterialRequisition, MaterialRequisitionDetailResponse>()
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.MaterialName));
        }
    }
}