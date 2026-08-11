using Notify.Core.Models;

namespace Notify.Core.Abstractions
{
    public interface ICustomerRepository
    {
        Task<CustomerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    }
}