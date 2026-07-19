using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Supplier;
using cpms_Application.Response;
using cpms_Application.Response.Supplier;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public SupplierService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateSupplierAsync(CreateSupplierRequest request)
        {
            var companyName = request.CompanyName.Trim();
            var contactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim().ToLowerInvariant();
            var duplicate = await _uow.Suppliers.GetAsync(s => !s.IsDeleted &&
                (s.CompanyName == companyName || (contactEmail != null && s.ContactEmail == contactEmail)));
            if (duplicate != null)
                return new ApiResponse().SetConflict("An active supplier already uses this company name or contact email.");
            var supplier = _mapper.Map<Supplier>(request);
            supplier.CompanyName = companyName;
            supplier.ContactEmail = contactEmail;
            supplier.ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim();
            await _uow.Suppliers.AddAsync(supplier);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Created, true,
                result: _mapper.Map<SupplierResponse>(supplier));
        }

        public async Task<ApiResponse> GetAllSuppliersAsync()
        {
            var suppliers = await _uow.Suppliers.GetAllAsync(s => !s.IsDeleted);
            return new ApiResponse().SetOk(_mapper.Map<List<SupplierResponse>>(suppliers));
        }

        public async Task<ApiResponse> GetSupplierByIdAsync(int supplierId)
        {
            var supplier = await _uow.Suppliers.GetByIdAsync(supplierId);
            return supplier == null || supplier.IsDeleted
                ? new ApiResponse().SetNotFound("Supplier not found.")
                : new ApiResponse().SetOk(_mapper.Map<SupplierResponse>(supplier));
        }

        public async Task<ApiResponse> UpdateSupplierAsync(int supplierId, UpdateSupplierRequest request)
        {
            var supplier = await _uow.Suppliers.GetByIdAsync(supplierId);
            if (supplier == null || supplier.IsDeleted) return new ApiResponse().SetNotFound("Supplier not found.");
            var companyName = request.CompanyName.Trim();
            var contactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim().ToLowerInvariant();
            var duplicate = await _uow.Suppliers.GetAsync(s => s.SupplierId != supplierId && !s.IsDeleted &&
                (s.CompanyName == companyName || (contactEmail != null && s.ContactEmail == contactEmail)));
            if (duplicate != null)
                return new ApiResponse().SetConflict("Another active supplier already uses this company name or contact email.");
            supplier.CompanyName = companyName;
            supplier.ContactEmail = contactEmail;
            supplier.ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim();
            supplier.ModifiedDate = DateTime.UtcNow;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<SupplierResponse>(supplier));
        }

        public async Task<ApiResponse> DeactivateSupplierAsync(int supplierId)
        {
            var supplier = await _uow.Suppliers.GetByIdAsync(supplierId);
            if (supplier == null || supplier.IsDeleted) return new ApiResponse().SetNotFound("Supplier not found.");
            var openOrder = await _uow.PurchaseOrders.GetAsync(order => order.SupplierId == supplierId &&
                (order.Status == PurchaseOrderStatus.PENDING || order.Status == PurchaseOrderStatus.APPROVED ||
                 order.Status == PurchaseOrderStatus.PROCESSING || order.Status == PurchaseOrderStatus.SHIPPED ||
                 order.Status == PurchaseOrderStatus.PARTIALLY_RECEIVED));
            if (openOrder != null)
                return new ApiResponse().SetConflict("Close or cancel this supplier's open purchase orders before deactivation.");
            var offers = await _uow.SupplierCatalogs.GetAllAsync(offer => offer.SupplierId == supplierId && offer.IsAvailable);
            foreach (var offer in offers)
            {
                offer.IsAvailable = false;
                offer.ModifiedDate = DateTime.UtcNow;
            }
            supplier.IsDeleted = true;
            supplier.ModifiedDate = DateTime.UtcNow;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Supplier deactivated and its catalog offers disabled.");
        }
    }
}
