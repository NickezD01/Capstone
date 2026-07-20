using cpms_Application.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        public readonly DbSet<T> _db;
        public readonly AppDbContext _context;

        public GenericRepository(AppDbContext context)
        {
            _context = context;
            _db = _context.Set<T>();
        }
        public async Task AddAsync(T entity)
        {
            await _db.AddAsync(entity);
        }

        public async Task<int> CountAsync() => await _db.CountAsync();

        // 🛠️ ĐÃ FIX: Thêm .AsNoTracking() để bẻ cache khi lấy danh sách có kèm Include
        public async Task<List<T>> GetAllAsync(System.Linq.Expressions.Expression<Func<T, bool>>? filter,
                                               Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null,
                                               int pageIndex = 1,
                                               int pageSize = 0)
        {
            // Ép EF Core đọc trực tiếp từ Database mới nhất, không lấy từ bộ nhớ đệm Tracking
            IQueryable<T> query = _db.AsNoTracking();

            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (include != null)
            {
                query = include(query);
            }
            if (pageSize > 0)
            {
                pageIndex = Math.Max(1, pageIndex);
                query = query.Skip((pageIndex - 1) * pageSize).Take(pageSize);
            }
            return await query.ToListAsync();
        }

        // 🛠️ ĐÃ FIX: Thêm .AsNoTracking() để bẻ cache khi lấy danh sách chỉ có Filter
        public async Task<List<T>> GetAllAsync(System.Linq.Expressions.Expression<Func<T, bool>>? filter)
        {
            if (filter != null)
            {
                return await _db.AsNoTracking().Where(filter).ToListAsync();
            }
            return await _db.AsNoTracking().ToListAsync();
        }

        public async Task<List<T>> GetAllIgnoringQueryFiltersAsync(Expression<Func<T, bool>>? filter)
        {
            IQueryable<T> query = _db.IgnoreQueryFilters().AsNoTracking();
            if (filter != null) query = query.Where(filter);
            return await query.ToListAsync();
        }

        public async Task<T> GetAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter)
        {
#nullable disable
            IQueryable<T> query = _db;
            return (await query.FirstOrDefaultAsync(filter))!;
#nullable restore
        }

        public async Task<T> GetAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null)
        {
            IQueryable<T> query = _db;
            if (include != null)
            {
                query = include(query);
            }
            return (await query.FirstOrDefaultAsync(filter))!;
        }

        public async Task RemoveByIdAsync(object id)
        {
            T? existing = await GetByIdAsync(id);
            if (existing != null)
            {
                _db.Remove(existing);
            }
            else throw new cpms_Application.CustomExceptions.NotFoundException($"{typeof(T).Name} with key '{id}' was not found.");
        }

        public async Task AddRangeAsync(List<T> entities)
        {
            await _db.AddRangeAsync(entities);
        }

        public void Remove(T entity)
        {
            _db.Remove(entity);
        }

        public async Task<T?> GetByIdAsync(object id)
        {
            var entityType = _context.Model.FindEntityType(typeof(T))
                ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} is not part of the EF model.");
            var key = entityType.FindPrimaryKey()
                ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} does not have a primary key.");
            if (key.Properties.Count != 1)
                throw new NotSupportedException("Generic ID lookup supports only single-column primary keys.");

            var keyProperty = key.Properties[0];
            var keyType = keyProperty.ClrType;
            var convertedId = id.GetType() == keyType ? id : Convert.ChangeType(id, Nullable.GetUnderlyingType(keyType) ?? keyType);
            var parameter = Expression.Parameter(typeof(T), "entity");
            var property = Expression.Call(typeof(EF), nameof(EF.Property), new[] { keyType }, parameter,
                Expression.Constant(keyProperty.Name));
            var predicate = Expression.Lambda<Func<T, bool>>(
                Expression.Equal(property, Expression.Constant(convertedId, keyType)), parameter);
            return await _db.FirstOrDefaultAsync(predicate);
        }

        public void Update(T entity)
        {
            _db.Update(entity);
        }
    }
}
