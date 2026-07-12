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
            // 🚀 ĐÃ SỬA CÚ PHÁP & BỔ SUNG KHÓA AN TOÀN: Bỏ qua toàn bộ các quan hệ để tránh lỗi Inner Exception khi SaveChanges
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
                // 🚀 BỔ SUNG: Tự động ánh xạ danh sách định mức của Task sang DTO đầu ra
                .ForMember(dest => dest.MaterialRequirements, opt => opt.MapFrom(src => src.MaterialRequirements));

            // 🚀 BỔ SUNG: Định nghĩa chi tiết cách bóc tách dữ liệu từ Entity sang Response phẳng cho Front-end
            CreateMap<TaskMaterialRequirement, TaskMaterialResponse>()
                // Bốc tên vật tư từ thực thể Material liên kết sang
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material != null ? src.Material.MaterialName : null))
                // Bốc đơn vị tính (Bao, Tấn, Khối...) từ thực thể Material liên kết sang
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Material != null ? src.Material.Unit : null))
                // Thêm dòng này để Front-end hiển thị được tên của Task chứa định mức vật tư đó
                .ForMember(dest => dest.TaskName, opt => opt.MapFrom(src => src.TaskItem != null ? src.TaskItem.TaskName : null));

            CreateMap<SubmitProgressReportRequest, ProgressReport>();

            CreateMap<ProgressReport, ProgressReportResponse>()
                .ForMember(dest => dest.TaskName, opt => opt.MapFrom(src => src.Task != null ? src.Task.TaskName : null))
                .ForMember(dest => dest.EngineerName, opt => opt.MapFrom(src => src.Engineer != null ? $"{src.Engineer.LastName} {src.Engineer.FirstName}".Trim() : string.Empty));

            // ========================================================
            // MATERIAL REQUEST (PHIẾU YÊU CẦU VẬT TƯ) MAPPINGS
            // ========================================================

            // 1. Map từ từng dòng chi tiết trong Request body sang Entity DB
            CreateMap<MaterialItemRequest, MaterialRequisition>();

            // 2. Map từ Entity phiếu tổng sang Phiếu tổng Response DTO trả về cho Client
            CreateMap<cpms_Domain.Models.MaterialRequest, MaterialRequestResponse>()
                .ForMember(dest => dest.RequestedByName, opt => opt.MapFrom(src => src.Requester != null ? $"{src.Requester.LastName} {src.Requester.FirstName}".Trim() : string.Empty))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Requisitions));

            // 3. Map từ Entity chi tiết dòng vật tư sang DTO dòng vật tư hiển thị kèm tên cụ thể
            CreateMap<MaterialRequisition, MaterialRequisitionDetailResponse>()
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material != null ? src.Material.MaterialName : null));
        }
    }
}