using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly DefaultContext _context;

    public SaleRepository(DefaultContext context)
    {
        _context = context;
    }

    public async Task<Sale> CreateAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        await _context.Sales.AddAsync(sale, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return sale;
    }

    public async Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Sale>> GetAllAsync(int page, int pageSize, string? orderBy = null, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(s => s.Items)
            .ApplyOrdering(orderBy)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sales.CountAsync(cancellationToken);
    }

    public async Task<Sale> UpdateAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        _context.Sales.Update(sale);
        await _context.SaveChangesAsync(cancellationToken);
        return sale;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await GetByIdAsync(id, cancellationToken);
        if (sale == null)
        {
            return false;
        }

        _context.Sales.Remove(sale);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

internal static class SaleQueryExtensions
{
    internal static IOrderedQueryable<Sale> ApplyOrdering(this IQueryable<Sale> query, string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
            return query.OrderByDescending(s => s.SaleDate);

        var parts = orderBy.Trim('"').Split(',', StringSplitOptions.RemoveEmptyEntries);
        IOrderedQueryable<Sale>? ordered = null;

        foreach (var part in parts)
        {
            var tokens = part.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var field = tokens[0].Trim().ToLowerInvariant();
            var isDesc = tokens.Length > 1 && tokens[1].Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);

            if (ordered == null)
            {
                ordered = field switch
                {
                    "salenumber" => isDesc ? query.OrderByDescending(s => s.SaleNumber) : query.OrderBy(s => s.SaleNumber),
                    "saledate" => isDesc ? query.OrderByDescending(s => s.SaleDate) : query.OrderBy(s => s.SaleDate),
                    "customername" => isDesc ? query.OrderByDescending(s => s.CustomerName) : query.OrderBy(s => s.CustomerName),
                    "branchname" => isDesc ? query.OrderByDescending(s => s.BranchName) : query.OrderBy(s => s.BranchName),
                    "totalamount" => isDesc ? query.OrderByDescending(s => s.TotalAmount) : query.OrderBy(s => s.TotalAmount),
                    _ => query.OrderByDescending(s => s.SaleDate)
                };
            }
            else
            {
                ordered = field switch
                {
                    "salenumber" => isDesc ? ordered.ThenByDescending(s => s.SaleNumber) : ordered.ThenBy(s => s.SaleNumber),
                    "saledate" => isDesc ? ordered.ThenByDescending(s => s.SaleDate) : ordered.ThenBy(s => s.SaleDate),
                    "customername" => isDesc ? ordered.ThenByDescending(s => s.CustomerName) : ordered.ThenBy(s => s.CustomerName),
                    "branchname" => isDesc ? ordered.ThenByDescending(s => s.BranchName) : ordered.ThenBy(s => s.BranchName),
                    "totalamount" => isDesc ? ordered.ThenByDescending(s => s.TotalAmount) : ordered.ThenBy(s => s.TotalAmount),
                    _ => ordered.ThenByDescending(s => s.SaleDate)
                };
            }
        }

        return ordered ?? query.OrderByDescending(s => s.SaleDate);
    }
}
