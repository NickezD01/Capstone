using AutoMapper;
using cpms_Application.Request.Material;
using cpms_Application.Request.Project;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Supplier;
using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response.Project;
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
            // Project mapping
            CreateMap<CreateProjectRequest, Project>()
                .ForMember(dest => dest.BaselineStart, opt => opt.MapFrom(src => src.StartDate.HasValue ? (DateOnly?)DateOnly.FromDateTime(src.StartDate.Value) : null))
                .ForMember(dest => dest.ProjectManagerId, opt => opt.MapFrom(src => src.ProjectManagerId))
                .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.CustomerId));

            CreateMap<Project, ProjectResponse>()
                .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.BaselineStart.HasValue ? (DateTime?)src.BaselineStart.Value.ToDateTime(TimeOnly.MinValue) : null))
                .ForMember(dest => dest.ProjectManagerId, opt => opt.MapFrom(src => src.ProjectManagerId))
                .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.CustomerId));

            CreateMap<CreatePurchaseOrderRequest, PurchaseOrder>();
            CreateMap<OrderLineItemDto, PurchaseOrderDetail>();

            CreateMap<CreateMaterialRequest, Material>();

            CreateMap<CreateSupplierRequest, Supplier>();
            
            CreateMap<CreateCatalogRequest, SupplierCatalog>();

            CreateMap<CreateWarehouseRequest, Warehouse>();
        }
    }
}
