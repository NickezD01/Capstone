using cpms_Application;
using cpms_Application.Repository;
using cpms_Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        // =========================================
        // PROPERTIES (Chỉ đọc bên ngoài, gán bên trong)
        // =========================================
        public IUserAccountRepository UserAccounts { get; }
        public IProjectRepository Projects { get; }
        public IGenericRepository<cpms_Domain.Models.ProjectPhase> ProjectPhases { get; }
        public ITaskItemRepository TaskItems { get; }
        public IProgressReportRepository ProgressReports { get; }
        public IMaterialRepository Materials { get; }
        public IGenericRepository<cpms_Domain.Models.MaterialVariant> MaterialVariants { get; }
        public ISupplierRepository Suppliers { get; }
        public ISupplierCatalogRepository SupplierCatalogs { get; }
        public ISupplierMetricRepository SupplierMetrics { get; }
        public IPurchaseOrderRepository PurchaseOrders { get; }
        public IOrderLineItemRepository OrderLineItems { get; }
        public IEmailVerificationRepository EmailVerifications { get; }
        public IGenericRepository<cpms_Domain.Models.EmailOutboxMessage> EmailOutboxMessages { get; }
        public IRefreshTokenRepository RefreshTokens { get; }
        public ICategoryRepository Categories { get; }
        public IWarehouseRepository Warehouses { get; }
        public IInventoryRepository Inventories { get; }
        public IGenericRepository<cpms_Domain.Models.InventoryReservation> InventoryReservations { get; }
        public IGenericRepository<cpms_Domain.Models.InventoryTransaction> InventoryTransactions { get; }
        public IGenericRepository<cpms_Domain.Models.InventoryAdjustment> InventoryAdjustments { get; }
        public IGenericRepository<cpms_Domain.Models.PhysicalCountSession> PhysicalCountSessions { get; }
        public IGenericRepository<cpms_Domain.Models.PhysicalCountLine> PhysicalCountLines { get; }
        public ITaskMaterialRequirementRepository TaskMaterialRequirements { get; }
        public IWarehouseTransferRepository WarehouseTransfers { get; }
        public IWarehouseTransferItemRepository WarehouseTransferItems { get; }
        public IGenericRepository<cpms_Domain.Models.TransferInventoryReservation> TransferInventoryReservations { get; }
        public IChatConversationRepository ChatConversations { get; }
        public IChatParticipantRepository ChatParticipants { get; }
        public IChatMessageRepository ChatMessages { get; }
        public IAiChatSessionRepository AiChatSessions { get; }
        public IAiChatMessageRepository AiChatMessages { get; }
        public IMeetingRepository Meetings { get; }
        public IMeetingParticipantRepository MeetingParticipants { get; }


        public IMaterialRequestRepository MaterialRequests { get; }
        public IMaterialRequisitionRepository MaterialRequisitions { get; }
        public IGenericRepository<cpms_Domain.Models.MaterialReturn> MaterialReturns { get; }
        public IProjectBudgetHistoryRepository ProjectBudgetHistories { get; }
        public IGenericRepository<cpms_Domain.Models.MrpPlanningRun> MrpPlanningRuns { get; }


        // =========================================
        // CONSTRUCTOR (Khởi tạo toàn bộ Repo bằng context)
        // =========================================
        public UnitOfWork(AppDbContext context)
        {
            _context = context;

            UserAccounts = new UserAccountRepository(context);
            Projects = new ProjectRepository(context);
            ProjectPhases = new GenericRepository<cpms_Domain.Models.ProjectPhase>(context);
            TaskItems = new TaskItemRepository(context);
            ProgressReports = new ProgressReportRepository(context);
            Materials = new MaterialRepository(context);
            MaterialVariants = new GenericRepository<cpms_Domain.Models.MaterialVariant>(context);
            Suppliers = new SupplierRepository(context);
            SupplierCatalogs = new SupplierCatalogRepository(context);
            SupplierMetrics = new SupplierMetricRepository(context);
            PurchaseOrders = new PurchaseOrderRepository(context);
            OrderLineItems = new OrderLineItemRepository(context);
            EmailVerifications = new EmailVerificationRepository(context);
            EmailOutboxMessages = new GenericRepository<cpms_Domain.Models.EmailOutboxMessage>(context);
            RefreshTokens = new RefreshTokenRepository(context);
            Categories = new CategoryRepository(context);
            Warehouses = new WarehouseRepository(context);
            Inventories = new InventoryRepository(context);
            InventoryReservations = new GenericRepository<cpms_Domain.Models.InventoryReservation>(context);
            InventoryTransactions = new GenericRepository<cpms_Domain.Models.InventoryTransaction>(context);
            InventoryAdjustments = new GenericRepository<cpms_Domain.Models.InventoryAdjustment>(context);
            PhysicalCountSessions = new GenericRepository<cpms_Domain.Models.PhysicalCountSession>(context);
            PhysicalCountLines = new GenericRepository<cpms_Domain.Models.PhysicalCountLine>(context);
            ChatConversations = new ChatConversationRepository(context);
            ChatParticipants = new ChatParticipantRepository(context);
            ChatMessages = new ChatMessageRepository(context);
            AiChatSessions = new AiChatSessionRepository(context);
            AiChatMessages = new AiChatMessageRepository(context);
            Meetings = new MeetingRepository(context);
            MeetingParticipants = new MeetingParticipantRepository(context);

 
            MaterialRequests = new MaterialRequestRepository(context);
            MaterialRequisitions = new MaterialRequisitionRepository(context);
            MaterialReturns = new GenericRepository<cpms_Domain.Models.MaterialReturn>(context);
            TaskMaterialRequirements = new TaskMaterialRequirementRepository(context);
            ProjectBudgetHistories = new ProjectBudgetHistoryRepository(context);
            MrpPlanningRuns = new GenericRepository<cpms_Domain.Models.MrpPlanningRun>(context);
            WarehouseTransfers = new WarehouseTransferRepository(context);
            WarehouseTransferItems = new WarehouseTransferItemRepository(context);
            TransferInventoryReservations = new GenericRepository<cpms_Domain.Models.TransferInventoryReservation>(context);
        }

        // =========================================
        // SAVE CHANGES
        // =========================================
        public async Task SaveChangeAsync()
        {
            await _context.SaveChangesAsync();
        }

        // =========================================
        // TRANSACTION MANAGEMENT
        // =========================================
        public async Task BeginTransactionAsync() => await _context.Database.BeginTransactionAsync();
        public async Task BeginTransactionAsync(IsolationLevel isolationLevel) => await _context.Database.BeginTransactionAsync(isolationLevel);
        public async Task CommitTransactionAsync() => await _context.Database.CommitTransactionAsync();
        public async Task RollbackTransactionAsync() => await _context.Database.RollbackTransactionAsync();

        // =========================================
        // EXECUTE SCALAR
        // =========================================
        public async Task<T> ExecuteScalarAsync<T>(string sql)
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            try
            {
                if (command.Connection!.State != ConnectionState.Open)
                {
                    await command.Connection.OpenAsync();
                }
                command.CommandText = sql;
                var result = await command.ExecuteScalarAsync();
                return (T)Convert.ChangeType(result!, typeof(T));
            }
            finally
            {
                if (command.Connection!.State == ConnectionState.Open)
                {
                    await command.Connection.CloseAsync();
                }
            }
        }

        // =========================================
        // EXECUTE RAW SQL
        // =========================================
        public async Task ExecuteRawSqlAsync(string sql)
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            try
            {
                if (command.Connection!.State != ConnectionState.Open)
                {
                    await command.Connection.OpenAsync();
                }
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (command.Connection!.State == ConnectionState.Open)
                {
                    await command.Connection.CloseAsync();
                }
            }
        }

        // =========================================
        // DISPOSE
        // =========================================
        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
